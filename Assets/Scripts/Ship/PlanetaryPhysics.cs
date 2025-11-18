using UnityEngine;

namespace Ship
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlanetaryPhysics : MonoBehaviour
    {
        private const float SurfaceGravity = 9.81f;
        private static Environment _env;

        private Rigidbody2D _rb;

        [SerializeField] private float _airDensityFalloff = 5f;
        [SerializeField] private float _dragCoefficient = 20f;
        [SerializeField] private float _angularDamping = 100f;

        private void Awake()
        {
            if (!_env) _env = FindAnyObjectByType<Environment>();

            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
        }

        public float GetAltitude()
        {
            var distanceToCore = Vector2.Distance(transform.position, _env.EarthCore.position);
            var altitude = distanceToCore - _env.EarthCoreRadius;
            return Mathf.Max(0f, altitude);
        }

        public float GetAtmosphereProgress()
        {
            var distanceToCenter = Vector2.Distance(transform.position, _env.EarthCore.position);
            return Mathf.Clamp01(
                (distanceToCenter - _env.EarthCoreRadius) / (_env.EarthAtmosphereRadius - _env.EarthCoreRadius)
            );
        }

        public float GetGravity()
        {
            var distanceToCore = Vector2.Distance(transform.position, _env.EarthCore.position);
            var r = Mathf.Max(distanceToCore, _env.EarthCoreRadius);
            return SurfaceGravity * (_env.EarthCoreRadius * _env.EarthCoreRadius) / (r * r);
        }

        public float GetAirDensity()
        {
            var progress = GetAtmosphereProgress();
            return progress < 1f ? Mathf.Exp(-_airDensityFalloff * progress) : 0f;
        }

        private void FixedUpdate()
        {
            // gravity
            var directionToTarget = (_env.EarthCore.position - transform.position).normalized;
            var gravity = directionToTarget * GetGravity();

            _rb.AddForce(gravity * _rb.mass, ForceMode2D.Force);

            // air resistance/linear drag
            var airDensity = GetAirDensity();
            var speed = _rb.linearVelocity.magnitude;
            var drag = -_rb.linearVelocity.normalized * (_dragCoefficient * airDensity * speed * speed);
            _rb.AddForce(drag, ForceMode2D.Force);

            // angular drag
            _rb.angularDamping = _angularDamping * airDensity;
        }
    }
}