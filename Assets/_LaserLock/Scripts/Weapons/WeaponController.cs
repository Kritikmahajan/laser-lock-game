using UnityEngine;
using LaserLock.Characters;

namespace LaserLock.Weapons
{
    public class WeaponController : MonoBehaviour
    {
        [Header("Weapon Transforms")]
        [Tooltip("The pivot transform to rotate when aiming (can be the gun itself or an arm/weapon pivot)")]
        [SerializeField] Transform weaponPivot;
        [Tooltip("The tip of the barrel where the laser originates")]
        [SerializeField] Transform muzzleTransform;

        [Header("Components")]
        [SerializeField] LaserController laserController;
        [SerializeField] CharacterState ownerState;

        [Header("Aiming Settings")]
        [SerializeField] float aimSpeed = 720f;

        public Transform MuzzleTransform => muzzleTransform;
        public LaserController LaserController => laserController;
        public Vector3 FireDirection => muzzleTransform != null ? muzzleTransform.forward : transform.forward;

        void Awake()
        {
            if (weaponPivot == null)
            {
                weaponPivot = transform;
            }
            if (laserController == null)
            {
                laserController = GetComponentInChildren<LaserController>();
            }
            if (ownerState == null)
            {
                ownerState = GetComponentInParent<CharacterState>();
            }
        }

        public void AimAt(Vector3 targetWorldPosition)
        {
            if (ownerState != null && (!ownerState.IsAlive || ownerState.IsControlLocked)) return;
            if (weaponPivot == null) return;

            Vector3 direction = targetWorldPosition - weaponPivot.position;
            direction.y = 0f; // Keep laser aiming on horizontal plane

            if (direction.sqrMagnitude < 0.001f) return;

            Quaternion targetRotation = Quaternion.LookRotation(direction);
            weaponPivot.rotation = Quaternion.RotateTowards(
                weaponPivot.rotation,
                targetRotation,
                aimSpeed * Time.deltaTime
            );
        }

        public void SetLaserActive(bool active)
        {
            if (laserController != null)
            {
                laserController.SetLaserActive(active);
            }
        }

        public void SetBeamColor(Color color)
        {
            if (laserController != null)
            {
                laserController.SetBeamColor(color);
            }
        }
    }
}
