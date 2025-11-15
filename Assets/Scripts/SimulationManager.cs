using UnityEngine;

public class SimulationManager : MonoBehaviour
{
    [HideInInspector] public int CurrentGeneration = 1;

    [SerializeField] private int _maxGenerations = 100;
    [SerializeField] private int _populationSize = 100;
    [SerializeField] private float _trainingSpeed = 1f;
    [SerializeField] private float _generationTimeBudget = 60f;
    [SerializeField] private float _timePerSnapshot = 0.2f;

    [Range(0f, 1f)] [SerializeField] private float _mutationRate = 0.05f;
}