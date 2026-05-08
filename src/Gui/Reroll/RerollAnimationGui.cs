using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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
        private readonly Dictionary<string, ItemStack> iconStacks = new Dictionary<string, ItemStack>();
        private float autoCloseTimer;
        private const float AutoCloseDelay = 3f; // Auto-close after 3 seconds

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
            autoCloseTimer = 0f;

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

            // Handle item codes that may or may not include domain
            AssetLocation asset;
            if (itemCode.Contains(":"))
            {
                asset = new AssetLocation(itemCode);
            }
            else
            {
                asset = AssetLocation.Create(itemCode, "game");
            }

            var collectible = capi.World.GetItem(asset) as CollectibleObject ?? capi.World.GetBlock(asset) as CollectibleObject;
            if (collectible != null)
            {
                stack = new ItemStack(collectible);
                iconStacks[itemCode] = stack;
            }
            return stack;
        }

        private string GetLocalizedName(string itemCode)
        {
            if (string.IsNullOrWhiteSpace(itemCode)) return itemCode;
            
            // Try direct lookup with the itemCode (albase:item-name format)
            string directName = Lang.Get(itemCode);
            if (!string.IsNullOrEmpty(directName) && directName != itemCode)
            {
                return StripHtml(directName);
            }
            
            // Try with domain-item format (albase-item-name)
            string dashedCode = itemCode.Replace(":", "-");
            string dashedName = Lang.Get(dashedCode);
            if (!string.IsNullOrEmpty(dashedName) && dashedName != dashedCode)
            {
                return StripHtml(dashedName);
            }
            
            // Try item-domain-item format
            string itemKey = $"item-{dashedCode}";
            string itemName = Lang.Get(itemKey);
            if (!string.IsNullOrEmpty(itemName) && itemName != itemKey)
            {
                return StripHtml(itemName);
            }
            
            // Fallback: try getting from stack
            var stack = GetIconStack(itemCode);
            if (stack != null)
            {
                string name = stack.GetName();
                if (!string.IsNullOrEmpty(name) && name != itemCode && !name.Contains(":"))
                {
                    return StripHtml(name);
                }
            }
            
            // Last resort: return the code itself (this is the bug we need to fix)
            // Return empty string or placeholder instead of raw code
            return "";
        }

        private string StripHtml(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return Regex.Replace(text, "<[^>]*>", "");
        }

        private void recompose()
        {
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            // Icon area centered - larger area for bigger icon
            ElementBounds iconBounds = ElementBounds.Fixed(0, 40, 200, 120);

            bgBounds.BothSizing = ElementSizing.FitToChildren;

            string titleText = LocalizationUtils.GetSafe("alegacyvsquest:reroll-animation-title");

            SingleComposer = capi.Gui.CreateCompo("RerollAnimation-", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(titleText, () => { }) // Disable close button during animation
                .BeginChildElements(bgBounds)
                .AddStaticText("", CairoFont.WhiteSmallishText(), iconBounds, "iconarea"); // Placeholder for icon

            // No button - auto-claim after delay

            SingleComposer.EndChildElements().Compose();
        }

        private bool OnClaim()
        {
            // Send claim message to server
            if (!string.IsNullOrWhiteSpace(message.GroupId))
            {
                capi.Network.GetChannel(VsQuestNetworkRegistry.RerollChannelName).SendPacket(new ClaimRerollRewardMessage
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

                // Play tick sound - interval based on animation speed
                float currentSpeed = animation.SpinSpeed; // items per second
                float soundInterval = 1f / Math.Max(0.5f, currentSpeed); // slower sound when animation slows
                soundTimer += deltaTime;
                if (soundTimer >= soundInterval)
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
            else
            {
                // Animation complete - auto-close after delay
                autoCloseTimer += deltaTime;
                if (autoCloseTimer >= AutoCloseDelay)
                {
                    OnClaim();
                }
            }

            // Render item icon centered in dialog
            string currentItemCode = animation.GetCurrentItemCode();
            if (!string.IsNullOrWhiteSpace(currentItemCode))
            {
                var stack = GetIconStack(currentItemCode);
                if (stack != null)
                {
                    var slot = new DummySlot(stack);
                    // Center icon in dialog (center of the dialog bounds)
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
