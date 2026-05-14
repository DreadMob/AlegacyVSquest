using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsQuest
{
    /// <summary>
    /// Boss ability that takes control of a random player for a short duration.
    /// The controlled player's AI attacks other players.
    /// </summary>
    public class EntityBehaviorBossMindControl : BossAbilityBase
    {
        private const string MindControlFlagKey = "alegacyvsquest:mindcontrol:active";
        private const string MindControlBossIdKey = "alegacyvsquest:mindcontrol:bossid";
        private const string MindControlEndTimeKey = "alegacyvsquest:mindcontrol:endtime";

        private class Stage : BossAbilityStage
        {
            public float controlDurationSeconds;
            public float controlRange;
            public float victimMoveSpeed;
            public float attackDamage;
            public int attackDamageTier;
            public string particleEffect;
            public string controlSound;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                controlDurationSeconds = json["controlDurationSeconds"].AsFloat(5f);
                controlRange = json["controlRange"].AsFloat(30f);
                victimMoveSpeed = json["victimMoveSpeed"].AsFloat(0.04f);
                attackDamage = json["attackDamage"].AsFloat(10f);
                attackDamageTier = json["attackDamageTier"].AsInt(3);
                particleEffect = json["particleEffect"].AsString(null);
                controlSound = json["controlSound"].AsString("albase:dark-magic-charge-up");
            }
        }

        private List<Stage> stages = new List<Stage>();
        protected override string CooldownKey => "alegacyvsquest:bossmindcontrol:lastCheckMs";

        private long controlEndCallbackId;
        private EntityPlayer controlledPlayer;
        private long tickListenerId;

        public EntityBehaviorBossMindControl(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossmindcontrol";

        protected override void InitializeStages(JsonObject attributes)
        {
            stages = ParseStages<Stage>(attributes);
        }

        protected override int GetStageCount() => stages.Count;
        protected override object GetStage(int index) => stages[index];
        protected override float GetStageHealthThreshold(object stage) => ((Stage)stage).whenHealthRelBelow;
        protected override float GetStageCooldown(object stage) => ((Stage)stage).cooldownSeconds;
        protected override float GetMaxTargetRange(object stage) => ((Stage)stage).controlRange;

        protected override void ActivateAbility(object stage, int stageIndex, EntityPlayer target)
        {
            if (Sapi == null || entity == null) return;
            var settings = (Stage)stage;

            // Find a random player to control
            var players = Sapi.World.AllOnlinePlayers;
            if (players == null || players.Length == 0) return;

            var candidates = new List<IPlayer>();
            foreach (var player in players)
            {
                if (player?.Entity == null || !player.Entity.Alive) continue;
                if (player.Entity.Pos.Dimension != entity.Pos.Dimension) continue;
                double dist = player.Entity.Pos.DistanceTo(entity.Pos);
                if (dist <= settings.controlRange)
                {
                    candidates.Add(player);
                }
            }

            if (candidates.Count == 0) return;

            // Pick random player
            var selectedPlayer = candidates[Sapi.World.Rand.Next(candidates.Count)];
            controlledPlayer = selectedPlayer.Entity;

            // Apply mind control
            ApplyMindControl(selectedPlayer, settings);

            // Play sound
            TryPlaySound(settings.controlSound, 32f, 0, 0.5f);

            // Start animation
            TryPlayAnimation("attack");

            // Mark cooldown
            MarkCooldownStart();

            // Schedule end of control
            int durationMs = (int)(settings.controlDurationSeconds * 1000);
            controlEndCallbackId = RegisterCallbackTracked(dt => EndMindControl(), durationMs);

            // Start tick listener for AI behavior
            tickListenerId = Sapi.Event.RegisterGameTickListener(dt => OnMindControlTick(dt, settings), 200);
        }

        private void ApplyMindControl(IPlayer player, Stage settings)
        {
            if (player?.Entity == null) return;

            var entityPlayer = player.Entity;
            
            // Set control flags
            entityPlayer.WatchedAttributes.SetBool(MindControlFlagKey, true);
            entityPlayer.WatchedAttributes.SetLong(MindControlBossIdKey, entity.EntityId);
            entityPlayer.WatchedAttributes.SetLong(MindControlEndTimeKey, Sapi.World.ElapsedMilliseconds + (long)(settings.controlDurationSeconds * 1000));
            entityPlayer.WatchedAttributes.MarkPathDirty(MindControlFlagKey);

            // Visual effect - spawn particles around the player
            SpawnControlParticles(entityPlayer, settings);

            // Notify player they are being controlled
            var msg = Lang.Get("alegacyvsquest:mindcontrol-start");
            (player as IServerPlayer)?.SendMessage(GlobalConstants.GeneralChatGroup, msg, EnumChatType.Notification);
        }

        private void SpawnControlParticles(EntityPlayer player, Stage settings)
        {
            if (player?.World == null || Sapi == null) return;

            // Spawn dark arcane spiral around the controlled player
            var pos = player.Pos.XYZ.Add(0, 1, 0);
            ParticleUtils.SpawnSpiral(Sapi, pos, 0.8f, 2f, ParticleUtils.Colors.Arcane, 12, 0.3f);
        }

        private float randomWalkYaw;
        private long lastDirectionChangeMs;

        private void OnMindControlTick(float dt, Stage settings)
        {
            if (controlledPlayer == null || !controlledPlayer.Alive || Sapi == null)
            {
                EndMindControl();
                return;
            }

            // Check if control has ended
            long endTime = controlledPlayer.WatchedAttributes.GetLong(MindControlEndTimeKey, 0);
            if (Sapi.World.ElapsedMilliseconds >= endTime)
            {
                EndMindControl();
                return;
            }

            // Find nearest other player to attack
            EntityPlayer target = null;
            double minDist = double.MaxValue;

            var players = Sapi.World.AllOnlinePlayers;
            foreach (var player in players)
            {
                if (player?.Entity == null || !player.Entity.Alive) continue;
                if (player.Entity == controlledPlayer) continue;
                if (player.Entity.Pos.Dimension != controlledPlayer.Pos.Dimension) continue;

                double dist = controlledPlayer.Pos.DistanceTo(player.Entity.Pos);
                if (dist < minDist && dist <= 30)
                {
                    minDist = dist;
                    target = player.Entity;
                }
            }

            float moveSpeed = settings.victimMoveSpeed;
            
            if (target != null)
            {
                // Move towards target using ServerPos.Motion (works server-side)
                var dir = target.Pos.XYZ.Sub(controlledPlayer.Pos.XYZ).Normalize();
                
                float yaw = (float)Math.Atan2(dir.X, dir.Z);
                controlledPlayer.Pos.Yaw = yaw;
                controlledPlayer.ServerPos.Yaw = yaw;

                // Apply motion directly (server-authoritative movement)
                controlledPlayer.ServerPos.Motion.X = dir.X * moveSpeed;
                controlledPlayer.ServerPos.Motion.Z = dir.Z * moveSpeed;

                // Attack if close enough — deal damage directly instead of using Controls
                if (minDist <= 3.5)
                {
                    if (Sapi.World.Rand.NextDouble() < 0.3) // 30% chance per tick (200ms interval)
                    {
                        var dmgSource = new DamageSource
                        {
                            Source = EnumDamageSource.Entity,
                            SourceEntity = controlledPlayer,
                            Type = EnumDamageType.BluntAttack,
                            DamageTier = settings.attackDamageTier
                        };
                        target.ReceiveDamage(dmgSource, settings.attackDamage * 0.1f);
                    }
                }
            }
            else
            {
                // No other players - wander randomly towards boss
                var dirToBoss = entity.Pos.XYZ.Sub(controlledPlayer.Pos.XYZ).Normalize();
                
                if (Sapi.World.ElapsedMilliseconds - lastDirectionChangeMs > 2000)
                {
                    // Wander towards boss with some randomness
                    randomWalkYaw = (float)Math.Atan2(dirToBoss.X, dirToBoss.Z) + (float)(Sapi.World.Rand.NextDouble() - 0.5) * 1.5f;
                    lastDirectionChangeMs = Sapi.World.ElapsedMilliseconds;
                }
                
                controlledPlayer.Pos.Yaw = randomWalkYaw;
                controlledPlayer.ServerPos.Yaw = randomWalkYaw;
                
                controlledPlayer.ServerPos.Motion.X = Math.Sin(randomWalkYaw) * moveSpeed;
                controlledPlayer.ServerPos.Motion.Z = Math.Cos(randomWalkYaw) * moveSpeed;
            }

            // Spawn particles periodically
            if (Sapi.World.Rand.NextDouble() < 0.1)
            {
                SpawnControlParticles(controlledPlayer, settings);
            }
        }

        private void EndMindControl()
        {
            if (controlledPlayer != null)
            {
                // Clear control flags
                controlledPlayer.WatchedAttributes.SetBool(MindControlFlagKey, false);
                controlledPlayer.WatchedAttributes.RemoveAttribute(MindControlBossIdKey);
                controlledPlayer.WatchedAttributes.RemoveAttribute(MindControlEndTimeKey);
                controlledPlayer.WatchedAttributes.MarkPathDirty(MindControlFlagKey);

                // Reset controls
                controlledPlayer.Controls.WalkVector.Set(0, 0, 0);
                controlledPlayer.Controls.HandUse = EnumHandInteract.None;

                // Notify player
                var sapi = controlledPlayer.World?.Api as ICoreServerAPI;
                if (sapi != null)
                {
                    var player = sapi.World.PlayerByUid(controlledPlayer.PlayerUID) as IServerPlayer;
                    var msg = Lang.Get("alegacyvsquest:mindcontrol-end");
                    player?.SendMessage(GlobalConstants.GeneralChatGroup, msg, EnumChatType.Notification);
                }
            }

            controlledPlayer = null;

            // Unregister tick listener
            if (tickListenerId != 0 && Sapi != null)
            {
                Sapi.Event.UnregisterGameTickListener(tickListenerId);
                tickListenerId = 0;
            }

            // Unregister callback
            UnregisterCallbackSafe(ref controlEndCallbackId);
        }

        protected override void StopAbility()
        {
            EndMindControl();
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            EndMindControl();
            base.OnEntityDeath(damageSourceForDeath);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            EndMindControl();
            base.OnEntityDespawn(despawn);
        }
    }
}
