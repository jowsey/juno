using UnityEngine;

namespace Ship
{
    public class BodyPart : MonoBehaviour
    {
        public float BaseWeight;

        public SpaceshipStage Stage { get; private set; }

        protected void Awake()
        {
            Stage = GetComponentInParent<SpaceshipStage>();
        }

        public virtual void Reinitialise()
        {
            gameObject.SetActive(true);
        }
    }
}