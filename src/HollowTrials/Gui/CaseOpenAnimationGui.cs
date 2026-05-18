using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace VsQuest
{
    /// <summary>
    /// Case opening animation GUI — void/abyss themed spin animation.
    /// Based on RerollAnimationGui but with purple/void styling.
    /// </summary>
    public class CaseOpenAnimationGui : GuiDialog
    {
        public override string ToggleKeyCombinationCode => null;

        private readonly StartCaseOpenAnimationMessage message;
        private readonly Dictionary<string, ItemStack> iconStacks = new();

        private int currentIndex;
        private float elapsed;
        private float spinSpeed;
        private float timeSinceLastChange;
        private float soundTimer;
        private float autoCloseTimer;
        private bool showingResult;

        private const float InitialSpeed = 10f;
        private const float MinSpeed = 0.4f;
        private const float TotalDuration = 4f;
        private const float AutoCloseDelay = 3.5f;

        private bool IsComplete => elapsed >= TotalDuration;

        public CaseOpenAnimationGui(ICoreClientAPI capi, StartCaseOpenAnimationMessage message) : base(capi)
        {
            this.message = message;
            elapsed = 0f;
            currentIndex = 0;
            timeSinceLastChange = 0f;
            spinSpeed = InitialSpeed;
            soundTimer = 0f;
            autoCloseTimer = 0f;
            showingResult = false;

            Compose();
        }

        private void Compose()
        {
            double width = 320;
            double height = 180;

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds bgBounds = ElementBounds.Fixed(0, 0, width, height);

            string title = LocalizationUtils.GetSafe("albase:trial-case-opening-title");
            if (string.IsNullOrWhiteSpace(title) || title.StartsWith("albase:"))
                title = "Разлом открывается...";

            SingleComposer = capi.Gui.CreateCompo("CaseOpenAnimation", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(title, () => { })
                .BeginChildElements(bgBounds)
                .EndChildElements()
                .Compose();
        }

        private void ComposeResult()
        {
            double width = 320;
            double height = 200;

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds bgBounds = ElementBounds.Fixed(0, 0, width, height);

            string qualityColor = message.ResultQualityColor ?? "#A78BFA";
            string qualityName = message.ResultQualityName ?? "";
            string itemName = message.ResultItemName ?? "";

            string resultText = $"<font color=\"{qualityColor}\">{qualityName}</font>";
            string itemText = $"<font color=\"#E5E7EB\">{itemName}</font>";

            SingleComposer = capi.Gui.CreateCompo("CaseOpenAnimation", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar("Разлом раскололся!", () => { OnClaim(); })
                .BeginChildElements(bgBounds)
                .AddRichtext(resultText, CairoFont.WhiteSmallText(),
                    ElementBounds.Fixed(15, 100, width - 30, 25))
                .AddRichtext(itemText, CairoFont.WhiteSmallText(),
                    ElementBounds.Fixed(15, 125, width - 30, 25))
                .EndChildElements()
                .Compose();
        }

        private ItemStack GetIconStack(string itemCode)
        {
            if (string.IsNullOrWhiteSpace(itemCode)) return null;
            if (iconStacks.TryGetValue(itemCode, out var stack)) return stack;

            AssetLocation asset = itemCode.Contains(":")
                ? new AssetLocation(itemCode)
                : AssetLocation.Create(itemCode, "game");

            var collectible = capi.World.GetItem(asset) as CollectibleObject
                ?? capi.World.GetBlock(asset) as CollectibleObject;

            if (collectible != null)
            {
                stack = new ItemStack(collectible);
                iconStacks[itemCode] = stack;
            }
            return stack;
        }

        public override void OnRenderGUI(float deltaTime)
        {
            base.OnRenderGUI(deltaTime);
            if (!IsOpened()) return;

            if (!IsComplete)
            {
                elapsed += deltaTime;

                // Decelerate: ease-out curve
                float t = Math.Min(1f, elapsed / TotalDuration);
                spinSpeed = InitialSpeed - (InitialSpeed - MinSpeed) * (t * t * t);

                // Advance items
                timeSinceLastChange += deltaTime;
                float changeInterval = 1f / spinSpeed;

                while (timeSinceLastChange >= changeInterval && !IsComplete)
                {
                    timeSinceLastChange -= changeInterval;
                    if (message.PoolItemCodes != null && message.PoolItemCodes.Length > 0)
                        currentIndex = (currentIndex + 1) % message.PoolItemCodes.Length;
                }

                // Tick sound
                soundTimer += deltaTime;
                float soundInterval = 1f / Math.Max(0.5f, spinSpeed);
                if (soundTimer >= soundInterval)
                {
                    soundTimer = 0f;
                    try
                    {
                        capi.World.PlaySoundAt(
                            new AssetLocation("game", "sounds/tick.ogg"),
                            capi.World.Player.Entity, null, false, 16f, 0.6f);
                    }
                    catch { }
                }

                // Render spinning icon
                string currentCode = message.PoolItemCodes?[currentIndex];
                RenderItemIcon(currentCode);

                // Check completion
                if (IsComplete && !showingResult)
                {
                    showingResult = true;
                    // Play reveal sound
                    try
                    {
                        capi.World.PlaySoundAt(
                            new AssetLocation("game", "sounds/effect/translocate-active.ogg"),
                            capi.World.Player.Entity, null, false, 32f, 0.8f);
                    }
                    catch { }
                    ComposeResult();
                }
            }
            else
            {
                // Show result icon
                RenderItemIcon(message.ResultItemCode);

                // Auto-close and claim
                autoCloseTimer += deltaTime;
                if (autoCloseTimer >= AutoCloseDelay)
                {
                    OnClaim();
                }
            }
        }

        private void RenderItemIcon(string itemCode)
        {
            if (string.IsNullOrWhiteSpace(itemCode)) return;
            var stack = GetIconStack(itemCode);
            if (stack == null) return;

            var slot = new DummySlot(stack);
            double iconX = SingleComposer.Bounds.absX + SingleComposer.Bounds.InnerWidth / 2;
            double iconY = SingleComposer.Bounds.absY + GuiElement.scaled(70);
            float size = (float)GuiElement.scaled(64);
            capi.Render.RenderItemstackToGui(slot, iconX, iconY, 500, size, -1, false, false, false);
        }

        private void OnClaim()
        {
            if (!string.IsNullOrWhiteSpace(message.ClaimToken))
            {
                capi.Network.GetChannel(TrialShopNetworkHandler.ChannelName)
                    .SendPacket(new ClaimCaseRewardMessage { ClaimToken = message.ClaimToken });
            }
            TryClose();
        }

        public static void Show(StartCaseOpenAnimationMessage message, ICoreClientAPI capi)
        {
            new CaseOpenAnimationGui(capi, message).TryOpen();
        }
    }
}
