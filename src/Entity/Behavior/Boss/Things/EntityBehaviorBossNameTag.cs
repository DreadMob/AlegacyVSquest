using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VsQuest
{
    public class EntityBehaviorBossNameTag : EntityBehavior, IRenderer, IDisposable
    {
        private ICoreClientAPI capi;
        private LoadedTexture nameTagTexture;
        private double[] color = ColorUtil.WhiteArgbDouble;
        private TextBackground background;
        private int renderRange = 80;
        private bool showOnlyWhenTargeted;
        private string nameLangKey;
        private string rawName;

        private string lastRenderedText;
        private long lastUpdateMs;

        public double RenderOrder => 1.0;
        public int RenderRange => renderRange;

        public EntityBehaviorBossNameTag(Entity entity) : base(entity)
        {
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            capi = entity.World.Api as ICoreClientAPI;
            if (capi == null) return;

            renderRange = attributes["renderRange"].AsInt(80);
            showOnlyWhenTargeted = attributes["showOnlyWhenTargeted"].AsBool(false);

            nameLangKey = attributes["nameLangKey"].AsString(null);
            rawName = attributes["name"].AsString(null);

            string hex = attributes["color"].AsString("#ff0000");
            color = TryHexToRgbaDouble(hex, out var parsed) ? parsed : ColorUtil.WhiteArgbDouble;

            string backgroundHex = attributes["backgroundColor"].AsString("#000000");
            double backgroundOpacity = attributes["backgroundOpacity"].AsDouble(0.35);
            var fill = ColorUtil.Hex2Doubles(backgroundHex, backgroundOpacity);

            background = new TextBackground
            {
                FillColor = fill,
                Padding = 3,
                Radius = GuiStyle.ElementBGRadius,
                Shade = false,
                BorderColor = ColorUtil.Hex2Doubles("#000000", 0.5),
                BorderWidth = 2.0
            };

            capi.Event.RegisterRenderer(this, EnumRenderStage.Ortho, "alegacyvsquest-bossnametag");
            RegenTexture();
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (capi == null) return;

            // Don't show boss bar for mirror image clones
            if (entity.WatchedAttributes.GetBool("alegacyvsquest:bossclone", false)) return;

            long nowMs = capi.World.ElapsedMilliseconds;
            if (nowMs - lastUpdateMs >= 1000)
            {
                lastUpdateMs = nowMs;
                RegenTexture();
            }

            if (nameTagTexture == null) return;

            if (showOnlyWhenTargeted && capi.World.Player.CurrentEntitySelection?.Entity != entity)
            {
                return;
            }

            if (capi.World.Player.Entity.Pos.SquareDistanceTo(entity.Pos) > renderRange * renderRange) return;

            if (entity.Properties.Client.Renderer is not EntityShapeRenderer esr) return;

            IRenderAPI rapi = capi.Render;
            Vec3d pos = MatrixToolsd.Project(esr.getAboveHeadPosition(capi.World.Player.Entity), rapi.PerspectiveProjectionMat, rapi.PerspectiveViewMat, rapi.FrameWidth, rapi.FrameHeight);
            if (pos.Z < 0.0) return;

            float scale = 4f / Math.Max(1f, (float)pos.Z);
            float cappedScale = Math.Min(1f, scale);
            if (cappedScale > 0.75f)
            {
                cappedScale = 0.75f + (cappedScale - 0.75f) / 2f;
            }

            float posx = (float)pos.X - cappedScale * nameTagTexture.Width / 2f;
            float posy = (float)rapi.FrameHeight - (float)pos.Y - nameTagTexture.Height * Math.Max(0f, cappedScale);

            rapi.Render2DTexture(nameTagTexture.TextureId, posx, posy, cappedScale * nameTagTexture.Width, cappedScale * nameTagTexture.Height, 20f);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            Dispose();
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            lastRenderedText = null;
            RegenTexture();
        }

        public void Dispose()
        {
            if (nameTagTexture != null)
            {
                nameTagTexture.Dispose();
                nameTagTexture = null;
            }

            capi?.Event.UnregisterRenderer(this, EnumRenderStage.Ortho);
        }

        public override string PropertyName() => "bossnametag";

        private void RegenTexture()
        {
            if (capi == null) return;

            string text = null;
            if (!string.IsNullOrWhiteSpace(nameLangKey))
            {
                if (nameLangKey.Contains(":"))
                {
                    text = LocalizationUtils.GetSafe(nameLangKey);
                }
                else
                {
                    string domain = entity?.Code?.Domain;
                    if (!string.IsNullOrWhiteSpace(domain))
                    {
                        text = LocalizationUtils.GetSafe(domain + ":" + nameLangKey);
                    }

                    text ??= LocalizationUtils.GetSafe(nameLangKey) ?? LocalizationUtils.GetSafe("game:" + nameLangKey);
                }

                if (string.IsNullOrWhiteSpace(text) || string.Equals(text, nameLangKey, StringComparison.OrdinalIgnoreCase))
                {
                    text = nameLangKey;
                }
            }
            else if (!string.IsNullOrWhiteSpace(rawName))
            {
                text = rawName;
            }

            if (!string.IsNullOrWhiteSpace(text) && entity != null && !entity.Alive)
            {
                double respawnAt = entity.WatchedAttributes.GetDouble("alegacyvsquest:bossrespawnAtTotalHours", double.NaN);
                if (double.IsNaN(respawnAt))
                {
                    respawnAt = entity.WatchedAttributes.GetDouble("vsquest:bossrespawnAtTotalHours", double.NaN);
                }

                if (!double.IsNaN(respawnAt) && respawnAt > capi.World.Calendar.TotalHours)
                {
                    double hoursLeft = Math.Max(0, respawnAt - capi.World.Calendar.TotalHours);
                    text = LocalizationUtils.GetSafe("alegacyvsquest:boss-respawn-suffix", text, hoursLeft);
                }
            }

            if (string.IsNullOrWhiteSpace(text)) return;

            if (string.Equals(lastRenderedText, text, StringComparison.Ordinal)) return;
            lastRenderedText = text;

            nameTagTexture?.Dispose();
            nameTagTexture = capi.Gui.TextTexture.GenUnscaledTextTexture(text, CairoFont.WhiteMediumText().WithColor(color), background);
        }

        private static bool TryHexToRgbaDouble(string hex, out double[] rgba)
        {
            rgba = null;
            if (string.IsNullOrWhiteSpace(hex)) return false;

            string s = hex.Trim();
            if (s.StartsWith("#")) s = s.Substring(1);

            if (s.Length == 6)
            {
                if (!int.TryParse(s.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out int r)) return false;
                if (!int.TryParse(s.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out int g)) return false;
                if (!int.TryParse(s.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out int b)) return false;

                rgba = new[] { r / 255.0, g / 255.0, b / 255.0, 1.0 };
                return true;
            }

            if (s.Length == 8)
            {
                if (!int.TryParse(s.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out int r)) return false;
                if (!int.TryParse(s.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out int g)) return false;
                if (!int.TryParse(s.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out int b)) return false;
                if (!int.TryParse(s.Substring(6, 2), System.Globalization.NumberStyles.HexNumber, null, out int a)) return false;

                rgba = new[] { r / 255.0, g / 255.0, b / 255.0, a / 255.0 };
                return true;
            }

            return false;
        }
    }
}
