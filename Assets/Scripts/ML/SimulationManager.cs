using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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

        public bool SpeedTrainingMode;

        private int _currentGeneration;

        public const int InputCount = 10;
        public const int OutputCount = 4;

        [Header("Generation parameters")] [SerializeField]
        private int[] _hiddenLayers = { 12 };

        [SerializeField] private int _maxGenerations = 100;

        [SerializeField] private int _populationSize = 100;
        [SerializeField] private float _generationDuration = 90f;

        [SerializeField] private int _eliteCount = 5;
        [Range(0f, 1f)] [SerializeField] private float _mutationRate = 0.02f;
        [Range(0f, 1f)] [SerializeField] private float _mutationStrength = 0.1f;

        [Header("Fitness")] [SerializeField] private float _periapsisWeight = 1f;
        [SerializeField] private float _eccentricityWeight = 3f;
        [SerializeField] private float _stableOrbitBonus = 10f;

        [Header("Ship")] [SerializeField] private SpaceshipController _spaceshipPrefab;
        [SerializeField] private Vector3 _spawnPosition;
        [SerializeField] private Transform _shipContainer;

        [Header("UI")] [SerializeField] private TextMeshProUGUI _timerText;
        [SerializeField] private Image _timerProgressBar;
        [SerializeField] private TextMeshProUGUI _generationText;
        [SerializeField] private TextMeshProUGUI _fitnessText;
        [SerializeField] private TMP_InputField _speedInput;
        [SerializeField] private Button _copyStateButton;
        [SerializeField] private Button _pasteStateButton;

        private float _generationStartTime;

        private List<SpaceshipController> _population;
        private List<float> _fitnessScores;

        private void Awake()
        {
            Instance = this;

            _population = new List<SpaceshipController>(_populationSize);
            _fitnessScores = new List<float>(_populationSize);

            _speedInput.onValueChanged.AddListener(OnSpeedValueChanged);
            _copyStateButton.onClick.AddListener(OnCopyStateClicked);
            _pasteStateButton.onClick.AddListener(OnPasteStateClicked);
        }

        private void Start()
        {
            var networkShape = new int[_hiddenLayers.Length + 2];
            networkShape[0] = InputCount; // inputs
            Array.Copy(_hiddenLayers, 0, networkShape, 1, _hiddenLayers.Length);
            networkShape[^1] = OutputCount; // outputs

            Debug.Log("Creating brains with shape " + string.Join("-", networkShape));

            for (var i = 0; i < _populationSize; i++)
            {
                var ship = Instantiate(_spaceshipPrefab, _spawnPosition, Quaternion.identity);
                ship.transform.parent = _shipContainer;

                ship.Brain = new NeuralNetwork(networkShape);

                _population.Add(ship);
                _fitnessScores.Add(0f);
            }

            UpdateFitnessUI(); // set initial text

            StartCoroutine(TrainingLoop());
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

            var timespan = TimeSpan.FromSeconds(Time.time - _generationStartTime);
            _timerText.text = $"{timespan.Minutes:D2}:{timespan.Seconds:D2}.{timespan.Milliseconds:D3}";

            var progress = Mathf.Clamp01((Time.time - _generationStartTime) / _generationDuration);
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
            // copy all parameters and weights to clipboard
            var stateStrings = new List<string>
            {
                _currentGeneration.ToString(),
                _maxGenerations.ToString(),
                _populationSize.ToString(),
                _eliteCount.ToString(),
                _mutationRate.ToString("F6", CultureInfo.InvariantCulture),
                _mutationStrength.ToString("F6", CultureInfo.InvariantCulture),
                // rewards
                _periapsisWeight.ToString("F6", CultureInfo.InvariantCulture),
                _eccentricityWeight.ToString("F6", CultureInfo.InvariantCulture),
                _stableOrbitBonus.ToString("F6", CultureInfo.InvariantCulture),
            };

            foreach (var ship in _population)
            {
                var weights = ship.Brain.GetFlatWeights();
                var weightStrings = weights.Select(w => w.ToString("F6", CultureInfo.InvariantCulture));
                stateStrings.Add(string.Join(",", weightStrings));
            }

            var fullState = string.Join("\n", stateStrings);
            var encodedState = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(fullState));
            GUIUtility.systemCopyBuffer = encodedState;
            Debug.Log("<color=cyan>Copied state to clipboard.</color>");
        }

        private void OnPasteStateClicked()
        {
            try
            {
                var encodedState = GUIUtility.systemCopyBuffer;
                var fullState = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encodedState));
                var stateLines = fullState.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

                const int numParameters = 9;

                _currentGeneration = int.Parse(stateLines[0]);
                _maxGenerations = int.Parse(stateLines[1]);
                _populationSize = int.Parse(stateLines[2]);
                _eliteCount = int.Parse(stateLines[3]);
                _mutationRate = float.Parse(stateLines[4], CultureInfo.InvariantCulture);
                _mutationStrength = float.Parse(stateLines[5], CultureInfo.InvariantCulture);
                // rewards
                _periapsisWeight = float.Parse(stateLines[6], CultureInfo.InvariantCulture);
                _eccentricityWeight = float.Parse(stateLines[7], CultureInfo.InvariantCulture);
                _stableOrbitBonus = float.Parse(stateLines[8], CultureInfo.InvariantCulture);

                if (_populationSize != _population.Count)
                {
                    throw new Exception($"Population size mismatch. (Expected {_population.Count}, got {_populationSize})");
                }

                for (var i = 0; i < _population.Count; i++)
                {
                    var weightStrings = stateLines[numParameters + i].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    var weights = weightStrings.Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray();
                    _population[i].Brain.SetFlatWeights(weights);
                    _fitnessScores[i] = 0f;
                }

                Debug.Log("<color=cyan>Loaded state from clipboard.</color>");

                StopCoroutine(TrainingLoop());
                StartCoroutine(TrainingLoop());
            }
            catch (Exception e)
            {
                Debug.LogError("<color=red>Failed to load state from clipboard: " + e.Message + "</color>");
            }
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

        private IEnumerator TrainingLoop()
        {
            while (_currentGeneration < _maxGenerations)
            {
                _currentGeneration++;

                Debug.Log($"<color=green>Running generation <b>{_currentGeneration}</b></color>");
                _generationStartTime = Time.time;

                var maxNumDigits = _maxGenerations.ToString().Length;
                var currentGenStr = _currentGeneration.ToString().PadLeft(maxNumDigits, '0');
                _generationText.text = $"Generation {currentGenStr} / {_maxGenerations}";

                foreach (var ship in _population)
                {
                    ship.Rb.MovePositionAndRotation(_spawnPosition, Quaternion.identity);
                    ship.Reinitialise();
                }

                Physics2D.SyncTransforms();

                yield return new WaitForSeconds(_generationDuration);

                var prevAverage = _fitnessScores.Average();
                var prevMax = _fitnessScores.Max();

                for (var i = 0; i < _population.Count; i++)
                {
                    var ship = _population[i];
                    var fitness = EvaluateFitness(ship);
                    _fitnessScores[i] = fitness;
                }

                UpdateFitnessUI(prevAverage, prevMax);

                EvolvePopulation();
            }
        }

        private float EvaluateFitness(SpaceshipController ship)
        {
            // crashed, failed
            if (!ship.isActiveAndEnabled) return -1f;

            var fitness = 0f;

            var position = ship.Rb.position;
            var velocity = ship.Rb.linearVelocity;

            var distanceFromLaunch = Vector2.Distance(position, _spawnPosition);
            fitness += distanceFromLaunch * 0.0001f; // small reward for getting a move on

            var orbit = OrbitDescription.Calculate(position, velocity);

            var atmosphereRadius = Environment.Instance.AtmosphereRadius;
            var minOrbitAltitude = atmosphereRadius + 500f;

            // reward for higher periapsis
            fitness += (orbit.Periapsis / minOrbitAltitude) * _periapsisWeight;

            // bonus for more circular orbits
            fitness += Mathf.Clamp01(1f - orbit.Eccentricity) * _eccentricityWeight;

            // immediately reward any kind of stable orbit
            if (orbit.IsStable)
            {
                fitness += _stableOrbitBonus;
            }


            return fitness;
        }

        private void EvolvePopulation()
        {
            var sortedIndices = _fitnessScores
                .Select((fitness, index) => new { fitness, index })
                .OrderByDescending(x => x.fitness)
                .Select(x => x.index)
                .ToArray();

            var newBrains = new List<NeuralNetwork>(_populationSize);

            // keep best performers
            for (var i = 0; i < _eliteCount; i++)
            {
                newBrains.Add(new NeuralNetwork(_population[sortedIndices[i]].Brain));
            }

            while (newBrains.Count < _populationSize)
            {
                var parent1 = TournamentSelect(5);
                var parent2 = TournamentSelect(5);
                var childBrain = new NeuralNetwork(parent1, parent2);

                var weights = childBrain.GetFlatWeights();
                for (var i = 0; i < weights.Length; i++)
                {
                    if (Random.value < _mutationRate)
                    {
                        weights[i] += (Random.value - Random.value) * _mutationStrength;
                    }
                }

                childBrain.SetFlatWeights(weights);
                newBrains.Add(childBrain);
            }

            for (var i = 0; i < _population.Count; i++)
            {
                _population[i].Brain = newBrains[i];
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