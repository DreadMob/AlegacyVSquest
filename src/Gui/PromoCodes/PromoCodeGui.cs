using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace VsQuest
{
    /// <summary>
    /// Client-side GUI dialog for entering promo codes.
    /// Features a text input field, submit button, and result display.
    /// </summary>
    public class PromoCodeGui : GuiDialog
    {
        public override string ToggleKeyCombinationCode => null;

        private const string InputKey = "promoInput";
        private const string ResultKey = "resultText";
        private const string SubmitButtonKey = "submitBtn";

        private string resultText = "";
        private bool resultIsSuccess;

        public PromoCodeGui(ICoreClientAPI capi) : base(capi)
        {
            Compose();
        }

        private void Compose()
        {
            double dialogWidth = 420;
            double dialogHeight = 200;

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds bgBounds = ElementBounds.Fixed(0, 0, dialogWidth, dialogHeight).WithFixedPadding(GuiStyle.ElementToDialogPadding);

            ElementBounds inputBounds = ElementBounds.Fixed(10, 40, dialogWidth - 20, 30);
            ElementBounds buttonBounds = ElementBounds.Fixed((dialogWidth - 200) / 2, 85, 200, 30);
            ElementBounds resultBounds = ElementBounds.Fixed(10, 130, dialogWidth - 20, 40);

            string title = LocalizationUtils.GetSafe("alegacyvsquest:promo-gui-title");
            string placeholder = LocalizationUtils.GetSafe("alegacyvsquest:promo-gui-placeholder");
            string buttonText = LocalizationUtils.GetSafe("alegacyvsquest:promo-gui-submit");

            SingleComposer = capi.Gui.CreateCompo("PromoCodeDialog-", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(title, () => TryClose())
                .BeginChildElements(bgBounds)
                    .AddTextInput(inputBounds, OnTextChanged, CairoFont.WhiteSmallishText(), InputKey)
                    .AddSmallButton(buttonText, OnSubmit, buttonBounds, EnumButtonStyle.Normal, SubmitButtonKey)
                    .AddRichtext("", CairoFont.WhiteSmallishText(), resultBounds, ResultKey)
                .EndChildElements()
                .Compose();

            var inputElement = SingleComposer.GetTextInput(InputKey);
            if (inputElement != null)
            {
                inputElement.SetPlaceHolderText(placeholder);
            }
        }

        private void OnTextChanged(string text)
        {
            // Could add real-time validation here if needed
        }

        private bool OnSubmit()
        {
            var inputElement = SingleComposer.GetTextInput(InputKey);
            if (inputElement == null) return true;

            string code = inputElement.GetText()?.Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                ShowResult(LocalizationUtils.GetSafe("alegacyvsquest:promo-error-empty"), false);
                return true;
            }

            // Play submit click sound
            capi.Gui.PlaySound(new Vintagestory.API.Common.AssetLocation("sounds/toggleswitch"), false, 0.3f);

            // Send to server
            capi.Network.GetChannel(PromoCodeNetworkHandler.ChannelName)
                .SendPacket(new RedeemPromoCodeMessage { Code = code });

            // Show "processing" state
            ShowResult(LocalizationUtils.GetSafe("alegacyvsquest:promo-processing"), false);

            return true;
        }

        /// <summary>
        /// Called when server responds with the result.
        /// </summary>
        public void ShowResult(string message, bool success)
        {
            resultText = message;
            resultIsSuccess = success;

            var resultElement = SingleComposer?.GetRichtext(ResultKey);
            if (resultElement != null)
            {
                string color = success ? "#4ADE80" : "#F87171";
                string formatted = $"<font color=\"{color}\">{message}</font>";
                resultElement.SetNewText(formatted, CairoFont.WhiteSmallishText());
            }
        }

        /// <summary>
        /// Clear the input field after successful redemption.
        /// </summary>
        public void ClearInput()
        {
            var inputElement = SingleComposer?.GetTextInput(InputKey);
            if (inputElement != null)
            {
                inputElement.SetValue("");
            }
        }
    }
}
