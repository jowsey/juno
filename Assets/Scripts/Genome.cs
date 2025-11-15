// using UnityEngine;
//
// public class Genome
// {
//         
//     // Create a new random genome
//     public Genome(int snapshotCount)
//     {
//         Snapshots = new ControlSnapshot[snapshotCount];
//         for (var i = 0; i < snapshotCount; i++)
//         {
//             var snapshot = new ControlSnapshot
//             {
//                 ThrustControl = Random.Range(0f, 1f),
//                 SteeringControl = Random.Range(-1f, 1f),
//                 SeparateStage = Random.Range(0f, 1f) > 0.5f
//             };
//
//             Snapshots[i] = snapshot;
//         }
//     }
//
//     // Combine two genomes to create a new genome
//     public Genome(Genome parent1, Genome parent2)
//     {
//         var snapshotCount = parent1.Snapshots.Length;
//         Snapshots = new ControlSnapshot[snapshotCount];
//
//         for (var i = 0; i < snapshotCount; i++)
//         {
//             var chosenParent = Random.value < 0.5f ? parent1 : parent2;
//             var snapshot = chosenParent.Snapshots[i];
//             Snapshots[i] = snapshot;
//         }
//     }
// }

