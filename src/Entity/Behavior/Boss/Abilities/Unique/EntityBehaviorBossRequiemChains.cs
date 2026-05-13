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
    public class EntityBehaviorBossRequiemChains : BossAbilityBase
    {
        protected override string CooldownKey => "alegacyvsquest:bossrequiemchains:lastCastMs";
        protected override bool UseHealthBasedStages() => false;
        protected override bool RequiresTarget() => false;
        protected override int CheckIntervalMs => 500;

        private class Stage : BossAbilityStage
        {
            public int cooldownMs;
            public float range;
            public int maxTargets;
            public int durationMs;
            public float pullSpeed;
            public float damagePerSecond;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                whenHealthRelBelow = json["whenHealthRelBelow"].AsFloat(1f);
                cooldownMs = json["cooldownSeconds"].AsInt(10) * 1000;
                range = json["range"].AsFloat(8f);
                maxTargets = json["maxTargets"].AsInt(2);
                durationMs = json["duration"].AsInt(5) * 1000;
                pullSpeed = json["pullSpeed"].AsFloat(0.08f);
                damagePerSecond = json["damagePerSecond"].AsFloat(5f);
            }
        }

        private List<Stage> stages = new List<Stage>();

        private int currentStageIndex;
        private Dictionary<long, long> chainedPlayers = new Dictionary<long, long>();

        public EntityBehaviorBossRequiemChains(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossrequiemchains";

        protected override void InitializeStages(JsonObject attributes)
        {
            stages = ParseStages<Stage>(attributes);
        }

        protected override bool ShouldCheckAbility()
        {
            return !IsAbilityActive;
        }

        protected override void ActivateAbility(object stageObj, int stageIndex, EntityPlayer target)
        {
            if (stageObj is not Stage stage) return;
            MarkCooldownStart();
            SetAbilityActive(true);
            currentStageIndex = stageIndex;
            StartChains(stage, stageIndex, target);
        }

        private void StartChains(Stage stage, int index, EntityPlayer target)
        {
            List<EntityPlayer> targets = new List<EntityPlayer>();

            foreach (var player in Sapi.World.AllOnlinePlayers)
            {
                if (player.Entity?.Pos == null) continue;
                if (player.Entity.Pos.Dimension != entity.Pos.Dimension) continue;
                if (player.Entity.Pos.DistanceTo(entity.Pos) > stage.range) continue;
                if (chainedPlayers.ContainsKey(player.Entity.EntityId)) continue;

                targets.Add(player.Entity);
            }

            if (targets.Count == 0) return;

            // Select random targets up to maxTargets
            Random rand = new Random((int)Sapi.World.ElapsedMilliseconds);
            int toChain = Math.Min(stage.maxTargets, targets.Count);

            for (int i = 0; i < toChain; i++)
            {
                int idx = rand.Next(targets.Count);
                var targetPlayer = targets[idx];
                targets.RemoveAt(idx);

                long until = Sapi.World.ElapsedMilliseconds + stage.durationMs;
                chainedPlayers[targetPlayer.EntityId] = until;

                // Visual - chain binding effect on player
                ParticleUtils.SpawnEntityAura(Sapi, targetPlayer, ParticleUtils.Colors.Chain, 25, 0.5f, 0.6f);
            }
        }

        protected override bool OnAbilityTick(float dt)
        {
            if (!IsAbilityActive) return false;

            long now = Sapi.World.ElapsedMilliseconds;
            ProcessChainedPlayers(dt, now);
            return true;
        }

        private void TryApplyChains(Stage stage, long now)
        {
            StartChains(stage, currentStageIndex, null);
        }

        private void ProcessChainedPlayers(float dt, long now)
        {
            List<long> toRemove = new List<long>();

            foreach (var kvp in chainedPlayers)
            {
                if (now > kvp.Value)
                {
                    toRemove.Add(kvp.Key);
                    continue;
                }

                var player = Sapi.World.PlayerByUid(kvp.Key.ToString())?.Entity;
                if (player == null)
                {
                    toRemove.Add(kvp.Key);
                    continue;
                }

                var stage = stages[currentStageIndex];

                // Pull towards boss
                Vec3d dir = entity.Pos.XYZ - player.Pos.XYZ;
                dir.Normalize();
                dir.Mul(stage.pullSpeed * dt);
                player.Pos.Motion.Add(dir);

                // Disable abilities
                player.WatchedAttributes.SetBool("alegacyvsquest:canshift", false);
                player.WatchedAttributes.SetBool("alegacyvsquest:canjump", false);
                player.WatchedAttributes.SetBool("alegacyvsquest:canuseitems", false);

                // Apply damage
                float damage = stage.damagePerSecond * dt;
                player.ReceiveDamage(
                    new DamageSource
                    {
                        Source = EnumDamageSource.Entity,
                        SourceEntity = entity,
                        Type = EnumDamageType.PiercingAttack
                    },
                    damage
                );

                // Visual chain line connecting boss to player
                if (Sapi.World.ElapsedMilliseconds % 200 < 50)
                {
                    Vec3d chainStart = entity.Pos.XYZ.Add(0, 1.5, 0);
                    Vec3d chainEnd = player.Pos.XYZ.Add(0, 1, 0);
                    ParticleUtils.SpawnLine(Sapi, chainStart, chainEnd, ParticleUtils.Colors.Chain, 6, 0.25f);
                }
            }

            // Cleanup expired chains
            foreach (var id in toRemove)
            {
                chainedPlayers.Remove(id);
                var player = Sapi.World.PlayerByUid(id.ToString())?.Entity;
                if (player != null)
                {
                    player.WatchedAttributes.SetBool("alegacyvsquest:canshift", true);
                    player.WatchedAttributes.SetBool("alegacyvsquest:canjump", true);
                    player.WatchedAttributes.SetBool("alegacyvsquest:canuseitems", true);
                }
            }
        }

        // Required abstract overrides for BossAbilityBase (event-driven mode)
        protected override int GetStageCount() => stages.Count;
        protected override object GetStage(int index) => stages[index];
        protected override float GetStageHealthThreshold(object stage) => ((Stage)stage).whenHealthRelBelow;
        protected override float GetStageCooldown(object stage) => ((Stage)stage).cooldownMs / 1000f;
        protected override float GetMaxTargetRange(object stage) => ((Stage)stage).range;

        protected override void StopAbility()
        {
        }
    }
}
