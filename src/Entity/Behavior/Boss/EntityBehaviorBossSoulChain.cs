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
    /// Soul Chain: boss tethers the nearest player with a chain.
    /// While chained: player is slowed. Break by: distance, damage dealt to boss, or timeout.
    /// Uses periodic tick to check chain state and spawn line particles.
    /// </summary>
    public class EntityBehaviorBossSoulChain : BossAbilityBase
    {
        private const string LastChainKey = "alegacyvsquest:bosssoulchain:lastMs";
        protected override string CooldownKey => LastChainKey;

        private class Stage : BossAbilityStage
        {
            public float chainDurationSec;
            public float slowFactor;
            public float breakDistance;
            public float breakDamage;
            public string sound;
            public float soundRange;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                chainDurationSec = json["chainDurationSec"].AsFloat(5f);
                slowFactor = json["slowFactor"].AsFloat(0.4f);
                breakDistance = json["breakDistance"].AsFloat(12f);
                breakDamage = json["breakDamage"].AsFloat(20f);
                sound = json["sound"].AsString("block/meteoriciron");
                soundRange = json["soundRange"].AsFloat(24f);
            }
        }

        private List<Stage> stages = new();

        private bool chainActive;
        private long chainStartMs;
        private string chainedPlayerUid;
        private float damageDealtDuringChain;
        private int activeStageIndex;

        public bool IsChainActive => chainActive;

        public EntityBehaviorBossSoulChain(Entity entity) : base(entity) { }
        public override string PropertyName() => "bosssoulchain";

        protected override bool UsePeriodicTick() => true;

        protected override void InitializeStages(JsonObject attributes) { stages = ParseStages<Stage>(attributes); }
        protected override int GetStageCount() => stages.Count;
        protected override object GetStage(int index) => stages[index];
        protected override float GetStageHealthThreshold(object stage) => ((Stage)stage).whenHealthRelBelow;
        protected override float GetStageCooldown(object stage) => ((Stage)stage).cooldownSeconds;
        protected override float GetMaxTargetRange(object stage) => ((Stage)stage).maxTargetRange;

        protected override void ActivateAbility(object stageObj, int stageIndex, EntityPlayer target)
        {
            if (stageObj is not Stage stage) return;
            if (target == null) return;

            MarkCooldownStart();

            chainActive = true;
            chainStartMs = Sapi.World.ElapsedMilliseconds;
            chainedPlayerUid = target.PlayerUID;
            damageDealtDuringChain = 0;
            activeStageIndex = stageIndex;

            // Apply slow
            target.Stats.Set("walkspeed", "soulchain", -stage.slowFactor, false);

            // Sound on attach
            TryPlaySound(stage.sound, stage.soundRange);
        }

        public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
        {
            base.OnEntityReceiveDamage(damageSource, ref damage);

            if (!chainActive || damage <= 0) return;

            // Track damage dealt during chain (to check break condition)
            var sourcePlayer = damageSource?.SourceEntity as EntityPlayer;
            if (sourcePlayer != null && string.Equals(sourcePlayer.PlayerUID, chainedPlayerUid, StringComparison.OrdinalIgnoreCase))
            {
                damageDealtDuringChain += damage;
            }
        }

        protected override void OnPeriodicTick(float dt)
        {
            if (Sapi == null || !chainActive) return;
            if (activeStageIndex < 0 || activeStageIndex >= stages.Count)
            {
                BreakChain();
                return;
            }

            var stage = stages[activeStageIndex];
            long nowMs = Sapi.World.ElapsedMilliseconds;

            // Check duration expired
            if (nowMs - chainStartMs >= stage.chainDurationSec * 1000)
            {
                BreakChain();
                return;
            }

            // Check break by damage
            if (damageDealtDuringChain >= stage.breakDamage)
            {
                BreakChain();
                return;
            }

            // Check break by distance and spawn line particles
            if (!string.IsNullOrWhiteSpace(chainedPlayerUid))
            {
                var player = Sapi.World.PlayerByUid(chainedPlayerUid) as IServerPlayer;
                if (player?.Entity != null && player.Entity.Alive)
                {
                    double dist = Math.Sqrt(player.Entity.Pos.SquareDistanceTo(entity.Pos.XYZ));
                    if (dist >= stage.breakDistance)
                    {
                        // Break chain and deal break damage
                        player.Entity.ReceiveDamage(new DamageSource
                        {
                            Source = EnumDamageSource.Entity,
                            SourceEntity = entity,
                            Type = EnumDamageType.Injury,
                            DamageTier = 3
                        }, stage.breakDamage);
                        ParticleUtils.SpawnImpact(Sapi, player.Entity, ParticleUtils.Colors.Chain, 12, 0.4f);
                        entity.World.PlaySoundAt(new AssetLocation("game:sounds/effect/reverbhit"), player.Entity.Pos.X, player.Entity.Pos.Y, player.Entity.Pos.Z, null, true, 56, 0.5f);
                        BreakChain();
                        return;
                    }

                    // Pull force: when player is beyond 70% of break distance, pull toward boss
                    double pullThreshold = stage.breakDistance * 0.7;
                    if (dist > pullThreshold)
                    {
                        Vec3d dir = entity.Pos.XYZ.SubCopy(player.Entity.Pos.XYZ);
                        dir.Normalize();
                        double pullStrength = 0.08 * ((dist - pullThreshold) / (stage.breakDistance - pullThreshold));
                        player.Entity.Pos.Motion.Add(dir.X * pullStrength, 0, dir.Z * pullStrength);

                        // Sound when chain pulls player (every 1 sec)
                        double chainElapsed = (nowMs - chainStartMs) / 1000.0;
                        if ((int)(chainElapsed * 1000) % 1000 < (int)(dt * 1000) + 50)
                        {
                            entity.World.PlaySoundAt(new AssetLocation("game:sounds/block/metalhit"), entity.Pos.X, entity.Pos.Y, entity.Pos.Z, null, true, 40, 0.2f);
                        }
                    }

                    // Spawn chain line particles between boss and player
                    var fromCenter = entity.Pos.XYZ.AddCopy(0, 1, 0);
                    var toCenter = player.Entity.Pos.XYZ.AddCopy(0, 1, 0);
                    ParticleUtils.SpawnLine(Sapi, fromCenter, toCenter, ParticleUtils.Colors.Chain, 10, 0.2f);
                }
                else
                {
                    BreakChain();
                }
            }
        }

        private void BreakChain()
        {
            chainActive = false;

            if (Sapi != null && !string.IsNullOrWhiteSpace(chainedPlayerUid))
            {
                var player = Sapi.World.PlayerByUid(chainedPlayerUid) as IServerPlayer;
                player?.Entity?.Stats.Remove("walkspeed", "soulchain");
            }

            chainedPlayerUid = null;
        }

        protected override void StopAbility()
        {
            BreakChain();
        }
    }
}
