using UnityEngine;

namespace LaserLock.Characters
{
    public class CharacterMotor : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] float moveSpeed = 6f;
        [SerializeField] float rotationSpeed = 720f;

        [Header("Dependencies (Optional - auto resolved if null)")]
        [SerializeField] CharacterState state;
        [SerializeField] Rigidbody rb;

        void Awake()
        {
            if (state == null)
            {
                state = GetComponent<CharacterState>();
            }
            if (rb == null)
            {
                rb = GetComponent<Rigidbody>();
            }
        }

        public void Move(Vector3 direction)
        {
            if (state != null && (!state.IsAlive || state.IsControlLocked)) return;
            if (direction.sqrMagnitude < 0.001f) return;

            Vector3 movement = direction.normalized * moveSpeed * Time.deltaTime;

            if (rb != null && !rb.isKinematic)
            {
                rb.MovePosition(rb.position + movement);
            }
            else
            {
                transform.position += movement;
            }
        }

        public void RotateTowards(Vector3 targetWorldPosition)
        {
            if (state != null && (!state.IsAlive || state.IsControlLocked)) return;

            Vector3 direction = targetWorldPosition - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.001f) return;

            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }

        public void SetSpeed(float newSpeed)
        {
            moveSpeed = newSpeed;
        }

        public void SetRotationSpeed(float newRotSpeed)
        {
            rotationSpeed = newRotSpeed;
        }
    }
}
