using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using LaserLock.UI;

namespace LaserLock.Player
{
    /// <summary>
    /// Cross-platform input handler supporting Mobile Touchscreen (Virtual Joysticks, Direct Touch-to-Aim),
    /// Gamepad, and PC Keyboard/Mouse using Unity's New Input System.
    /// </summary>
    public class PlayerInputHandler : MonoBehaviour
    {
        [Header("Mobile Virtual Joysticks (Optional - Auto detected if present)")]
        [Tooltip("Virtual joystick for character movement (left side of screen)")]
        [SerializeField] VirtualJoystick moveJoystick;

        [Tooltip("Optional virtual joystick for direct 360-degree aiming (right side of screen)")]
        [SerializeField] VirtualJoystick aimJoystick;

        [Header("Mobile Direct Touch Settings")]
        [Tooltip("Allow dragging on left screen area to move without a visible joystick")]
        [SerializeField] bool enableFloatingTouchMovement = true;

        [Tooltip("Allow touching/dragging anywhere to directly aim the laser beam")]
        [SerializeField] bool enableTouchToAim = true;

        [Header("Custom Actions Asset (Optional)")]
        [SerializeField] InputActionReference moveActionReference;

        public Vector2 MoveInput { get; private set; }
        public Vector2 AimDirection { get; private set; }
        public Vector2 PointerScreenPosition { get; private set; }
        public bool HasPointerPosition { get; private set; }

        // Touch tracking for mobile floating movement
        int moveTouchId = -1;
        Vector2 moveTouchStartPos;

        void Awake()
        {
            FindJoysticksIfNeeded();
        }

        void FindJoysticksIfNeeded()
        {
            if (moveJoystick == null || aimJoystick == null)
            {
                var joysticks = FindObjectsByType<VirtualJoystick>(FindObjectsSortMode.None);
                foreach (var j in joysticks)
                {
                    string jName = j.gameObject.name.ToLower();
                    if (moveJoystick == null && (jName.Contains("move") || jName.Contains("left")))
                    {
                        moveJoystick = j;
                    }
                    else if (aimJoystick == null && (jName.Contains("aim") || jName.Contains("right") || jName.Contains("look")))
                    {
                        aimJoystick = j;
                    }
                }

                // If only 1 joystick exists, assign to moveJoystick
                if (moveJoystick == null && joysticks.Length == 1)
                {
                    moveJoystick = joysticks[0];
                }
            }
        }

        void OnEnable()
        {
            if (moveActionReference != null && moveActionReference.action != null)
            {
                moveActionReference.action.Enable();
            }
        }

        void OnDisable()
        {
            if (moveActionReference != null && moveActionReference.action != null)
            {
                moveActionReference.action.Disable();
            }
            MoveInput = Vector2.zero;
            AimDirection = Vector2.zero;
            moveTouchId = -1;
        }

        void Update()
        {
            UpdateMoveInput();
            UpdateAimInput();
        }

        void UpdateMoveInput()
        {
            Vector2 input = Vector2.zero;

            // 1. Mobile Virtual Joystick
            if (moveJoystick != null && moveJoystick.Direction.sqrMagnitude > 0.001f)
            {
                input = moveJoystick.Direction;
            }

            // 2. Custom InputActionReference
            if (input.sqrMagnitude < 0.001f && moveActionReference != null && moveActionReference.action != null)
            {
                input = moveActionReference.action.ReadValue<Vector2>();
            }

            // 3. PC Keyboard (WASD / Arrows)
            if (input.sqrMagnitude < 0.001f && Keyboard.current != null)
            {
                var k = Keyboard.current;
                if (k.wKey.isPressed || k.upArrowKey.isPressed) input.y += 1f;
                if (k.sKey.isPressed || k.downArrowKey.isPressed) input.y -= 1f;
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) input.x -= 1f;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) input.x += 1f;
            }

            // 4. Gamepad Left Stick
            if (input.sqrMagnitude < 0.001f && Gamepad.current != null)
            {
                input = Gamepad.current.leftStick.ReadValue();
            }

            // 5. Mobile Floating Touch Movement (Fallback when no physical joystick is present)
            if (input.sqrMagnitude < 0.001f && enableFloatingTouchMovement && Touchscreen.current != null)
            {
                input = ProcessFloatingTouchMovement();
            }

            MoveInput = Vector2.ClampMagnitude(input, 1f);
        }

        Vector2 ProcessFloatingTouchMovement()
        {
            var touches = Touchscreen.current.touches;
            float leftBoundary = Screen.width * 0.45f;

            // Check existing touch
            if (moveTouchId != -1)
            {
                foreach (var touch in touches)
                {
                    if (touch.touchId.ReadValue() == moveTouchId)
                    {
                        if (touch.press.isPressed)
                        {
                            Vector2 currentPos = touch.position.ReadValue();
                            Vector2 delta = currentPos - moveTouchStartPos;
                            float maxDrag = 80f;
                            return Vector2.ClampMagnitude(delta / maxDrag, 1f);
                        }
                        else
                        {
                            moveTouchId = -1;
                            return Vector2.zero;
                        }
                    }
                }
                moveTouchId = -1;
            }

            // Find new touch on left side of screen
            foreach (var touch in touches)
            {
                if (touch.press.wasPressedThisFrame)
                {
                    Vector2 pos = touch.position.ReadValue();
                    if (pos.x < leftBoundary && !IsPointerOverUI(pos))
                    {
                        moveTouchId = touch.touchId.ReadValue();
                        moveTouchStartPos = pos;
                        return Vector2.zero;
                    }
                }
            }

            return Vector2.zero;
        }

        void UpdateAimInput()
        {
            AimDirection = Vector2.zero;
            HasPointerPosition = false;

            // 1. Mobile Aim Joystick (Directional Stick)
            if (aimJoystick != null && aimJoystick.Direction.sqrMagnitude > 0.01f)
            {
                AimDirection = aimJoystick.Direction;
                return;
            }

            // 2. Mobile Touch to Aim (Direct Touch / Drag on Screen)
            if (enableTouchToAim && Touchscreen.current != null)
            {
                foreach (var touch in Touchscreen.current.touches)
                {
                    if (touch.press.isPressed)
                    {
                        // Ignore the finger being used for left-side movement
                        if (touch.touchId.ReadValue() == moveTouchId) continue;

                        Vector2 touchPos = touch.position.ReadValue();
                        if (!IsPointerOverUI(touchPos))
                        {
                            PointerScreenPosition = touchPos;
                            HasPointerPosition = true;
                            return;
                        }
                    }
                }
            }

            // 3. PC Mouse / Pointer Position
            if (Mouse.current != null)
            {
                PointerScreenPosition = Mouse.current.position.ReadValue();
                HasPointerPosition = true;
            }
            else if (Pointer.current != null)
            {
                PointerScreenPosition = Pointer.current.position.ReadValue();
                HasPointerPosition = true;
            }
        }

        bool IsPointerOverUI(Vector2 screenPosition)
        {
            if (EventSystem.current == null) return false;

            PointerEventData eventData = new PointerEventData(EventSystem.current)
            {
                position = screenPosition
            };

            var results = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            return results.Count > 0;
        }

        public void SetJoysticks(VirtualJoystick moveJoy, VirtualJoystick aimJoy = null)
        {
            moveJoystick = moveJoy;
            aimJoystick = aimJoy;
        }

        public void DisableInput()
        {
            enabled = false;
            MoveInput = Vector2.zero;
            AimDirection = Vector2.zero;
            moveTouchId = -1;
        }

        public void EnableInput()
        {
            enabled = true;
        }
    }
}
