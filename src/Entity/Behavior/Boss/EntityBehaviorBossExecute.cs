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
    /// Boss Execute: when a player is below HP threshold, boss charges up (with sound + particles)
    /// then dashes to the player and deals massive damage.
    /// If player heals above threshold during charge — cancelled.
    /// </summary>
    public class EntityBehaviorBossExecute : BossAbilityBase
    {
        private const string LastExecuteKey = "alegacyvsquest:bossexecute:lastMs";
        protected override string CooldownKey => LastExecuteKey;

        private class Stage : BossAbilityStage
        {
            public float executeThreshold; // HP % below which execute triggers (0.3 = 30%)
            public float executeDamage;
            public int windupMs;
            public int damageTier;
            public string chargeSound;
            public float chargeSoundRange;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                executeThreshold = json["executeThreshold"].AsFloat(0.3f);
                executeDamage = json["executeDamage"].AsFloat(40f);
                windupMs = json["windupMs"].AsInt(3000);
                damageTier = json["damageTier"].AsInt(4);
                chargeSound = json["chargeSound"].AsString("effect/translocate-active");
                chargeSoundRange = json["chargeSoundRange"].AsFloat(32f);
            }
        }

        private List<Stage> stages = new();
        private bool charging;
        private long chargeStartMs;
        private string targetPlayerUid;
        private int activeStageIndex;
        private long lastChargeSoundMs;

        public EntityBehaviorBossExecute(Entity entity) : base(entity) { }
        public override string PropertyName() => "bossexecute";

        protected override bool UsePeriodicTick() => true;
        protected override bool RequiresTarget() => false;
        protected override bool ShouldCheckAbility() => false;

        protected override void InitializeStages(JsonObject attributes) { stages = ParseStages<Stage>(attributes); }
        protected override int GetStageCount() => stages.Count;
        protected override object GetStage(int index) => stages[index];
        protected override float GetStageHealthThreshold(object stage) => ((Stage)stage).whenHealthRelBelow;
        protected override float GetStageCooldown(object stage) => ((Stage)stage).cooldownSeconds;
        protected override float GetMaxTargetRange(object stage) => ((Stage)stage).maxTargetRange;

        protected override void ActivateAbility(object stageObj, int stageIndex, EntityPlayer target) { }

        protected override void OnPeriodicTick(float dt)
        {
            if (Sapi == null || !entity.Alive) return;
            if (IsBossClone) return;
            if (stages.Count == 0) return;

            long nowMs = Sapi.World.ElapsedMilliseconds;

            // If charging — check if target healed or windup complete
            if (charging)
            {
                ProcessCharge(nowMs);
                return;
            }

            // Scan for low-HP targets
            if (!entity.TryGetHealthFraction(out float frac)) return;
            var (stageObj, stageIndex) = FindStageForHealth(frac);
            if (stageObj is not Stage stage) return;
            if (!IsCooldownReady(stageObj)) return;
            if (IsAbilityActive) return;

            // Find low-HP player
            foreach (var p in Sapi.World.AllOnlinePlayers)
            {
                if (p is not IServerPlayer sp) continue;
                var pe = sp.Entity;
                if (pe == null || !pe.Alive) continue;
                if (pe.Pos.Dimension != entity.Pos.Dimension) continue;

                double dist = Math.Sqrt(pe.Pos.SquareDistanceTo(entity.Pos.XYZ));
                if (dist > stage.maxTargetRange) continue;

                // Check HP threshold
                var health = pe.GetBehavior<Vintagestory.GameContent.EntityBehaviorHealth>();
                if (health == null || health.MaxHealth <= 0) continue;

                float hpFrac = health.Health / health.MaxHealth;
                if (hpFrac < stage.executeThreshold)
                {
                    StartCharge(stage, stageIndex, sp.PlayerUID, nowMs);
                    break;
                }
            }
        }

        private void StartCharge(Stage stage, int stageIndex, string playerUid, long nowMs)
        {
            charging = true;
            chargeStartMs = nowMs;
            targetPlayerUid = playerUid;
            activeStageIndex = stageIndex;
            lastChargeSoundMs = 0;

            MarkCooldownStart();

            // Initial charge sound
            TryPlaySound(stage.chargeSound, stage.chargeSoundRange);

            // Red warning particles connecting boss to target
            var target = Sapi.World.PlayerByUid(playerUid) as IServerPlayer;
            if (target?.Entity != null)
            {
                ParticleUtils.SpawnLine(Sapi, entity.Pos.XYZ.AddCopy(0, 1.5, 0),
                    target.Entity.Pos.XYZ.AddCopy(0, 1, 0), ParticleUtils.Colors.Blood, 12, 0.4f);
            }
        }

        private void ProcessCharge(long nowMs)
        {
            if (activeStageIndex < 0 || activeStageIndex >= stages.Count)
            {
                CancelCharge();
                return;
            }

            var stage = stages[activeStageIndex];
            long elapsed = nowMs - chargeStartMs;

            // Check if target healed above threshold
            var target = Sapi.World.PlayerByUid(targetPlayerUid) as IServerPlayer;
            if (target?.Entity == null || !target.Entity.Alive)
            {
                CancelCharge();
                return;
            }

            var health = target.Entity.GetBehavior<Vintagestory.GameContent.EntityBehaviorHealth>();
            if (health != null && health.MaxHealth > 0)
            {
                float hpFrac = health.Health / health.MaxHealth;
                if (hpFrac >= stage.executeThreshold)
                {
                    // Player healed — cancel
                    CancelCharge();
                    TryPlaySound("effect/stonecrush", 24f);
                    return;
                }
            }

            // Charge sound loop (every 800ms)
            if (nowMs - lastChargeSoundMs >= 800)
            {
                lastChargeSoundMs = nowMs;
                TryPlaySound(stage.chargeSound, stage.chargeSoundRange, 0, 0.5f + (elapsed / (float)stage.windupMs) * 0.5f);

                // Intensifying red particles
                float progress = Math.Min(1f, elapsed / (float)stage.windupMs);
                int count = 4 + (int)(progress * 12);
                ParticleUtils.SpawnEntityAura(Sapi, entity, ParticleUtils.Colors.Blood, count, 0.3f + progress * 0.5f, 0.5f + progress * 0.5f);

                // Line to target
                if (target?.Entity != null)
                {
                    ParticleUtils.SpawnLine(Sapi, entity.Pos.XYZ.AddCopy(0, 1.5, 0),
                        target.Entity.Pos.XYZ.AddCopy(0, 1, 0), ParticleUtils.Colors.Blood, 8, 0.3f + progress * 0.3f);
                }
            }

            // Windup complete — execute!
            if (elapsed >= stage.windupMs)
            {
                ExecuteStrike(stage, target);
            }
        }

        private void ExecuteStrike(Stage stage, IServerPlayer target)
        {
            charging = false;

            if (target?.Entity == null || !target.Entity.Alive) return;

            // Teleport boss to target
            Vec3d targetPos = target.Entity.Pos.XYZ.Clone();
            entity.TeleportTo(targetPos);

            // Deal massive damage
            float dmg = ApplyDamageMultiplier(stage.executeDamage);
            target.Entity.ReceiveDamage(new DamageSource
            {
                Source = EnumDamageSource.Entity,
                SourceEntity = entity,
                Type = EnumDamageType.Injury,
                DamageTier = stage.damageTier
            }, dmg);

            // Impact effects
            ParticleUtils.SpawnShockwave(Sapi, targetPos, 3f, ParticleUtils.Colors.Blood, 24, 0.7f);
            ParticleUtils.SpawnImpact(Sapi, target.Entity, ParticleUtils.Colors.Blood, 20, 0.6f);
            TryPlaySound("effect/reverbhit", 32f);
            TryPlaySound("environment/thunder1", 48f);
        }

        private void CancelCharge()
        {
            charging = false;
            targetPlayerUid = null;
        }

        protected override void StopAbility()
        {
            CancelCharge();
        }
    }
}
