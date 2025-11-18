using UnityEngine;

public class Environment : MonoBehaviour
{
    [field: SerializeField] public Transform EarthCore { get; private set; }

    [field: SerializeField] public Transform EarthAtmosphere { get; private set; }

    public float EarthCoreRadius { get; private set; }

    public float EarthAtmosphereRadius { get; private set; }

    public Vector2 EarthCorePosition { get; private set; }

    public Vector2 EarthAtmospherePosition { get; private set; }

    private void Awake()
    {
        EarthCoreRadius = EarthCore.lossyScale.x * 0.5f;
        EarthAtmosphereRadius = EarthAtmosphere.lossyScale.x * 0.5f;

        EarthCorePosition = EarthCore.position;
        EarthAtmospherePosition = EarthAtmosphere.position;
    }
}