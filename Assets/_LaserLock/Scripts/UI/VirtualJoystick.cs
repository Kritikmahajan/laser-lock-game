using UnityEngine;
using UnityEngine.EventSystems;

namespace LaserLock.UI
{
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("Joystick Transforms")]
        [SerializeField] RectTransform background;
        [SerializeField] RectTransform handle;

        [Header("Settings")]
        [SerializeField] float handleRange = 60f;
        [SerializeField] float deadZone = 0.05f;

        Vector2 inputVector = Vector2.zero;
        Canvas parentCanvas;
        Camera uiCamera;

        public Vector2 Direction => inputVector;
        public bool IsDragging { get; private set; }

        void Awake()
        {
            if (background == null)
            {
                background = GetComponent<RectTransform>();
            }
            if (handle == null && transform.childCount > 0)
            {
                handle = transform.GetChild(0).GetComponent<RectTransform>();
            }

            parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                uiCamera = parentCanvas.worldCamera;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            IsDragging = true;
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (background == null) return;

            Vector2 position = RectTransformUtility.WorldToScreenPoint(uiCamera, background.position);
            Vector2 radius = background.sizeDelta * 0.5f;

            // Calculate offset from background center
            Vector2 offset = (eventData.position - position);
            float maxDist = handleRange > 0f ? handleRange : radius.x;

            Vector2 clamped = Vector2.ClampMagnitude(offset, maxDist);

            if (handle != null)
            {
                handle.anchoredPosition = clamped;
            }

            // Normalize input vector
            Vector2 rawInput = clamped / maxDist;
            if (rawInput.magnitude < deadZone)
            {
                inputVector = Vector2.zero;
            }
            else
            {
                inputVector = rawInput;
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            IsDragging = false;
            inputVector = Vector2.zero;

            if (handle != null)
            {
                handle.anchoredPosition = Vector2.zero;
            }
        }

        void OnDisable()
        {
            IsDragging = false;
            inputVector = Vector2.zero;
            if (handle != null)
            {
                handle.anchoredPosition = Vector2.zero;
            }
        }
    }
}
