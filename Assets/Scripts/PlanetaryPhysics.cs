using UnityEngine;

public class PlanetaryPhysics : MonoBehaviour
{
    private Rigidbody2D _rb;

    [SerializeField] private float _surfaceGravity = 9.81f;
    [SerializeField] private float _airDensityFalloff = 5f;
    [SerializeField] private float _dragCoefficient = 8f;

    private static Environment _env;

    private void Start()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;

        _env ??= FindAnyObjectByType<Environment>();
    }

    private float GetGravity()
    {
        var planetRadius = _env.EarthCore.lossyScale.x * 0.5f;
        var distanceToCore = Vector2.Distance(transform.position, _env.EarthCore.position);

        var r = Mathf.Max(distanceToCore, planetRadius);

        return _surfaceGravity * (planetRadius * planetRadius) / (r * r);
    }

    private float GetAirDensity()
    {
        if (_rb.linearVelocity.sqrMagnitude < 0.001f) return 0f;

        var atmosphereRadius = _env.EarthAtmosphere.lossyScale.x * 0.5f;
        var planetRadius = _env.EarthCore.lossyScale.x * 0.5f;
        var distanceToCenter = Vector2.Distance(transform.position, _env.EarthCore.position);
        var progress = Mathf.Clamp01((distanceToCenter - planetRadius) / (atmosphereRadius - planetRadius));

        // above atmosphere
        if (progress >= 1f) return 0f;

        var airDensity = Mathf.Exp(-_airDensityFalloff * progress);
        return airDensity;
    }

    private void FixedUpdate()
    {
        // gravity
        var directionToTarget = (_env.EarthCore.position - transform.position).normalized;
        var gravity = directionToTarget * GetGravity();

        _rb.AddForce(gravity * _rb.mass, ForceMode2D.Force);

        // air resistance/drag
        var airDensity = GetAirDensity();
        var speed = _rb.linearVelocity.magnitude;
        var dragForce = -_rb.linearVelocity.normalized * (_dragCoefficient * airDensity * speed * speed);
        _rb.AddForce(dragForce, ForceMode2D.Force);

        // angular drag
        var angularDragTorque = -_rb.angularVelocity * 0.1f * airDensity;
        _rb.AddTorque(angularDragTorque, ForceMode2D.Force);
    }
}