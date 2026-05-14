using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace VsQuest
{
    /// <summary>
    /// Client-side GUI for the Trial Warden shop.
    /// Shows reputation, void shards balance, rank, and purchasable items.
    /// </summary>
    public class TrialShopGui : GuiDialog
    {
        public override string ToggleKeyCombinationCode => null;

        private readonly OpenTrialShopMessage data;
        private const string NetworkChannel = "alegacyvsquest:trialshop";

        public TrialShopGui(ICoreClientAPI capi, OpenTrialShopMessage data) : base(capi)
        {
            this.data = data;
            Compose();
        }

        private void Compose()
        {
            double width = 420;
            double headerHeight = 80;
            double itemHeight = 45;
            int itemCount = data.ShopItems?.Length ?? 0;
            double listHeight = Math.Max(200, itemCount * itemHeight);
            double totalHeight = headerHeight + listHeight + 60;

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds bgBounds = ElementBounds.Fixed(0, 0, width, totalHeight);

            string title = LocalizationUtils.GetSafe("albase:trial-warden-shop-title");

            var composer = capi.Gui.CreateCompo("TrialShopGui", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(title, OnClose)
                .BeginChildElements(bgBounds);

            // Header: reputation + shards + rank
            double y = 35;
            string repLabel = LocalizationUtils.GetSafe("albase:trial-reputation-label");
            string currencyLabel = LocalizationUtils.GetSafe("albase:trial-currency-name");

            string headerText = $"<font color=\"#A78BFA\">{data.RankName}</font>  |  {repLabel}: <font color=\"#FFD700\">{data.Reputation}</font>  |  {currencyLabel}: <font color=\"#60A5FA\">{data.VoidShards}</font>";

            composer.AddRichtext(headerText, CairoFont.WhiteSmallText(),
                ElementBounds.Fixed(15, y, width - 30, 30));

            y += 45;

            // Separator
            composer.AddStaticText("─────────────────────────────────", CairoFont.WhiteSmallText().WithColor(new double[] { 0.4, 0.4, 0.4, 1 }),
                ElementBounds.Fixed(15, y, width - 30, 15));

            y += 20;

            // Shop items
            if (data.ShopItems != null)
            {
                for (int i = 0; i < data.ShopItems.Length; i++)
                {
                    var item = data.ShopItems[i];
                    AddShopItem(composer, item, y, width, i);
                    y += itemHeight;
                }
            }

            composer.EndChildElements();
            SingleComposer = composer.Compose();
        }

        private void AddShopItem(GuiComposer composer, TrialShopItemData item, double y, double width, int index)
        {
            string name = !string.IsNullOrWhiteSpace(item.NameKey)
                ? LocalizationUtils.GetSafe(item.NameKey)
                : item.ItemCode;

            string costText = item.Cost.ToString();
            bool canBuy = !item.IsLocked && data.VoidShards >= item.Cost;
            bool soldOut = item.MaxPurchases > 0 && item.PurchasesMade >= item.MaxPurchases;

            string color = item.IsLocked ? "#666666" : (soldOut ? "#888888" : "#FFFFFF");
            string costColor = canBuy ? "#60A5FA" : "#CD5C5C";

            string statusSuffix = "";
            if (item.IsLocked)
            {
                statusSuffix = $" <font color=\"#666666\">[{LocalizationUtils.GetSafe("albase:trial-warden-shop-locked")}]</font>";
            }
            else if (soldOut)
            {
                statusSuffix = " <font color=\"#888888\">[—]</font>";
            }

            string lineText = $"<font color=\"{color}\">{name}</font>  <font color=\"{costColor}\">{costText}◆</font>{statusSuffix}";

            composer.AddRichtext(lineText, CairoFont.WhiteSmallText(),
                ElementBounds.Fixed(15, y, width - 100, 35));

            // Buy button
            if (canBuy && !soldOut)
            {
                string btnKey = "buy_" + index;
                int capturedIndex = index;
                composer.AddSmallButton(LocalizationUtils.GetSafe("alegacyvsquest:dialogue-buy") != "alegacyvsquest:dialogue-buy"
                    ? LocalizationUtils.GetSafe("alegacyvsquest:dialogue-buy") : "✓",
                    () => OnBuyClicked(capturedIndex),
                    ElementBounds.Fixed(width - 75, y, 55, 28));
            }
        }

        private bool OnBuyClicked(int index)
        {
            if (data.ShopItems == null || index < 0 || index >= data.ShopItems.Length) return false;

            var item = data.ShopItems[index];

            capi.Network.GetChannel(NetworkChannel).SendPacket(new BuyTrialShopItemMessage
            {
                ItemCode = item.ItemCode,
                Cost = item.Cost
            });

            TryClose();
            return true;
        }

        private void OnClose()
        {
            TryClose();
        }

        public static void ShowFromMessage(OpenTrialShopMessage message, ICoreClientAPI capi)
        {
            new TrialShopGui(capi, message).TryOpen();
        }
    }
}
