using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class EntityBehaviorBossIntoxicationAura : BossAbilityBase
    {
        private const string IntoxUntilMsKey = "alegacyvsquest:bossintoxaura:until";

        protected override string CooldownKey => "alegacyvsquest:bossintoxaura:lastTickMs";
        protected override bool UsePeriodicTick() => true;
        protected override int CheckIntervalMs => 500;

        private class Stage : BossAbilityStage
        {
            public float range;
            public float intoxication;
            public int intervalMs;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                range = json["range"].AsFloat(24f);
                intoxication = json["intoxication"].AsFloat(0f);
                intervalMs = json["intervalMs"].AsInt(500);

                if (range <= 0f) range = 24f;
                if (intervalMs < 100) intervalMs = 100;
            }
        }

        private List<Stage> stages = new List<Stage>();
        private float maxRange;

        private long lastCleanupMs;

        public EntityBehaviorBossIntoxicationAura(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossintoxaura";

        protected override void InitializeStages(JsonObject attributes)
        {
            stages = ParseStages<Stage>(attributes);
            maxRange = 0f;
            foreach (var s in stages) if (s.range > maxRange) maxRange = s.range;
        }

        protected override void OnPeriodicTick(float dt)
        {
            if (Sapi == null || entity == null || stages.Count == 0 || !entity.Alive) return;

            CleanupExpiredIntoxication();

            if (!TryGetHealthFraction(out float frac)) return;

            (object stageObj, int stageIndex) = FindStageForHealth(frac);
            if (stageObj is not Stage stage) return;

            long nowMs = Sapi.World.ElapsedMilliseconds;
            if (!ShouldRunInterval(CooldownKey, stage.intervalMs, nowMs)) return;

            // Avoid values > 1.0 as they can break client rendering/controls.
            float targetIntox = GameMath.Clamp(stage.intoxication, 0f, 1.0f);
            if (targetIntox <= 0f) return;

            // Periodic poison aura visual
            if (Sapi.World.Rand.NextDouble() < 0.3)
            {
                ParticleUtils.SpawnAuraRing(Sapi, entity.Pos.XYZ.Add(0, 0.5, 0), stage.range * 0.4f, ParticleUtils.Colors.Poison, 10, 0.25f);
            }

            var players = Sapi.World.AllOnlinePlayers;
            if (players == null || players.Length == 0) return;

            double range = stage.range > 0 ? stage.range : 24f;
            double rangeSq = range * range;
            var selfPos = entity.Pos;

            for (int i = 0; i < players.Length; i++)
            {
                var player = players[i] as IServerPlayer;
                var playerEntity = player?.Entity;
                if (playerEntity == null || playerEntity.Pos.Dimension != selfPos.Dimension) continue;

                double dx = playerEntity.Pos.X - selfPos.X;
                double dy = playerEntity.Pos.Y - selfPos.Y;
                double dz = playerEntity.Pos.Z - selfPos.Z;
                double distSq = dx * dx + dy * dy + dz * dz;
                if (distSq > rangeSq) continue;

                float current = playerEntity.WatchedAttributes.GetFloat("intoxication", 0f);
                if (current >= targetIntox) continue;

                // Apply as a timed effect, not a permanent attribute.
                playerEntity.WatchedAttributes.SetLong(IntoxUntilMsKey, nowMs + Math.Max(1000, stage.intervalMs * 6));
                playerEntity.WatchedAttributes.MarkPathDirty(IntoxUntilMsKey);

                playerEntity.WatchedAttributes.SetFloat("intoxication", targetIntox);
                playerEntity.WatchedAttributes.MarkPathDirty("intoxication");
            }
        }

        private void CleanupExpiredIntoxication()
        {
            if (Sapi == null) return;

            long nowMs = Sapi.World.ElapsedMilliseconds;
            if (lastCleanupMs != 0 && nowMs - lastCleanupMs < 500) return;
            lastCleanupMs = nowMs;

            var players = Sapi.World.AllOnlinePlayers;
            if (players == null || players.Length == 0) return;

            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] is not IServerPlayer sp) continue;
                if (sp.Entity is not EntityPlayer plr) continue;

                long until = plr.WatchedAttributes.GetLong(IntoxUntilMsKey, 0);

                if (until <= 0)
                {
                    // Hard clamp in case something else pushed intoxication to invalid values.
                    float cur = plr.WatchedAttributes.GetFloat("intoxication", 0f);
                    if (cur > 1.0f)
                    {
                        plr.WatchedAttributes.SetFloat("intoxication", 1.0f);
                        plr.WatchedAttributes.MarkPathDirty("intoxication");
                    }

                    // Legacy fail-safe: if intoxication is stuck near max without our timer key,
                    // clear it so players can recover (black screen / broken controls).
                    // This should only trigger for extreme values.
                    if (cur >= 0.95f)
                    {
                        plr.WatchedAttributes.SetFloat("intoxication", 0f);
                        plr.WatchedAttributes.MarkPathDirty("intoxication");
                    }

                    continue;
                }

                // World.ElapsedMilliseconds resets on relog/server restart, but WatchedAttributes persist.
                // If 'until' is far in the future compared to 'now', it is almost certainly stale data.
                if (nowMs > 0)
                {
                    const long MaxFutureMs = 10L * 60L * 1000L;
                    if (until - nowMs > MaxFutureMs)
                    {
                        plr.WatchedAttributes.SetLong(IntoxUntilMsKey, 0);
                        plr.WatchedAttributes.MarkPathDirty(IntoxUntilMsKey);
                        plr.WatchedAttributes.SetFloat("intoxication", 0f);
                        plr.WatchedAttributes.MarkPathDirty("intoxication");
                        continue;
                    }
                }

                if (nowMs >= until)
                {
                    plr.WatchedAttributes.SetLong(IntoxUntilMsKey, 0);
                    plr.WatchedAttributes.MarkPathDirty(IntoxUntilMsKey);
                    plr.WatchedAttributes.SetFloat("intoxication", 0f);
                    plr.WatchedAttributes.MarkPathDirty("intoxication");
                }
            }
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            ClearIntoxication();
            base.OnEntityDeath(damageSourceForDeath);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            ClearIntoxication();
            base.OnEntityDespawn(despawn);
        }

        private void ClearIntoxication()
        {
            if (Sapi == null || entity == null) return;
            if (maxRange <= 0f) maxRange = 24f;

            var players = Sapi.World.AllOnlinePlayers;
            if (players == null || players.Length == 0) return;

            double range = maxRange * 1.1f;
            double rangeSq = range * range;
            var selfPos = entity.Pos;

            for (int i = 0; i < players.Length; i++)
            {
                var player = players[i] as IServerPlayer;
                var playerEntity = player?.Entity;
                if (playerEntity == null) continue;
                if (playerEntity.Pos.Dimension != selfPos.Dimension) continue;

                double dx = playerEntity.Pos.X - selfPos.X;
                double dy = playerEntity.Pos.Y - selfPos.Y;
                double dz = playerEntity.Pos.Z - selfPos.Z;
                double distSq = dx * dx + dy * dy + dz * dz;
                if (distSq > rangeSq) continue;

                playerEntity.WatchedAttributes.SetLong(IntoxUntilMsKey, 0);
                playerEntity.WatchedAttributes.MarkPathDirty(IntoxUntilMsKey);
                playerEntity.WatchedAttributes.SetFloat("intoxication", 0f);
                playerEntity.WatchedAttributes.MarkPathDirty("intoxication");
            }
        }

        // Required abstract overrides for BossAbilityBase (not used in periodic tick mode)
        protected override void ActivateAbility(object stage, int stageIndex, EntityPlayer target) { }
        protected override void StopAbility() { }
        protected override int GetStageCount() => stages.Count;
        protected override object GetStage(int index) => index >= 0 && index < stages.Count ? stages[index] : null;
        protected override float GetStageHealthThreshold(object stage) => stage is Stage s ? s.whenHealthRelBelow : 1f;
        protected override float GetStageCooldown(object stage) => 0f;
        protected override float GetMaxTargetRange(object stage) => 0f;
    }
}
