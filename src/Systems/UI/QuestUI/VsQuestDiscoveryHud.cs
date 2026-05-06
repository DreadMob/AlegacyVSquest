using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace VsQuest
{
    public class VsQuestDiscoveryHud : HudElement
    {
        private GuiElementHoverText elem;
        private Vec4f fadeCol = new Vec4f(1f, 1f, 1f, 1f);
        private long textActiveMs;
        private int durationVisibleMs = 3500;

        public override double InputOrder => 1.0;
        public override string ToggleKeyCombinationCode => null;
        public override bool Focusable => false;

        public VsQuestDiscoveryHud(ICoreClientAPI capi) : base(capi)
        {
            capi.Event.RegisterGameTickListener(OnGameTick, 100);
            capi.Event.LevelFinalize += ComposeGuis;
        }

        public void Show(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            if (elem == null)
            {
                ComposeGuis();
                if (elem == null) return;
            }

            textActiveMs = capi.InWorldEllapsedMilliseconds;
            fadeCol.A = 0f;
            elem.SetNewText(text);
            elem.RenderColor = fadeCol;
            elem.SetVisible(true);
            TryOpen();
        }

        private void OnGameTick(float dt)
        {
            if (elem == null) return;
            if (textActiveMs == 0L) return;

            long visibleMsPassed = capi.InWorldEllapsedMilliseconds - textActiveMs;
            long visibleMsLeft = durationVisibleMs - visibleMsPassed;

            if (visibleMsLeft <= 0)
            {
                textActiveMs = 0L;
                elem.SetVisible(false);
                return;
            }

            if (visibleMsPassed < 250)
            {
                fadeCol.A = (float)visibleMsPassed / 240f;
            }
            else
            {
                fadeCol.A = 1f;
            }

            if (visibleMsLeft < 600)
            {
                fadeCol.A = (float)visibleMsLeft / 590f;
            }

            elem.RenderColor = fadeCol;
        }

        public void ComposeGuis()
        {
            if (capi == null) return;

            ElementBounds dialogBounds = new ElementBounds
            {
                Alignment = EnumDialogArea.CenterMiddle,
                BothSizing = ElementSizing.Fixed,
                fixedWidth = 600.0,
                fixedHeight = 5.0
            };

            ElementBounds iteminfoBounds = ElementBounds.Fixed(0.0, -155.0, 700.0, 30.0);

            ClearComposers();

            CairoFont font = CairoFont.WhiteMediumText().WithFont(GuiStyle.DecorativeFontName).WithColor(GuiStyle.DiscoveryTextColor)
                .WithStroke(GuiStyle.DialogBorderColor, 2.0)
                .WithOrientation(EnumTextOrientation.Center);

            Composers["alegacyvsquest-discovery"] = capi.Gui.CreateCompo("alegacyvsquest-discovery", dialogBounds.FlatCopy()).PremultipliedAlpha(false)
                .BeginChildElements(dialogBounds)
                .AddTranspHoverText("", font, 700, iteminfoBounds, "discoverytext")
                .EndChildElements()
                .Compose();

            elem = Composers["alegacyvsquest-discovery"].GetHoverText("discoverytext");
            elem.SetFollowMouse(false);
            elem.SetAutoWidth(false);
            elem.SetAutoDisplay(false);
            elem.fillBounds = true;
            elem.RenderColor = fadeCol;
            elem.ZPosition = 60f;
            elem.RenderAsPremultipliedAlpha = false;
            elem.SetVisible(false);
        }

        public override bool TryClose()
        {
            return false;
        }

        public override bool ShouldReceiveKeyboardEvents()
        {
            return false;
        }

        public override bool ShouldReceiveMouseEvents()
        {
            return false;
        }

        public override void OnRenderGUI(float deltaTime)
        {
            if (elem != null && fadeCol.A > 0f)
            {
                base.OnRenderGUI(deltaTime);
            }
        }

        protected override void OnFocusChanged(bool on)
        {
        }
    }
}
