using UnityEngine;

public class Genome
{
    [Range(0f, 1f)] public float BoosterSeparationDelay;
    [Range(0f, 1f)] public float MainStageSeparationDelay;

    [Range(0f, 1f)] public float MaxQStartTime;
    [Range(0f, 1f)] public float MaxQDuration;
    [Range(0f, 1f)] public float MaxQThrustControl;
    
    [Range(0f, 1f)] public float GravityTurnStartTime;
    [Range(0f, 1f)] public float GravityTurnDuration;

    // Create a new random genome
    public Genome(int snapshotCount)
    {
    }

    // Combine two genomes to create a new genome
    public Genome(Genome parent1, Genome parent2)
    {
    }
}