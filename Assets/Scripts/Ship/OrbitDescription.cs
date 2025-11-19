using UnityEngine;

namespace Ship
{
    public struct OrbitDescription
    {
        // 0 = circle, 0-1 = ellipse, 1 = parabola, >1 = hyperbola
        public float Eccentricity;

        // lowest point (radius from center)
        public float Periapsis;

        // highest point (radius from center)
        public float Apoapsis;

        // time for one complete orbit in seconds
        public float Period;

        // true if orbit does not intersect atmosphere
        public bool IsStable;

        // true if orbit will leave planet's gravity well
        public bool IsEscaping;

        public static OrbitDescription Calculate(Vector2 position, Vector2 velocity)
        {
            var data = new OrbitDescription();

            var planetRadius = Environment.Instance.PlanetRadius;
            var atmosphereRadius = Environment.Instance.AtmosphereRadius;

            var planetPosition = Environment.Instance.PlanetPosition;
            position -= planetPosition; // equation is relative to planet center

            var mu = PlanetaryPhysics.SurfaceGravity * planetRadius * planetRadius;

            // specific orbital energy (epsilon)
            var r = position.magnitude;
            var vSqr = velocity.sqrMagnitude;
            var energy = (vSqr / 2f) - (mu / r);

            // semi-major axis (a)
            // if energy >= 0, we are escaping (hyperbolic/parabolic)
            if (Mathf.Abs(energy) < Mathf.Epsilon) energy = -Mathf.Epsilon;
            var a = -mu / (2f * energy);

            // angular momentum
            var h = position.x * velocity.y - position.y * velocity.x;

            var eTerm = 1f + (2f * energy * h * h) / (mu * mu);
            data.Eccentricity = Mathf.Sqrt(Mathf.Max(0f, eTerm));

            data.Periapsis = a * (1f - data.Eccentricity);
            if (energy < 0f)
            {
                data.Apoapsis = a * (1f + data.Eccentricity);
                data.IsEscaping = false;

                // kepler's 3rd law
                data.Period = 2f * Mathf.PI * Mathf.Sqrt(Mathf.Pow(a, 3) / mu);
            }
            else
            {
                data.Apoapsis = float.PositiveInfinity;
                data.IsEscaping = true;
                data.Period = float.PositiveInfinity;
            }

            data.IsStable = !data.IsEscaping && data.Periapsis > atmosphereRadius;
            return data;
        }
    }
}