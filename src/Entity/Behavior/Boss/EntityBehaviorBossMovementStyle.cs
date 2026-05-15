using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    /// <summary>
    /// Souls-like movement patterns for trial bosses.
    /// Instead of just walking straight at the player, bosses will:
    /// - Strafe around the player (circling)
    /// - Dash forward for attacks
    /// - Retreat after combos
    /// - Pause menacingly before engaging
    /// 
    /// Styles: "assassin", "tank", "kiter", "berserker", "controller"
    /// </summary>
    public class EntityBehaviorBossMovementStyle : EntityBehavior
    {
        private string style = "default";
        private float strafeSpeed = 0.03f;
        private float dashSpeed = 0.12f;
        private float retreatSpeed = 0.05f;
        private float engageDistance = 4f;
        private float strafeDistance = 6f;
        private float retreatDistance = 10f;
        private float dashCooldownSec = 5f;
        private float strafeDurationSec = 2f;
        private float retreatDurationSec = 1.5f;
        private float pauseDurationSec = 1f;

        private enum MovementState { Idle, Approaching, Strafing, Dashing, Retreating, Pausing }
        private MovementState currentState = MovementState.Idle;
        private double stateStartMs;
        private double lastDashMs;
        private bool strafeClockwise;
        private long tickListenerId;

        public EntityBehaviorBossMovementStyle(Entity entity) : base(entity) { }
        public override string PropertyName() => "bossmovementstyle";

        public override void Initialize(EntityProperties properties, JsonObject typeAttributes)
        {
            base.Initialize(properties, typeAttributes);

            style = typeAttributes["style"].AsString("default");
            strafeSpeed = typeAttributes["strafeSpeed"].AsFloat(0.03f);
            dashSpeed = typeAttributes["dashSpeed"].AsFloat(0.12f);
            retreatSpeed = typeAttributes["retreatSpeed"].AsFloat(0.05f);
            engageDistance = typeAttributes["engageDistance"].AsFloat(4f);
            strafeDistance = typeAttributes["strafeDistance"].AsFloat(6f);
            retreatDistance = typeAttributes["retreatDistance"].AsFloat(10f);
            dashCooldownSec = typeAttributes["dashCooldownSec"].AsFloat(5f);
            strafeDurationSec = typeAttributes["strafeDurationSec"].AsFloat(2f);
            retreatDurationSec = typeAttributes["retreatDurationSec"].AsFloat(1.5f);
            pauseDurationSec = typeAttributes["pauseDurationSec"].AsFloat(1f);

            ApplyStylePreset();

            if (entity.Api?.Side == EnumAppSide.Server)
            {
                tickListenerId = entity.World.RegisterGameTickListener(OnMovementTick, 200);
            }
        }

        private void ApplyStylePreset()
        {
            switch (style.ToLowerInvariant())
            {
                case "assassin":
                    strafeSpeed = 0.04f;
                    dashSpeed = 0.15f;
                    retreatSpeed = 0.06f;
                    engageDistance = 3f;
                    strafeDistance = 5f;
                    strafeDurationSec = 1.5f;
                    dashCooldownSec = 3f;
                    retreatDurationSec = 1f;
                    pauseDurationSec = 0.5f;
                    break;
                case "tank":
                    strafeSpeed = 0.015f;
                    dashSpeed = 0.06f;
                    retreatSpeed = 0.02f;
                    engageDistance = 5f;
                    strafeDistance = 7f;
                    strafeDurationSec = 3f;
                    dashCooldownSec = 8f;
                    retreatDurationSec = 0f; // tanks don't retreat
                    pauseDurationSec = 2f;
                    break;
                case "kiter":
                    strafeSpeed = 0.05f;
                    dashSpeed = 0.08f;
                    retreatSpeed = 0.07f;
                    engageDistance = 6f;
                    strafeDistance = 8f;
                    strafeDurationSec = 1f;
                    dashCooldownSec = 6f;
                    retreatDurationSec = 2.5f;
                    pauseDurationSec = 0.3f;
                    break;
                case "berserker":
                    strafeSpeed = 0.02f;
                    dashSpeed = 0.14f;
                    retreatSpeed = 0.03f;
                    engageDistance = 2.5f;
                    strafeDistance = 4f;
                    strafeDurationSec = 0.8f;
                    dashCooldownSec = 2f;
                    retreatDurationSec = 0.5f;
                    pauseDurationSec = 0.3f;
                    break;
                case "controller":
                    strafeSpeed = 0.025f;
                    dashSpeed = 0.05f;
                    retreatSpeed = 0.04f;
                    engageDistance = 7f;
                    strafeDistance = 9f;
                    strafeDurationSec = 3f;
                    dashCooldownSec = 10f;
                    retreatDurationSec = 2f;
                    pauseDurationSec = 1.5f;
                    break;
            }
        }

        private void OnMovementTick(float dt)
        {
            if (entity.Api?.Side != EnumAppSide.Server) return;
            if (!entity.Alive) return;

            var sapi = entity.Api as ICoreServerAPI;
            if (sapi == null) return;

            // Find target player
            EntityPlayer target = FindNearestPlayer(sapi);
            if (target == null)
            {
                currentState = MovementState.Idle;
                return;
            }

            double distToTarget = entity.Pos.DistanceTo(target.Pos);
            double nowMs = entity.World.ElapsedMilliseconds;
            double stateAge = (nowMs - stateStartMs) / 1000.0;

            switch (currentState)
            {
                case MovementState.Idle:
                    // Transition to approaching or strafing
                    if (distToTarget > strafeDistance)
                    {
                        TransitionTo(MovementState.Approaching, nowMs);
                    }
                    else if (distToTarget <= engageDistance)
                    {
                        // Close enough — decide: dash, strafe, or pause
                        if (nowMs - lastDashMs >= dashCooldownSec * 1000 && entity.World.Rand.NextDouble() < 0.4)
                        {
                            TransitionTo(MovementState.Dashing, nowMs);
                        }
                        else
                        {
                            TransitionTo(MovementState.Strafing, nowMs);
                            strafeClockwise = entity.World.Rand.NextDouble() > 0.5;
                        }
                    }
                    else
                    {
                        TransitionTo(MovementState.Strafing, nowMs);
                        strafeClockwise = entity.World.Rand.NextDouble() > 0.5;
                    }
                    break;

                case MovementState.Approaching:
                    if (distToTarget <= strafeDistance)
                    {
                        TransitionTo(MovementState.Pausing, nowMs);
                    }
                    break;

                case MovementState.Strafing:
                    ApplyStrafe(target, dt);
                    if (stateAge >= strafeDurationSec)
                    {
                        // After strafing, decide next action
                        if (distToTarget <= engageDistance && nowMs - lastDashMs >= dashCooldownSec * 1000)
                        {
                            TransitionTo(MovementState.Dashing, nowMs);
                        }
                        else if (entity.World.Rand.NextDouble() < 0.3 && retreatDurationSec > 0)
                        {
                            TransitionTo(MovementState.Retreating, nowMs);
                        }
                        else
                        {
                            TransitionTo(MovementState.Pausing, nowMs);
                        }
                    }
                    break;

                case MovementState.Dashing:
                    ApplyDash(target, dt);
                    lastDashMs = nowMs;
                    if (distToTarget <= engageDistance * 0.7 || stateAge >= 0.5)
                    {
                        // After dash, retreat or pause
                        if (retreatDurationSec > 0 && entity.World.Rand.NextDouble() < 0.5)
                        {
                            TransitionTo(MovementState.Retreating, nowMs);
                        }
                        else
                        {
                            TransitionTo(MovementState.Strafing, nowMs);
                            strafeClockwise = !strafeClockwise;
                        }
                    }
                    break;

                case MovementState.Retreating:
                    ApplyRetreat(target, dt);
                    if (stateAge >= retreatDurationSec || distToTarget >= retreatDistance)
                    {
                        TransitionTo(MovementState.Pausing, nowMs);
                    }
                    break;

                case MovementState.Pausing:
                    // Boss stands still menacingly
                    if (stateAge >= pauseDurationSec)
                    {
                        TransitionTo(MovementState.Idle, nowMs);
                    }
                    break;
            }
        }

        private void TransitionTo(MovementState newState, double nowMs)
        {
            currentState = newState;
            stateStartMs = nowMs;
        }

        private void ApplyStrafe(EntityPlayer target, float dt)
        {
            // Move perpendicular to the direction toward the target
            double dx = target.Pos.X - entity.Pos.X;
            double dz = target.Pos.Z - entity.Pos.Z;
            double dist = Math.Sqrt(dx * dx + dz * dz);
            if (dist < 0.1) return;

            // Normalize
            double ndx = dx / dist;
            double ndz = dz / dist;

            // Perpendicular (strafe direction)
            double perpX = strafeClockwise ? -ndz : ndz;
            double perpZ = strafeClockwise ? ndx : -ndx;

            // Also slightly adjust distance (maintain strafeDistance)
            double distCorrection = 0;
            if (dist < strafeDistance * 0.8) distCorrection = -0.3; // move away
            else if (dist > strafeDistance * 1.2) distCorrection = 0.3; // move closer

            double moveX = perpX * strafeSpeed + ndx * distCorrection * strafeSpeed;
            double moveZ = perpZ * strafeSpeed + ndz * distCorrection * strafeSpeed;

            entity.Pos.Motion.Add(moveX, 0, moveZ);
        }

        private void ApplyDash(EntityPlayer target, float dt)
        {
            double dx = target.Pos.X - entity.Pos.X;
            double dz = target.Pos.Z - entity.Pos.Z;
            double dist = Math.Sqrt(dx * dx + dz * dz);
            if (dist < 0.1) return;

            entity.Pos.Motion.Add(dx / dist * dashSpeed, 0, dz / dist * dashSpeed);

            // Dash particles
            var sapi = entity.Api as ICoreServerAPI;
            if (sapi != null)
            {
                ParticleUtils.SpawnEntityAura(sapi, entity, ParticleUtils.Colors.Lightning, 4, 0.2f, 0.3f);
            }
        }

        private void ApplyRetreat(EntityPlayer target, float dt)
        {
            double dx = entity.Pos.X - target.Pos.X;
            double dz = entity.Pos.Z - target.Pos.Z;
            double dist = Math.Sqrt(dx * dx + dz * dz);
            if (dist < 0.1) return;

            entity.Pos.Motion.Add(dx / dist * retreatSpeed, 0, dz / dist * retreatSpeed);
        }

        private EntityPlayer FindNearestPlayer(ICoreServerAPI sapi)
        {
            EntityPlayer nearest = null;
            double nearestDist = double.MaxValue;

            foreach (var p in sapi.World.AllOnlinePlayers)
            {
                if (p is not IServerPlayer sp) continue;
                var pe = sp.Entity;
                if (pe == null || !pe.Alive) continue;
                if (pe.Pos.Dimension != entity.Pos.Dimension) continue;

                double dist = pe.Pos.SquareDistanceTo(entity.Pos.XYZ);
                if (dist < nearestDist && dist < 40 * 40)
                {
                    nearestDist = dist;
                    nearest = pe;
                }
            }

            return nearest;
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            base.OnEntityDespawn(despawn);
            if (tickListenerId != 0 && entity.World != null)
            {
                entity.World.UnregisterGameTickListener(tickListenerId);
                tickListenerId = 0;
            }
        }
    }
}
