using System.Collections;
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
        private static Collider2D[] _nearbyParts = new Collider2D[10];

        private SpaceshipStage _stage;

        protected void Awake()
        {
            _explosionPrefab ??= Resources.Load<GameObject>("FX/Explosion");
            if (!_contactFilter.useLayerMask)
            {
                _contactFilter.SetLayerMask(LayerMask.GetMask("Spaceship"));
            }

            _stage = GetComponentInParent<SpaceshipStage>();
        }

        public IEnumerator Explode()
        {
            Instantiate(_explosionPrefab, transform.position, Quaternion.identity);

            gameObject.SetActive(false);
            _stage.CheckForDestruction();

            // chain reaction to nearby parts
            yield return new WaitForFixedUpdate();

            var found = Physics2D.OverlapCircle(transform.position, ChainRadius, _contactFilter, _nearbyParts);
            for (var i = 0; i < found; i++)
            {
                // coroutines are weirdly async? if we put this check above overlapcircle,
                // it can become inactive between then and now, so we just check later and suck up the
                // potentially unnecessary overlapcircle call
                // todo this whole section can definitely be cleaned up
                if (!_stage.isActiveAndEnabled) yield break;

                var other = _nearbyParts[i];
                if (!other.isActiveAndEnabled) continue; // already destroyed (also avoids self)

                // don't explode other ships' parts
                if (other.GetComponentInParent<SpaceshipStage>().Ship != _stage.Ship) continue;

                if (other.TryGetComponent<BodyPart>(out var part))
                {
                    _stage.StartCoroutine(part.Explode());
                }
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision.relativeVelocity.magnitude < CrashVelocityThreshold) return;
            if (!_stage.isActiveAndEnabled) return; // todo check if needed

            _stage.StartCoroutine(Explode());
        }

        public virtual void Reinitialise()
        {
            gameObject.SetActive(true);
        }
    }
}