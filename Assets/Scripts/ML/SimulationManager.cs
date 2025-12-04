using System;
using System.Collections.Generic;
using System.Linq;
using Ship;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace ML
{
    public class SimulationManager : MonoBehaviour
    {
        [Serializable]
        public class SimulationOptions
        {
            public float GenerationDuration;
            public int PopulationSize;
            public int EliteCount;
            [Range(0, 1)] public float MutationRate;
            [Range(0, 1)] public float MutationStrength;
            public int[] HiddenLayers;
            public bool PassOutputsToInputs;
        }

        public static SimulationManager Instance { get; private set; }

        public bool SpeedTrainingMode { get; private set; }

        [Header("Generation parameters")]
        [field: SerializeField]
        public SimulationOptions Options { get; private set; } = new()
        {
            GenerationDuration = 180f,
            PopulationSize = 100,
            EliteCount = 5,
            MutationRate = 0.02f,
            MutationStrength = 0.25f,
            HiddenLayers = new[] { 8 }
        };

        [Header("Environment")] [SerializeField]
        private SpaceshipController _spaceshipPrefab;

        [SerializeField] private Vector2 _spawnPosition;
        [SerializeField] private Transform _shipContainer;

        [Header("UI")] [SerializeField] private TextMeshProUGUI _timerText;
        [SerializeField] private Image _timerProgressBar;
        [SerializeField] private TextMeshProUGUI _generationText;
        [SerializeField] private TextMeshProUGUI _fitnessText;
        [SerializeField] private TMP_InputField _speedInput;
        [SerializeField] private Button _copyStateButton;
        [SerializeField] private Button _pasteStateButton;
        [SerializeField] private Button _viewBestButton;
        [SerializeField] private TextMeshProUGUI _viewBestButtonText;
        [SerializeField] private Slider _mutationRateSlider;
        [SerializeField] private Slider _mutationStrengthSlider;

        [SerializeField] private RectTransform _simUI;
        [SerializeField] private RectTransform _setupUI;

        private CameraController _cameraController;

        private int _currentGeneration;
        private float _generationElapsedTime;
        private bool _restartGeneration; // todo i don't think we need both flags
        private bool _runGenerationLoop; // todo ^^

        private List<SpaceshipController> _population;
        private List<float> _fitnessScores;

        public int TotalInputCount => SpaceshipController.InputCount + (Options.PassOutputsToInputs ? SpaceshipController.OutputCount : 0);

        private void Awake()
        {
            Instance = this;

            _simUI.gameObject.SetActive(false);
            _setupUI.gameObject.SetActive(true);

            _cameraController = FindAnyObjectByType<CameraController>();

            _population = new List<SpaceshipController>(Options.PopulationSize);
            _fitnessScores = new List<float>(Options.PopulationSize);

            _speedInput.onValueChanged.AddListener(OnSpeedValueChanged);
            _copyStateButton.onClick.AddListener(OnCopyStateClicked);
            _pasteStateButton.onClick.AddListener(OnPasteStateClicked);
            _viewBestButton.onClick.AddListener(OnViewBestClicked);
            _mutationRateSlider.onValueChanged.AddListener(OnMutationRateChanged);
            _mutationStrengthSlider.onValueChanged.AddListener(OnMutationStrengthChanged);
        }

        private void FixedUpdate()
        {
            if (!_runGenerationLoop) return;
            _generationElapsedTime += Time.fixedDeltaTime;

            var generationComplete = _generationElapsedTime >= Options.GenerationDuration;
            if (generationComplete && !_restartGeneration)
            {
                var prevAverage = _fitnessScores.Average();
                var prevMax = _fitnessScores.Max();

                for (var i = 0; i < _population.Count; i++)
                {
                    var ship = _population[i];
                    var fitness = EvaluateFitness(ship);
                    _fitnessScores[i] = fitness;
                }

                var newMax = _fitnessScores.Max();

                if (newMax < prevMax - 0.00001f)
                {
                    // fwiw, i think 99% of the time this is just due to floating point (im)precision (and accumulation thereof), sadly
                    // every warning i get atp is for, like, 0.000016 -> 0.000015
                    Debug.LogWarning(
                        "<color=orange>Max fitness dropped - determinism broken?</color> " +
                        $"<color=green>{prevMax:G9}</color> -> <color=red>{newMax:G9}</color>"
                    );
                }

                UpdateFitnessUI(prevAverage, prevMax);
                EvolvePopulation();

                _currentGeneration++;
            }

            if (generationComplete || _restartGeneration)
            {
                _generationElapsedTime = 0f;
                _restartGeneration = false;

                Debug.Log($"<color=green>Running generation <b>{_currentGeneration}</b></color>");
                _generationText.text = $"Generation {_currentGeneration}";

                foreach (var ship in _population)
                {
                    // prevent some ships somehow ticking during reset
                    ship.gameObject.SetActive(false);
                }

                foreach (var ship in _population)
                {
                    ship.transform.SetPositionAndRotation(_spawnPosition, Quaternion.identity);
                    ship.Reinitialise();
                }

                Physics2D.SyncTransforms();
            }
        }

        private void LateUpdate()
        {
            if (!EventSystem.current.currentSelectedGameObject)
            {
                if (Keyboard.current.digit1Key.wasPressedThisFrame) Time.timeScale = 1f;
                if (Keyboard.current.digit2Key.wasPressedThisFrame) Time.timeScale = 5f;
                if (Keyboard.current.digit3Key.wasPressedThisFrame) Time.timeScale = 10f;
                if (Keyboard.current.digit4Key.wasPressedThisFrame) Time.timeScale = 60f;
                if (Keyboard.current.digit5Key.wasPressedThisFrame) Time.timeScale = 100f;

                SpeedTrainingMode = Time.timeScale >= 60f;
            }

            if (!_speedInput.isFocused)
            {
                _speedInput.text = Time.timeScale.ToString("N0");
            }

            var timespan = TimeSpan.FromSeconds(_generationElapsedTime);
            _timerText.text = $"{timespan.Minutes:D2}:{timespan.Seconds:D2}.{timespan.Milliseconds:D3}";

            var progress = Mathf.Clamp01(_generationElapsedTime / Options.GenerationDuration);
            if (_currentGeneration % 2 == 0)
            {
                _timerProgressBar.fillOrigin = (int)Image.OriginHorizontal.Right;
                _timerProgressBar.fillAmount = 1f - progress;
            }
            else
            {
                _timerProgressBar.fillOrigin = (int)Image.OriginHorizontal.Left;
                _timerProgressBar.fillAmount = progress;
            }
        }

        private void LaunchSimulation(int startingGeneration = 1)
        {
            // clear existing simulation
            foreach (var ship in _population)
            {
                Destroy(ship.gameObject);
            }

            _population.Clear();
            _fitnessScores.Clear();
            _currentGeneration = startingGeneration;
            _generationElapsedTime = 0f;

            // build new simulation
            var networkShape = new int[Options.HiddenLayers.Length + 2];

            networkShape[0] = TotalInputCount; // inputs
            // hidden layers
            Array.Copy(Options.HiddenLayers, 0, networkShape, 1, Options.HiddenLayers.Length);
            networkShape[^1] = SpaceshipController.OutputCount; // outputs

            Debug.Log("Creating brains with shape " + string.Join("-", networkShape));

            for (var i = 0; i < Options.PopulationSize; i++)
            {
                var ship = Instantiate(_spaceshipPrefab, _spawnPosition, Quaternion.identity);
                ship.transform.parent = _shipContainer;

                ship.Brain = new NeuralNetwork(networkShape);

                _population.Add(ship);
                _fitnessScores.Add(0f);
            }

            UpdateFitnessUI();

            _runGenerationLoop = true;
            _restartGeneration = true;

            Debug.Log("<color=green>Launched new simulation.</color>");
        }

        public void LaunchWithOptions(SimulationOptions options, int startingGeneration = 1)
        {
            Options = options;

            _mutationRateSlider.value = Options.MutationRate * 100f;
            _mutationStrengthSlider.value = Options.MutationStrength * 100f;

            LaunchSimulation(startingGeneration);
        }

        private void OnSpeedValueChanged(string input)
        {
            if (int.TryParse(input, out var timeScale))
            {
                Time.timeScale = timeScale;
            }
        }

        private void OnCopyStateClicked()
        {
            var lines = new List<string>();

            var parameters = new[]
            {
                _currentGeneration.ToString(),
                Options.GenerationDuration.ToString("G9"),
                Options.PopulationSize.ToString(),
                Options.EliteCount.ToString(),
                Options.MutationRate.ToString("G9"),
                Options.MutationStrength.ToString("G9"),
                string.Join("-", Options.HiddenLayers),
                Options.PassOutputsToInputs.ToString(),
            };

            lines.Add(string.Join(",", parameters));

            lines.AddRange(
                _population
                    .Select(ship => ship.Brain.GetFlatWeights())
                    .Select(weights => string.Join(",", weights.Select(w => w.ToString("G9"))))
            );

            var base64Lines = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(string.Join("\n", lines)));
            GUIUtility.systemCopyBuffer = base64Lines;
            Debug.Log("<color=cyan>Copied state to clipboard.</color>");
        }

        private void OnPasteStateClicked()
        {
            try
            {
                var encodedState = GUIUtility.systemCopyBuffer;
                var decodedString = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encodedState));
                var lines = decodedString.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

                var parameters = lines[0].Split(new[] { ',' });
                Debug.Log("Pasting state with parameters: " + string.Join(", ", parameters));

                var generation = int.Parse(parameters[0]);
                var options = new SimulationOptions
                {
                    GenerationDuration = float.Parse(parameters[1]),
                    PopulationSize = int.Parse(parameters[2]),
                    EliteCount = int.Parse(parameters[3]),
                    MutationRate = float.Parse(parameters[4]),
                    MutationStrength = float.Parse(parameters[5]),
                    HiddenLayers = parameters[6].Split('-', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray(),
                    PassOutputsToInputs = bool.Parse(parameters[7]),
                };

                LaunchWithOptions(options, generation);

                foreach (Transform child in _shipContainer)
                {
                    Debug.Log(child.name);
                }

                // at this point, population is initialised with correct size
                for (var i = 0; i < _population.Count; i++)
                {
                    var weightStrings = lines[i + 1].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    var weights = weightStrings.Select(float.Parse).ToArray();

                    _population[i].Brain.SetFlatWeights(weights);
                    _fitnessScores[i] = 0f;
                }

                Debug.Log("<color=green>Loaded state from clipboard.</color>");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void OnViewBestClicked()
        {
            if (_cameraController.FollowTarget)
            {
                _cameraController.FollowTarget = null;
                _viewBestButtonText.text = "View best";
                Debug.Log("Stopped following best ship.");
                return;
            }

            // elitism means first Options.EliteCount items will be sorted best
            var bestShip = _population[0];

            _cameraController.FollowTarget = bestShip.transform;
            _viewBestButtonText.text = "Stop viewing";
            Debug.Log($"Following best ship with fitness of {_fitnessScores[0]:F3}).");
        }

        private void OnMutationRateChanged(float value)
        {
            Options.MutationRate = value / 100f;
        }

        private void OnMutationStrengthChanged(float value)
        {
            Options.MutationStrength = value / 100f;
        }

        private void UpdateFitnessUI(float prevAverage = 0f, float prevMax = 0f)
        {
            var avgFitness = _fitnessScores.Average();
            var maxFitness = _fitnessScores.Max();

            var avgDiff = avgFitness - prevAverage;
            var maxDiff = maxFitness - prevMax;
            var avgDiffFmt = avgDiff >= 0 ? $"+{avgDiff:F3}" : $"{avgDiff:F3}";
            var maxDiffFmt = maxDiff >= 0 ? $"+{maxDiff:F3}" : $"{maxDiff:F3}";

            var fmt =
                $"avg. <color=yellow>{avgFitness:F3}</color> ({avgDiffFmt}), " +
                $"max. <color=green>{maxFitness:F3}</color> ({maxDiffFmt})";

            _fitnessText.text = fmt;
            Debug.Log(fmt);
        }

        private float EvaluateFitness(SpaceshipController ship)
        {
            var fitness = 0f;

            var position = ship.Rb.position;
            var velocity = ship.Rb.linearVelocity;

            var orbit = OrbitDescription.Calculate(position, velocity);

            var env = Environment.Instance;

            // var planetRadius = env.PlanetRadius;
            var atmosphereRadius = env.AtmosphereRadius;
            var goalOrbitAltitude = atmosphereRadius + 500f;

            // var apoapsisProgress = (orbit.Apoapsis - planetRadius) / (goalOrbitAltitude - planetRadius);
            var periapsisProgress = orbit.Periapsis / goalOrbitAltitude;

            // tiny reward for getting higher up to nudge off the ground
            const float maxAtmosphereFitness = 0.01f;
            fitness += ship.HighestAtmosphereProgress * maxAtmosphereFitness;

            var angle = Vector2.Angle(
                (_spawnPosition - env.PlanetPosition).normalized,
                (position - env.PlanetPosition).normalized
            );

            // another tiny reward for going further around the planet
            const float maxAngleFitness = 0.01f;
            fitness += Mathf.Clamp01(angle / 180f) * maxAngleFitness;

            // reward for higher periapsis
            fitness += periapsisProgress;

            // bonus for more circular orbits
            const float maxEccentricityFitness = 2f;
            fitness += Mathf.Clamp01(1f - orbit.Eccentricity) * maxEccentricityFitness;

            // immediately reward any kind of stable orbit (ultimate goal)
            if (orbit.IsStable)
            {
                fitness += 1f;

                // slightly reward not using as much fuel
                const float maxFuelFitness = 0.1f;
                fitness += ship.GetFuelRemainingRatio() * maxFuelFitness;

                // reward not ending up in a spin
                const float maxAntiSpinFitness = 0.2f;
                var angularSpeed = Mathf.Abs(ship.Rb.angularVelocity);
                fitness += Mathf.Clamp01(1f - (angularSpeed / 360f)) * maxAntiSpinFitness;
            }

            return fitness;
        }

        private void EvolvePopulation()
        {
            var sortedPop = _population
                .Zip(_fitnessScores, (ship, fitness) => new { ship, fitness })
                .OrderByDescending(x => x.fitness)
                .ToList();

            var newBrains = new List<NeuralNetwork>(Options.PopulationSize);

            // keep best performers
            for (var i = 0; i < Options.EliteCount; i++)
            {
                newBrains.Add(new NeuralNetwork(sortedPop[i].ship.Brain));
            }

            while (newBrains.Count < Options.PopulationSize)
            {
                // crossover
                // var parent1 = TournamentSelect(5);
                // var parent2 = TournamentSelect(5);
                // var childBrain = new NeuralNetwork(parent1, parent2);

                // asexual
                var parent = TournamentSelect(5);
                var newBrain = new NeuralNetwork(parent);

                var weights = newBrain.GetFlatWeights();
                for (var i = 0; i < weights.Length; i++)
                {
                    if (Random.value < Options.MutationRate)
                    {
                        weights[i] += (Random.value - Random.value) * Options.MutationStrength;
                    }
                }

                newBrain.SetFlatWeights(weights);
                newBrains.Add(newBrain);
            }

            for (var i = 0; i < _population.Count; i++)
            {
                _population[i].Brain = newBrains[i];
                _fitnessScores[i] = sortedPop[i].fitness; // keep fitness scores aligned with population
            }
        }

        private NeuralNetwork TournamentSelect(int tournamentSize)
        {
            var bestIndex = -1;
            var bestFitness = float.NegativeInfinity;

            for (var i = 0; i < tournamentSize; i++)
            {
                var randomIndex = Random.Range(0, _population.Count);
                if (_fitnessScores[randomIndex] > bestFitness)
                {
                    bestFitness = _fitnessScores[randomIndex];
                    bestIndex = randomIndex;
                }
            }

            return _population[bestIndex].Brain;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(_spawnPosition, 0.5f);
        }
    }
}