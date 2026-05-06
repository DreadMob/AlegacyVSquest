using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsQuest
{
    public class EntityBehaviorBossWoundedMark : BossAbilityBase
    {
        protected override string CooldownKey => "alegacyvsquest:bosswoundedmark:lastCastMs";
        protected override bool UseHealthBasedStages() => false;
        protected override bool RequiresTarget() => false;
        protected override int CheckIntervalMs => 750;

        private class Stage : BossAbilityStage
        {
            public float triggerHpPercent;
            public float explosionDamage;
            public float explosionRange;
            public int maxTargets;
            public float detectionRange;
            public int markDurationMs;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                triggerHpPercent = json["triggerHp"].AsFloat(0.45f);
                explosionDamage = json["explosionDamage"].AsFloat(210f);
                explosionRange = json["range"].AsFloat(10f);
                maxTargets = json["maxTargets"].AsInt(2);
                detectionRange = json["detectionRange"].AsFloat(40f);
                markDurationMs = json["markDurationSeconds"].AsInt(15) * 1000;
                whenHealthRelBelow = json["whenHealthRelBelow"].AsFloat(0.5f);
                cooldownSeconds = json["cooldownSeconds"].AsFloat(18f);
            }
        }

        private List<Stage> stages = new List<Stage>();
        private Dictionary<long, long> woundedPlayers = new Dictionary<long, long>();

        public EntityBehaviorBossWoundedMark(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bosswoundedmark";

        protected override void InitializeStages(JsonObject attributes)
        {
            stages = ParseStages<Stage>(attributes);
        }

        protected override int GetStageCount() => stages.Count;

        protected override object GetStage(int index) => stages[index];

        protected override float GetStageHealthThreshold(object stage) => ((Stage)stage).whenHealthRelBelow;

        protected override float GetStageCooldown(object stage) => ((Stage)stage).cooldownSeconds;

        protected override float GetMaxTargetRange(object stage) => ((Stage)stage).detectionRange;

        protected override bool ShouldCheckAbility() => !IsAbilityActive && woundedPlayers.Count == 0;

        protected override void ActivateAbility(object stageObj, int stageIndex, EntityPlayer target)
        {
            if (stageObj is not Stage stage) return;
            MarkCooldownStart();
            SetAbilityActive(true);
            TryApplyMark(stage, Sapi.World.ElapsedMilliseconds);
        }

        protected override void StopAbility()
        {
            woundedPlayers.Clear();
            SetAbilityActive(false);
        }

        protected override bool OnAbilityTick(float dt)
        {
            if (!IsAbilityActive) return false;
            if (stages.Count == 0) return false;
            var stage = stages[0];

            long now = Sapi.World.ElapsedMilliseconds;
            CheckForHealingExplosions(stage, now);
            SpawnParticlesOnMarkedPlayers(now);
            
            bool anyMarked = woundedPlayers.Count > 0;
            if (!anyMarked)
            {
                SetAbilityActive(false);
            }
            return anyMarked;
        }

        private void TryApplyMark(Stage stage, long now)
        {
            List<EntityPlayer> targets = new List<EntityPlayer>();

            foreach (var player in Sapi.World.AllOnlinePlayers)
            {
                if (player.Entity?.Pos == null) continue;
                if (player.Entity.Pos.Dimension != entity.Pos.Dimension) continue;
                if (player.Entity.Pos.DistanceTo(entity.Pos) > stage.detectionRange) continue;

                // Check HP threshold
                var health = player.Entity.GetBehavior<EntityBehaviorHealth>();
                if (health == null) continue;

                float hpPercent = health.Health / health.MaxHealth;
                if (hpPercent > stage.triggerHpPercent) continue;

                // Check if already marked
                if (woundedPlayers.ContainsKey(player.Entity.EntityId)) continue;

                targets.Add(player.Entity);
            }

            if (targets.Count == 0) return;

            // Select random targets up to maxTargets
            Random rand = new Random((int)now);
            int toMark = Math.Min(stage.maxTargets, targets.Count);

            for (int i = 0; i < toMark; i++)
            {
                int idx = rand.Next(targets.Count);
                var target = targets[idx];
                targets.RemoveAt(idx);

                woundedPlayers[target.EntityId] = now + stage.markDurationMs; // Store expiry time

                // Visual effect - pulsing red aura
                var targetPos = target.Pos.XYZ;
                Sapi.World.SpawnParticles(
                    new SimpleParticleProperties(
                        30, 40,
                        ColorUtil.ToRgba(200, 255, 50, 50),
                        targetPos.Add(-0.3, 0, -0.3),
                        targetPos.Add(0.3, 2, 0.3),
                        new Vec3f(-0.2f, -0.2f, -0.2f),
                        new Vec3f(0.2f, 0.2f, 0.2f),
                        1.5f,
                        -0.2f,
                        0.5f,
                        0.5f,
                        EnumParticleModel.Quad
                    )
                );

                entity.Api.Logger.Debug($"[BossWoundedMark] Applied mark to player {target.EntityId}");
            }
        }

        private void CheckForHealingExplosions(Stage stage, long now)
        {
            List<long> toRemove = new List<long>();

            foreach (var kvp in woundedPlayers)
            {
                // Check if mark expired
                if (now >= kvp.Value)
                {
                    toRemove.Add(kvp.Key);
                    continue;
                }

                // Correctly find player entity from EntityId
                EntityPlayer player = null;
                foreach (var onlinePlayer in Sapi.World.AllOnlinePlayers)
                {
                    if (onlinePlayer.Entity?.EntityId == kvp.Key)
                    {
                        player = onlinePlayer.Entity;
                        break;
                    }
                }

                if (player == null || !player.Alive)
                {
                    toRemove.Add(kvp.Key);
                    continue;
                }

                // Check if player healed (health increased)
                // Note: Improved detection logic using lastHealMs from vsquest if available
                float healRate = player.Stats.GetBlended("healrate");
                if (healRate > 0 && player.WatchedAttributes.GetLong("lastHealMs", 0) > now - 1000)
                {
                    // Explosion!
                    ExplodeOnPlayer(stage, player);
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var id in toRemove)
            {
                woundedPlayers.Remove(id);
            }
        }

        private void SpawnParticlesOnMarkedPlayers(long now)
        {
            foreach (var kvp in woundedPlayers)
            {
                if (now >= kvp.Value) continue; // Skip expired

                EntityPlayer player = null;
                foreach (var onlinePlayer in Sapi.World.AllOnlinePlayers)
                {
                    if (onlinePlayer.Entity?.EntityId == kvp.Key)
                    {
                        player = onlinePlayer.Entity;
                        break;
                    }
                }
                
                if (player?.Pos == null) continue;

                var targetPos = player.Pos.XYZ;
                Sapi.World.SpawnParticles(
                    new SimpleParticleProperties(
                        8, 12,
                        ColorUtil.ToRgba(200, 255, 50, 80),
                        targetPos.Add(-0.2, 0.5, -0.2),
                        targetPos.Add(0.2, 1.5, 0.2),
                        new Vec3f(-0.1f, 0.1f, -0.1f),
                        new Vec3f(0.1f, 0.2f, 0.1f),
                        0.8f,
                        0f,
                        0.3f,
                        0.3f,
                        EnumParticleModel.Quad
                    )
                );
            }
        }

        private void ExplodeOnPlayer(Stage stage, EntityPlayer player)
        {
            var pos = player.Pos.XYZ;

            // Damage in AoE
            var nearbyEntities = Sapi.World.GetEntitiesAround(pos, stage.explosionRange, stage.explosionRange, (e) => e is EntityPlayer);
            foreach (var e in nearbyEntities)
            {
                if (e is EntityPlayer p)
                {
                    p.ReceiveDamage(
                        new DamageSource
                        {
                            Source = EnumDamageSource.Entity,
                            SourceEntity = entity,
                            Type = EnumDamageType.PiercingAttack
                        },
                        stage.explosionDamage
                    );
                }
            }

            // Visual explosion effect
            Sapi.World.SpawnParticles(
                new SimpleParticleProperties(
                    50, 80,
                    ColorUtil.ToRgba(255, 255, 100, 50),
                    pos.Add(-stage.explosionRange, 0, -stage.explosionRange),
                    pos.Add(stage.explosionRange, stage.explosionRange, stage.explosionRange),
                    new Vec3f(-0.5f, 0.3f, -0.5f),
                    new Vec3f(0.5f, 0.8f, 0.5f),
                    1.0f,
                    0.5f,
                    0.5f,
                    0.5f,
                    EnumParticleModel.Quad
                )
            );

            // Sound effect
            Sapi.World.PlaySoundAt(
                new AssetLocation("effect/smallexplosion"),
                pos.X, pos.Y, pos.Z,
                null, false, 32, 0.5f
            );
        }
    }
}
