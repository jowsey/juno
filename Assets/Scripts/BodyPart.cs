using UnityEngine;

public class BodyPart : MonoBehaviour
{
    public float BaseWeight;

    private void OnValidate()
    {
        var parentStage = GetComponentInParent<SpaceshipStage>();
        if (parentStage)
        {
            parentStage.RecalculateLinkedParts();
        }
    }
}