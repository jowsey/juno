using System.Collections.Generic;
using ML;
using UnityEngine;
using UnityEngine.Events;

namespace Ship
{
    public class SpaceshipStage : MonoBehaviour
    {
        private static GameObject _explosionPrefab;

        // owning parent ship
        public SpaceshipController Ship { get; private set; }

        // rigidbody we contribute to, either our parent's or our own when separated
        public Rigidbody2D Rb { get; private set; }

        // true if we're not connected to any stage above us (i.e. we own our physics)
        public bool IsRootStage;

        private SpaceshipStage _parentStage;

        private Transform _originalParent;
        private Vector3 _originalLocalPosition;
        private Quaternion _originalLocalRotation;
        private Vector3 _originalLocalScale;

        public const float RelativeSeparationForce = 2f;

        public const float CrashVelocityThreshold = 3.5f;
        public const float AngularVelocityExplodeThreshold = 1800f;

        [SerializeField] private bool _fixCenterOfMass = true;
        [SerializeField] private GameObject _fairing;
        [SerializeField] private Vector3 _separateDirection;

        // parts directly owned by this stage
        private BodyPart[] _stageParts;
        private Engine[] _engines;
        private Transform[] _engineTransforms;
        private FuelTank[] _fuelTanks;

        // all parts connected to this stage (including child stages)
        private BodyPart[] _allOwnedParts;

        // allows child stages to detach themselves from us
        private UnityEvent _onExplode = new();

        private void Awake()
        {
            if (!_explosionPrefab) _explosionPrefab = Resources.Load<GameObject>("FX/Explosion");

            Ship = GetComponentInParent<SpaceshipController>();
            if (transform.parent) _parentStage = transform.parent.GetComponentInParent<SpaceshipStage>();

            // detach from parent when it explodes
            if (_parentStage) _parentStage._onExplode.AddListener(OnParentExplode);

            Rb = Ship.Rb;

            _originalParent = transform.parent;
            _originalLocalPosition = transform.localPosition;
            _originalLocalRotation = transform.localRotation;
            _originalLocalScale = transform.localScale;

            RescanParts();
        }

        private void OnParentExplode()
        {
            // explode only if we're connected, don't tell parent again
            if (!IsRootStage) Explode(false);
        }

        public void RescanParts()
        {
            var stageParts = new List<BodyPart>();
            var engines = new List<Engine>();
            var engineTransforms = new List<Transform>();
            var fuelTanks = new List<FuelTank>();

            foreach (Transform child in transform)
            {
                if (!child.TryGetComponent<BodyPart>(out var part)) continue;
                stageParts.Add(part);

                if (part is Engine engine)
                {
                    engines.Add(engine);
                    engineTransforms.Add(engine.transform);
                }

                if (part is FuelTank tank) fuelTanks.Add(tank);
            }

            _allOwnedParts = GetComponentsInChildren<BodyPart>();

            // cache arrays for enumerator performance
            _stageParts = stageParts.ToArray();
            _engines = engines.ToArray();
            _engineTransforms = engineTransforms.ToArray();
            _fuelTanks = fuelTanks.ToArray();

            if (IsRootStage) RecalculateLinkedMass();
        }

        private void RecalculateLinkedMass()
        {
            var totalMass = 0f;
            foreach (var part in _allOwnedParts)
            {
                totalMass += part.BaseWeight;
                if (part is FuelTank tank)
                {
                    totalMass += tank.StoredFuelKg;
                }
            }

            Rb.mass = totalMass;
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

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision.relativeVelocity.magnitude < CrashVelocityThreshold) return;
            Explode();
        }

        private void FixedUpdate()
        {
            // explode if rotating too fast (ripped apart by force)
            if (Mathf.Abs(Rb.angularVelocity) >= AngularVelocityExplodeThreshold)
            {
                Explode();
                return;
            }

            var linkedFuelAvailable = 0f;
            foreach (var tank in _fuelTanks)
            {
                linkedFuelAvailable += tank.StoredFuelKg;
            }

            var totalFuelUsage = 0f;
            // distribute fuel evenly among engines (prevents a tick where one engine runs and others dont)
            var maxPerEngineFuelBudget = linkedFuelAvailable / Mathf.Max(1, _engines.Length);
            for (var i = 0; i < _engines.Length; i++)
            {
                var engine = _engines[i];
                var engineTransform = _engineTransforms[i];

                // handle steering rotation
                var rotationChange = engine.SteeringControl * engine.RotationSpeed * Time.fixedDeltaTime;
                var newZRot = Mathf.Clamp(
                    engine.StoredZRot + rotationChange,
                    -engine.MaxRotation,
                    engine.MaxRotation
                );

                if (Mathf.Abs(newZRot - engine.StoredZRot) > Mathf.Epsilon)
                {
                    engine.StoredZRot = newZRot;
                    engineTransform.localRotation = Quaternion.Euler(0f, 0f, engine.StoredZRot);
                }

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
                    engineTransform.up * (cappedThrust * engine.MaxThrust),
                    engineTransform.position
                );

                linkedFuelAvailable -= cappedFuelUsage;
                totalFuelUsage += cappedFuelUsage;
            }

            Rb.mass -= totalFuelUsage;

            // consume fuel from tanks
            foreach (var tank in _fuelTanks)
            {
                var fuelToConsume = Mathf.Min(tank.StoredFuelKg, totalFuelUsage);
                tank.StoredFuelKg -= fuelToConsume;
                totalFuelUsage -= fuelToConsume;
                if (totalFuelUsage <= 0f) break;
            }
        }

        public void Separate()
        {
            if (IsRootStage) return;

            if (SimulationManager.Instance.SpeedTrainingMode)
            {
                gameObject.SetActive(false);
                _parentStage.RescanParts();
                return;
            }

            IsRootStage = true;

            if (_fairing) _fairing.SetActive(false);

            foreach (Transform child in transform)
            {
                if (child.CompareTag("StageConnector"))
                {
                    child.gameObject.SetActive(false);
                }
            }

            SetThrustControl(0f);

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

            if (_fixCenterOfMass && Rb.centerOfMass.x != 0f)
            {
                FixCenterOfMass();
            }

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

        public void Explode(bool tellParent = true)
        {
            if (!SimulationManager.Instance.SpeedTrainingMode)
            {
                foreach (var part in _stageParts)
                {
                    var fx = Instantiate(_explosionPrefab, part.transform.position, Quaternion.identity);
                    fx.transform.parent = Ship.transform.parent;
                }
            }

            // propagate downwards
            _onExplode.Invoke();

            // propagate upwards
            if (tellParent && _parentStage && !IsRootStage)
            {
                _parentStage.Explode();
            }

            gameObject.SetActive(false);
        }

        public void Reinitialise()
        {
            // if we were separated, reattach to ship
            if (IsRootStage && _parentStage)
            {
                DestroyImmediate(GetComponent<PlanetaryPhysics>());
                DestroyImmediate(Rb);

                Rb = Ship.Rb;
                IsRootStage = false;
            }

            gameObject.SetActive(true);

            // reinitialise parts directly owned by this stage, child stages handle their own parts
            foreach (var part in _stageParts)
            {
                part.Reinitialise();
            }

            if (_fairing) _fairing.SetActive(true);

            if (_originalParent)
            {
                transform.parent = _originalParent;
                transform.SetLocalPositionAndRotation(_originalLocalPosition, _originalLocalRotation);
                transform.localScale = _originalLocalScale;
            }

            RescanParts();

            if (IsRootStage && _fixCenterOfMass && Rb.centerOfMass.x != 0f)
            {
                FixCenterOfMass();
            }
        }
    }
}