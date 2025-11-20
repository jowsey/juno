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
        private float[] _inputs;

        public const int InputCount = 11;
        public const int OutputCount = 4;

        private SpaceshipStage _topLevelStage;
        [SerializeField] private StageGroup _boosterStageGroup;
        [SerializeField] private StageGroup _heavyStageGroup;

        [HideInInspector] public float HighestAtmosphereProgress;

        private void Awake()
        {
            Rb = GetComponent<Rigidbody2D>();
            PlanetaryPhysics = GetComponent<PlanetaryPhysics>();

            _topLevelStage = GetComponent<SpaceshipStage>();
            _topLevelStage.IsRootStage = true;

            _inputs = new float[InputCount];
        }

        [ContextMenu("Print inputs")]
        private void PrintInputs()
        {
            string[] inputLabels =
            {
                "Atmosphere Progress",
                "Velocity X",
                "Velocity Y",
                "Angular Velocity",
                "Rotation sin",
                "Rotation cos",
                "Top Level Fuel",
                "Heavy Stage Fuel",
                "Booster Stage Fuel",
                "Heavy Stage Separated",
                "Booster Stage Separated"
            };

            for (var i = 0; i < _inputs.Length; i++)
            {
                Debug.Log($"{inputLabels[i]}: {_inputs[i]}");
            }
        }

        private void FixedUpdate()
        {
            if (Brain == null || !_useBrain) return;

            var pos = Rb.position;
            var linearVelocity = Rb.linearVelocity;

            var atmosphereProgress = PlanetaryPhysics.GetAtmosphereProgress(pos);
            if (atmosphereProgress > HighestAtmosphereProgress)
            {
                HighestAtmosphereProgress = atmosphereProgress;
            }

            var upDir = (pos - Environment.Instance.PlanetPosition).normalized;
            var tangentDir = new Vector2(upDir.y, -upDir.x);

            var relativeVelocityY = Vector2.Dot(linearVelocity, upDir);
            var relativeVelocityX = Vector2.Dot(linearVelocity, tangentDir);

            var surfaceAngle = Mathf.Atan2(upDir.y, upDir.x) * Mathf.Rad2Deg - 90f;
            var relativeRotation = Mathf.DeltaAngle(surfaceAngle, Rb.rotation);

            _inputs[0] = atmosphereProgress;
            _inputs[1] = (float)Math.Tanh(relativeVelocityX / 500f);
            _inputs[2] = (float)Math.Tanh(relativeVelocityY / 500f);
            _inputs[3] = (float)Math.Tanh(Rb.angularVelocity / 45f);
            _inputs[4] = Mathf.Sin(relativeRotation * Mathf.Deg2Rad);
            _inputs[5] = Mathf.Cos(relativeRotation * Mathf.Deg2Rad);
            _inputs[6] = _topLevelStage.GetFuelRemaining();
            _inputs[7] = !_heavyStageGroup.Separated ? _heavyStageGroup.GetAverageFuelRemaining() : 0;
            _inputs[8] = !_boosterStageGroup.Separated ? _boosterStageGroup.GetAverageFuelRemaining() : 0;
            _inputs[9] = _heavyStageGroup.Separated ? 1 : 0;
            _inputs[10] = _boosterStageGroup.Separated ? 1 : 0;

            var outputs = Brain.FeedForward(_inputs);

            var thrustControl = outputs[0] * 0.5f + 0.5f;
            var steeringControl = outputs[1];
            var separateBoosterStage = outputs[2] > 0.5f;
            var separateHeavyStage = outputs[3] > 0.5f;

            if (separateBoosterStage && !_boosterStageGroup.Separated && !_heavyStageGroup.Separated)
            {
                _boosterStageGroup.Separate();
            }

            if (separateHeavyStage && !_heavyStageGroup.Separated)
            {
                _heavyStageGroup.Separate();
                _boosterStageGroup.Separated = true;
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