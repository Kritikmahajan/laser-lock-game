using System.Collections.Generic;
using UnityEngine;
using LaserLock.Characters;
using LaserLock.Arena;

namespace LaserLock.Bots
{
    public class BotAI : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] BotSettings settings;

        public BotSettings Settings => settings;

        public void SetSettings(BotSettings newSettings)
        {
            settings = newSettings;
        }

        /// <summary>
        /// Selects a new position inside the arena for the positioning phase.
        /// Avoids staying in the exact same spot.
        /// </summary>
        public Vector3 ChooseRepositionPoint(ArenaBounds arenaBounds, Vector3 currentPosition)
        {
            float margin = settings != null ? settings.boundarySafetyMargin : 1.2f;

            if (arenaBounds == null)
            {
                // Fallback if no arena bounds assigned
                return currentPosition + new Vector3(Random.Range(-4f, 4f), 0f, Random.Range(-4f, 4f));
            }

            Vector3 newPos = arenaBounds.GetRandomPosition(margin);

            // Ensure the bot actually relocates instead of picking the identical spot
            int attempts = 0;
            while (Vector3.Distance(newPos, currentPosition) < 2f && attempts < 5)
            {
                newPos = arenaBounds.GetRandomPosition(margin);
                attempts++;
            }

            return newPos;
        }

        /// <summary>
        /// Selects an opponent to aim at and returns the predicted/offset aim position.
        /// </summary>
        public Vector3 ChooseAimTarget(Transform selfTransform, List<ICharacter> allCharacters)
        {
            if (allCharacters == null || allCharacters.Count == 0)
            {
                // Random forward-facing direction across arena
                return selfTransform.position + selfTransform.forward * 10f;
            }

            // Filter to only alive opponents
            List<ICharacter> opponents = new List<ICharacter>();
            ICharacter humanPlayer = null;

            foreach (var ch in allCharacters)
            {
                if (ch != null && ch.IsAlive && ch.Transform != selfTransform)
                {
                    opponents.Add(ch);
                    if (ch.IsHumanPlayer)
                    {
                        humanPlayer = ch;
                    }
                }
            }

            if (opponents.Count == 0)
            {
                return selfTransform.position + selfTransform.forward * 10f;
            }

            float preference = settings != null ? settings.targetPlayerPreference : 0.6f;
            float accuracy = settings != null ? settings.aimAccuracy : 0.85f;

            ICharacter selectedTarget;

            // Roll to decide if we target the human player or a random other bot
            if (humanPlayer != null && Random.value < preference)
            {
                selectedTarget = humanPlayer;
            }
            else
            {
                selectedTarget = opponents[Random.Range(0, opponents.Count)];
            }

            Vector3 targetPos = selectedTarget.Transform.position;

            // Apply accuracy offset (imperfect aim based on difficulty)
            float inaccuracySpread = (1f - accuracy) * 2.5f;
            if (inaccuracySpread > 0.01f)
            {
                Vector2 randomOffset = Random.insideUnitCircle * inaccuracySpread;
                targetPos += new Vector3(randomOffset.x, 0f, randomOffset.y);
            }

            return targetPos;
        }
    }
}
