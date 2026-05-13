using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class EntityBehaviorBossStillnessMark : BossAbilityBase
    {
        protected override string CooldownKey => "alegacyvsquest:bossstillnessmark:lastCastMs";
        protected override bool UseHealthBasedStages() => false;
        protected override bool RequiresTarget() => false;
        protected override int CheckIntervalMs => 500;

        private const string MarkUntilKey = "alegacyvsquest:bossstillnessmark:until";
        private const string MarkDamageKey = "alegacyvsquest:bossstillnessmark:damage";

        private const int DamageTickIntervalMs = 3000;

        private class Stage : BossAbilityStage
        {
            public int durationMs;
            public float damagePerSecond;
            public int maxTargets;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                durationMs = json["duration"].AsInt(8) * 1000;
                damagePerSecond = json["damagePerSecond"].AsFloat(15f);
                maxTargets = json["maxTargets"].AsInt(2);

                // Validation
                if (durationMs < 0) durationMs = 0;
                if (damagePerSecond < 0f) damagePerSecond = 0f;
                if (maxTargets < 1) maxTargets = 1;
            }
        }

        private List<Stage> stages = new List<Stage>();
        private DamageSource damageSource;

        private Dictionary<long, long> markedPlayers = new Dictionary<long, long>();
        private Dictionary<long, long> lastDamageTickMsByPlayer = new Dictionary<long, long>();
        private Dictionary<long, EntityPlayer> cachedPlayers = new Dictionary<long, EntityPlayer>();

        public EntityBehaviorBossStillnessMark(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossstillnessmark";

        protected override void InitializeStages(JsonObject attributes)
        {
            stages = ParseStages<Stage>(attributes);
            damageSource = new DamageSource
            {
                Source = EnumDamageSource.Entity,
                SourceEntity = entity,
                Type = EnumDamageType.PiercingAttack
            };
        }

        protected override int GetStageCount() => stages.Count;

        protected override object GetStage(int index) => stages[index];

        protected override float GetStageHealthThreshold(object stage) => ((Stage)stage).whenHealthRelBelow;

        protected override float GetStageCooldown(object stage) => ((Stage)stage).cooldownSeconds;

        protected override float GetMaxTargetRange(object stage) => ((Stage)stage).maxTargetRange;

        protected override void ActivateAbility(object stageObj, int stageIndex, EntityPlayer target)
        {
            if (stageObj is not Stage stage) return;
            
            MarkCooldownStart();
            SetAbilityActive(true);
            TryApplyMark(stage, Sapi.World.ElapsedMilliseconds);
        }

        protected override bool ShouldCheckAbility() => markedPlayers.Count == 0;

        protected override void StopAbility()
        {
            ClearAllMarks();
        }

        protected override bool OnAbilityTick(float dt)
        {
            long now = Sapi.World.ElapsedMilliseconds;
            ProcessMarkedPlayers(dt, now);
            
            bool anyMarked = markedPlayers.Count > 0;
            if (!anyMarked)
            {
                SetAbilityActive(false);
            }
            return anyMarked;
        }

        private void ClearAllMarks()
        {
            markedPlayers.Clear();
            lastDamageTickMsByPlayer.Clear();
            cachedPlayers.Clear();
            
            // Clear marks from all players
            var players = Sapi.World.AllOnlinePlayers;
            for (int i = 0; i < players.Length; i++)
            {
                var plrEntity = players[i].Entity;
                if (plrEntity?.WatchedAttributes != null)
                {
                    plrEntity.WatchedAttributes.RemoveAttribute(MarkUntilKey);
                    plrEntity.WatchedAttributes.RemoveAttribute(MarkDamageKey);
                    plrEntity.WatchedAttributes.MarkPathDirty(MarkUntilKey);
                }
            }
        }

        private void TryApplyMark(Stage stage, long now)
        {
            if (stage == null) return;
            List<EntityPlayer> targets = new List<EntityPlayer>();

            var players = Sapi.World.AllOnlinePlayers;
            for (int i = 0; i < players.Length; i++)
            {
                var plrEntity = players[i].Entity;
                if (plrEntity?.Pos == null) continue;
                if (plrEntity.Pos.Dimension != entity.Pos.Dimension) continue;
                if (plrEntity.Pos.DistanceTo(entity.Pos) > stage.maxTargetRange) continue;

                // Check if already marked
                if (markedPlayers.ContainsKey(plrEntity.EntityId)) continue;

                targets.Add(plrEntity);
            }

            if (targets.Count == 0) return;

            // Select random targets up to maxTargets
            Random rand = new Random((int)now);
            int toMark = Math.Min(stage.maxTargets, targets.Count);

            for (int i = 0; i < toMark; i++)
            {
                int idx = rand.Next(targets.Count);
                var targetPlr = targets[idx];
                targets.RemoveAt(idx);

                long until = now + stage.durationMs;
                markedPlayers[targetPlr.EntityId] = until;

                // Visual effect - stillness mark binding
                ParticleUtils.SpawnAuraRing(Sapi, targetPlr.Pos.XYZ.Add(0, 0.1, 0), 0.8f, ColorUtil.ToRgba(255, 200, 50, 50), 10, 0.4f);
            }
        }

        private void ProcessMarkedPlayers(float dt, long now)
        {
            if (stages.Count == 0) return;
            var stage = stages[0];

            List<long> toRemove = new List<long>();

            foreach (var kvp in markedPlayers)
            {
                if (now > kvp.Value)
                {
                    toRemove.Add(kvp.Key);
                    continue;
                }

                // Find player by EntityId - use cached lookup
                EntityPlayer player = null;
                if (!cachedPlayers.TryGetValue(kvp.Key, out player) || player == null || !player.Alive)
                {
                    // Cache miss or invalid - find player
                    foreach (var onlinePlayer in Sapi.World.AllOnlinePlayers)
                    {
                        if (onlinePlayer.Entity?.EntityId == kvp.Key)
                        {
                            player = onlinePlayer.Entity;
                            cachedPlayers[kvp.Key] = player;
                            break;
                        }
                    }
                }
                
                if (player == null || !player.Alive || player.Pos?.Dimension != entity.Pos.Dimension)
                {
                    toRemove.Add(kvp.Key);
                    cachedPlayers.Remove(kvp.Key);
                    continue;
                }

                // Check if moving - apply damage if they try to move
                float walkSpeed = player.Stats.GetBlended("walkspeed");
                if (walkSpeed > 0.05f)
                {
                    // Apply damage at fixed intervals (avoid per-tick spam)
                    if (!lastDamageTickMsByPlayer.TryGetValue(kvp.Key, out long lastDmgMs))
                    {
                        lastDmgMs = 0;
                    }

                    if (lastDmgMs == 0 || now - lastDmgMs >= DamageTickIntervalMs)
                    {
                        float damage = stage.damagePerSecond * (DamageTickIntervalMs / 1000f);
                        player.ReceiveDamage(damageSource, damage);
                        lastDamageTickMsByPlayer[kvp.Key] = now;
                    }

                    // Visual feedback - blood particles when moving while marked
                    if (Sapi.World.ElapsedMilliseconds % DamageTickIntervalMs < 50)
                    {
                        ParticleUtils.SpawnImpact(Sapi, player, ParticleUtils.Colors.Blood, 6, 0.3f);
                    }
                }
            }

            foreach (var id in toRemove)
            {
                markedPlayers.Remove(id);
                lastDamageTickMsByPlayer.Remove(id);
                cachedPlayers.Remove(id);
            }
        }
    }
}
