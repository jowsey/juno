using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Ship;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ML
{
    public class SimulationManager : MonoBehaviour
    {
        [Header("Generation parameters")] [SerializeField]
        private int _maxGenerations = 100;

        [SerializeField] private int _populationSize = 100;
        [SerializeField] private float _trainingTimeScale = 1f;
        [SerializeField] private float _generationDuration = 60f;

        [SerializeField] private int _eliteCount = 10;
        [Range(0f, 1f)] [SerializeField] private float _mutationRate = 0.05f;
        [Range(0f, 1f)] [SerializeField] private float _mutationStrength = 0.5f;

        [Header("Ship")] [SerializeField] private SpaceshipController _spaceshipPrefab;
        [SerializeField] private Vector3 _spawnPosition;
        [SerializeField] private Transform _shipContainer;

        [Header("State")] public int CurrentGeneration = 1;

        [Header("UI")] [SerializeField] private TextMeshProUGUI _timerText;
        [SerializeField] private TextMeshProUGUI _generationText;

        private float _generationStartTime;

        private List<SpaceshipController> _population;
        private List<float> _fitnessScores;

        private void Awake()
        {
            _population = new List<SpaceshipController>(_populationSize);
            _fitnessScores = new List<float>(_populationSize);
        }

        private void Start()
        {
            for (var i = 0; i < _populationSize; i++)
            {
                var ship = Instantiate(_spaceshipPrefab, _spawnPosition, Quaternion.identity);
                ship.transform.parent = _shipContainer;

                _population.Add(ship);
                _fitnessScores.Add(0f);
            }

            StartCoroutine(TrainingLoop());
        }

        private void Update()
        {
            Time.timeScale = _trainingTimeScale;
        }

        private void LateUpdate()
        {
            var timespan = TimeSpan.FromSeconds(Time.time - _generationStartTime);
            _timerText.text = $"{timespan.Minutes:D2}:{timespan.Seconds:D2}.{timespan.Milliseconds:D3}";

            var maxNumDigits = _maxGenerations.ToString().Length;
            var currentGenStr = CurrentGeneration.ToString().PadLeft(maxNumDigits, '0');
            _generationText.text = $"Generation {currentGenStr} / {_maxGenerations}";
        }

        private IEnumerator TrainingLoop()
        {
            // we do everything through physics, so disable auto-sync
            Physics2D.autoSyncTransforms = false;

            while (CurrentGeneration <= _maxGenerations)
            {
                Debug.Log($"<color=green>Running generation <b>{CurrentGeneration}</b></color>");
                _generationStartTime = Time.time;

                foreach (var ship in _population)
                {
                    ship.Reinitialise();
                }

                yield return new WaitForSeconds(_generationDuration);

                for (var i = 0; i < _population.Count; i++)
                {
                    var ship = _population[i];
                    var fitness = EvaluateFitness(ship);
                    _fitnessScores[i] = fitness;
                }

                var maxFitness = _fitnessScores.Max();
                var avgFitness = _fitnessScores.Average();
                var highestShip = _population[_fitnessScores.IndexOf(maxFitness)];
                Debug.Log($"Highest fitness: {maxFitness} ({highestShip}), average: {avgFitness}");

                EvolvePopulation();
                CurrentGeneration++;
            }
        }

        private float EvaluateFitness(SpaceshipController ship)
        {
            var fitness = 0f;

            // crashed, failed
            if (!ship.isActiveAndEnabled)
            {
                return -1f;
            }

            // todo detect if in orbit
            // todo detect circularity of orbit

            fitness += ship.PlanetaryPhysics.GetAltitude();

            return Mathf.Max(0f, fitness);
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
                var childBrain = NeuralNetwork.FromParents(parent1, parent2);

                var weights = childBrain.GetFlatWeights();
                for (var i = 0; i < weights.Length; i++)
                {
                    if (Random.value < _mutationRate)
                    {
                        weights[i] += Random.Range(-_mutationStrength, _mutationStrength);
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