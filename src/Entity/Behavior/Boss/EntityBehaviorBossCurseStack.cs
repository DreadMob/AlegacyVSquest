using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Curse Stacks: reactive ability that triggers when boss deals damage to a player.
    /// Each hit applies a curse stack. At maxStacks — triggers an effect (stun, teleport, or slow).
    /// Stacks decay after stackDecaySeconds without being hit.
    /// Uses periodic tick to check for boss melee hits via lastDamageMs tracking.
    /// </summary>
    public class EntityBehaviorBossCurseStack : BossAbilityBase
    {
        private const string LastCurseKey = "alegacyvsquest:bosscursestack:lastMs";
        protected override string CooldownKey => LastCurseKey;

        private class Stage : BossAbilityStage
        {
            public int maxStacks;
            public string effectType;
            public float stackDecaySeconds;
            public float stunDurationMs;
            public float slowFactor;
            public float slowDurationMs;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                maxStacks = json["maxStacks"].AsInt(5);
                effectType = json["effectType"].AsString("stun");
                stackDecaySeconds = json["stackDecaySeconds"].AsFloat(4f);
                stunDurationMs = json["stunDurationMs"].AsFloat(2000f);
                slowFactor = json["slowFactor"].AsFloat(0.5f);
                slowDurationMs = json["slowDurationMs"].AsFloat(3000f);
            }
        }

        private List<Stage> stages = new();
        private readonly Dictionary<string, CurseData> playerCurses = new(StringComparer.OrdinalIgnoreCase);
        private long lastKnownDamageMs;

        public EntityBehaviorBossCurseStack(Entity entity) : base(entity) { }
        public override string PropertyName() => "bosscursestack";

        protected override bool UsePeriodicTick() => true;
        protected override bool RequiresTarget() => false;
        protected override bool ShouldCheckAbility() => false;

        protected override void InitializeStages(JsonObject attributes) { stages = ParseStages<Stage>(attributes); }
        protected override int GetStageCount() => stages.Count;
        protected override object GetStage(int index) => stages[index];
        protected override float GetStageHealthThreshold(object stage) => ((Stage)stage).whenHealthRelBelow;
        protected override float GetStageCooldown(object stage) => ((Stage)stage).cooldownSeconds;
        protected override float GetMaxTargetRange(object stage) => ((Stage)stage).maxTargetRange;

        protected override void ActivateAbility(object stageObj, int stageIndex, EntityPlayer target)
        {
            // Not used — this is a reactive ability
        }

        /// <summary>
        /// Called when the boss deals damage to a player. Adds a curse stack.
        /// </summary>
        public void OnBossDealtDamage(IServerPlayer target)
        {
            if (target?.Entity == null || !target.Entity.Alive) return;
            if (Sapi == null || !entity.Alive) return;
            if (stages.Count == 0) return;
            if (IsBossClone) return;

            // Find appropriate stage for current health
            if (!entity.TryGetHealthFraction(out float frac)) return;
            var (stageObj, _) = FindStageForHealth(frac);
            if (stageObj is not Stage stage) return;

            string uid = target.PlayerUID;
            long nowMs = Sapi.World.ElapsedMilliseconds;

            if (!playerCurses.TryGetValue(uid, out var data))
            {
                data = new CurseData();
                playerCurses[uid] = data;
            }

            // Decay check
            if (data.stacks > 0 && nowMs - data.lastStackMs > stage.stackDecaySeconds * 1000)
            {
                data.stacks = 0;
            }

            data.stacks++;
            data.lastStackMs = nowMs;

            // Particles per stack
            ParticleUtils.SpawnEntityAura(Sapi, target.Entity, ParticleUtils.Colors.Shadow, 4, 0.3f, 0.4f);

            // Trigger effect at max stacks
            if (data.stacks >= stage.maxStacks)
            {
                TriggerCurseEffect(stage, target);
                data.stacks = 0;
            }
        }

        protected override void OnPeriodicTick(float dt)
        {
            // Check if boss recently dealt damage by monitoring WatchedAttributes
            // The melee attack system sets lastDamageByEntityMs when boss hits
            if (Sapi == null || !entity.Alive || stages.Count == 0) return;

            long nowMs = Sapi.World.ElapsedMilliseconds;

            // Find nearby players within melee range and apply curse if boss is attacking
            foreach (var p in Sapi.World.AllOnlinePlayers)
            {
                if (p is not IServerPlayer sp) continue;
                if (sp.Entity == null || !sp.Entity.Alive) continue;
                if (sp.Entity.Pos.Dimension != entity.Pos.Dimension) continue;

                // Check if player was recently damaged by this entity (within last tick interval)
                long lastHurt = sp.Entity.WatchedAttributes.GetLong("lastDamageByEntityMs", 0);
                long lastHurtBy = sp.Entity.WatchedAttributes.GetLong("lastDamageByEntityId", 0);

                if (lastHurtBy == entity.EntityId && nowMs - lastHurt < CheckIntervalMs + 50)
                {
                    if (lastHurt > lastKnownDamageMs)
                    {
                        lastKnownDamageMs = lastHurt;
                        OnBossDealtDamage(sp);
                    }
                }
            }
        }

        private void TriggerCurseEffect(Stage stage, IServerPlayer target)
        {
            var pe = target.Entity;
            if (pe == null || !pe.Alive) return;

            switch (stage.effectType.ToLowerInvariant())
            {
                case "stun":
                    pe.Stats.Set("walkspeed", "cursestun", -0.95f, false);
                    RegisterCallbackTracked(_ =>
                    {
                        pe.Stats.Remove("walkspeed", "cursestun");
                    }, (int)stage.stunDurationMs);
                    break;

                case "teleport":
                    var bossPos = entity.Pos.XYZ;
                    pe.TeleportTo(new Vec3d(bossPos.X, bossPos.Y, bossPos.Z));
                    break;

                case "slow":
                    pe.Stats.Set("walkspeed", "curseslow", -stage.slowFactor, false);
                    RegisterCallbackTracked(_ =>
                    {
                        pe.Stats.Remove("walkspeed", "curseslow");
                    }, (int)stage.slowDurationMs);
                    break;
            }

            // Visual burst on trigger
            ParticleUtils.SpawnShadowExplosion(Sapi, pe.Pos.XYZ, 1.5f, 1);
            ParticleUtils.SpawnImpact(Sapi, pe, ParticleUtils.Colors.Shadow, 15, 0.4f);

            // Sound on trigger
            TryPlaySound("effect/reverbhit", 24f);
        }

        protected override void StopAbility()
        {
            playerCurses.Clear();
        }

        private class CurseData
        {
            public int stacks;
            public long lastStackMs;
        }
    }
}
