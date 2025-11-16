using System.Collections;
using UnityEngine;

public class BodyPart : MonoBehaviour
{
    public float BaseWeight;

    private const float CrashVelocityThreshold = 3f;
    private const float ChainRadius = 2.5f;
    private static GameObject _explosionPrefab;
    private static ContactFilter2D _contactFilter;
    private static Collider2D[] _nearbyColliderResults = new Collider2D[10];

    private SpaceshipStage _parentStage;

    protected void Start()
    {
        _explosionPrefab ??= Resources.Load<GameObject>("FX/Explosion");
        if (!_contactFilter.useLayerMask)
        {
            _contactFilter.SetLayerMask(LayerMask.GetMask("BodyPart"));
        }

        _parentStage = GetComponentInParent<SpaceshipStage>();
    }

    private IEnumerator Explode()
    {
        var fx = Instantiate(_explosionPrefab, transform.position, Quaternion.identity);

        gameObject.SetActive(false); // immediately disable since destroy only queues for end of frame
        Destroy(gameObject);
        _parentStage.RecalculateLinkedParts();

        // chain reaction to nearby parts
        yield return new WaitForFixedUpdate();

        if (!_parentStage)
        {
            // explosions already destroyed everything this frame i guess?
            yield break;
        }

        var found = Physics2D.OverlapCircle(fx.transform.position, ChainRadius, _contactFilter, _nearbyColliderResults);
        for (var i = 0; i < found; i++)
        {
            var other = _nearbyColliderResults[i];

            if (other.TryGetComponent<BodyPart>(out var part))
            {
                _parentStage.StartCoroutine(part.Explode());
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        if (other.relativeVelocity.magnitude < CrashVelocityThreshold) return;
        _parentStage.StartCoroutine(Explode());
    }
}