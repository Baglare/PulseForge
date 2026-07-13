using System.Collections.Generic;
using PulseForge.Runtime.Unity.Prototype;
using UnityEngine;
using UnityEngine.UI;

namespace PulseForge.Runtime.Unity.UI
{
    public sealed class ErrorPanelView : PulseForgePanelView
    {
        [SerializeField] private Text errorText;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button backButton;

        private DebugRhythmPrototypeController boundController;

        public static ErrorPanelView Create(Transform parent)
        {
            RectTransform card = FlowPanelBuilder.CreateScreenWithCard(
                "Error Panel",
                parent,
                new Vector2(760f, 520f),
                out GameObject root);
            ErrorPanelView view = root.AddComponent<ErrorPanelView>();
            view.ConfigurePanelRoot(root);
            Text title = FlowPanelBuilder.AddHeading(card, "Something went wrong");
            title.color = PulseForgeUITheme.Miss;
            view.errorText = FlowPanelBuilder.AddCenteredText(card, string.Empty, 22, PulseForgeUITheme.PrimaryText, 150f);
            view.retryButton = FlowPanelBuilder.AddButton(card, "Retry", PulseForgeUITheme.Primary, 68f, 26);
            view.backButton = FlowPanelBuilder.AddButton(card, "Back to Setup", PulseForgeUITheme.SecondaryText);
            return view;
        }

        public void Bind(DebugRhythmPrototypeController controller)
        {
            if (boundController == controller)
            {
                return;
            }

            Unbind();
            boundController = controller;
            if (controller == null)
            {
                return;
            }

            PulseForgeUIFactory.BindButton(retryButton, controller.RetryAnalysis);
            PulseForgeUIFactory.BindButton(backButton, controller.ChangeSettings);
        }

        public void Unbind()
        {
            PulseForgeUIFactory.UnbindButton(retryButton);
            PulseForgeUIFactory.UnbindButton(backButton);
            boundController = null;
        }

        public void Refresh(DebugRhythmPrototypeController controller)
        {
            errorText.text = CreateUserFacingMessage(controller.ErrorMessage);
        }

        private static string CreateUserFacingMessage(string rawMessage)
        {
            if (string.IsNullOrWhiteSpace(rawMessage))
            {
                return "The track could not be prepared. Please try again or return to Setup.";
            }

            string message = rawMessage.Trim();
            int lineBreak = message.IndexOf('\n');
            if (lineBreak >= 0)
            {
                message = message.Substring(0, lineBreak).Trim();
            }

            const int maxLength = 220;
            if (message.Length > maxLength)
            {
                message = message.Substring(0, maxLength).TrimEnd() + "…";
            }

            return message;
        }

        public override void CollectValidationErrors(List<string> errors)
        {
            base.CollectValidationErrors(errors);
            PulseForgeUIValidation.AddMissing(errors, errorText, "Error: message text is missing.");
            PulseForgeUIValidation.AddMissing(errors, retryButton, "Error: Retry button is missing.");
            PulseForgeUIValidation.AddMissing(errors, backButton, "Error: Back to Setup button is missing.");
        }
    }
}
