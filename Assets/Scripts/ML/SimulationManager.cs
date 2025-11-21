using System;
using System.Collections.Generic;
using System.Linq;
using Ship;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace ML
{
    public class SimulationManager : MonoBehaviour
    {
        public static SimulationManager Instance { get; private set; }

        public bool SpeedTrainingMode { get; private set; }

        [Header("Controls")] [SerializeField] private bool _runGenerationLoop = true;
        [SerializeField] private bool _restartGeneration = true;

        [Header("Generation parameters")] [SerializeField]
        private int[] _hiddenLayers = { 12 };

        [field: SerializeField] public bool UseOutputsAsInput { get; private set; } = true;

        [SerializeField] private int _maxGenerations = 100;
        [SerializeField] private float _generationDuration = 90f;

        [SerializeField] private int _populationSize = 100;
        [SerializeField] private int _eliteCount = 5;

        [Range(0f, 1f)] [SerializeField] private float _mutationRate = 0.02f;
        [Range(0f, 1f)] [SerializeField] private float _mutationStrength = 0.1f;

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

        private CameraController _cameraController;

        private int _currentGeneration = 1;
        private float _generationElapsedTime;

        private List<SpaceshipController> _population;
        private List<float> _fitnessScores;

        private void Awake()
        {
            Instance = this;

            _cameraController = FindAnyObjectByType<CameraController>();

            _population = new List<SpaceshipController>(_populationSize);
            _fitnessScores = new List<float>(_populationSize);

            _speedInput.onValueChanged.AddListener(OnSpeedValueChanged);
            _copyStateButton.onClick.AddListener(OnCopyStateClicked);
            _pasteStateButton.onClick.AddListener(OnPasteStateClicked);
            _viewBestButton.onClick.AddListener(OnViewBestClicked);
        }

        private void Start()
        {
            var networkShape = new int[_hiddenLayers.Length + 2];
            networkShape[0] = SpaceshipController.InputCount + (UseOutputsAsInput ? SpaceshipController.OutputCount : 0); // inputs
            Array.Copy(_hiddenLayers, 0, networkShape, 1, _hiddenLayers.Length);
            networkShape[^1] = SpaceshipController.OutputCount; // outputs

            Debug.Log("Creating brains with shape " + string.Join("-", networkShape));

            for (var i = 0; i < _populationSize; i++)
            {
                var ship = Instantiate(_spaceshipPrefab, _spawnPosition, Quaternion.identity);
                ship.transform.parent = _shipContainer;

                ship.Brain = new NeuralNetwork(networkShape);

                _population.Add(ship);
                _fitnessScores.Add(0f);
            }

            UpdateFitnessUI();
        }

        private void FixedUpdate()
        {
            if (!_runGenerationLoop) return;
            _generationElapsedTime += Time.fixedDeltaTime;

            var generationComplete = _generationElapsedTime >= _generationDuration;
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

                if (newMax < prevMax)
                {
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

                var maxNumDigits = _maxGenerations.ToString().Length;
                var currentGenStr = _currentGeneration.ToString().PadLeft(maxNumDigits, '0');
                _generationText.text = $"Generation {currentGenStr} / {_maxGenerations}";

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
            if (Keyboard.current.digit1Key.wasPressedThisFrame) Time.timeScale = 1f;
            if (Keyboard.current.digit2Key.wasPressedThisFrame) Time.timeScale = 5f;
            if (Keyboard.current.digit3Key.wasPressedThisFrame) Time.timeScale = 10f;
            if (Keyboard.current.digit4Key.wasPressedThisFrame) Time.timeScale = 60f;
            if (Keyboard.current.digit5Key.wasPressedThisFrame) Time.timeScale = 100f;

            SpeedTrainingMode = Time.timeScale >= 60f;

            if (!_speedInput.isFocused)
            {
                _speedInput.text = Time.timeScale.ToString("N0");
            }

            var timespan = TimeSpan.FromSeconds(_generationElapsedTime);
            _timerText.text = $"{timespan.Minutes:D2}:{timespan.Seconds:D2}.{timespan.Milliseconds:D3}";

            var progress = Mathf.Clamp01(_generationElapsedTime / _generationDuration);
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
                _maxGenerations.ToString(),
                _generationDuration.ToString("G9"),
                _populationSize.ToString(),
                _eliteCount.ToString(),
                _mutationRate.ToString("G9"),
                _mutationStrength.ToString("G9")
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

                if (lines.Length != _population.Count + 1)
                {
                    throw new Exception(
                        "Invalid state data: probably different population size"
                    );
                }

                var parameters = lines[0].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                _currentGeneration = int.Parse(parameters[0]);
                _maxGenerations = int.Parse(parameters[1]);
                _generationDuration = float.Parse(parameters[2]);
                _populationSize = int.Parse(parameters[3]);
                _eliteCount = int.Parse(parameters[4]);
                _mutationRate = float.Parse(parameters[5]);
                _mutationStrength = float.Parse(parameters[6]);

                for (var i = 0; i < _population.Count; i++)
                {
                    var weightStrings = lines[i + 1].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    var weights = weightStrings.Select(float.Parse).ToArray();

                    _population[i].Brain.SetFlatWeights(weights);
                    _fitnessScores[i] = 0f;
                }

                UpdateFitnessUI();
                _restartGeneration = true;

                Debug.Log("<color=green>Loaded state from clipboard.</color>");
            }
            catch (Exception e)
            {
                Debug.LogError("<color=red>Failed to load state from clipboard: " + e.Message + "</color>");
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

            // elitism means first _eliteCount items will be sorted best
            var bestShip = _population[0];

            _cameraController.FollowTarget = bestShip.transform;
            _viewBestButtonText.text = "Stop viewing";
            Debug.Log($"Following best ship with fitness of {_fitnessScores[0]:F3}).");
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
            fitness += ship.HighestAtmosphereProgress * 0.01f;

            var planetPos = env.PlanetPosition.normalized;
            var angle = Vector2.Angle(
                _spawnPosition.normalized - planetPos,
                position.normalized - planetPos
            );

            // another tiny reward for going further around the planet
            fitness += Mathf.Clamp01(angle / 180f) * 0.01f;

            // reward for higher periapsis
            fitness += periapsisProgress;

            // bonus for more circular orbits
            fitness += Mathf.Clamp01(1f - orbit.Eccentricity) * 2f;

            // immediately reward any kind of stable orbit
            if (orbit.IsStable)
            {
                fitness += 5f;
            }

            return fitness;
        }

        private void EvolvePopulation()
        {
            var sortedPop = _population
                .Zip(_fitnessScores, (ship, fitness) => new { ship, fitness })
                .OrderByDescending(x => x.fitness)
                .ToList();

            var newBrains = new List<NeuralNetwork>(_populationSize);

            // keep best performers
            for (var i = 0; i < _eliteCount; i++)
            {
                newBrains.Add(new NeuralNetwork(sortedPop[i].ship.Brain));
            }

            while (newBrains.Count < _populationSize)
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
                    if (Random.value < _mutationRate)
                    {
                        weights[i] += (Random.value - Random.value) * _mutationStrength;
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