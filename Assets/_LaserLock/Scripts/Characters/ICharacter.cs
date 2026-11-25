using System;
using UnityEngine;
using LaserLock.Weapons;

namespace LaserLock.Characters
{
    public interface ICharacter
    {
        Transform Transform { get; }
        bool IsAlive { get; }
        bool IsHumanPlayer { get; }
        string CharacterName { get; }
        CharacterState State { get; }
        WeaponController Weapon { get; }

        void Move(Vector3 direction);
        void RotateTowards(Vector3 worldPosition);
        void SetVisibility(bool visible);
        void SetControlLocked(bool locked);
        void SetLaserActive(bool active);
        void Eliminate();

        event Action<ICharacter> OnEliminated;
    }
}
