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
            // Fixed size dialog
            double dialogWidth = 500;
            double dialogHeight = Math.Max(250, 100 + groups.Count * 60);

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds bgBounds = ElementBounds.Fixed(0, 0, dialogWidth, dialogHeight).WithFixedPadding(GuiStyle.ElementToDialogPadding);

            string titleText = LocalizationUtils.GetSafe("alegacyvsquest:reroll-dialog-title");

            SingleComposer = capi.Gui.CreateCompo("RerollDialog-", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(titleText, () => TryClose())
                .BeginChildElements(bgBounds);

            int yOffset = 30; // Increased top margin

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
                    // Icon placeholder (we'll render icons in OnRenderGUI)
                    ElementBounds iconBounds = ElementBounds.Fixed(10, yOffset, 40, 40);
                    SingleComposer.AddIf(group.iconItemCode != null)
                        .AddStaticText("", CairoFont.WhiteSmallishText(), iconBounds, $"icon_{group.groupId}");

                    // Group name and count
                    string groupText = $"{group.groupName}";
                    string countText = $"({group.itemCount}/{group.itemsRequired})";

                    ElementBounds textBounds = ElementBounds.Fixed(60, yOffset + 5, 280, 20);
                    ElementBounds countBounds = ElementBounds.Fixed(60, yOffset + 25, 100, 16);
                    ElementBounds buttonBounds = ElementBounds.Fixed(dialogWidth - 120, yOffset + 10, 100, 24);

                    SingleComposer
                        .AddStaticText(groupText, CairoFont.WhiteSmallishText(), textBounds)
                        .AddStaticText(countText, group.canReroll ? CairoFont.WhiteSmallishText() : CairoFont.WhiteSmallishText().WithColor(new double[] { 0.6, 0.6, 0.6, 1.0 }), countBounds);

                    if (group.canReroll)
                    {
                        string buttonText = LocalizationUtils.GetSafe("alegacyvsquest:reroll-button");
                        SingleComposer.AddButton(buttonText, () => OnRerollClick(group.groupId), buttonBounds);
                    }
                    else
                    {
                        string disabledText = LocalizationUtils.GetSafe("alegacyvsquest:reroll-button-disabled");
                        SingleComposer.AddStaticText(disabledText, CairoFont.WhiteSmallishText().WithColor(new double[] { 0.5, 0.5, 0.5, 1.0 }), 
                            ElementBounds.Fixed(dialogWidth - 110, yOffset + 15, 100, 16));
                    }

                    yOffset += 60; // Increased spacing between items
                }
            }

            ElementBounds closeButtonBounds = ElementBounds.Fixed(dialogWidth / 2 - 100, yOffset + 10, 200, 24);
            SingleComposer.AddButton(Lang.Get("alegacyvsquest:button-cancel"), TryClose, closeButtonBounds);

            SingleComposer.EndChildElements().Compose();
        }

        public override void OnRenderGUI(float deltaTime)
        {
            base.OnRenderGUI(deltaTime);

            // Render item icons
            int yOffset = 30; // Match recompose offset
            foreach (var group in groups)
            {
                if (!string.IsNullOrWhiteSpace(group.iconItemCode))
                {
                    var stack = GetIconStack(group.iconItemCode);
                    if (stack != null)
                    {
                        var slot = new DummySlot(stack);
                        // Use SingleComposer.Bounds for positioning
                        double iconX = SingleComposer.Bounds.absX + GuiElement.scaled(20);
                        double iconY = SingleComposer.Bounds.absY + GuiElement.scaled(48 + yOffset);
                        float size = (float)GuiElement.scaled(36);
                        capi.Render.RenderItemstackToGui(slot, iconX, iconY, 500, size, -1, false, false, false);
                    }
                }
                yOffset += 60; // Match spacing
            }
        }

        private bool OnRerollClick(string groupId)
        {
            capi.Network.GetChannel("alegacyvsquest").SendPacket(new ExecuteRerollMessage
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
