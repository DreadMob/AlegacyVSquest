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
    /// Curse Stacks: each boss hit applies a curse stack to the player.
    /// At maxStacks — triggers an effect (stun, teleport to boss, or slow).
    /// Stacks decay after stackDecaySeconds without being hit.
    /// </summary>
    public class EntityBehaviorBossCurseStack : EntityBehavior
    {
        private int maxStacks = 5;
        private string effectType = "stun"; // "stun", "teleport", "slow"
        private float stackDecaySeconds = 4f;
        private float stunDurationMs = 2000f;
        private float slowFactor = 0.5f;
        private float slowDurationMs = 3000f;

        private readonly Dictionary<string, CurseData> playerCurses = new(StringComparer.OrdinalIgnoreCase);

        public EntityBehaviorBossCurseStack(Entity entity) : base(entity) { }
        public override string PropertyName() => "bosscursestack";

        public override void Initialize(EntityProperties properties, JsonObject typeAttributes)
        {
            base.Initialize(properties, typeAttributes);
            maxStacks = typeAttributes["maxStacks"].AsInt(5);
            effectType = typeAttributes["effectType"].AsString("stun");
            stackDecaySeconds = typeAttributes["stackDecaySeconds"].AsFloat(4f);
            stunDurationMs = typeAttributes["stunDurationMs"].AsFloat(2000f);
            slowFactor = typeAttributes["slowFactor"].AsFloat(0.5f);
            slowDurationMs = typeAttributes["slowDurationMs"].AsFloat(3000f);
        }

        /// <summary>
        /// Called when the boss deals damage to a player. Adds a curse stack.
        /// </summary>
        public void OnBossDealtDamage(IServerPlayer target)
        {
            if (target?.Entity == null || !target.Entity.Alive) return;
            if (entity.Api?.Side != EnumAppSide.Server) return;

            string uid = target.PlayerUID;
            double nowMs = entity.World.ElapsedMilliseconds;

            if (!playerCurses.TryGetValue(uid, out var data))
            {
                data = new CurseData();
                playerCurses[uid] = data;
            }

            // Decay check
            if (data.stacks > 0 && nowMs - data.lastStackMs > stackDecaySeconds * 1000)
            {
                data.stacks = 0;
            }

            data.stacks++;
            data.lastStackMs = nowMs;

            // Notify player of stacks
            SpawnCurseParticles(target.Entity);

            // Trigger effect at max stacks
            if (data.stacks >= maxStacks)
            {
                TriggerCurseEffect(target);
                data.stacks = 0;
            }
        }

        private void TriggerCurseEffect(IServerPlayer target)
        {
            var pe = target.Entity;
            if (pe == null || !pe.Alive) return;

            switch (effectType.ToLowerInvariant())
            {
                case "stun":
                    // Apply stun via stat (remove walkspeed temporarily)
                    pe.Stats.Set("walkspeed", "cursestun", -0.95f, false);
                    entity.World.RegisterCallback(_ =>
                    {
                        pe.Stats.Remove("walkspeed", "cursestun");
                    }, (int)stunDurationMs);
                    break;

                case "teleport":
                    // Teleport player to boss position
                    var bossPos = entity.Pos.XYZ;
                    pe.TeleportTo(new Vec3d(bossPos.X, bossPos.Y, bossPos.Z));
                    break;

                case "slow":
                    // Apply slow
                    pe.Stats.Set("walkspeed", "curseslow", -slowFactor, false);
                    entity.World.RegisterCallback(_ =>
                    {
                        pe.Stats.Remove("walkspeed", "curseslow");
                    }, (int)slowDurationMs);
                    break;
            }

            // Visual burst on trigger
            SpawnCurseTriggerParticles(pe);
        }

        private void SpawnCurseParticles(Entity target)
        {
            var pos = target.Pos;
            entity.World.SpawnParticles(new SimpleParticleProperties(
                2, 4, ColorUtil.ToRgba(180, 100, 0, 150),
                new Vec3d(pos.X - 0.3, pos.Y + 1, pos.Z - 0.3),
                new Vec3d(pos.X + 0.3, pos.Y + 1.5, pos.Z + 0.3),
                new Vec3f(0, 0.1f, 0), new Vec3f(0, 0.2f, 0),
                0.5f, 0f, 0.1f, 0.2f));
        }

        private void SpawnCurseTriggerParticles(Entity target)
        {
            var pos = target.Pos;
            entity.World.SpawnParticles(new SimpleParticleProperties(
                15, 25, ColorUtil.ToRgba(220, 150, 0, 200),
                new Vec3d(pos.X - 1, pos.Y, pos.Z - 1),
                new Vec3d(pos.X + 1, pos.Y + 2, pos.Z + 1),
                new Vec3f(-0.2f, 0.3f, -0.2f), new Vec3f(0.2f, 0.6f, 0.2f),
                1.0f, -0.02f, 0.2f, 0.5f));
        }

        private class CurseData
        {
            public int stacks;
            public double lastStackMs;
        }
    }
}
