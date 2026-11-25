using UnityEngine;

namespace LaserLock.Characters
{
    /// <summary>
    /// Attach this component to the Character's body collider.
    /// Used by the Laser System to distinguish body hits from weapons, beams, or scenery.
    /// </summary>
    public class CharacterHitBox : MonoBehaviour
    {
        [SerializeField] CharacterState ownerState;

        public CharacterState OwnerState => ownerState;

        void Awake()
        {
            if (ownerState == null)
            {
                ownerState = GetComponentInParent<CharacterState>();
            }
        }

        public void TakeHit(Vector3 hitPoint = default)
        {
            if (ownerState != null && ownerState.IsAlive)
            {
                if (hitPoint == default)
                {
                    hitPoint = transform.position + Vector3.up * 0.5f;
                }
                ownerState.Eliminate(hitPoint);
            }
        }
    }
}
