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
        private readonly Dictionary<string, ItemStack> iconStacks = new Dictionary<string, ItemStack>();

        public RerollAnimationGui(ICoreClientAPI capi, StartRerollAnimationMessage message) : base(capi)
        {
            this.message = message;

            // Register available animations
            RegisterAnimation(new SimpleSpinAnimation());

            // Create animation instance
            if (!string.IsNullOrEmpty(message.AnimationType) && animationRegistry.TryGetValue(message.AnimationType, out var anim))
            {
                animation = anim;
                animation.Initialize(message.ItemIds, message.ItemNames, message.ItemCodes, message.ResultItemId, message.ResultItemName, message.ResultItemCode);
            }
            else
            {
                // Fallback to simple spin
                animation = new SimpleSpinAnimation();
                animation.Initialize(message.ItemIds, message.ItemNames, message.ItemCodes, message.ResultItemId, message.ResultItemName, message.ResultItemCode);
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

        private ItemStack GetIconStack(string itemCode)
        {
            if (string.IsNullOrWhiteSpace(itemCode)) return null;
            if (iconStacks.TryGetValue(itemCode, out var stack)) return stack;

            var asset = AssetLocation.Create(itemCode, "game");
            var collectible = capi.World.GetItem(asset) as CollectibleObject ?? capi.World.GetBlock(asset) as CollectibleObject;
            if (collectible != null)
            {
                stack = new ItemStack(collectible);
                iconStacks[itemCode] = stack;
            }
            return stack;
        }

        private void recompose()
        {
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            // Icon area centered
            ElementBounds iconBounds = ElementBounds.Fixed(0, 40, 200, 100);
            // Text below icon
            ElementBounds textBounds = ElementBounds.Fixed(0, 150, 200, 60);
            ElementBounds buttonBounds = ElementBounds.Fixed(0, 220, 200, 30);

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
                .AddStaticText("", CairoFont.WhiteSmallishText(), iconBounds, "iconarea") // Placeholder for icon
                .AddDynamicText(currentText, CairoFont.WhiteSmallishText().WithOrientation(EnumTextOrientation.Center), textBounds, "itemtext");

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

            // Render item icon centered
            string currentItemCode = animation.GetCurrentItemCode();
            if (!string.IsNullOrWhiteSpace(currentItemCode))
            {
                var stack = GetIconStack(currentItemCode);
                if (stack != null)
                {
                    var slot = new DummySlot(stack);
                    // Center icon in dialog
                    double iconX = SingleComposer.Bounds.absX + SingleComposer.Bounds.InnerWidth / 2;
                    double iconY = SingleComposer.Bounds.absY + GuiElement.scaled(90);
                    float size = (float)GuiElement.scaled(64);
                    capi.Render.RenderItemstackToGui(slot, iconX, iconY, 500, size, -1, false, false, false);
                }
            }
        }

        public static void ShowFromMessage(StartRerollAnimationMessage message, ICoreClientAPI capi)
        {
            new RerollAnimationGui(capi, message).TryOpen();
        }
    }
}
