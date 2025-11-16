using System.Collections;
using UnityEngine;

public class BodyPart : MonoBehaviour
{
    public float BaseWeight;

    private const float CrashVelocityThreshold = 3f;
    private static GameObject _explosionPrefab;
    private static ContactFilter2D _contactFilter;
    private static Collider2D[] _nearbyColliderResults = new Collider2D[10];

    [SerializeField] private SpaceshipStage _parentStage;

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
        fx.transform.localScale = transform.lossyScale;

        gameObject.SetActive(false); // immediately disable since destroy only queues for end of frame
        Destroy(gameObject);
        _parentStage.RecalculateLinkedParts();

        // chain reaction to nearby parts
        yield return new WaitForFixedUpdate();
        var numFound = Physics2D.OverlapCircle(fx.transform.position, 2f, _contactFilter, _nearbyColliderResults);
        for (var i = 0; i < numFound; i++)
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
        if (!_parentStage)
        {
            Debug.Log(this + " doesn't have parent stage????", this);
        }

        _parentStage.StartCoroutine(Explode());
    }
}