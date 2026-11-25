using System;
using UnityEngine;
using LaserLock.Characters;
using LaserLock.Weapons;
using LaserLock.Arena;

namespace LaserLock.Player
{
    [RequireComponent(typeof(CharacterState))]
    [RequireComponent(typeof(CharacterMotor))]
    [RequireComponent(typeof(PlayerInputHandler))]
    public class PlayerController : MonoBehaviour, ICharacter
    {
        [Header("References")]
        [SerializeField] Camera aimCamera;
        [SerializeField] ArenaBounds arenaBounds;
        [SerializeField] WeaponController weapon;

        [Header("Components (Auto-assigned if null)")]
        [SerializeField] CharacterMotor motor;
        [SerializeField] CharacterState state;
        [SerializeField] PlayerInputHandler inputHandler;

        public Transform Transform => transform;
        public bool IsAlive => state != null && state.IsAlive;
        public bool IsHumanPlayer => true;
        public string CharacterName => gameObject.name;
        public CharacterState State => state;
        public WeaponController Weapon => weapon;

        public event Action<ICharacter> OnEliminated;

        void Awake()
        {
            if (motor == null) motor = GetComponent<CharacterMotor>();
            if (state == null) state = GetComponent<CharacterState>();
            if (inputHandler == null) inputHandler = GetComponent<PlayerInputHandler>();
            if (weapon == null) weapon = GetComponentInChildren<WeaponController>();
            if (aimCamera == null) aimCamera = Camera.main;
            if (arenaBounds == null) arenaBounds = FindAnyObjectByType<ArenaBounds>();
        }

        void Start()
        {
            if (state != null)
            {
                state.OnEliminated += HandleEliminated;
            }
        }

        void OnDestroy()
        {
            if (state != null)
            {
                state.OnEliminated -= HandleEliminated;
            }
        }

        void Update()
        {
            if (!IsAlive) return;

            // When controls are locked (e.g. during target check / resolution phase), do not move or aim
            if (state != null && state.IsControlLocked) return;

            HandleMovement();
            HandleAiming();
        }

        void HandleMovement()
        {
            Vector2 input = inputHandler.MoveInput;
            if (input.sqrMagnitude > 0.001f)
            {
                Vector3 moveDir = new Vector3(input.x, 0f, input.y);
                motor.Move(moveDir);
            }

            if (arenaBounds != null)
            {
                transform.position = arenaBounds.ClampPosition(transform.position);
            }
        }

        void HandleAiming()
        {
            if (aimCamera == null || inputHandler == null) return;

            // 1. Direct Aim Direction (Mobile Right Joystick)
            if (inputHandler.AimDirection.sqrMagnitude > 0.01f)
            {
                Vector3 targetWorldPos = transform.position + new Vector3(inputHandler.AimDirection.x, 0f, inputHandler.AimDirection.y) * 10f;
                motor.RotateTowards(targetWorldPos);

                if (weapon != null)
                {
                    weapon.AimAt(targetWorldPos);
                }
                return;
            }

            // 2. Screen Pointer / Touch Position (Mobile Touch-to-Aim or Mouse Cursor)
            if (inputHandler.HasPointerPosition)
            {
                Vector2 screenPos = inputHandler.PointerScreenPosition;
                Ray ray = aimCamera.ScreenPointToRay(screenPos);
                Plane aimPlane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));

                if (aimPlane.Raycast(ray, out float enter))
                {
                    Vector3 targetWorldPos = ray.GetPoint(enter);

                    motor.RotateTowards(targetWorldPos);

                    if (weapon != null)
                    {
                        weapon.AimAt(targetWorldPos);
                    }
                }
            }
        }

        public void Move(Vector3 direction)
        {
            motor.Move(direction);
        }

        public void RotateTowards(Vector3 worldPosition)
        {
            motor.RotateTowards(worldPosition);
            if (weapon != null)
            {
                weapon.AimAt(worldPosition);
            }
        }

        public void SetVisibility(bool visible)
        {
            if (state != null)
            {
                state.SetVisibility(visible);
            }
        }

        public void SetControlLocked(bool locked)
        {
            if (state != null)
            {
                state.SetControlLocked(locked);
            }
        }

        public void SetLaserActive(bool active)
        {
            if (weapon != null)
            {
                weapon.SetLaserActive(active);
            }
        }

        public void Eliminate()
        {
            if (state != null)
            {
                state.Eliminate();
            }
        }

        void HandleEliminated(CharacterState _)
        {
            if (inputHandler != null)
            {
                inputHandler.DisableInput();
            }
            if (weapon != null)
            {
                weapon.SetLaserActive(false);
            }
            OnEliminated?.Invoke(this);
        }
    }
}
