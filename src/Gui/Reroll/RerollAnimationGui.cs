using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace VsQuest
{
    /// <summary>
    /// GUI dialog for reroll animation.
    /// Shows spinning items and reveals the result.
    /// </summary>
    public class RerollAnimationGui : GuiDialog
    {
        public override string ToggleKeyCombinationCode => null;

        private readonly StartRerollAnimationMessage message;
        private IRerollAnimation animation;
        private float accum;
        private bool showingResult;
        private readonly Dictionary<string, IRerollAnimation> animationRegistry = new Dictionary<string, IRerollAnimation>();
        private float soundTimer;
        private const float SoundInterval = 0.15f; // Play tick every 150ms

        public RerollAnimationGui(ICoreClientAPI capi, StartRerollAnimationMessage message) : base(capi)
        {
            this.message = message;

            // Register available animations
            RegisterAnimation(new SimpleSpinAnimation());

            // Create animation instance
            if (!string.IsNullOrEmpty(message.AnimationType) && animationRegistry.TryGetValue(message.AnimationType, out var anim))
            {
                animation = anim;
                animation.Initialize(message.ItemIds, message.ItemNames, message.ResultItemId, message.ResultItemName);
            }
            else
            {
                // Fallback to simple spin
                animation = new SimpleSpinAnimation();
                animation.Initialize(message.ItemIds, message.ItemNames, message.ResultItemId, message.ResultItemName);
            }

            accum = 0f;
            showingResult = false;
            soundTimer = 0f;

            recompose();
        }

        private void RegisterAnimation(IRerollAnimation animation)
        {
            animationRegistry[animation.Id] = animation;
        }

        private void recompose()
        {
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            ElementBounds textBounds = ElementBounds.Fixed(0, 40, 400, 60);
            ElementBounds buttonBounds = ElementBounds.Fixed(100, 120, 200, 30);

            bgBounds.BothSizing = ElementSizing.FitToChildren;

            string titleText = LocalizationUtils.GetSafe("alegacyvsquest:reroll-animation-title");

            // Get current text to display
            string currentText = animation?.IsComplete == true 
                ? animation.GetCurrentItemName() 
                : "";

            SingleComposer = capi.Gui.CreateCompo("RerollAnimation-", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(titleText, () => { if (animation.IsComplete) TryClose(); })
                .BeginChildElements(bgBounds)
                .AddDynamicText(currentText, CairoFont.WhiteSmallishText(), textBounds, "itemtext");

            if (animation.IsComplete)
            {
                SingleComposer.AddButton(LocalizationUtils.GetSafe("alegacyvsquest:reroll-animation-claim"), OnClaim, buttonBounds);
            }

            SingleComposer.EndChildElements().Compose();
        }

        private bool OnClaim()
        {
            // Send claim message to server
            if (!string.IsNullOrWhiteSpace(message.GroupId))
            {
                capi.Network.GetChannel("alegacyvsquest").SendPacket(new ClaimRerollRewardMessage
                {
                    GroupId = message.GroupId
                });
            }
            TryClose();
            return true;
        }

        public override void OnRenderGUI(float deltaTime)
        {
            base.OnRenderGUI(deltaTime);

            if (animation == null || !IsOpened()) return;

            // Update animation
            if (!animation.IsComplete)
            {
                accum += deltaTime;
                animation.Update(deltaTime);

                // Update displayed text
                string currentItemName = animation.GetCurrentItemName();
                SingleComposer.GetDynamicText("itemtext")?.SetNewText(currentItemName);

                // Play tick sound periodically
                soundTimer += deltaTime;
                if (soundTimer >= SoundInterval)
                {
                    soundTimer = 0f;
                    try
                    {
                        capi.World.PlaySoundAt(new AssetLocation("game", "sounds/tick.ogg"), capi.World.Player.Entity, null, false, 16f, 1f);
                    }
                    catch
                    {
                        // Ignore sound errors
                    }
                }

                // Check if just completed
                if (animation.IsComplete && !showingResult)
                {
                    showingResult = true;
                    recompose();
                }
            }
        }

        public static void ShowFromMessage(StartRerollAnimationMessage message, ICoreClientAPI capi)
        {
            new RerollAnimationGui(capi, message).TryOpen();
        }
    }
}
