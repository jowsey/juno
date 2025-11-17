using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ship
{
    public class SpaceshipStage : MonoBehaviour
    {
        // owning parent ship
        public SpaceshipController Ship { get; private set; }

        private SpaceshipStage _parentStage;

        private Transform _originalParent;
        private Vector3 _originalLocalPosition;
        private Quaternion _originalLocalRotation;

        // rigidbody we contribute to, either our parent's or our own when separated
        public Rigidbody2D Rb { get; private set; }

        // true if we're not connected to any stage above us (i.e. we own our physics)
        [HideInInspector] public bool IsRootStage;

        [SerializeField] private GameObject _fairing;

        [SerializeField] private Vector3 _separateDirection;
        private const float RelativeSeparationForce = 2f;

        [SerializeField] private bool _fixCenterOfMass = true;

        // parts directly owned by this stage
        private List<BodyPart> _stageParts = new();
        private List<Engine> _engines = new();
        private List<FuelTank> _fuelTanks = new();

        // all parts connected to this stage (including child stages)
        private List<BodyPart> _allOwnedParts = new();

        private void Awake()
        {
            Ship = GetComponentInParent<SpaceshipController>();
            _parentStage = transform.parent?.GetComponentInParent<SpaceshipStage>();

            Rb = Ship.Rb;

            _originalParent = transform.parent;
            _originalLocalPosition = transform.localPosition;
            _originalLocalRotation = transform.localRotation;

            RescanParts();
        }

        public void RescanParts()
        {
            _stageParts.Clear();
            _engines.Clear();
            _fuelTanks.Clear();

            foreach (Transform child in transform)
            {
                if (child.TryGetComponent<BodyPart>(out var part))
                {
                    _stageParts.Add(part);
                    if (part is Engine engine) _engines.Add(engine);
                    if (part is FuelTank tank) _fuelTanks.Add(tank);
                }
            }

            GetComponentsInChildren<BodyPart>(_allOwnedParts);
            CheckForDestruction();
        }

        public void CheckForDestruction()
        {
            if (!_allOwnedParts.Any(part => part.isActiveAndEnabled))
            {
                // stage fully destroyed, we have no further use
                gameObject.SetActive(false);

                // propagate up the ship
                if (_parentStage) _parentStage.CheckForDestruction();
            }
        }

        private void RecalculateLinkedMass()
        {
            var totalMass = 0f;
            foreach (var part in _allOwnedParts)
            {
                if (!part.isActiveAndEnabled) continue;

                totalMass += part.BaseWeight;
                if (part is FuelTank tank)
                {
                    totalMass += tank.StoredFuelKg;
                }
            }

            Rb.mass = totalMass;

            if (_fixCenterOfMass && Rb.centerOfMass.x != 0f)
            {
                FixCenterOfMass();
            }
        }

        private void FixCenterOfMass()
        {
            // fix issue with polygon colliders & center of mass precision
            var com = Rb.centerOfMass;
            com.x = 0f;
            Rb.centerOfMass = com;
        }

        // normalized amount of fuel remaining relative to max
        public float GetFuelRemaining()
        {
            var totalFuel = 0f;
            var totalFuelCapacity = 0f;

            foreach (var tank in _fuelTanks)
            {
                if (!tank.isActiveAndEnabled) continue;

                totalFuel += tank.StoredFuelKg;
                totalFuelCapacity += tank.MaxFuelKg;
            }

            if (totalFuelCapacity <= 0f) return 0f;

            return totalFuel / totalFuelCapacity;
        }

        public void SetThrustControl(float level)
        {
            foreach (var engine in _engines)
            {
                engine.ThrustControl = Mathf.Clamp01(level);
            }
        }

        public void SetSteeringControl(float level)
        {
            foreach (var engine in _engines)
            {
                engine.SteeringControl = Mathf.Clamp(level, -1f, 1f);
            }
        }

        private void FixedUpdate()
        {
            // recalculate mass if root stage
            if (IsRootStage) RecalculateLinkedMass();

            var linkedFuelAvailable = 0f;
            foreach (var part in _stageParts)
            {
                if (!part.isActiveAndEnabled) continue;

                if (part is FuelTank tank)
                {
                    linkedFuelAvailable += tank.StoredFuelKg;
                }
            }

            var totalFuelUsage = 0f;
            // distribute fuel evenly among engines (prevents a tick where one engine runs and others dont)
            var maxPerEngineFuelBudget = linkedFuelAvailable / Mathf.Max(1, _engines.Count);
            foreach (var engine in _engines)
            {
                if (!engine.isActiveAndEnabled) continue;

                // handle steering rotation
                var rotationChange = engine.SteeringControl * engine.RotationSpeed * Time.fixedDeltaTime;
                engine.StoredZRot = Mathf.Clamp(
                    engine.StoredZRot + rotationChange,
                    -engine.MaxRotation,
                    engine.MaxRotation
                );
                engine.transform.localEulerAngles = new Vector3(
                    engine.transform.localEulerAngles.x,
                    engine.transform.localEulerAngles.y,
                    engine.StoredZRot
                );

                if (linkedFuelAvailable <= 0f || engine.ThrustControl <= 0f)
                {
                    engine.SetParticleRatio(0f);
                    continue;
                }

                var fuelUsage = engine.ThrustControl * engine.FuelConsumptionRate * Time.fixedDeltaTime;
                var cappedFuelUsage = Mathf.Min(fuelUsage, maxPerEngineFuelBudget);
                var fuelAvailability = cappedFuelUsage / fuelUsage;
                var cappedThrust = engine.ThrustControl * fuelAvailability;

                engine.SetParticleRatio(cappedThrust);

                Rb.AddForceAtPosition(
                    engine.transform.up * (cappedThrust * engine.MaxThrust),
                    engine.transform.position
                );

                linkedFuelAvailable -= cappedFuelUsage;
                totalFuelUsage += cappedFuelUsage;
            }

            // consume fuel from tanks
            foreach (var part in _stageParts)
            {
                if (!part.isActiveAndEnabled || part is not FuelTank tank) continue;

                var fuelToConsume = Mathf.Min(tank.StoredFuelKg, totalFuelUsage);
                tank.StoredFuelKg -= fuelToConsume;
                totalFuelUsage -= fuelToConsume;
                if (totalFuelUsage <= 0f) break;
            }
        }

        public void Separate()
        {
            if (IsRootStage) return;
            IsRootStage = true;

            if (_fairing) _fairing.SetActive(false);

            foreach (Transform child in transform)
            {
                if (child.CompareTag("StageConnector"))
                {
                    child.gameObject.SetActive(false);
                }
            }

            var inheritedLinearVelocity = Rb.linearVelocity;
            var inheritedAngularVelocity = Rb.angularVelocity;

            // detach from parent and tell it to rescan parts (we took some with us)
            transform.SetParent(Ship.transform.parent);
            _parentStage.RescanParts();

            // become our own physics object
            Rb = gameObject.AddComponent<Rigidbody2D>();
            Rb.linearVelocity = inheritedLinearVelocity;
            Rb.angularVelocity = inheritedAngularVelocity;
            Rb.linearDamping = 0f;
            Rb.angularDamping = 0f;
            Rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
            Rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            RecalculateLinkedMass();

            gameObject.AddComponent<PlanetaryPhysics>();

            // apply separation force
            var localDir = _parentStage.transform.TransformDirection(_separateDirection);
            Rb.AddForce(localDir.normalized * (RelativeSeparationForce * Rb.mass), ForceMode2D.Impulse);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            var worldDir = transform.TransformDirection(_separateDirection.normalized);
            Gizmos.DrawLine(transform.position, transform.position + worldDir);
        }

        public void Reinitialise()
        {
            // if we were separated, reattach to ship
            if (IsRootStage && _parentStage)
            {
                Destroy(GetComponent<PlanetaryPhysics>());
                Destroy(Rb);

                Rb = Ship.Rb;
                IsRootStage = false;
            }

            gameObject.SetActive(true);

            // reinitialise parts directly owned by this stage, child stages handle their own parts
            foreach (var part in _stageParts)
            {
                part.Reinitialise();
            }

            if (_originalParent) transform.parent = _originalParent;
            transform.SetLocalPositionAndRotation(_originalLocalPosition, _originalLocalRotation);
        }
    }
}