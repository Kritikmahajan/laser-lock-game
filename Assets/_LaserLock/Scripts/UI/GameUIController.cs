using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LaserLock.UI
{
    public class GameUIController : MonoBehaviour
    {
        [Header("Start Window References")]
        [Tooltip("The initial setup window where player selects bot count")]
        [SerializeField] GameObject startPanel;
        [Tooltip("Slider capped from 1 to 10")]
        [SerializeField] Slider botCountSlider;
        [Tooltip("Text showing the currently selected bot count (e.g., '3 Bots')")]
        [SerializeField] TextMeshProUGUI botCountText;
        [Tooltip("Button that starts the match")]
        [SerializeField] Button startButton;
        [Tooltip("Optional Plus button to increase bot count")]
        [SerializeField] Button increaseBotButton;
        [Tooltip("Optional Minus button to decrease bot count")]
        [SerializeField] Button decreaseBotButton;

        [Header("Gameplay HUD References")]
        [SerializeField] Canvas gameCanvas;
        [SerializeField] TextMeshProUGUI timerText;
        [SerializeField] TextMeshProUGUI statusText;
        [SerializeField] TextMeshProUGUI aliveCountText;
        [Tooltip("Restart button accessible during gameplay")]
        [SerializeField] Button gameplayRestartButton;

        [Header("Mobile Controls (Optional)")]
        [Tooltip("Optional Canvas panel holding virtual joysticks / touch controls")]
        [SerializeField] GameObject mobileControlsPanel;
        [Tooltip("Force show mobile controls in Editor for testing")]
        [SerializeField] bool showMobileControls = true;

        [Header("Result Panel References")]
        [SerializeField] GameObject resultPanel;
        [SerializeField] TextMeshProUGUI resultText;
        [Tooltip("Restart button displayed on the game over / victory panel")]
        [SerializeField] Button resultRestartButton;
        [Tooltip("Legacy / Fallback Restart Button")]
        [SerializeField] Button restartButton;

        [Header("Immediate GUI Fallback")]
        [Tooltip("Automatically disabled if Canvas UI elements are hooked up.")]
        [SerializeField] bool enableOnGUIFallback = true;

        [Header("Settings")]
        [Range(1, 10)]
        [SerializeField] int selectedBotCount = 3;

        float currentTimer = 0f;
        bool isTimerActive = false;
        string currentStatus = "READY";
        int currentAliveCount = 0;
        bool isMatchOver = false;
        bool isMatchStarted = false;
        string currentResult = "";

        Color originalTimerColor = Color.black;

        Action<int> onStartRequested;
        Action onRestartRequested;

        void Awake()
        {
            // Configure Bot Count Slider
            if (botCountSlider != null)
            {
                botCountSlider.minValue = 1;
                botCountSlider.maxValue = 10;
                botCountSlider.wholeNumbers = true;
                botCountSlider.value = selectedBotCount;
                botCountSlider.onValueChanged.AddListener(HandleBotSliderChanged);
            }

            // Plus / Minus Buttons
            if (increaseBotButton != null)
            {
                increaseBotButton.onClick.AddListener(() => SetBotCount(selectedBotCount + 1));
            }
            if (decreaseBotButton != null)
            {
                decreaseBotButton.onClick.AddListener(() => SetBotCount(selectedBotCount - 1));
            }

            // Start Button
            if (startButton != null)
            {
                startButton.onClick.AddListener(HandleStartClicked);
            }

            // Auto fallback for gameplay and result restart buttons if assigned via legacy field or inside resultPanel
            if (gameplayRestartButton == null && restartButton != null)
            {
                gameplayRestartButton = restartButton;
            }
            if (resultRestartButton == null && resultPanel != null)
            {
                var panelButtons = resultPanel.GetComponentsInChildren<Button>(true);
                if (panelButtons != null && panelButtons.Length > 0)
                {
                    resultRestartButton = panelButtons[0];
                }
            }

            // Restart Buttons
            if (gameplayRestartButton != null)
            {
                gameplayRestartButton.onClick.RemoveListener(HandleRestartClicked);
                gameplayRestartButton.onClick.AddListener(HandleRestartClicked);
            }
            if (resultRestartButton != null)
            {
                resultRestartButton.onClick.RemoveListener(HandleRestartClicked);
                resultRestartButton.onClick.AddListener(HandleRestartClicked);
            }
            if (restartButton != null && restartButton != gameplayRestartButton && restartButton != resultRestartButton)
            {
                restartButton.onClick.RemoveListener(HandleRestartClicked);
                restartButton.onClick.AddListener(HandleRestartClicked);
            }

            // Hide Result Panel at start
            if (resultPanel != null)
            {
                resultPanel.SetActive(false);
            }

            // Auto fallback for aliveCountText and statusText if unassigned in inspector
            if (aliveCountText == null)
            {
                var allTmps = GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var t in allTmps)
                {
                    if (t.name.IndexOf("Alive", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        (t.transform.parent != null && t.transform.parent.name.IndexOf("Alive", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        aliveCountText = t;
                        break;
                    }
                }
            }
            if (statusText == null)
            {
                var allTmps = GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var t in allTmps)
                {
                    if (t.name.IndexOf("Status", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        (t.transform.parent != null && t.transform.parent.name.IndexOf("Status", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        statusText = t;
                        break;
                    }
                }
            }

            // Cache timer text color
            if (timerText != null)
            {
                originalTimerColor = timerText.color;
            }

            UpdateBotCountDisplay();
            UpdateAliveCount(1 + selectedBotCount);

            // Disable OnGUI fallback automatically if Canvas elements are configured
            if (gameCanvas != null && (timerText != null || statusText != null || startButton != null || startPanel != null))
            {
                enableOnGUIFallback = false;
            }
        }

        public void BindStartAction(Action<int> startAction)
        {
            onStartRequested = startAction;
        }

        public void BindRestartAction(Action restartAction)
        {
            onRestartRequested = restartAction;
        }

        public void ShowStartWindow()
        {
            isMatchStarted = false;
            isMatchOver = false;

            if (startPanel != null)
            {
                startPanel.SetActive(true);
            }
            if (resultPanel != null)
            {
                resultPanel.SetActive(false);
            }
            if (mobileControlsPanel != null)
            {
                mobileControlsPanel.SetActive(false);
            }

            HideTimer();
            SetStatus("SELECT BOTS & START");
            UpdateAliveCount(1 + selectedBotCount);
        }

        public void HideStartWindow()
        {
            isMatchStarted = true;
            if (startPanel != null)
            {
                startPanel.SetActive(false);
            }
            if (mobileControlsPanel != null)
            {
                bool showMobile = Application.isMobilePlatform || showMobileControls;
                mobileControlsPanel.SetActive(showMobile);
            }
        }

        public void SetBotCount(int count)
        {
            selectedBotCount = Mathf.Clamp(count, 1, 10);
            if (botCountSlider != null && (int)botCountSlider.value != selectedBotCount)
            {
                botCountSlider.value = selectedBotCount;
            }
            UpdateBotCountDisplay();
            if (!isMatchStarted)
            {
                UpdateAliveCount(1 + selectedBotCount);
            }
        }

        void HandleBotSliderChanged(float value)
        {
            selectedBotCount = Mathf.Clamp((int)value, 1, 10);
            UpdateBotCountDisplay();
            if (!isMatchStarted)
            {
                UpdateAliveCount(1 + selectedBotCount);
            }
        }

        void UpdateBotCountDisplay()
        {
            if (botCountText != null)
            {
                botCountText.text = $"Bots: {selectedBotCount}";
            }
        }

        void HandleStartClicked()
        {
            HideStartWindow();
            onStartRequested?.Invoke(selectedBotCount);
        }

        void HandleRestartClicked()
        {
            Debug.Log("[GameUIController] Restart Button clicked! Requesting match restart...");
            HideMatchResult();
            onRestartRequested?.Invoke();
        }

        public void ShowTimer(float seconds)
        {
            currentTimer = seconds;
            isTimerActive = true;

            if (timerText != null)
            {
                timerText.gameObject.SetActive(true);
                timerText.text = $"Timer : {Mathf.Max(0f, seconds):0.0}s";
                timerText.color = originalTimerColor;
            }
        }

        public void HideTimer()
        {
            isTimerActive = false;
            if (timerText != null)
            {
                timerText.text = "Timer : --";
                timerText.color = originalTimerColor;
            }
        }

        public void UpdateTimer(float remainingSeconds)
        {
            currentTimer = remainingSeconds;
            if (timerText != null)
            {
                timerText.text = $"Timer : {Mathf.Max(0f, remainingSeconds):0.0}s";

                // Visual urgency feedback when time is low
                if (remainingSeconds <= 3f && remainingSeconds > 0f)
                {
                    timerText.color = new Color(0.9f, 0.15f, 0.15f); // Urgency Red
                }
                else
                {
                    timerText.color = originalTimerColor;
                }
            }
        }

        public void SetStatus(string message)
        {
            currentStatus = message;
            if (statusText != null)
            {
                statusText.text = $"Status : {message}";
            }
        }

        public void UpdateAliveCount(int count)
        {
            currentAliveCount = count;
            if (aliveCountText != null)
            {
                aliveCountText.text = $"Alive : {count}";
            }
        }

        public void ShowMatchResult(string message)
        {
            isMatchOver = true;
            currentResult = message;
            HideTimer();

            if (resultPanel != null)
            {
                resultPanel.SetActive(true);
            }
            if (mobileControlsPanel != null)
            {
                mobileControlsPanel.SetActive(false);
            }
            if (resultText != null)
            {
                resultText.text = message;
            }
            if (resultRestartButton != null)
            {
                resultRestartButton.gameObject.SetActive(true);
            }
            if (restartButton != null)
            {
                restartButton.gameObject.SetActive(true);
            }
        }

        public void HideMatchResult()
        {
            isMatchOver = false;
            currentResult = "";

            if (resultPanel != null)
            {
                resultPanel.SetActive(false);
            }
        }

        void OnGUI()
        {
            if (!enableOnGUIFallback) return;

            // Start Window Fallback if Canvas start window is not hooked up
            if (!isMatchStarted && (startPanel == null || !startPanel.activeInHierarchy))
            {
                float swWidth = 360f;
                float swHeight = 240f;
                float swX = (Screen.width - swWidth) * 0.5f;
                float swY = (Screen.height - swHeight) * 0.5f;

                GUI.Box(new Rect(swX, swY, swWidth, swHeight), "LASER LOCK - MATCH SETUP");

                GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 18,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };

                GUI.Label(new Rect(swX + 20, swY + 40, swWidth - 40, 30), $"Number of Bots: {selectedBotCount}", labelStyle);

                // Minus & Plus
                if (GUI.Button(new Rect(swX + 50, swY + 80, 50, 40), "-"))
                {
                    SetBotCount(selectedBotCount - 1);
                }

                selectedBotCount = (int)GUI.HorizontalSlider(new Rect(swX + 115, swY + 95, 130, 30), selectedBotCount, 1, 10);

                if (GUI.Button(new Rect(swX + 260, swY + 80, 50, 40), "+"))
                {
                    SetBotCount(selectedBotCount + 1);
                }

                if (GUI.Button(new Rect(swX + 60, swY + 150, swWidth - 120, 55), "START MATCH"))
                {
                    HandleStartClicked();
                }
                return;
            }

            // Gameplay Fallback Box
            float barWidth = 420f;
            float barHeight = 85f;
            float startX = (Screen.width - barWidth) * 0.5f;

            GUI.Box(new Rect(startX, 15, barWidth, barHeight), GUIContent.none);

            GUIStyle headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            headerStyle.normal.textColor = Color.white;

            GUIStyle timerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 30,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            timerStyle.normal.textColor = currentTimer <= 3f ? new Color(1f, 0.25f, 0.25f) : new Color(1f, 0.9f, 0.2f);

            GUI.Label(new Rect(startX, 18, barWidth, 26), currentStatus, headerStyle);

            if (isTimerActive)
            {
                GUI.Label(new Rect(startX, 42, barWidth, 48), $"{Mathf.Max(0f, currentTimer):0.0}s", timerStyle);
            }

            // Survivors indicator on left
            GUI.Label(new Rect(25, 20, 200, 30), $"Survivors: {currentAliveCount}");

            // Result Overlay Fallback
            if (isMatchOver && (resultPanel == null || !resultPanel.activeInHierarchy))
            {
                float panelW = 440f;
                float panelH = 220f;
                float pX = (Screen.width - panelW) * 0.5f;
                float pY = (Screen.height - panelH) * 0.5f;

                GUI.Box(new Rect(pX, pY, panelW, panelH), GUIContent.none);

                GUIStyle resultStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 24,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                resultStyle.normal.textColor = currentResult.Contains("VICTORY")
                    ? new Color(0.2f, 1f, 0.3f)
                    : (currentResult.Contains("DRAW") ? Color.yellow : new Color(1f, 0.3f, 0.3f));

                GUI.Label(new Rect(pX + 20, pY + 30, panelW - 40, 50), currentResult, resultStyle);

                if (GUI.Button(new Rect(pX + 110, pY + 115, panelW - 220, 55), "RESTART MATCH"))
                {
                    HandleRestartClicked();
                }
            }
        }
    }
}
