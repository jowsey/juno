using UnityEngine;

namespace Ship
{
    public class Engine : BodyPart
    {
        [SerializeField] private ParticleSystem _flameParticles;
        private float _initialParticleRateOverTime;

        // Max thrust force in Newtons
        public float MaxThrust;

        // Fuel consumption rate in units per second at full thrust
        public float FuelConsumptionRate;

        // Max total rotation in degrees
        public float MaxRotation;

        // Rotation speed in degrees per second at full steering control
        public float RotationSpeed;

        [Range(0f, 1f)] public float ThrustControl;
        [Range(-1f, 1f)] public float SteeringControl;

        public float StoredZRot;

        private new void Awake()
        {
            base.Awake();

            _initialParticleRateOverTime = _flameParticles.emission.rateOverTime.constant;

            var emission = _flameParticles.emission;
            emission.rateOverTime = 0f;
        }

        public void SetParticleRatio(float ratio)
        {
            var emission = _flameParticles.emission;
            emission.rateOverTime = _initialParticleRateOverTime * ratio;
        }

        public override void Reinitialise()
        {
            base.Reinitialise();

            ThrustControl = 0f;
            SteeringControl = 0f;
            StoredZRot = 0f;
            transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        }
    }
}