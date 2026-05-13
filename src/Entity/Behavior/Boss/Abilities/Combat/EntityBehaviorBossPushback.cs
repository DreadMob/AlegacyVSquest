using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class EntityBehaviorBossPushback : BossAbilityBase
    {
        private const string LastPushKey = "alegacyvsquest:bosspushback:lastPushMs";
        private const string PlayerCooldownKey = "alegacyvsquest:bosspushback:playerCooldown:";

        protected override string CooldownKey => LastPushKey;

        private class Stage : BossAbilityStage
        {
            public float force;
            public float playerCooldownSeconds;
            public string sound;
            public float soundRange;
            public string animation;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                force = json["force"].AsFloat(0.5f);
                playerCooldownSeconds = json["playerCooldownSeconds"].AsFloat(1.5f);
                sound = json["sound"].AsString(null);
                soundRange = json["soundRange"].AsFloat(16f);
                animation = json["animation"].AsString(null);
            }
        }

        private List<Stage> stages = new List<Stage>();

        public EntityBehaviorBossPushback(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bosspushback";

        protected override void InitializeStages(JsonObject attributes)
        {
            stages = ParseStages<Stage>(attributes);
        }

        protected override int GetStageCount() => stages.Count;

        protected override object GetStage(int index) => stages[index];

        protected override float GetStageHealthThreshold(object stage) => ((Stage)stage).whenHealthRelBelow;

        protected override float GetStageCooldown(object stage) => ((Stage)stage).cooldownSeconds;

        protected override float GetMaxTargetRange(object stage) => ((Stage)stage).maxTargetRange;

        protected override bool ShouldCheckAbility() => true;

        protected override void ActivateAbility(object stageObj, int stageIndex, EntityPlayer target)
        {
            if (stageObj is not Stage stage) return;
            PushPlayers(stage);
        }

        protected override void StopAbility()
        {
        }

        private void PushPlayers(Stage stage)
        {
            if (Sapi == null || !entity.Alive || stage == null) return;

            long now = Sapi.World.ElapsedMilliseconds;
            MarkCooldownStart();

            // Find nearby players
            List<EntityPlayer> nearbyPlayers = new List<EntityPlayer>();
            foreach (var player in Sapi.World.AllOnlinePlayers)
            {
                if (player.Entity?.Pos == null) continue;
                if (player.Entity.Pos.Dimension != entity.Pos.Dimension) continue;

                double dist = player.Entity.Pos.DistanceTo(entity.Pos);
                if (dist <= stage.maxTargetRange)
                {
                    // Check player-specific cooldown
                    string playerKey = PlayerCooldownKey + player.Entity.EntityId;
                    long playerLastPush = entity.WatchedAttributes.GetLong(playerKey, 0);
                    if (now - playerLastPush >= stage.playerCooldownSeconds * 1000)
                    {
                        nearbyPlayers.Add(player.Entity);
                    }
                }
            }

            if (nearbyPlayers.Count == 0) return;

            TryPlaySound(stage.sound, stage.soundRange, 0, 1.0f);
            TryPlayAnimation(stage.animation);

            // Shockwave particles from boss center
            ParticleUtils.SpawnShockwave(Sapi, entity.Pos.XYZ, stage.maxTargetRange, ParticleUtils.Colors.Lightning, 20, 0.4f);

            foreach (var player in nearbyPlayers)
            {
                // Set player cooldown
                string playerKey = PlayerCooldownKey + player.EntityId;
                entity.WatchedAttributes.SetLong(playerKey, now);

                // Calculate knockback direction
                Vec3d dir = player.Pos.XYZ - entity.Pos.XYZ;
                dir.Normalize();
                dir.Y = 0.3; // Slightly up
                dir.Mul(stage.force);

                // Apply knockback
                player.Pos.Motion.Add(dir);

                // Visual impact on pushed player
                ParticleUtils.SpawnImpact(Sapi, player, ParticleUtils.Colors.Lightning, 8, 0.25f);
            }
        }
    }
}
