using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using LaserLock.Characters;
using LaserLock.Player;
using LaserLock.Bots;
using LaserLock.Weapons;
using LaserLock.Arena;
using LaserLock.UI;

namespace LaserLock.Core
{
    public enum MatchPhase
    {
        IntroPreview,
        Positioning,
        LockAndReveal,
        Resolution,
        RoundEnd,
        MatchOver
    }

    public class MatchManager : MonoBehaviour
    {
        [Header("Character Prefabs (Drag from Assets/_LaserLock/Prefabs)")]
        [SerializeField] GameObject playerPrefab;
        [SerializeField] GameObject botPrefab;

        [Header("Arena Reference")]
        [SerializeField] ArenaBounds arenaBounds;

        [Header("UI Controller")]
        [SerializeField] GameUIController uiController;

        [Header("Phase Durations (Seconds)")]
        [Tooltip("Seconds all players are visible at match start to see the player count")]
        [SerializeField] float introDuration = 3f;

        [Header("Difficulty Scaling (Timer Reduction per Round)")]
        [Tooltip("Starting positioning time for Round 1 (e.g. 10s)")]
        [SerializeField] float initialPositioningDuration = 10f;

        [Tooltip("Seconds deducted from the positioning timer after each round")]
        [SerializeField] float timerReductionPerRound = 1.5f;

        [Tooltip("Minimum capped duration for the positioning timer (e.g. 4s)")]
        [SerializeField] float minPositioningDuration = 4f;

        [Header("Resolution Timings")]
        [Tooltip("Seconds beams remain locked before evaluating and resolving hits")]
        [SerializeField] float lockDuration = 0.6f;

        [Tooltip("Seconds after resolution to observe hits/eliminations before next round")]
        [SerializeField] float resolutionDuration = 2.5f;

        [Header("Settings")]
        [SerializeField] bool showStartWindowOnStart = true;
        [Range(1, 10)]
        [SerializeField] int defaultBotCount = 3;

        readonly List<ICharacter> characters = new List<ICharacter>();
        readonly List<GameObject> spawnedCharacterObjects = new List<GameObject>();
        ICharacter humanPlayer;

        public MatchPhase CurrentPhase { get; private set; }
        public int AliveCount { get; private set; }
        public bool IsMatchOver => CurrentPhase == MatchPhase.MatchOver;

        Coroutine matchLoopCoroutine;
        int lastSelectedBotCount = 3;

        void Awake()
        {
            if (uiController == null)
            {
                uiController = FindAnyObjectByType<GameUIController>();
            }
            if (arenaBounds == null)
            {
                arenaBounds = FindAnyObjectByType<ArenaBounds>();
            }

            lastSelectedBotCount = defaultBotCount;
        }

        void Start()
        {
            if (uiController != null)
            {
                uiController.BindStartAction(StartMatchWithBotCount);
                uiController.BindRestartAction(RestartMatch);
                uiController.SetBotCount(defaultBotCount);
            }

            if (showStartWindowOnStart)
            {
                if (uiController != null)
                {
                    uiController.ShowStartWindow();
                }
            }
            else
            {
                StartMatchWithBotCount(defaultBotCount);
            }
        }

        void OnDestroy()
        {
            UnsubscribeCharacterEvents();
        }

