using System;
using ML;
using UnityEngine;

namespace Ship
{
    [Serializable]
    public struct StageGroup
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

        public NeuralNetwork Brain;

        private SpaceshipStage _topLevelStage;
        [SerializeField] private StageGroup _boosterStageGroup;
        [SerializeField] private StageGroup _heavyStageGroup;

        private float[] _inputs;

        [HideInInspector] public float HighestAtmosphereProgress;

        private void Awake()
        {
            Rb = GetComponent<Rigidbody2D>();
            PlanetaryPhysics = GetComponent<PlanetaryPhysics>();

            _topLevelStage = GetComponent<SpaceshipStage>();
            _topLevelStage.IsRootStage = true;

            _inputs = new float[SimulationManager.InputCount];
        }

        private static float NormalizeRotation(float rotation)
        {
            rotation %= 360f;
            if (rotation > 180f) rotation -= 360f;
            return rotation / 180f;
        }

        private void FixedUpdate()
        {
            if (Brain == null || !_useBrain) return;

            var atmosphereProgress = PlanetaryPhysics.GetAtmosphereProgress(Rb.position);
            if (atmosphereProgress > HighestAtmosphereProgress)
            {
                HighestAtmosphereProgress = atmosphereProgress;
            }

            _inputs[0] = atmosphereProgress;
            _inputs[1] = Rb.linearVelocity.x / 1000f;
            _inputs[2] = Rb.linearVelocity.y / 1000f;
            _inputs[3] = Mathf.Clamp(Rb.angularVelocity / SpaceshipStage.AngularVelocityExplodeThreshold, -1, 1);
            _inputs[4] = NormalizeRotation(Rb.rotation);
            _inputs[5] = _topLevelStage.GetFuelRemaining();
            _inputs[6] = _heavyStageGroup.Separated ? 0f : _heavyStageGroup.GetAverageFuelRemaining();
            _inputs[7] = _boosterStageGroup.Separated ? 0f : _boosterStageGroup.GetAverageFuelRemaining();
            _inputs[8] = _heavyStageGroup.Separated ? 1f : -1f;
            _inputs[9] = _boosterStageGroup.Separated ? 1f : -1f;

            var outputs = Brain.FeedForward(_inputs);

            var thrustControl = Mathf.Clamp01(outputs[0] * 0.5f + 0.5f);
            var steeringControl = Mathf.Clamp(outputs[1], -1f, 1f);
            var separateBoosterStage = outputs[2] > 0f;
            var separateHeavyStage = outputs[3] > 0f;

            if (separateBoosterStage && !_boosterStageGroup.Separated && !_heavyStageGroup.Separated)
            {
                _boosterStageGroup.Separate();
            }

            if (separateHeavyStage && !_heavyStageGroup.Separated)
            {
                _heavyStageGroup.Separate();
            }

            if (!_boosterStageGroup.Separated)
            {
                _boosterStageGroup.SetMasterThrust(thrustControl);
                _boosterStageGroup.SetMasterSteering(steeringControl);
            }

            if (!_heavyStageGroup.Separated)
            {
                _heavyStageGroup.SetMasterThrust(thrustControl);
                _heavyStageGroup.SetMasterSteering(steeringControl);
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
            StageGroup? group = null;

            if (!_boosterStageGroup.Separated)
            {
                group = _boosterStageGroup;
            }
            else if (!_heavyStageGroup.Separated)
            {
                group = _heavyStageGroup;
            }

            group?.Separate();
        }

        public void Reinitialise()
        {
            HighestAtmosphereProgress = 0f;

            Rb.linearVelocity = Vector2.zero;
            Rb.angularVelocity = 0f;

            gameObject.SetActive(true);

            // note: order matters (children first), todo: find a nice way to make it not matter or enforce it
            _boosterStageGroup.ReinitialiseAll();
            _heavyStageGroup.ReinitialiseAll();
            _topLevelStage.Reinitialise();
        }
    }
}