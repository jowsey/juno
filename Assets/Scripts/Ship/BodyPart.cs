using UnityEngine;

namespace Ship
{
    public class BodyPart : MonoBehaviour
    {
        public float BaseWeight;

        public virtual void Reinitialise()
        {
            gameObject.SetActive(true);
        }
    }
}