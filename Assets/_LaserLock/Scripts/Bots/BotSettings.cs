using UnityEngine;

namespace LaserLock.Bots
{
    public enum BotDifficulty
    {
        Easy,
        Medium,
        Hard
    }

    [CreateAssetMenu(fileName = "BotSettings", menuName = "LaserLock/Bot Settings")]
    public class BotSettings : ScriptableObject
    {
        [Header("Movement & Rotation")]
        public float moveSpeed = 5f;
        public float rotationSpeed = 360f;

        [Header("AI Behavior")]
        public BotDifficulty difficulty = BotDifficulty.Medium;
        public float detectionRange = 20f;

        [Range(0f, 1f)]
        [Tooltip("Chance (0 to 1) that this bot prioritizes aiming at the human player over other bots")]
        public float targetPlayerPreference = 0.65f;

        [Range(0f, 1f)]
        [Tooltip("Accuracy of the bot's aim: 1 = dead center, 0.5 = slight offset angle")]
        public float aimAccuracy = 0.85f;

        [Tooltip("Margin distance to keep away from arena boundaries")]
        public float boundarySafetyMargin = 1.2f;
    }
}
