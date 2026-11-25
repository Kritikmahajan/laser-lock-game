using System;
using System.Collections.Generic;
using UnityEngine;
using LaserLock.Characters;
using LaserLock.Weapons;
using LaserLock.Arena;

namespace LaserLock.Bots
{
    [RequireComponent(typeof(CharacterState))]
    [RequireComponent(typeof(CharacterMotor))]
    public class BotController : MonoBehaviour, ICharacter
    {
        [Header("References")]
        [SerializeField] ArenaBounds arenaBounds;
        [SerializeField] WeaponController weapon;
        [SerializeField] BotAI botAI;

        [Header("Components (Auto-assigned if null)")]
        [SerializeField] CharacterMotor motor;
        [SerializeField] CharacterState state;

        public Transform Transform => transform;
        public bool IsAlive => state != null && state.IsAlive;
        public bool IsHumanPlayer => false;
        public string CharacterName => gameObject.name;
        public CharacterState State => state;
        public WeaponController Weapon => weapon;

        public event Action<ICharacter> OnEliminated;

        void Awake()
        {
            if (motor == null) motor = GetComponent<CharacterMotor>();
            if (state == null) state = GetComponent<CharacterState>();
            if (weapon == null) weapon = GetComponentInChildren<WeaponController>();
            if (botAI == null) botAI = GetComponent<BotAI>();
            if (botAI == null) botAI = gameObject.AddComponent<BotAI>();
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

        /// <summary>
        /// Called by MatchManager when the 10-second positioning phase begins.
        /// The bot picks a new position and aim target in the background while invisible.
        /// </summary>
        public void PerformBackgroundPositioning(List<ICharacter> allCharacters)
        {
            if (!IsAlive) return;

            // 1. Choose new location
            Vector3 newPosition = botAI.ChooseRepositionPoint(arenaBounds, transform.position);
            newPosition.y = transform.position.y;
            transform.position = newPosition;

            // 2. Choose aim target
            Vector3 aimTarget = botAI.ChooseAimTarget(transform, allCharacters);
            aimTarget.y = transform.position.y;

            // 3. Face and aim towards target
            Vector3 lookDir = (aimTarget - transform.position).normalized;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(lookDir);
            }

            if (weapon != null)
            {
                weapon.AimAt(aimTarget);
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
            if (weapon != null)
            {
                weapon.SetLaserActive(false);
            }
            OnEliminated?.Invoke(this);
        }
    }
}
