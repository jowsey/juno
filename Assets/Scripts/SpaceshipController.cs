using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class StageSet
{
    public SpaceshipStage[] Stages;
}

[RequireComponent(typeof(Rigidbody2D), typeof(SpaceshipStage))]
public class SpaceshipController : MonoBehaviour
{
    private Rigidbody2D _rb;
    private SpaceshipStage _topLevelStage;

    [SerializeField] private List<StageSet> _stageSets;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _topLevelStage = GetComponent<SpaceshipStage>();
        _topLevelStage.IsTopLevel = true;
    }

    private void Start()
    {
        // mitigate unity physics glitch on initialise
        _rb.rotation = 0f;
        Physics2D.SyncTransforms();
    }

    public void SeparateStage()
    {
        if (_stageSets.Count == 0) return;

        foreach (var stage in _stageSets[0].Stages)
        {
            stage.Separate();
        }

        _stageSets.RemoveAt(0);
    }
}