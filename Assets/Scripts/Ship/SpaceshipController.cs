using System;
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
            var total = 0f;
            foreach (var stage in Stages)
            {
                total += stage.GetFuelRemaining();
            }

            return total / Stages.Length;
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

        private float[] _inputs;

        private void Awake()
        {
            Rb = GetComponent<Rigidbody2D>();
            PlanetaryPhysics = GetComponent<PlanetaryPhysics>();

            _topLevelStage = GetComponent<SpaceshipStage>();
            _topLevelStage.IsRootStage = true;

            _inputs = new float[_networkShape[0]];
        }

        private void Start()
        {
            Debug.Log("Creating brain with shape " + string.Join("-", _networkShape));
            Brain = new NeuralNetwork(_networkShape);
        }

        private float[] GetNormalizedInputs()
        {
            _inputs[0] = PlanetaryPhysics.GetAtmosphereProgress(Rb.position);
            _inputs[1] = Rb.linearVelocity.x / 500f;
            _inputs[2] = Rb.linearVelocity.y / 500f;
            _inputs[3] = Mathf.Clamp(Rb.angularVelocity / 360f, -1, 1);
            _inputs[4] = NormalizeRotation(Rb.rotation);
            _inputs[5] = _topLevelStage.GetFuelRemaining();
            _inputs[6] = _firstStageGroup.GetAverageFuelRemaining();
            _inputs[7] = _boosterStageGroup.GetAverageFuelRemaining();
            _inputs[8] = _firstStageGroup.Separated ? 1f : -1f;
            _inputs[9] = _boosterStageGroup.Separated ? 1f : -1f;

            return _inputs;
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

            // note: order matters (children first), todo: find a nice way to make it not matter or enforce it
            _boosterStageGroup.ReinitialiseAll();
            _firstStageGroup.ReinitialiseAll();
            _topLevelStage.Reinitialise();
        }
    }
}