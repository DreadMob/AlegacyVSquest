using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace VsQuest
{
    public enum ReputationNodeStatus
    {
        Locked,
        Available,
        Claimed
    }

    public class ReputationTreeNode
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string RequirementText { get; set; }
        public string IconItemCode { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public ReputationNodeStatus Status { get; set; }
    }

    public class ReputationTreeElement : GuiElement
    {
        private List<ReputationTreeNode> nodes;
        private readonly Action<string> onNodeClicked;
        private LoadedTexture mapTexture;
        private string hoveredNodeId;
        private string selectedNodeId;
        private readonly Dictionary<string, ItemStack> iconStacks = new Dictionary<string, ItemStack>(StringComparer.OrdinalIgnoreCase);

        private double NodeIconSize => GuiElement.scaled(36.0);
        // Ensure the circle fully contains the square icon: radius >= (size/2) * sqrt(2), plus a small padding.
        private double NodeRadius => (NodeIconSize / 2.0) * Math.Sqrt(2.0) + GuiElement.scaled(14.0);

        public ReputationTreeElement(ICoreClientAPI capi, ElementBounds bounds, List<ReputationTreeNode> nodes, Action<string> onNodeClicked)
            : base(capi, bounds)
        {
            this.nodes = nodes ?? new List<ReputationTreeNode>();
            this.onNodeClicked = onNodeClicked;
            mapTexture = new LoadedTexture(capi);
        }

        public void SetData(List<ReputationTreeNode> nodes)
        {
            this.nodes = nodes ?? new List<ReputationTreeNode>();
            hoveredNodeId = null;
            iconStacks.Clear();
            if (selectedNodeId != null && !this.nodes.Exists(n => string.Equals(n?.Id, selectedNodeId, StringComparison.Ordinal)))
            {
                selectedNodeId = null;
            }
            RegenerateTexture();
        }

        public override void ComposeElements(Cairo.Context ctxStatic, Cairo.ImageSurface surfaceStatic)
        {
            RegenerateTexture();
        }

        public override void RenderInteractiveElements(float deltaTime)
        {
            if (mapTexture == null) return;
            if (Bounds?.ParentBounds == null) return;
            string newHover = null;
            try
            {
                newHover = Bounds.PointInside(api.Input.MouseX, api.Input.MouseY)
                    ? TryGetNodeIdAt(api.Input.MouseX, api.Input.MouseY)
                    : null;
            }
            catch
            {
                return;
            }
            if (!string.Equals(newHover, hoveredNodeId, StringComparison.Ordinal))
            {
                hoveredNodeId = newHover;
                RegenerateTexture();
            }
            api.Render.Render2DLoadedTexture(mapTexture, (float)Bounds.absX, (float)Bounds.absY);

            RenderNodeIcons();
        }

        private void RenderNodeIcons()
        {
            if (nodes == null || nodes.Count == 0) return;

            Bounds.CalcWorldBounds();

            // Match RegenerateTexture math (it uses integer width/height from Bounds.InnerWidth/Height).
            int width = Math.Max(1, (int)Bounds.InnerWidth);
            int height = Math.Max(1, (int)Bounds.InnerHeight);
            double pad = GuiElement.scaled(12.0);
            double plotWidth = Math.Max(1.0, width - pad * 2.0);
            double plotHeight = Math.Max(1.0, height - pad * 2.0);

            double NodeX(float x) => Bounds.absX + pad + plotWidth * x;
            double NodeY(float y) => Bounds.absY + pad + plotHeight * y;

            double size = NodeIconSize;

            foreach (var node in nodes)
            {
                if (node == null || string.IsNullOrWhiteSpace(node.IconItemCode)) continue;

                var stack = GetIconStack(node.IconItemCode);
                if (stack == null) continue;

                var slot = new DummySlot(stack);

                double drawX = NodeX(node.X);
                double drawY = NodeY(node.Y);

                api.Render.RenderItemstackToGui(slot, drawX, drawY, 500, (float)size, -1, true, false, false);
            }
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
                    // iconItemCode can also be an action item id (questitem). Resolve it to a real base collectible code.
                    try
                    {
                        var itemSystem = api.ModLoader.GetModSystem<ItemSystem>();
                        if (itemSystem?.ActionItemRegistry != null && itemSystem.ActionItemRegistry.TryGetValue(itemCode, out var actionItem))
                        {
                            if (!string.IsNullOrWhiteSpace(actionItem?.itemCode))
                            {
                                var baseLoc = new AssetLocation(actionItem.itemCode);
                                var baseItem = api.World.GetItem(baseLoc);
                                if (baseItem != null)
                                {
                                    stack = new ItemStack(baseItem);
                                }
                                else
                                {
                                    var baseBlock = api.World.GetBlock(baseLoc);
                                    if (baseBlock != null)
                                    {
                                        stack = new ItemStack(baseBlock);
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                    }
                }
            }

            if (stack != null)
            {
                iconStacks[itemCode] = stack;
            }
            return stack;
        }

        public override bool IsPositionInside(int posX, int posY)
        {
            if (Bounds?.ParentBounds == null) return false;
            try
            {
                return base.IsPositionInside(posX, posY);
            }
            catch
            {
                return false;
            }
        }

        public override void OnMouseUpOnElement(ICoreClientAPI api, MouseEvent args)
        {
            if (Bounds?.ParentBounds == null) return;
            try
            {
                if (!Bounds.PointInside(args.X, args.Y)) return;
            }
            catch
            {
                return;
            }

            string nodeId = TryGetNodeIdAt(args.X, args.Y);
            if (!string.IsNullOrWhiteSpace(nodeId))
            {
                onNodeClicked?.Invoke(nodeId);
                args.Handled = true;
                return;
            }

            base.OnMouseUpOnElement(api, args);
        }

        private string TryGetNodeIdAt(int mouseX, int mouseY)
        {
            if (nodes == null || nodes.Count == 0) return null;

            Bounds.CalcWorldBounds();
            double pad = GuiElement.scaled(12.0);
            double hitRadius = NodeRadius;

            double plotWidth = Math.Max(1.0, Bounds.InnerWidth - pad * 2.0);
            double plotHeight = Math.Max(1.0, Bounds.InnerHeight - pad * 2.0);

            double NodeX(float x) => Bounds.absX + pad + plotWidth * x;
            double NodeY(float y) => Bounds.absY + pad + plotHeight * y;

            foreach (var node in nodes)
            {
                double cx = NodeX(node.X);
                double cy = NodeY(node.Y);
                double dx = mouseX - cx;
                double dy = mouseY - cy;
                if (dx * dx + dy * dy <= hitRadius * hitRadius)
                {
                    return node.Id;
                }
            }

            return null;
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

            double pad = GuiElement.scaled(12.0);
            double radius = NodeRadius;
            double hoverRadius = radius + GuiElement.scaled(2.0);
            double selectedRadius = radius + GuiElement.scaled(3.0);

            double plotWidth = Math.Max(1, width - pad * 2.0);
            double plotHeight = Math.Max(1, height - pad * 2.0);

            double NodeX(float x) => pad + plotWidth * x;
            double NodeY(float y) => pad + plotHeight * y;

            context.SelectFontFace("Sans", Cairo.FontSlant.Normal, Cairo.FontWeight.Normal);
            context.SetFontSize(GuiElement.scaled(17.0));

            foreach (var node in nodes)
            {
                double x = NodeX(node.X);
                double y = NodeY(node.Y);

                bool isSelected = !string.IsNullOrWhiteSpace(selectedNodeId)
                    && string.Equals(selectedNodeId, node.Id, StringComparison.Ordinal);
                bool isHovered = !string.IsNullOrWhiteSpace(hoveredNodeId)
                    && string.Equals(hoveredNodeId, node.Id, StringComparison.Ordinal);

                var fill = GetNodeColor(node.Status);

                double circleRadius = isSelected ? selectedRadius : (isHovered ? hoverRadius : radius);

                // Fill circle background so the icon appears inside it.
                context.SetSourceRGBA(fill.Item1, fill.Item2, fill.Item3, isSelected ? 0.35 : (isHovered ? 0.28 : 0.22));
                context.Arc(x, y, circleRadius, 0, Math.PI * 2.0);
                context.Fill();

                // Outline for status/hover/selected.
                context.SetSourceRGBA(fill.Item1, fill.Item2, fill.Item3, isSelected ? 0.9 : (isHovered ? 0.7 : 0.55));
                context.LineWidth = GuiElement.scaled(2.0);
                context.Arc(x, y, circleRadius, 0, Math.PI * 2.0);
                context.Stroke();

                if (!string.IsNullOrWhiteSpace(node.Title))
                {
                    string label = FitLabelText(context, node.Title, GuiElement.scaled(180.0));
                    var extents = context.TextExtents(label);
                    double textX = x - extents.Width / 2.0;
                    double textY = y + radius + GuiElement.scaled(20.0);

                    context.SetSourceRGBA(1.0, 1.0, 1.0, 0.95);
                    context.MoveTo(textX, textY);
                    context.ShowText(label);

                    if ((isHovered || isSelected) && !string.IsNullOrWhiteSpace(node.RequirementText))
                    {
                        var lines = WrapText(context, node.RequirementText, GuiElement.scaled(360.0), 12);
                        double reqY = textY + GuiElement.scaled(14.0);
                        context.SetSourceRGBA(0.85, 0.85, 0.85, 0.9);

                        for (int i = 0; i < lines.Count; i++)
                        {
                            string line = lines[i];
                            var reqExtents = context.TextExtents(line);
                            double reqX = x - reqExtents.Width / 2.0;
                            context.MoveTo(reqX, reqY);
                            context.ShowText(line);
                            reqY += GuiElement.scaled(14.0);
                        }
                    }
                }
            }

            try
            {
                generateTexture(surface, ref mapTexture);
            }
            catch
            {
                return;
            }
            context.Dispose();
            surface.Dispose();
        }

        private static Tuple<double, double, double> GetNodeColor(ReputationNodeStatus status)
        {
            return status switch
            {
                ReputationNodeStatus.Claimed => Tuple.Create(0.25, 0.8, 0.35),
                ReputationNodeStatus.Available => Tuple.Create(0.95, 0.75, 0.25),
                _ => Tuple.Create(0.85, 0.2, 0.2)
            };
        }

        private static string FitLabelText(Cairo.Context context, string text, double maxWidth)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            if (maxWidth <= 0) return text;

            string result = text;
            var extents = context.TextExtents(result);
            if (extents.Width <= maxWidth) return result;

            const string suffix = "...";
            int maxChars = Math.Max(1, text.Length);
            while (maxChars > 1)
            {
                maxChars--;
                result = text.Substring(0, maxChars) + suffix;
                extents = context.TextExtents(result);
                if (extents.Width <= maxWidth) break;
            }

            return result;
        }

        private static List<string> WrapText(Cairo.Context context, string text, double maxWidth, int maxLines)
        {
            var lines = new List<string>();
            if (string.IsNullOrWhiteSpace(text) || maxWidth <= 0 || maxLines <= 0) return lines;

            var rawLines = text.Split(new[] { '\n' }, StringSplitOptions.None);
            for (int li = 0; li < rawLines.Length; li++)
            {
                string raw = rawLines[li]?.Trim();
                if (string.IsNullOrWhiteSpace(raw)) continue;

                var parts = raw.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                string cur = string.Empty;

                for (int i = 0; i < parts.Length; i++)
                {
                    string word = parts[i];
                    string candidate = string.IsNullOrWhiteSpace(cur) ? word : (cur + " " + word);
                    var ext = context.TextExtents(candidate);
                    if (ext.Width <= maxWidth)
                    {
                        cur = candidate;
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(cur))
                    {
                        lines.Add(cur);
                        if (lines.Count >= maxLines) return lines;
                        cur = word;
                    }
                    else
                    {
                        lines.Add(FitLabelText(context, word, maxWidth));
                        if (lines.Count >= maxLines) return lines;
                        cur = string.Empty;
                    }
                }

                if (!string.IsNullOrWhiteSpace(cur) && lines.Count < maxLines)
                {
                    lines.Add(cur);
                }

                if (lines.Count >= maxLines) return lines;
            }

            return lines;
        }

        public override void Dispose()
        {
            mapTexture?.Dispose();
            mapTexture = null;
            base.Dispose();
        }
    }
}
