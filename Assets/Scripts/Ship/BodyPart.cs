using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Ship
{
    public class BodyPart : MonoBehaviour
    {
        public float BaseWeight;

        private const float CrashVelocityThreshold = 5f;
        private const float ChainRadius = 2.5f;
        private static GameObject _explosionPrefab;
        private static ContactFilter2D _contactFilter;
        private static List<Collider2D> _nearbyParts = new();

        public SpaceshipStage Stage { get; private set; }

        protected void Awake()
        {
            _explosionPrefab ??= Resources.Load<GameObject>("FX/Explosion");
            if (!_contactFilter.useLayerMask)
            {
                _contactFilter.SetLayerMask(LayerMask.GetMask("Spaceship"));
            }

            Stage = GetComponentInParent<SpaceshipStage>();
        }

        public IEnumerator Explode()
        {
            Instantiate(_explosionPrefab, transform.position, Quaternion.identity);

            gameObject.SetActive(false);
            Stage.CheckForDestruction();

            // chain reaction to nearby parts
            yield return new WaitForFixedUpdate();

            var found = Physics2D.OverlapCircle(transform.position, ChainRadius, _contactFilter, _nearbyParts);
            for (var i = 0; i < found; i++)
            {
                var other = _nearbyParts[i];
                if (!other.isActiveAndEnabled) continue; // already destroyed by another explosion this frame

                if (!other.TryGetComponent<BodyPart>(out var otherPart)) continue;

                // don't explode other ships' parts
                if (otherPart.Stage.Ship != Stage.Ship) continue;

                otherPart.Stage.StartCoroutine(otherPart.Explode());
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision.relativeVelocity.magnitude < CrashVelocityThreshold) return;
            if (!Stage.isActiveAndEnabled) return; // todo check if needed

            Stage.StartCoroutine(Explode());
        }

        public virtual void Reinitialise()
        {
            gameObject.SetActive(true);
        }
    }
}