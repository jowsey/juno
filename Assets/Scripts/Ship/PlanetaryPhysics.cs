using UnityEngine;

namespace Ship
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlanetaryPhysics : MonoBehaviour
    {
        public const float SurfaceGravity = 9.81f;

        private Rigidbody2D _rb;

        [SerializeField] private float _airDensityFalloff = 5f;
        [SerializeField] private float _dragCoefficient = 20f;
        [SerializeField] private float _angularDamping = 100f;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
        }

        public float GetAltitude(Vector2 position)
        {
            var env = Environment.Instance;
            var distanceToCore = Vector2.Distance(position, env.PlanetPosition);
            var altitude = distanceToCore - env.PlanetRadius;
            return Mathf.Max(0f, altitude);
        }

        public float GetAtmosphereProgress(Vector2 position)
        {
            var env = Environment.Instance;
            var distanceToCore = Vector2.Distance(position, env.PlanetPosition);
            return Mathf.Clamp01(
                (distanceToCore - env.PlanetRadius) /
                (env.AtmosphereRadius - env.PlanetRadius)
            );
        }

        public float GetGravity(Vector2 position)
        {
            var env = Environment.Instance;
            var distanceToCore = Vector2.Distance(position, env.PlanetPosition);
            var r = Mathf.Max(distanceToCore, env.PlanetRadius);
            return SurfaceGravity * (env.PlanetRadius * env.PlanetRadius) / (r * r);
        }

        public float GetAirDensity(Vector2 position)
        {
            var progress = GetAtmosphereProgress(position);
            return progress < 1f ? Mathf.Exp(-_airDensityFalloff * progress) : 0f;
        }

        private void FixedUpdate()
        {
            var position = _rb.position;

            // gravity
            var directionToTarget = (Environment.Instance.PlanetPosition - position).normalized;
            var gravity = directionToTarget * GetGravity(position);

            _rb.AddForce(gravity * _rb.mass, ForceMode2D.Force);

            // air resistance/linear drag
            var airDensity = GetAirDensity(position);
            var speed = _rb.linearVelocity.magnitude;
            var drag = -_rb.linearVelocity.normalized * (_dragCoefficient * airDensity * speed * speed);
            _rb.AddForce(drag, ForceMode2D.Force);

            // angular drag
            _rb.angularDamping = _angularDamping * airDensity;
        }
    }
}