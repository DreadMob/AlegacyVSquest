using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace VsQuest
{
    /// <summary>
    /// Status of an item row in the list.
    /// </summary>
    public enum ItemRowStatus
    {
        Locked,
        Available,
        Owned
    }

    /// <summary>
    /// Data for a single row in the item list.
    /// </summary>
    public class ItemListRow
    {
        public string Id { get; set; }
        public string IconItemCode { get; set; }
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public ItemRowStatus Status { get; set; }
    }

    /// <summary>
    /// A reusable vertical list GUI element with icon nodes on the left and text on the right.
    /// Each row: [circle with item icon] Title
    ///                                    Subtitle (smaller, grey)
    /// Click on a row fires the callback with the row Id.
    /// </summary>
    public class ItemListElement : GuiElement
    {
        private List<ItemListRow> rows;
        private readonly Action<string> onRowClicked;
        private LoadedTexture listTexture;
        private string hoveredRowId;
        private readonly Dictionary<string, ItemStack> iconStacks = new(StringComparer.OrdinalIgnoreCase);

        private double RowHeight => scaled(72.0);
        private double NodeRadius => scaled(24.0);
        private double IconSize => scaled(26.0);
        private double NodeCenterX => scaled(32.0);
        private double TextLeft => scaled(62.0);
        private double PadTop => scaled(8.0);

        public ItemListElement(ICoreClientAPI capi, ElementBounds bounds, List<ItemListRow> rows, Action<string> onRowClicked)
            : base(capi, bounds)
        {
            this.rows = rows ?? new List<ItemListRow>();
            this.onRowClicked = onRowClicked;
            listTexture = new LoadedTexture(capi);
        }

        public void SetData(List<ItemListRow> rows)
        {
            this.rows = rows ?? new List<ItemListRow>();
            hoveredRowId = null;
            iconStacks.Clear();
            RegenerateTexture();
        }

        public override void ComposeElements(Cairo.Context ctxStatic, Cairo.ImageSurface surfaceStatic)
        {
            RegenerateTexture();
        }

        public override void RenderInteractiveElements(float deltaTime)
        {
            if (listTexture == null || Bounds?.ParentBounds == null) return;

            string newHover = null;
            try
            {
                newHover = Bounds.PointInside(api.Input.MouseX, api.Input.MouseY)
                    ? GetRowIdAt(api.Input.MouseY)
                    : null;
            }
            catch { return; }

            if (!string.Equals(newHover, hoveredRowId, StringComparison.Ordinal))
            {
                hoveredRowId = newHover;
                RegenerateTexture();
            }

            api.Render.Render2DLoadedTexture(listTexture, (float)Bounds.absX, (float)Bounds.absY);
            RenderIcons();
        }

        private void RenderIcons()
        {
            if (rows == null || rows.Count == 0) return;
            Bounds.CalcWorldBounds();

            double rowH = RowHeight;
            double iconSz = IconSize;
            double nodeCX = NodeCenterX;
            double padTop = PadTop;

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null || string.IsNullOrWhiteSpace(row.IconItemCode)) continue;

                var stack = GetIconStack(row.IconItemCode);
                if (stack == null) continue;

                var slot = new DummySlot(stack);
                double rowTop = padTop + i * rowH;
                double nodeCY = rowTop + rowH / 2.0;

                // RenderItemstackToGui expects CENTER coordinates when shading=true
                double centerX = Bounds.absX + nodeCX;
                double centerY = Bounds.absY + nodeCY;

                api.Render.RenderItemstackToGui(slot, centerX, centerY, 500, (float)iconSz, -1, true, false, false);
            }
        }

        public override void OnMouseUpOnElement(ICoreClientAPI api, MouseEvent args)
        {
            if (Bounds?.ParentBounds == null) return;
            if (!Bounds.PointInside(args.X, args.Y)) return;

            string rowId = GetRowIdAt(args.Y);
            if (!string.IsNullOrWhiteSpace(rowId))
            {
                var row = rows.Find(r => string.Equals(r?.Id, rowId, StringComparison.Ordinal));
                if (row != null && row.Status == ItemRowStatus.Available)
                {
                    onRowClicked?.Invoke(rowId);
                    args.Handled = true;
                    return;
                }
            }

            base.OnMouseUpOnElement(api, args);
        }

        private string GetRowIdAt(int mouseY)
        {
            if (rows == null || rows.Count == 0) return null;
            Bounds.CalcWorldBounds();

            double relY = mouseY - Bounds.absY - PadTop;
            if (relY < 0) return null;

            int index = (int)(relY / RowHeight);
            if (index < 0 || index >= rows.Count) return null;

            return rows[index]?.Id;
        }

        private void RegenerateTexture()
        {
            Bounds.CalcWorldBounds();

            int width = Math.Max(1, (int)Bounds.InnerWidth);
            int height = Math.Max(1, (int)Bounds.InnerHeight);

            var surface = new Cairo.ImageSurface(Cairo.Format.Argb32, width, height);
            var context = new Cairo.Context(surface);

            context.SetSourceRGBA(0, 0, 0, 0);
            context.Paint();

            if (rows == null || rows.Count == 0)
            {
                generateTexture(surface, ref listTexture);
                context.Dispose();
                surface.Dispose();
                return;
            }

            double rowH = RowHeight;
            double nodeCX = NodeCenterX;
            double nodeR = NodeRadius;
            double textX = TextLeft;
            double padTop = PadTop;

            context.SelectFontFace("Sans", Cairo.FontSlant.Normal, Cairo.FontWeight.Normal);

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null) continue;

                double rowTop = padTop + i * rowH;
                double nodeCY = rowTop + rowH / 2.0;
                bool isHovered = string.Equals(hoveredRowId, row.Id, StringComparison.Ordinal);

                // Hover background stripe
                if (isHovered && row.Status == ItemRowStatus.Available)
                {
                    context.SetSourceRGBA(1.0, 1.0, 1.0, 0.05);
                    context.Rectangle(0, rowTop, width, rowH);
                    context.Fill();
                }

                // Node circle (background fill)
                var (fillR, fillG, fillB) = GetNodeFillColor(row.Status);
                double fillAlpha = isHovered ? 0.3 : 0.18;
                context.SetSourceRGBA(fillR, fillG, fillB, fillAlpha);
                context.Arc(nodeCX, nodeCY, nodeR, 0, Math.PI * 2.0);
                context.Fill();

                // Node circle (outline)
                double outlineAlpha = isHovered ? 0.8 : 0.5;
                context.SetSourceRGBA(fillR, fillG, fillB, outlineAlpha);
                context.LineWidth = scaled(1.5);
                context.Arc(nodeCX, nodeCY, nodeR, 0, Math.PI * 2.0);
                context.Stroke();

                // Title text
                var (tR, tG, tB, tA) = GetTitleColor(row.Status, isHovered);
                context.SetSourceRGBA(tR, tG, tB, tA);
                context.SetFontSize(scaled(19.0));
                context.MoveTo(textX, rowTop + scaled(28.0));
                context.ShowText(row.Title ?? "");

                // Subtitle text
                if (!string.IsNullOrWhiteSpace(row.Subtitle))
                {
                    var (sR, sG, sB, sA) = GetSubtitleColor(row.Status);
                    context.SetSourceRGBA(sR, sG, sB, sA);
                    context.SetFontSize(scaled(17.0));
                    context.MoveTo(textX, rowTop + scaled(50.0));
                    context.ShowText(row.Subtitle);
                }
            }

            try { generateTexture(surface, ref listTexture); } catch { }
            context.Dispose();
            surface.Dispose();
        }

        private static (double, double, double) GetNodeFillColor(ItemRowStatus status)
        {
            return status switch
            {
                ItemRowStatus.Owned => (0.3, 0.8, 0.4),
                ItemRowStatus.Available => (0.65, 0.55, 0.95),
                _ => (0.5, 0.5, 0.5)
            };
        }

        private static (double, double, double, double) GetTitleColor(ItemRowStatus status, bool hovered)
        {
            return status switch
            {
                ItemRowStatus.Locked => (0.85, 0.85, 0.85, 0.85),
                ItemRowStatus.Owned => (0.55, 1.0, 0.6, 1.0),
                _ => (1.0, 1.0, 1.0, 1.0)
            };
        }

        private static (double, double, double, double) GetSubtitleColor(ItemRowStatus status)
        {
            return status switch
            {
                ItemRowStatus.Locked => (0.75, 0.75, 0.75, 0.75),
                ItemRowStatus.Owned => (0.7, 0.95, 0.7, 0.85),
                _ => (0.95, 0.95, 0.95, 0.95)
            };
        }

        private ItemStack GetIconStack(string itemCode)
        {
            if (string.IsNullOrWhiteSpace(itemCode)) return null;
            if (iconStacks.TryGetValue(itemCode, out var cached)) return cached;

            ItemStack stack = null;
            var loc = new AssetLocation(itemCode);
            var item = api.World.GetItem(loc);
            if (item != null)
            {
                stack = new ItemStack(item);
            }
            else
            {
                var block = api.World.GetBlock(loc);
                if (block != null)
                {
                    stack = new ItemStack(block);
                }
                else
                {
                    try
                    {
                        var itemSystem = api.ModLoader.GetModSystem<ItemSystem>();
                        if (itemSystem?.ActionItemRegistry != null && itemSystem.ActionItemRegistry.TryGetValue(itemCode, out var actionItem))
                        {
                            if (!string.IsNullOrWhiteSpace(actionItem?.itemCode))
                            {
                                var baseLoc = new AssetLocation(actionItem.itemCode);
                                var baseItem = api.World.GetItem(baseLoc);
                                if (baseItem != null) stack = new ItemStack(baseItem);
                                else
                                {
                                    var baseBlock = api.World.GetBlock(baseLoc);
                                    if (baseBlock != null) stack = new ItemStack(baseBlock);
                                }
                            }
                        }
                    }
                    catch { }
                }
            }

            if (stack != null) iconStacks[itemCode] = stack;
            return stack;
        }

        public override void Dispose()
        {
            listTexture?.Dispose();
            listTexture = null;
            base.Dispose();
        }
    }
}
