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
    /// Curse Mark: boss marks a player. After a delay, the mark detonates dealing damage.
    /// Damage is based on distance — closer to boss = more damage.
    /// Uses RegisterCallbackTracked for delayed detonation.
    /// </summary>
    public class EntityBehaviorBossCurseMark : BossAbilityBase
    {
        private const string LastMarkKey = "alegacyvsquest:bosscursemark:lastMs";
        protected override string CooldownKey => LastMarkKey;

        private class Stage : BossAbilityStage
        {
            public float markDelaySec;
            public float baseDamage;
            public float safeDistance;
            public string sound;
            public float soundRange;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                markDelaySec = json["markDelaySec"].AsFloat(3f);
                baseDamage = json["baseDamage"].AsFloat(12f);
                safeDistance = json["safeDistance"].AsFloat(10f);
                sound = json["sound"].AsString("environment/thunder1");
                soundRange = json["soundRange"].AsFloat(32f);
            }
        }

        private List<Stage> stages = new();
        private string markedPlayerUid;

        public EntityBehaviorBossCurseMark(Entity entity) : base(entity) { }
        public override string PropertyName() => "bosscursemark";

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
            markedPlayerUid = target.PlayerUID;

            // Pillar particles on marked player as warning
            ParticleUtils.SpawnPillar(Sapi, target.Pos.XYZ, 3f, 0.8f, ParticleUtils.Colors.Shadow, 20);

            // Warning sound on mark
            TryPlaySound(stage.sound, stage.soundRange);

            // Send warning message
            var sp = Sapi.World.PlayerByUid(target.PlayerUID) as IServerPlayer;
            if (sp != null)
            {
                Sapi.SendMessage(sp, Vintagestory.API.Config.GlobalConstants.GeneralChatGroup,
                    LocalizationUtils.GetSafe("albase:trial-cursemark-warning"), EnumChatType.Notification);
            }

            // Delayed detonation after markDelaySec
            RegisterCallbackTracked(_ =>
            {
                if (entity == null || !entity.Alive) return;
                Detonate(stage);
            }, (int)(stage.markDelaySec * 1000));
        }

        private void Detonate(Stage stage)
        {
            if (Sapi == null || string.IsNullOrWhiteSpace(markedPlayerUid)) return;

            var player = Sapi.World.PlayerByUid(markedPlayerUid) as IServerPlayer;
            markedPlayerUid = null;

            if (player?.Entity == null || !player.Entity.Alive)
            {
                SetAbilityActive(false);
                return;
            }

            // Calculate damage based on distance to boss
            double dist = Math.Sqrt(player.Entity.Pos.SquareDistanceTo(entity.Pos.XYZ));
            float distRatio = (float)Math.Min(1.0, dist / stage.safeDistance);

            // Closer = more damage, further = less (linear interpolation)
            float minDamageMultiplier = 0.2f;
            float damageMultiplier = 1f - distRatio * (1f - minDamageMultiplier);
            float finalDamage = ApplyDamageMultiplier(stage.baseDamage * damageMultiplier);

            if (finalDamage > 0.5f)
            {
                player.Entity.ReceiveDamage(new DamageSource
                {
                    Source = EnumDamageSource.Entity,
                    SourceEntity = entity,
                    Type = EnumDamageType.Injury
                }, finalDamage);
            }

            // Shadow explosion on detonation
            ParticleUtils.SpawnShadowExplosion(Sapi, player.Entity.Pos.XYZ, 2.5f, 2);
            ParticleUtils.SpawnShockwave(Sapi, player.Entity.Pos.XYZ, 3f, ParticleUtils.Colors.Shadow, 20, 0.4f);

            // Detonation sound
            TryPlaySound("effect/reverbhit", 32f);

            SetAbilityActive(false);
        }

        protected override void StopAbility()
        {
            markedPlayerUid = null;
        }
    }
}
