using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    public sealed class PulseForgeUIButtonMotion : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private PulseForgeUIMotionRunner runner;

        private RectTransform rectTransform;
        private Vector3 baseScale = Vector3.one;
        private bool isHovered;
        private bool isPressed;
        private bool wasInteractable;

        public void Configure(Button targetButton, PulseForgeUIMotionRunner motionRunner)
        {
            button = targetButton;
            runner = motionRunner;
            rectTransform = GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                baseScale = rectTransform.localScale;
            }

            wasInteractable = button != null && button.interactable;
        }

        public void ResetInteraction()
        {
            isHovered = false;
            isPressed = false;
            ApplyBaseScale();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            if (!isPressed)
            {
                AnimateTo(PulseForgeUIMotionTokens.ButtonHoverScale);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            isPressed = false;
            AnimateTo(1f);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!CanInteract())
            {
                ApplyBaseScale();
                return;
            }

            isPressed = true;
            AnimateTo(PulseForgeUIMotionTokens.ButtonPressedScale);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isPressed = false;
            AnimateTo(isHovered ? PulseForgeUIMotionTokens.ButtonHoverScale : 1f);
        }

        private void Awake()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (runner == null)
            {
                runner = GetComponentInParent<PulseForgeUIMotionRunner>(true);
            }

            rectTransform = GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                baseScale = rectTransform.localScale;
            }

            wasInteractable = button != null && button.interactable;
        }

        private void Update()
        {
            bool interactable = CanInteract();
            if (wasInteractable && !interactable)
            {
                isHovered = false;
                isPressed = false;
                ApplyBaseScale();
            }

            wasInteractable = interactable;
        }

        private void OnDisable()
        {
            isHovered = false;
            isPressed = false;
            ApplyBaseScale();
        }

        private void OnDestroy()
        {
            if (runner != null)
            {
                runner.Cancel(GetInstanceID(), false);
            }
        }

        private void AnimateTo(float multiplier)
        {
            if (!CanInteract())
            {
                ApplyBaseScale();
                return;
            }

            if (rectTransform == null)
            {
                return;
            }

            Vector3 targetScale = baseScale * multiplier;
            PulseForgeSceneUIRoot root = GetComponentInParent<PulseForgeSceneUIRoot>(true);
            bool animate = root == null || root.EnableMotion;
            if (runner == null || !animate)
            {
                rectTransform.localScale = targetScale;
                return;
            }

            runner.Scale(
                GetInstanceID(),
                rectTransform,
                rectTransform.localScale,
                targetScale,
                PulseForgeUIMotionTokens.FastDuration,
                PulseForgeUIMotionTokens.EaseOut);
        }

        private void ApplyBaseScale()
        {
            if (rectTransform == null)
            {
                return;
            }

            if (runner != null)
            {
                runner.ApplyScaleInstant(GetInstanceID(), rectTransform, baseScale);
            }
            else
            {
                rectTransform.localScale = baseScale;
            }
        }

        private bool CanInteract()
        {
            return button != null && button.isActiveAndEnabled && button.interactable;
        }
    }
}
