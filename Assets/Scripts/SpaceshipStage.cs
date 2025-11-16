using System.Collections.Generic;
using UnityEngine;

public class SpaceshipStage : MonoBehaviour
{
    [HideInInspector] public bool IsTopLevel = false;

    private Rigidbody2D _rb;
    [SerializeField] private GameObject _fairing;

    [SerializeField] private Vector3 _separateDirection;
    private const float SeparationForce = 2f;

    [SerializeField] private bool _fixCenterOfMass = true;

    // Parts directly owned by this stage
    private List<BodyPart> _stageParts = new();

    // All parts connected to this stage (including child stages)
    private BodyPart[] _allConnectedParts;

    private int _numEngines;


    private void Start()
    {
        RecalculateLinkedParts();
        _rb = GetComponentInParent<Rigidbody2D>();
    }

    public void RecalculateLinkedParts()
    {
        _stageParts.Clear();

        foreach (Transform child in transform)
        {
            if (child.gameObject.activeInHierarchy && child.TryGetComponent<BodyPart>(out var part))
            {
                _stageParts.Add(part);

                if (part is Engine)
                {
                    _numEngines++;
                }
            }
        }

        _allConnectedParts = GetComponentsInChildren<BodyPart>();

        if (IsTopLevel && _allConnectedParts.Length == 0)
        {
            // spaceship gone </3
            // todo end individual sim
            Debug.Log("ship gone rip");
            Destroy(gameObject);
        }
    }

    private void RecalculateLinkedMass()
    {
        if (!_rb) return;

        var totalMass = 0f;
        foreach (var part in _allConnectedParts)
        {
            totalMass += part.BaseWeight;
            if (part is FuelTank tank)
            {
                totalMass += tank.StoredFuelKg;
            }
        }

        _rb.mass = totalMass;

        if (IsTopLevel && _fixCenterOfMass && _rb.centerOfMass.x != 0f)
        {
            // fix issue with polygon colliders & center of mass precision
            var com = _rb.centerOfMass;
            com.x = 0f;
            _rb.centerOfMass = com;
        }
    }

    private void FixedUpdate()
    {
        // if we're the top-level stage
        if (IsTopLevel)
        {
            RecalculateLinkedMass();
        }

        var linkedFuelAvailable = 0f;
        foreach (var part in _stageParts)
        {
            if (part is FuelTank tank)
            {
                linkedFuelAvailable += tank.StoredFuelKg;
            }
        }

        var totalFuelUsage = 0f;
        // distribute fuel evenly among engines (prevents a tick where one engine runs and others dont)
        var maxPerEngineFuelBudget = linkedFuelAvailable / Mathf.Max(1, _numEngines);
        foreach (var part in _stageParts)
        {
            if (part is not Engine engine) continue;

            // handle steering rotation
            var targetRot = engine.SteeringControl * engine.MaxRotation;
            var diff = targetRot - engine.StoredZRot;
            var maxRotChange = engine.RotationSpeed * Time.fixedDeltaTime;
            var rotationChange = Mathf.Clamp(
                diff,
                -maxRotChange,
                maxRotChange
            );
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

            _rb.AddForceAtPosition(
                engine.transform.up * (cappedThrust * engine.MaxThrust),
                engine.transform.position
            );

            linkedFuelAvailable -= fuelUsage;
            totalFuelUsage += fuelUsage;
        }

        // consume fuel from tanks
        foreach (var part in _stageParts)
        {
            if (part is not FuelTank tank) continue;

            var fuelToConsume = Mathf.Min(tank.StoredFuelKg, totalFuelUsage);
            tank.StoredFuelKg -= fuelToConsume;
            totalFuelUsage -= fuelToConsume;
            if (totalFuelUsage <= 0f) break;
        }
    }

    public void Separate()
    {
        if (IsTopLevel) return;

        IsTopLevel = true;

        // store references before setting parent to null
        var parent = transform.parent;
        var parentRb = parent.GetComponentInParent<Rigidbody2D>();

        // become our own physics object
        gameObject.AddComponent<PlanetaryPhysics>();

        _rb = gameObject.AddComponent<Rigidbody2D>();
        _rb.linearVelocity = parentRb.linearVelocity;
        _rb.angularVelocity = parentRb.angularVelocity;
        _rb.angularDamping = 0f;
        _rb.linearDamping = 0f;
        _rb.gravityScale = 0f;
        _rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // detach from parent and tell it to recalculate children
        transform.SetParent(null);
        parent.GetComponentInParent<SpaceshipStage>().RecalculateLinkedParts();

        // apply separation force
        var localDir = parent.TransformDirection(_separateDirection);
        _rb.AddForce(localDir.normalized * SeparationForce, ForceMode2D.Impulse);

        Destroy(_fairing);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        var worldDir = transform.TransformDirection(_separateDirection.normalized);
        Gizmos.DrawLine(transform.position, transform.position + worldDir);
    }
}