using System;
using System.Linq;
using ML;
using UnityEngine;

namespace Ship
{
    [Serializable]
    public class StageGroup
    {
        public SpaceshipStage[] Stages;
        public bool Separated;

        public float GetAverageFuelRemaining()
        {
            return Stages.Sum(stage => stage.GetFuelRemaining()) / Stages.Length;
        }

        public void Separate()
        {
            foreach (var stage in Stages) stage.Separate();
            Separated = true;
        }

        public void ReinitialiseAll()
        {
            foreach (var stage in Stages) stage.Reinitialise();
            Separated = false;
        }

        public void SetMasterThrust(float level)
        {
            foreach (var stage in Stages) stage.SetThrustControl(level);
        }

        public void SetMasterSteering(float level)
        {
            foreach (var stage in Stages) stage.SetSteeringControl(level);
        }
    }

    [RequireComponent(typeof(Rigidbody2D), typeof(SpaceshipStage), typeof(PlanetaryPhysics))]
    public class SpaceshipController : MonoBehaviour
    {
        [SerializeField] private bool _useBrain = true;

        public Rigidbody2D Rb { get; private set; }

        [HideInInspector] public PlanetaryPhysics PlanetaryPhysics;

        [SerializeField] private int[] _networkShape = { 10, 8, 4 };
        public NeuralNetwork Brain;

        private SpaceshipStage _topLevelStage;
        [SerializeField] private StageGroup _boosterStageGroup;
        [SerializeField] private StageGroup _firstStageGroup;

        private void Awake()
        {
            Rb = GetComponent<Rigidbody2D>();
            PlanetaryPhysics = GetComponent<PlanetaryPhysics>();

            _topLevelStage = GetComponent<SpaceshipStage>();
            _topLevelStage.IsRootStage = true;
        }

        private void Start()
        {
            // mitigate unity physics glitch on initialise
            Rb.rotation = 0f;

            Debug.Log("Creating brain with shape " + string.Join("-", _networkShape));
            Brain = new NeuralNetwork(_networkShape);
        }

        private float[] GetNormalizedInputs()
        {
            return new[]
            {
                PlanetaryPhysics.GetAtmosphereProgress(),
                Rb.linearVelocity.x / 500f,
                Rb.linearVelocity.y / 500f,
                Mathf.Clamp(Rb.angularVelocity / 360f, -1, 1),
                NormalizeRotation(Rb.rotation),
                _topLevelStage.GetFuelRemaining(),
                _firstStageGroup.GetAverageFuelRemaining(),
                _boosterStageGroup.GetAverageFuelRemaining(),
                _firstStageGroup.Separated ? 1f : -1f,
                _boosterStageGroup.Separated ? 1f : -1f
            };
        }

        private float NormalizeRotation(float rotation)
        {
            rotation %= 360f;
            if (rotation > 180f) rotation -= 360f;
            return rotation / 180f;
        }

        private void FixedUpdate()
        {
            if (Brain == null || !_useBrain) return;

            var inputs = GetNormalizedInputs();
            var outputs = Brain.FeedForward(inputs);

            var thrustControl = Mathf.Clamp01(outputs[0] * 0.5f + 0.5f);
            var steeringControl = Mathf.Clamp(outputs[1], -1f, 1f);
            var separateBoosterStage = outputs[2] > 0f;
            var separateFirstStage = outputs[3] > 0f;

            if (separateBoosterStage && !_boosterStageGroup.Separated && !_firstStageGroup.Separated)
            {
                _boosterStageGroup.Separate();
            }

            if (separateFirstStage && !_firstStageGroup.Separated)
            {
                _firstStageGroup.Separate();
            }

            if (!_boosterStageGroup.Separated)
            {
                _boosterStageGroup.SetMasterThrust(thrustControl);
                _boosterStageGroup.SetMasterSteering(steeringControl);
            }

            if (!_firstStageGroup.Separated)
            {
                _firstStageGroup.SetMasterThrust(thrustControl);
                _firstStageGroup.SetMasterSteering(steeringControl);
            }
            else
            {
                _topLevelStage.SetThrustControl(thrustControl);
                _topLevelStage.SetSteeringControl(steeringControl);
            }
        }

        [ContextMenu("Force next separation")]
        public void SeparateStageGroup()
        {
            StageGroup group = null;

            if (!_boosterStageGroup.Separated)
            {
                group = _boosterStageGroup;
            }
            else if (!_firstStageGroup.Separated)
            {
                group = _firstStageGroup;
            }

            group?.Separate();
        }

        public void Reinitialise()
        {
            Rb.linearVelocity = Vector2.zero;
            Rb.angularVelocity = 0f;
            
            gameObject.SetActive(true);

            _topLevelStage.Reinitialise();
            _firstStageGroup.ReinitialiseAll();
            _boosterStageGroup.ReinitialiseAll();
        }
    }
}