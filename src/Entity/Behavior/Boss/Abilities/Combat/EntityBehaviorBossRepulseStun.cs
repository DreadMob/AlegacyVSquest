using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class EntityBehaviorBossRepulseStun : BossAbilityBase
    {
        protected override string CooldownKey => "alegacyvsquest:bossrepulsestun:lastStartMs";

        private const string StunUntilKey = "alegacyvsquest:bossrepulsestun:until";
        private const string StunMultKey = "alegacyvsquest:bossrepulsestun:mult";
        private const string StunStatKey = "alegacyvsquest:bossrepulsestun:stat";

        private class Stage : BossAbilityStage
        {
            public float knockbackStrength;
            public float maxPlayerMotion;
            public int stunMs;
            public float victimWalkSpeedMult;
            public string sound;
            public float soundRange;
            public int soundStartMs;
            public float soundVolume;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                knockbackStrength = json["knockbackStrength"].AsFloat(0.30f);
                maxPlayerMotion = json["maxPlayerMotion"].AsFloat(0.55f);
                stunMs = json["stunMs"].AsInt(900);
                victimWalkSpeedMult = json["victimWalkSpeedMult"].AsFloat(0.0f);
                sound = json["sound"].AsString(null);
                soundRange = json["soundRange"].AsFloat(24f);
                soundStartMs = json["soundStartMs"].AsInt(0);
                soundVolume = json["soundVolume"].AsFloat(1f);

                // Validation
                if (knockbackStrength < 0f) knockbackStrength = 0f;
                if (maxPlayerMotion <= 0f) maxPlayerMotion = 0.35f;
                if (stunMs < 0) stunMs = 0;
                victimWalkSpeedMult = GameMath.Clamp(victimWalkSpeedMult, 0f, 1f);
                if (soundVolume <= 0f) soundVolume = 1f;
            }
        }

        private List<Stage> stages = new List<Stage>();
        private long lastCleanupMs;

        public EntityBehaviorBossRepulseStun(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossrepulsestun";

        protected override void InitializeStages(JsonObject attributes)
        {
            stages = ParseStages<Stage>(attributes);
        }

        protected override int GetStageCount() => stages.Count;

        protected override object GetStage(int index) => stages[index];

        protected override float GetStageHealthThreshold(object stage) => ((Stage)stage).whenHealthRelBelow;

        protected override float GetStageCooldown(object stage) => ((Stage)stage).cooldownSeconds;

        protected override float GetMaxTargetRange(object stage) => ((Stage)stage).maxTargetRange;

        protected override float MinTargetRange => 0.75f;

        protected override bool ShouldCheckAbility() => true;

        protected override void ActivateAbility(object stageObj, int stageIndex, EntityPlayer target)
        {
            if (stageObj is not Stage stage) return;
            
            MarkCooldownStart();
            TryApplyKnockback(stage, target);
            TryApplyStun(stage, target);
            TryPlaySound(stage.sound, stage.soundRange, stage.soundStartMs, stage.soundVolume);
        }

        protected override bool UsePeriodicTick() => true;

        protected override void OnPeriodicTick(float dt)
        {
            CleanupExpiredStuns();
        }

        protected override void StopAbility()
        {
        }

        protected override bool OnAbilityTick(float dt) => false;

        private void CleanupExpiredStuns()
        {
            if (Sapi == null) return;

            long now = Sapi.World.ElapsedMilliseconds;
            if (lastCleanupMs != 0 && now - lastCleanupMs < 350) return;
            lastCleanupMs = now;

            var players = Sapi.World.AllOnlinePlayers;
            if (players == null || players.Length == 0) return;

            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] is not IServerPlayer sp) continue;
                if (sp.Entity is not EntityPlayer plr) continue;
                if (plr.Stats == null) continue;

                long until = plr.WatchedAttributes.GetLong(StunUntilKey, 0);
                if (until <= 0) continue;

                // World.ElapsedMilliseconds resets on relog/server restart, but WatchedAttributes persist.
                // If 'until' is far in the future compared to 'now', it is almost certainly stale data.
                // In that case, clear the effect so players don't get stuck with permanent 0 walkspeed.
                if (now > 0)
                {
                    const long MaxFutureMs = 5L * 60L * 1000L;
                    if (until - now > MaxFutureMs)
                    {
                        plr.WatchedAttributes.SetLong(StunUntilKey, 0);
                        plr.WatchedAttributes.MarkPathDirty(StunUntilKey);
                        plr.WatchedAttributes.SetFloat(StunMultKey, 1f);
                        plr.WatchedAttributes.MarkPathDirty(StunMultKey);
                        plr.Stats.Set("walkspeed", StunStatKey, 0f, true);
                        plr.walkSpeed = plr.Stats.GetBlended("walkspeed");
                        continue;
                    }
                }

                if (now >= until)
                {
                    plr.WatchedAttributes.SetLong(StunUntilKey, 0);
                    plr.WatchedAttributes.MarkPathDirty(StunUntilKey);
                    plr.WatchedAttributes.SetFloat(StunMultKey, 1f);
                    plr.WatchedAttributes.MarkPathDirty(StunMultKey);
                    plr.Stats.Set("walkspeed", StunStatKey, 0f, true);
                    plr.walkSpeed = plr.Stats.GetBlended("walkspeed");
                    continue;
                }

                float mult = GameMath.Clamp(plr.WatchedAttributes.GetFloat(StunMultKey, 0f), 0f, 1f);
                float modifier = mult - 1f;
                plr.Stats.Set("walkspeed", StunStatKey, modifier, true);
                plr.walkSpeed = plr.Stats.GetBlended("walkspeed");
            }
        }

        private void TryApplyKnockback(Stage stage, EntityPlayer target)
        {
            if (stage == null || target == null || entity == null) return;

            var dir = new Vec3d(target.Pos.X - entity.Pos.X, 0, target.Pos.Z - entity.Pos.Z);
            if (dir.Length() < 0.001) return;
            dir.Normalize();

            double kb = stage.knockbackStrength;
            if (kb <= 0.0001) kb = 0.20;

            double max = stage.maxPlayerMotion;
            if (max <= 0.0001) max = 0.35;

            double kbX = GameMath.Clamp(dir.X * kb, -max, max);
            double kbZ = GameMath.Clamp(dir.Z * kb, -max, max);

            // Only update if values changed significantly to reduce network sync spam
            double prevKbX = target.WatchedAttributes.GetDouble("kbdirX", 0.0);
            double prevKbZ = target.WatchedAttributes.GetDouble("kbdirZ", 0.0);
            
            if (Math.Abs(prevKbX - kbX) > 0.001 || Math.Abs(prevKbZ - kbZ) > 0.001)
            {
                target.WatchedAttributes.SetDouble("kbdirX", kbX);
                target.WatchedAttributes.SetDouble("kbdirY", 0.0);
                target.WatchedAttributes.SetDouble("kbdirZ", kbZ);

                target.WatchedAttributes.MarkPathDirty("kbdirX");
                target.WatchedAttributes.MarkPathDirty("kbdirY");
                target.WatchedAttributes.MarkPathDirty("kbdirZ");
            }

            // Entity.Attributes is not synced to the client. Vanilla sets Attributes["dmgkb"] client-side
            // via a WatchedAttributes["onHurt"] modified listener. We replicate that trigger with a tiny value.
            float prevOnHurt = target.WatchedAttributes.GetFloat("onHurt", 0f);
            if (Math.Abs(prevOnHurt - 0.01f) > 0.001f)
            {
                target.WatchedAttributes.SetFloat("onHurt", 0.01f);
                target.WatchedAttributes.MarkPathDirty("onHurt");
            }
            target.WatchedAttributes.SetInt("onHurtCounter", target.WatchedAttributes.GetInt("onHurtCounter") + 1);
            target.WatchedAttributes.MarkPathDirty("onHurtCounter");
        }

        private void TryApplyStun(Stage stage, EntityPlayer target)
        {
            if (stage == null || target == null) return;
            if (stage.stunMs <= 0) return;
            if (target.Stats == null) return;

            long until = Sapi.World.ElapsedMilliseconds + Math.Max(50, stage.stunMs);
            target.WatchedAttributes.SetLong(StunUntilKey, until);
            target.WatchedAttributes.MarkPathDirty(StunUntilKey);

            float mult = GameMath.Clamp(stage.victimWalkSpeedMult, 0f, 1f);
            target.WatchedAttributes.SetFloat(StunMultKey, mult);
            target.WatchedAttributes.MarkPathDirty(StunMultKey);

            float modifier = mult - 1f;
            target.Stats.Set("walkspeed", StunStatKey, modifier, true);
            BossBehaviorUtils.UpdatePlayerWalkSpeed(target);
        }

        private void TryPlaySound(Stage stage)
        {
            if (Sapi == null || stage == null) return;
            TryPlaySound(stage.sound, stage.soundRange, stage.soundStartMs, stage.soundVolume);
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            base.OnEntityDeath(damageSourceForDeath);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            base.OnEntityDespawn(despawn);
        }
    }
}
