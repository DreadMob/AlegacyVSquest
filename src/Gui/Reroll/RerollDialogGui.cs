using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace VsQuest
{
    /// <summary>
    /// GUI dialog for rerolling boss items.
    /// Shows all reroll groups with item icons and allows player to exchange items.
    /// </summary>
    public class RerollDialogGui : GuiDialog
    {
        public override string ToggleKeyCombinationCode => null;

        private readonly string[] availableGroups;
        private readonly List<RerollGroupData> groups = new List<RerollGroupData>();
        private readonly Dictionary<string, ItemStack> iconStacks = new Dictionary<string, ItemStack>(StringComparer.Ordinal);

        private class RerollGroupData
        {
            public string groupId;
            public string groupName;
            public int itemCount;
            public int itemsRequired;
            public string iconItemCode;
            public bool canReroll => itemCount >= itemsRequired;
        }

        public RerollDialogGui(ICoreClientAPI capi, string[] availableGroups) : base(capi)
        {
            this.availableGroups = availableGroups;
            ParseGroups();
            recompose();
        }

        private void ParseGroups()
        {
            if (availableGroups == null) return;

            foreach (var groupStr in availableGroups)
            {
                if (string.IsNullOrWhiteSpace(groupStr)) continue;

                var parts = groupStr.Split('|');
                if (parts.Length >= 5)
                {
                    string groupId = parts[0];
                    string groupName = parts[1];
                    if (int.TryParse(parts[2], out int itemCount) && int.TryParse(parts[3], out int itemsRequired))
                    {
                        string iconItemCode = parts[4];
                        groups.Add(new RerollGroupData
                        {
                            groupId = groupId,
                            groupName = groupName,
                            itemCount = itemCount,
                            itemsRequired = itemsRequired,
                            iconItemCode = iconItemCode
                        });
                    }
                }
            }
        }

        private ItemStack GetIconStack(string itemCode)
        {
            if (string.IsNullOrWhiteSpace(itemCode)) return null;
            if (iconStacks.TryGetValue(itemCode, out var cached)) return cached;

            ItemStack stack = null;
            var loc = new AssetLocation(itemCode);
            var item = capi.World.GetItem(loc);
            if (item != null)
            {
                stack = new ItemStack(item);
            }
            else
            {
                var block = capi.World.GetBlock(loc);
                if (block != null)
                {
                    stack = new ItemStack(block);
                }
            }

            if (stack != null)
            {
                iconStacks[itemCode] = stack;
            }
            return stack;
        }

        private void recompose()
        {
            // Fixed size dialog - increased width for text
            double dialogWidth = 700;
            double dialogHeight = Math.Max(300, 120 + groups.Count * 70);

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds bgBounds = ElementBounds.Fixed(0, 0, dialogWidth, dialogHeight).WithFixedPadding(GuiStyle.ElementToDialogPadding);

            string titleText = LocalizationUtils.GetSafe("alegacyvsquest:reroll-dialog-title");

            SingleComposer = capi.Gui.CreateCompo("RerollDialog-", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(titleText, () => TryClose())
                .BeginChildElements(bgBounds);

            int yOffset = 35; // Top margin

            if (groups.Count == 0)
            {
                string noItemsText = LocalizationUtils.GetSafe("alegacyvsquest:reroll-no-groups");
                SingleComposer
                    .AddRichtext(noItemsText, CairoFont.WhiteSmallishText(), ElementBounds.Fixed(10, yOffset, dialogWidth - 20, 60));
            }
            else
            {
                foreach (var group in groups)
                {
                    // Group name with count on same line
                    string groupText = $"{group.groupName} ({group.itemCount}/{group.itemsRequired})";

                    ElementBounds textBounds = ElementBounds.Fixed(80, yOffset + 8, 480, 24);
                    ElementBounds buttonBounds = ElementBounds.Fixed(dialogWidth - 160, yOffset + 5, 150, 32);

                    SingleComposer
                        .AddStaticText(groupText, CairoFont.WhiteSmallishText(), textBounds);

                    if (group.canReroll)
                    {
                        string buttonText = LocalizationUtils.GetSafe("alegacyvsquest:reroll-button");
                        SingleComposer.AddButton(buttonText, () => OnRerollClick(group.groupId), buttonBounds);
                    }
                    else
                    {
                        string disabledText = LocalizationUtils.GetSafe("alegacyvsquest:reroll-button-disabled");
                        SingleComposer.AddStaticText(disabledText, CairoFont.WhiteSmallishText().WithColor(new double[] { 0.5, 0.5, 0.5, 1.0 }), 
                            ElementBounds.Fixed(dialogWidth - 150, yOffset + 12, 140, 20));
                    }

                    yOffset += 70; // Increased spacing between groups
                }
            }

            ElementBounds closeButtonBounds = ElementBounds.Fixed(dialogWidth / 2 - 100, yOffset + 15, 200, 28);
            SingleComposer.AddButton(Lang.Get("alegacyvsquest:button-cancel"), TryClose, closeButtonBounds);

            SingleComposer.EndChildElements().Compose();
        }

        public override void OnRenderGUI(float deltaTime)
        {
            base.OnRenderGUI(deltaTime);

            // Render item icons centered with text
            int yOffset = 35; // Match recompose offset
            foreach (var group in groups)
            {
                if (!string.IsNullOrWhiteSpace(group.iconItemCode))
                {
                    var stack = GetIconStack(group.iconItemCode);
                    if (stack != null)
                    {
                        var slot = new DummySlot(stack);
                        // Position icon 6px more to the right and 6px higher
                        double iconX = SingleComposer.Bounds.absX + GuiElement.scaled(42);
                        double iconY = SingleComposer.Bounds.absY + GuiElement.scaled(39 + yOffset);
                        float size = (float)GuiElement.scaled(36);
                        capi.Render.RenderItemstackToGui(slot, iconX, iconY, 500, size, -1, false, false, false);
                    }
                }
                yOffset += 70; // Match spacing
            }
        }

        private bool OnRerollClick(string groupId)
        {
            capi.Network.GetChannel(VsQuestNetworkRegistry.RerollChannelName).SendPacket(new ExecuteRerollMessage
            {
                GroupId = groupId
            });
            TryClose();
            return true;
        }

        public static void ShowFromMessage(ShowRerollDialogMessage message, ICoreClientAPI capi)
        {
            new RerollDialogGui(capi, message.AvailableGroups).TryOpen();
        }
    }
}
