using System;
using System.Linq;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace VsQuest
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class VoidRiftAnchorConfigData
    {
        public int tier;
        public float yOffset;
        public float leashRange;
        public bool arenaEnabled;
        public bool arenaKeepInventory;
    }

    public class VoidRiftAnchorConfigGui : GuiDialog
    {
        public override string ToggleKeyCombinationCode => null;

        private const int PacketSave = 3001;

        private BlockPos blockPos;
        public VoidRiftAnchorConfigData Data;

        public VoidRiftAnchorConfigGui(BlockPos pos, ICoreClientAPI capi) : base(capi)
        {
            blockPos = pos;
        }

        public void UpdateFromServer(VoidRiftAnchorConfigData data)
        {
            Data = data;
            Compose();
        }

        private void Compose()
        {
            double width = 350;
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds bgBounds = ElementBounds.Fixed(0, 0, width, 310);

            int currentTier = Data?.tier ?? 1;
            string[] tierValues = new[] { "1", "2", "3" };
            string[] tierNames = new[] { "Tier 1 — Полое", "Tier 2 — Глубинное", "Tier 3 — Бездонное" };
            int selectedIndex = Math.Max(0, Math.Min(2, currentTier - 1));

            string[] boolValues = new[] { "false", "true" };
            string[] boolNames = new[] { "Нет", "Да" };

            SingleComposer = capi.Gui.CreateCompo("VoidRiftAnchorConfig", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar("Разлом Пустоты", () => TryClose())
                .BeginChildElements(bgBounds)
                    .AddStaticText("Тир разлома:", CairoFont.WhiteSmallText(), ElementBounds.Fixed(10, 40, 120, 25))
                    .AddDropDown(tierValues, tierNames, selectedIndex, OnTierChanged, ElementBounds.Fixed(130, 35, width - 150, 30), "tier")
                    .AddStaticText("Y Offset:", CairoFont.WhiteSmallText(), ElementBounds.Fixed(10, 80, 100, 25))
                    .AddNumberInput(ElementBounds.Fixed(130, 75, 80, 30), OnYOffsetChanged, CairoFont.WhiteSmallText(), "yoffset")
                    .AddStaticText("Leash Range:", CairoFont.WhiteSmallText(), ElementBounds.Fixed(10, 120, 120, 25))
                    .AddNumberInput(ElementBounds.Fixed(130, 115, 80, 30), OnLeashChanged, CairoFont.WhiteSmallText(), "leash")
                    .AddStaticText("Арена:", CairoFont.WhiteSmallText(), ElementBounds.Fixed(10, 160, 120, 25))
                    .AddDropDown(boolValues, boolNames, (Data?.arenaEnabled ?? false) ? 1 : 0, OnArenaEnabledChanged, ElementBounds.Fixed(130, 155, 100, 30), "arenaEnabled")
                    .AddStaticText("Сохранять вещи:", CairoFont.WhiteSmallText(), ElementBounds.Fixed(10, 200, 140, 25))
                    .AddDropDown(boolValues, boolNames, (Data?.arenaKeepInventory ?? true) ? 1 : 0, OnArenaKeepInvChanged, ElementBounds.Fixed(150, 195, 100, 30), "arenaKeepInv")
                    .AddSmallButton("Сохранить", OnSave, ElementBounds.Fixed(width / 2 - 60, 250, 120, 30))
                .EndChildElements()
                .Compose();

            SingleComposer.GetNumberInput("yoffset")?.SetValue(Data?.yOffset.ToString("0.0") ?? "1.0");
            SingleComposer.GetNumberInput("leash")?.SetValue(Data?.leashRange > 0 ? Data.leashRange.ToString("0") : "60");
        }

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            Compose();
        }

        private void OnTierChanged(string code, bool selected)
        {
            if (Data != null && selected && int.TryParse(code, out int t)) Data.tier = t;
        }

        private void OnYOffsetChanged(string value)
        {
            if (Data != null && float.TryParse(value, out float v)) Data.yOffset = v;
        }

        private void OnLeashChanged(string value)
        {
            if (Data != null && float.TryParse(value, out float v)) Data.leashRange = v;
        }

        private void OnArenaEnabledChanged(string code, bool selected)
        {
            if (Data != null && selected) Data.arenaEnabled = code == "true";
        }

        private void OnArenaKeepInvChanged(string code, bool selected)
        {
            if (Data != null && selected) Data.arenaKeepInventory = code == "true";
        }

        private bool OnSave()
        {
            if (Data == null) return true;

            var dropdown = SingleComposer.GetDropDown("tier");
            if (dropdown != null && int.TryParse(dropdown.SelectedValue, out int t))
            {
                Data.tier = t;
            }

            var numInput = SingleComposer.GetNumberInput("yoffset");
            if (numInput != null && float.TryParse(numInput.GetText(), out float y))
            {
                Data.yOffset = y;
            }

            var leashInput = SingleComposer.GetNumberInput("leash");
            if (leashInput != null && float.TryParse(leashInput.GetText(), out float l))
            {
                Data.leashRange = l;
            }

            var arenaDropdown = SingleComposer.GetDropDown("arenaEnabled");
            if (arenaDropdown != null) Data.arenaEnabled = arenaDropdown.SelectedValue == "true";

            var keepInvDropdown = SingleComposer.GetDropDown("arenaKeepInv");
            if (keepInvDropdown != null) Data.arenaKeepInventory = keepInvDropdown.SelectedValue == "true";

            capi.Network.SendBlockEntityPacket(blockPos, PacketSave, SerializerUtil.Serialize(Data));
            TryClose();
            return true;
        }
    }
}
