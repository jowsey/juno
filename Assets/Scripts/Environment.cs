using UnityEngine;

public class Environment : MonoBehaviour
{
    public static Environment Instance { get; private set; }

    [field: SerializeField] public Transform EarthCore { get; private set; }

    [field: SerializeField] public Transform EarthAtmosphere { get; private set; }

    public float PlanetRadius { get; private set; }

    public float AtmosphereRadius { get; private set; }

    public Vector2 PlanetPosition { get; private set; }

    public Vector2 AtmospherePosition { get; private set; }

    private void Awake()
    {
        Instance = this;

        PlanetRadius = EarthCore.lossyScale.x * 0.5f;
        AtmosphereRadius = EarthAtmosphere.lossyScale.x * 0.5f;

        PlanetPosition = EarthCore.position;
        AtmospherePosition = EarthAtmosphere.position;
    }
}