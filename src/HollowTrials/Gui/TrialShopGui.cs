using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace VsQuest
{
    /// <summary>
    /// Client-side GUI for the Trial Warden shop.
    /// Two tabs: Shop (items + case preview) and Stats (personal records).
    /// </summary>
    public class TrialShopGui : GuiDialog
    {
        public override string ToggleKeyCombinationCode => null;

        private readonly OpenTrialShopMessage data;
        private const string NetworkChannel = "alegacyvsquest:trialshop";

        public TrialShopGui(ICoreClientAPI capi, OpenTrialShopMessage data) : base(capi)
        {
            this.data = data;
            ComposeShop();
        }

        private void ComposeShop()
        {
            double width = 420;
            double headerHeight = 100;
            double itemHeight = 50;
            int itemCount = data.ShopItems?.Length ?? 0;
            double listHeight = Math.Max(200, itemCount * itemHeight);
            double totalHeight = headerHeight + listHeight + 80;

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds bgBounds = ElementBounds.Fixed(0, 0, width, totalHeight);

            string title = LocalizationUtils.GetSafe("albase:trial-warden-shop-title");

            var composer = capi.Gui.CreateCompo("TrialShopGui", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(title, OnClose)
                .BeginChildElements(bgBounds);

            double y = 35;

            // Tab buttons
            composer.AddSmallButton("Магазин", () => { ComposeShop(); return true; },
                ElementBounds.Fixed(15, y, 80, 25));
            composer.AddSmallButton("Статистика", () => { ComposeStats(); return true; },
                ElementBounds.Fixed(105, y, 100, 25));

            y += 35;

            // Header
            string repLabel = LocalizationUtils.GetSafe("albase:trial-reputation-label");
            string currencyLabel = LocalizationUtils.GetSafe("albase:trial-currency-name");
            string headerText = $"<font color=\"#A78BFA\">{data.RankName}</font>  |  {repLabel}: <font color=\"#FFD700\">{data.Reputation}</font>  |  {currencyLabel}: <font color=\"#60A5FA\">{data.VoidShards}</font>";
            composer.AddRichtext(headerText, CairoFont.WhiteSmallText(), ElementBounds.Fixed(15, y, width - 30, 25));
            y += 30;

            // Trials counter
            string trialsText = $"<font color=\"#9CA3AF\">Выполнено испытаний: <font color=\"#C4B5FD\">{data.CompletedTrials}</font>/{data.TotalTrials}</font>";
            composer.AddRichtext(trialsText, CairoFont.WhiteSmallText(), ElementBounds.Fixed(15, y, width - 30, 20));
            y += 25;

            // Separator
            composer.AddStaticText("─────────────────────────────────────", CairoFont.WhiteSmallText().WithColor(new double[] { 0.4, 0.4, 0.4, 1 }),
                ElementBounds.Fixed(15, y, width - 30, 15));
            y += 20;

            // Shop items
            if (data.ShopItems != null)
            {
                for (int i = 0; i < data.ShopItems.Length; i++)
                {
                    AddShopItem(composer, data.ShopItems[i], y, width, i);
                    y += itemHeight;
                }
            }

            composer.EndChildElements();
            SingleComposer = composer.Compose();
        }

        private void ComposeStats()
        {
            double width = 420;
            var statLines = data.PlayerStats ?? Array.Empty<string>();
            double lineHeight = 22;
            double totalHeight = 180 + statLines.Length * lineHeight;

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds bgBounds = ElementBounds.Fixed(0, 0, width, totalHeight);

            string title = LocalizationUtils.GetSafe("albase:trial-warden-shop-title");

            var composer = capi.Gui.CreateCompo("TrialShopGui", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(title, OnClose)
                .BeginChildElements(bgBounds);

            double y = 35;

            // Tab buttons
            composer.AddSmallButton("Магазин", () => { ComposeShop(); return true; },
                ElementBounds.Fixed(15, y, 80, 25));
            composer.AddSmallButton("Статистика", () => { ComposeStats(); return true; },
                ElementBounds.Fixed(105, y, 100, 25));

            y += 40;

            // Stats header
            composer.AddRichtext("<font color=\"#C4B5FD\">Личная статистика</font>", CairoFont.WhiteSmallText(),
                ElementBounds.Fixed(15, y, width - 30, 25));
            y += 30;

            if (statLines.Length == 0)
            {
                composer.AddRichtext("<font color=\"#6B7280\">Нет данных. Убей босса чтобы увидеть статистику.</font>",
                    CairoFont.WhiteSmallText(), ElementBounds.Fixed(15, y, width - 30, 25));
            }
            else
            {
                // Column headers
                string header = $"<font color=\"#9CA3AF\">{"Босс",-20} Тир  Убийств  Время  Без смертей</font>";
                composer.AddRichtext(header, CairoFont.WhiteSmallText(), ElementBounds.Fixed(15, y, width - 30, 20));
                y += 22;

                foreach (var line in statLines)
                {
                    // Format: "BossName|tier|kills|bestTime|deathlessKills|bestChallenges"
                    var parts = line.Split('|');
                    if (parts.Length < 6) continue;

                    string bossName = parts[0];
                    string tier = parts[1];
                    string kills = parts[2];
                    string bestTime = parts[3];
                    string deathless = parts[4];

                    string tierRoman = tier switch { "1" => "I", "2" => "II", "3" => "III", _ => tier };
                    string timeDisplay = bestTime == "—" ? "—" : $"{bestTime} мин";

                    string statLine = $"<font color=\"#E5E7EB\">{bossName}</font> <font color=\"#A78BFA\">{tierRoman}</font>  <font color=\"#60A5FA\">{kills}</font>  <font color=\"#FFD700\">{timeDisplay}</font>  <font color=\"#34D399\">{deathless}</font>";
                    composer.AddRichtext(statLine, CairoFont.WhiteSmallText(), ElementBounds.Fixed(15, y, width - 30, 20));
                    y += lineHeight;
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

            bool canBuy = !item.IsLocked && data.VoidShards >= item.Cost;
            bool soldOut = item.MaxPurchases > 0 && item.PurchasesMade >= item.MaxPurchases;

            string color = item.IsLocked ? "#666666" : (soldOut ? "#888888" : "#FFFFFF");
            string costColor = canBuy ? "#60A5FA" : "#CD5C5C";

            string statusSuffix = "";
            if (item.IsLocked)
                statusSuffix = $" <font color=\"#666666\">[Закрыто]</font>";
            else if (soldOut)
                statusSuffix = " <font color=\"#888888\">[Куплено]</font>";

            // Case preview: show pool contents below name
            string poolPreview = "";
            if (item.CasePoolNames != null && item.CasePoolNames.Length > 0)
            {
                poolPreview = $"\n<font color=\"#6B7280\">  Содержимое: {string.Join(", ", item.CasePoolNames)}</font>";
            }

            string lineText = $"<font color=\"{color}\">{name}</font>  <font color=\"{costColor}\">{item.Cost}◆</font>{statusSuffix}{poolPreview}";

            composer.AddRichtext(lineText, CairoFont.WhiteSmallText(),
                ElementBounds.Fixed(15, y, width - 100, 45));

            // Buy button
            if (canBuy && !soldOut)
            {
                int capturedIndex = index;
                composer.AddSmallButton("✓", () => OnBuyClicked(capturedIndex),
                    ElementBounds.Fixed(width - 65, y + 5, 45, 28));
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
