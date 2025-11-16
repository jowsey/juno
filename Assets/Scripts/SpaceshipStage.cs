using System;
using System.Collections.Generic;
using UnityEngine;

public class SpaceshipStage : MonoBehaviour
{
    [HideInInspector] public bool IsTopLevel = false;

    private Rigidbody2D _rb;
    [SerializeField] private GameObject _fairing;

    [SerializeField] private Vector3 _separateDirection;
    private const float SeparationForce = 2f;

    // Parts directly owned by this stage
    private List<BodyPart> _linkedParts = new();

    // All parts connected to this stage (including child stages)
    private BodyPart[] _allConnectedParts;

    private void Start()
    {
        RecalculateLinkedParts();
        _rb = GetComponentInParent<Rigidbody2D>();
    }

    public void RecalculateLinkedParts()
    {
        _linkedParts.Clear();

        foreach (Transform child in transform)
        {
            if (child.gameObject.activeInHierarchy && child.TryGetComponent<BodyPart>(out var part))
            {
                _linkedParts.Add(part);
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
    }

    private void FixedUpdate()
    {
        // if we're the top-level stage
        if (IsTopLevel)
        {
            RecalculateLinkedMass();
        }

        var fuelAvailable = 0f;
        foreach (var part in _linkedParts)
        {
            if (part is FuelTank tank)
            {
                fuelAvailable += tank.StoredFuelKg;
            }
        }

        var totalFuelUsage = 0f;
        foreach (var part in _linkedParts)
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

            if (fuelAvailable <= 0f)
            {
                engine.SetParticleRatio(0f);
                continue;
            }

            var fuelUsage = engine.ThrustControl * engine.FuelConsumptionRate * Time.fixedDeltaTime;
            var cappedThrust = engine.ThrustControl;
            if (fuelAvailable < fuelUsage)
            {
                // limit thrust to remaining fuel
                cappedThrust *= fuelAvailable / fuelUsage;
            }

            engine.SetParticleRatio(cappedThrust);

            _rb.AddForceAtPosition(
                engine.transform.up * (cappedThrust * engine.MaxThrust),
                engine.transform.position
            );

            fuelAvailable -= fuelUsage;
            totalFuelUsage += fuelUsage;
        }

        // consume fuel from tanks
        foreach (var part in _linkedParts)
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