        /// <summary>
        /// Spawns the Player and chosen number of Bots, then begins the match.
        /// </summary>
        public void StartMatchWithBotCount(int botCount)
        {
            lastSelectedBotCount = Mathf.Clamp(botCount, 1, 10);

            // 1. Clear any previously spawned character GameObjects
            ClearSpawnedCharacters();

            // Auto-resolve arena and UI references if needed
            if (arenaBounds == null)
            {
                arenaBounds = FindAnyObjectByType<ArenaBounds>();
            }
            if (uiController == null)
            {
                uiController = FindAnyObjectByType<GameUIController>();
            }

            // 2. Spawn from prefabs if assigned
            if (playerPrefab != null && botPrefab != null && arenaBounds != null)
            {
                int totalCount = 1 + lastSelectedBotCount;
                Vector3[] spawnPoints = arenaBounds.GetSpawnPositions(totalCount);

                // Spawn Player
                GameObject pObj = Instantiate(playerPrefab, spawnPoints[0], Quaternion.identity);
                pObj.name = "Player";
                spawnedCharacterObjects.Add(pObj);

                var pCtrl = pObj.GetComponent<PlayerController>();
                if (pCtrl != null)
                {
                    characters.Add(pCtrl);
                    humanPlayer = pCtrl;
                }

                // Look toward arena center
                Vector3 toCenter = (arenaBounds.transform.position - spawnPoints[0]);
                toCenter.y = 0f;
                if (toCenter.sqrMagnitude > 0.001f)
                {
                    pObj.transform.rotation = Quaternion.LookRotation(toCenter);
                }

                // Spawn Bots
                for (int i = 0; i < lastSelectedBotCount; i++)
                {
                    Vector3 bPos = spawnPoints[1 + i];
                    Vector3 bToCenter = (arenaBounds.transform.position - bPos);
                    bToCenter.y = 0f;
                    Quaternion bRot = bToCenter.sqrMagnitude > 0.001f ? Quaternion.LookRotation(bToCenter) : Quaternion.identity;

                    GameObject bObj = Instantiate(botPrefab, bPos, bRot);
                    bObj.name = $"Bot_{i + 1}";
                    spawnedCharacterObjects.Add(bObj);

                    var bCtrl = bObj.GetComponent<BotController>();
                    if (bCtrl != null)
                    {
                        characters.Add(bCtrl);
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[MatchManager] Missing prefabs or arena! playerPrefab: {playerPrefab}, botPrefab: {botPrefab}, arenaBounds: {arenaBounds}");
            }

            // 3. Initialize characters and begin the round loop
            InitializeMatch();
        }

        void ClearSpawnedCharacters()
        {
            UnsubscribeCharacterEvents();
            characters.Clear();
            humanPlayer = null;

            foreach (var go in spawnedCharacterObjects)
            {
                if (go != null)
                {
                    go.SetActive(false); // Instantly deactivate so queries ignore it
                    Destroy(go);
                }
            }
            spawnedCharacterObjects.Clear();
        }

        public void InitializeMatch()
        {
            UnsubscribeCharacterEvents();

            // Fallback: only search scene if characters was not directly populated
            if (characters.Count == 0)
            {
                var players = FindObjectsByType<PlayerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                foreach (var p in players) characters.Add(p);

                var bots = FindObjectsByType<BotController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                foreach (var b in bots) characters.Add(b);
            }

            humanPlayer = null;
            int initialAlive = 0;

            foreach (var ch in characters)
            {
                if (ch == null) continue;

                if (ch.IsHumanPlayer && humanPlayer == null)
                {
                    humanPlayer = ch;
                }

                if (ch.IsAlive)
                {
                    initialAlive++;
                }

                ch.OnEliminated -= HandleCharacterEliminated;
                ch.OnEliminated += HandleCharacterEliminated;
            }

            AliveCount = initialAlive > 0 ? initialAlive : characters.Count;

            if (uiController != null)
            {
                uiController.HideMatchResult();
                uiController.HideStartWindow();
                uiController.UpdateAliveCount(AliveCount);
            }

            if (matchLoopCoroutine != null)
            {
                StopCoroutine(matchLoopCoroutine);
                matchLoopCoroutine = null;
            }
            matchLoopCoroutine = StartCoroutine(RunMatchLoop());
        }

        IEnumerator RunMatchLoop()
        {
            // ==========================================
            // PHASE 1: INTRO PREVIEW
            // See all players to get the player count
            // ==========================================
            CurrentPhase = MatchPhase.IntroPreview;
            SetStatus($"MATCH START: {AliveCount} PLAYERS TOTAL");

            // Update UI with initial alive count
            if (uiController != null)
            {
                uiController.UpdateAliveCount(AliveCount);
            }

            foreach (var ch in characters)
            {
                if (ch != null && ch.IsAlive)
                {
                    ch.SetVisibility(true);
                    ch.SetLaserActive(false);
                    ch.SetControlLocked(true);
                }
            }

            yield return new WaitForSeconds(introDuration);

            // ==========================================
            // ROUND LOOP
            // ==========================================
            int roundNumber = 1;

            while (AliveCount > 1 && (humanPlayer != null && humanPlayer.IsAlive))
            {
                float currentRoundDuration = Mathf.Max(
                    minPositioningDuration,
                    initialPositioningDuration - (roundNumber - 1) * timerReductionPerRound
                );

                // ------------------------------------------
                // STEP A: POSITIONING PHASE
                // Bots disappear, only human player visible
                // Human moves & aims, bots relocate in background
                // ------------------------------------------
                CurrentPhase = MatchPhase.Positioning;
                SetStatus($"ROUND {roundNumber} ({currentRoundDuration:0.0}s): SET POSITION & AIM!");

                foreach (var ch in characters)
                {
                    if (ch == null || !ch.IsAlive) continue;

                    if (ch.IsHumanPlayer)
                    {
                        ch.SetVisibility(true);
                        ch.SetControlLocked(false);
                        ch.SetLaserActive(true);
                    }
                    else
                    {
                        ch.SetVisibility(false);
                        ch.SetLaserActive(false);
                        ch.SetControlLocked(true);

                        if (ch is BotController bot)
                        {
                            bot.PerformBackgroundPositioning(characters);
                        }
                    }
                }

                float timer = currentRoundDuration;
                if (uiController != null) uiController.ShowTimer(timer);

                while (timer > 0f)
                {
                    timer -= Time.deltaTime;
                    if (uiController != null) uiController.UpdateTimer(timer);
                    yield return null;
                }

                if (uiController != null) uiController.HideTimer();

                // ------------------------------------------
                // STEP B: LOCK & REVEAL
                // Time completes -> lock player movement
                // All alive players/bots become visible, lasers on
                // ------------------------------------------
                CurrentPhase = MatchPhase.LockAndReveal;
                SetStatus("LOCKED! REVEALING PLAYERS...");

                if (humanPlayer != null && humanPlayer.IsAlive)
                {
                    humanPlayer.SetControlLocked(true);
                }

                foreach (var ch in characters)
                {
                    if (ch != null && ch.IsAlive)
                    {
                        ch.SetVisibility(true);
                        ch.SetLaserActive(true);
                    }
                }

                yield return new WaitForSeconds(lockDuration);

                // ------------------------------------------
                // STEP C: CHECK TARGETS & SIMULTANEOUS ELIMINATION
                // ------------------------------------------
                CurrentPhase = MatchPhase.Resolution;
                SetStatus("FIRING BEAMS! CHECKING TARGETS...");

                var hitsToProcess = new List<(CharacterHitBox target, Vector3 hitPoint, LaserController laser)>();

                foreach (var shooter in characters)
                {
                    if (shooter == null || !shooter.IsAlive) continue;

                    if (shooter.Weapon != null && shooter.Weapon.LaserController != null)
                    {
                        if (shooter.Weapon.LaserController.CheckTarget(out CharacterHitBox targetHitBox, out Vector3 hitPoint))
                        {
                            if (targetHitBox != null && targetHitBox.OwnerState != null && targetHitBox.OwnerState.IsAlive)
                            {
                                hitsToProcess.Add((targetHitBox, hitPoint, shooter.Weapon.LaserController));
                                Debug.Log($"[MatchManager] {shooter.CharacterName}'s laser hit {targetHitBox.OwnerState.gameObject.name}!");
                            }
                        }
                    }
                }

                // 1. Highlight all eliminating laser beams
                foreach (var hit in hitsToProcess)
                {
                    if (hit.laser != null)
                    {
                        hit.laser.TriggerEliminationSurge(hit.hitPoint);
                    }
                }

                // 2. Eliminate all hit characters simultaneously
                var uniqueHits = new HashSet<CharacterHitBox>();
                int eliminatedThisRound = 0;

                foreach (var hit in hitsToProcess)
                {
                    if (uniqueHits.Add(hit.target))
                    {
                        hit.target.TakeHit(hit.hitPoint);
                        eliminatedThisRound++;
                    }
                }

                if (eliminatedThisRound > 0)
                {
                    SetStatus($"TARGET HIT! {eliminatedThisRound} ELIMINATED!");
                }
                else
                {
                    SetStatus("NO TARGETS HIT! PREPARE NEXT ROUND!");
                }

                yield return new WaitForSeconds(resolutionDuration);

                roundNumber++;
            }

            // ==========================================
            // MATCH OVER: EVALUATE FINAL OUTCOME
            // ==========================================
            CurrentPhase = MatchPhase.MatchOver;

            foreach (var ch in characters)
            {
                if (ch != null)
                {
                    ch.SetLaserActive(false);
                }
            }

            bool isPlayerAlive = humanPlayer != null && humanPlayer.IsAlive;

            if (AliveCount == 1 && isPlayerAlive)
            {
                string winMsg = "VICTORY!\nYOU ARE THE LAST SURVIVOR!";
                SetStatus("VICTORY! YOU SURVIVED!");
                if (uiController != null) uiController.ShowMatchResult(winMsg);
                Debug.Log("[MatchManager] Match Result: PLAYER VICTORY!");
            }
            else if (AliveCount == 0)
            {
                string tieMsg = "DRAW / TIE!\nMUTUAL ELIMINATION!";
                SetStatus("DRAW! MUTUAL ELIMINATION!");
                if (uiController != null) uiController.ShowMatchResult(tieMsg);
                Debug.Log("[MatchManager] Match Result: DRAW!");
            }
            else if (!isPlayerAlive)
            {
                ICharacter survivingBot = characters.Find(c => c != null && c.IsAlive);
                string botName = survivingBot != null ? survivingBot.CharacterName : "An Opponent";
                string defeatMsg = $"DEFEAT!\n{botName.ToUpper()} WON.";
                SetStatus($"DEFEAT! {botName} SURVIVED.");
                if (uiController != null) uiController.ShowMatchResult(defeatMsg);
                Debug.Log($"[MatchManager] Match Result: DEFEAT! {botName} won.");
            }
        }

        void HandleCharacterEliminated(ICharacter eliminatedChar)
        {
            // Recalculate true living character count directly from character list
            int living = 0;
            foreach (var ch in characters)
            {
                if (ch != null && ch.IsAlive)
                {
                    living++;
                }
            }
            AliveCount = living;

            if (uiController != null)
            {
                uiController.UpdateAliveCount(AliveCount);
            }
            Debug.Log($"[MatchManager] {eliminatedChar?.CharacterName} eliminated! Alive remaining: {AliveCount}");
        }

        void SetStatus(string message)
        {
            if (uiController != null)
            {
                uiController.SetStatus(message);
            }
        }

        /// <summary>
        /// Restart the match immediately with the selected bot count and refreshed alive count.
        /// </summary>
        public void RestartMatch()
        {
            Debug.Log($"[MatchManager] RestartMatch triggered! Rebuilding match with {lastSelectedBotCount} bots...");

            if (matchLoopCoroutine != null)
            {
                StopCoroutine(matchLoopCoroutine);
                matchLoopCoroutine = null;
            }

            UnsubscribeCharacterEvents();

            // Restart immediately with the last selected bot count
            StartMatchWithBotCount(lastSelectedBotCount);
        }

        /// <summary>
        /// Optional: reopen the start window if player wants to change bot count.
        /// </summary>
        public void OpenStartWindow()
        {
            if (matchLoopCoroutine != null)
            {
                StopCoroutine(matchLoopCoroutine);
            }

            ClearSpawnedCharacters();

            if (uiController != null)
            {
                uiController.ShowStartWindow();
            }
        }

        void UnsubscribeCharacterEvents()
        {
            foreach (var ch in characters)
            {
                if (ch != null)
                {
                    ch.OnEliminated -= HandleCharacterEliminated;
                }
            }
        }
    }
}
