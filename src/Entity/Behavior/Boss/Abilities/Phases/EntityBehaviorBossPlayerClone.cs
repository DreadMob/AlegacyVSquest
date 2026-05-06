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
    public class EntityBehaviorBossPlayerClone : BossAbilityBase
    {
        private const string CloneFlagKey = "alegacyvsquest:bossplayerclone";
        private const string CloneOwnerIdKey = "alegacyvsquest:bossplayerclone:ownerid";
        private const string ClonePlayerUidKey = "alegacyvsquest:bossplayerclone:playeruid";
        private const string ClonePlayerNameKey = "alegacyvsquest:bossplayerclone:playername";

        private const int HandSlotRight = 15;
        private const int HandSlotLeft = 16;

        private class Stage : BossAbilityStage
        {
            public string cloneEntityCode;
            public float cloneRange;

            public override void FromJson(JsonObject json)
            {
                base.FromJson(json);
                cloneEntityCode = json["cloneEntityCode"].AsString(null);
                cloneRange = json["cloneRange"].AsFloat(50f);
            }
        }

        private List<Stage> stages = new List<Stage>();
        protected override string CooldownKey => "alegacyvsquest:bossplayerclone:lastCheckMs";
        protected override bool UsePeriodicTick() => true;
        protected override int CheckIntervalMs => 500;

        private readonly Dictionary<string, long> cloneByPlayerUid = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        public EntityBehaviorBossPlayerClone(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => "bossplayerclone";

        protected override void InitializeStages(JsonObject attributes)
        {
            stages = ParseStages<Stage>(attributes);
        }

        protected override void OnPeriodicTick(float dt)
        {
            if (Sapi == null || entity == null) return;

            if (IsCloneEntity())
            {
                SyncClone();
                return;
            }

            if (!entity.Alive)
            {
                CleanupClones();
                return;
            }

            UpdateClones();
        }

        protected override int GetStageCount() => stages.Count;
        protected override object GetStage(int index) => stages[index];
        protected override float GetStageHealthThreshold(object stage) => ((Stage)stage).whenHealthRelBelow;
        protected override float GetStageCooldown(object stage) => ((Stage)stage).cooldownSeconds;
        protected override float GetMaxTargetRange(object stage) => 0f;

        protected override void ActivateAbility(object stage, int stageIndex, EntityPlayer target) { }
        protected override void StopAbility()
        {
            if (!IsCloneEntity())
            {
                CleanupClones();
            }
        }

        public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
        {
            if (mode != EnumInteractMode.Interact) return;
            if (handled == EnumHandling.Handled) return;
            if (Sapi == null || entity == null) return;
            if (byEntity is not EntityPlayer) return;

            // Prevent any debug/selection messages on right-click for Crypt Mirror and its clones.
            // This behavior is attached to those entities, so we can safely consume the interaction.
            handled = EnumHandling.Handled;
        }

        public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
        {
            base.OnEntityReceiveDamage(damageSource, ref damage);

            if (!IsCloneEntity()) return;
            if (damage <= 0f) return;

            var owner = GetCloneOwner();
            if (owner == null || !owner.Alive) return;

            owner.ReceiveDamage(damageSource, damage);
            damage = 0f;
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            if (!IsCloneEntity())
            {
                CleanupClones();
            }

            base.OnEntityDeath(damageSourceForDeath);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            if (!IsCloneEntity())
            {
                CleanupClones();
            }

            base.OnEntityDespawn(despawn);
        }

        private void UpdateClones()
        {
            if (stages.Count == 0) return;
            var settings = stages[0];
            if (string.IsNullOrWhiteSpace(settings.cloneEntityCode)) return;

            var players = Sapi.World.AllOnlinePlayers;
            if (players == null) return;

            var aliveUids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var player in players)
            {
                var plrEntity = player?.Entity;
                if (plrEntity == null || !plrEntity.Alive) continue;
                if (plrEntity.Pos.Dimension != entity.Pos.Dimension) continue;

                double distSq = plrEntity.Pos.DistanceTo(entity.Pos);
                if (distSq > settings.cloneRange) continue;

                string uid = player.PlayerUID;
                if (string.IsNullOrWhiteSpace(uid)) continue;

                aliveUids.Add(uid);

                if (!cloneByPlayerUid.TryGetValue(uid, out var cloneId) || cloneId <= 0)
                {
                    SpawnCloneFor(player);
                    continue;
                }

                var cloneEntity = Sapi.World.GetEntityById(cloneId);
                if (cloneEntity == null || !cloneEntity.Alive)
                {
                    cloneByPlayerUid.Remove(uid);
                    SpawnCloneFor(player);
                }
            }

            if (cloneByPlayerUid.Count == 0) return;

            var toRemove = new List<string>();
            foreach (var pair in cloneByPlayerUid)
            {
                if (aliveUids.Contains(pair.Key)) continue;

                var clone = Sapi.World.GetEntityById(pair.Value);
                if (clone != null)
                {
                    Sapi.World.DespawnEntity(clone, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                }

                toRemove.Add(pair.Key);
            }

            foreach (var key in toRemove)
            {
                cloneByPlayerUid.Remove(key);
            }
        }

        private void SpawnCloneFor(IPlayer player)
        {
            if (player?.Entity == null || Sapi == null || entity == null) return;
            if (stages.Count == 0) return;

            var type = Sapi.World.GetEntityType(new AssetLocation(stages[0].cloneEntityCode));
            if (type == null) return;

            Entity clone = Sapi.World.ClassRegistry.CreateEntity(type);
            if (clone == null) return;

            ApplyCloneFlags(clone, player);

            var spawnPos = GetSpawnPositionNear(player.Entity.Pos.XYZ);
            int dim = entity.Pos.Dimension;
            clone.Pos.SetPosWithDimension(new Vec3d(spawnPos.X, spawnPos.Y + dim * 32768.0, spawnPos.Z));
            clone.Pos.Yaw = player.Entity.Pos.Yaw;
            clone.Pos.SetFrom(clone.Pos);

            Sapi.World.SpawnEntity(clone);

            // Important: many behaviors (including seraphinventory) finish initialization during SpawnEntity.
            // Copying inventory/appearance before SpawnEntity can silently do nothing.
            Sapi.Event.EnqueueMainThreadTask(() =>
            {
                try
                {
                    if (clone == null || !clone.Alive) return;

                    CopyAppearanceAttributes(player, clone);
                    CopyPlayerInventory(player, clone);
                    clone.MarkShapeModified();
                }
                catch (Exception ex)
                {
                    entity?.Api?.Logger?.Error($"[vsquest] Exception in SpawnCloneFor callback: {ex}");
                }
            }, "bossplayerclone-copy");

            cloneByPlayerUid[player.PlayerUID] = clone.EntityId;
        }

        private Vec3d GetSpawnPositionNear(Vec3d basePos)
        {
            if (basePos == null) return entity.Pos.XYZ.Clone();

            double angle = Sapi.World.Rand.NextDouble() * Math.PI * 2.0;
            double dist = 1.5 + Sapi.World.Rand.NextDouble() * 1.5;
            return new Vec3d(basePos.X + Math.Cos(angle) * dist, basePos.Y, basePos.Z + Math.Sin(angle) * dist);
        }

        private void ApplyCloneFlags(Entity clone, IPlayer player)
        {
            if (clone?.WatchedAttributes == null) return;

            clone.WatchedAttributes.SetBool(CloneFlagKey, true);
            clone.WatchedAttributes.SetLong(CloneOwnerIdKey, entity.EntityId);
            clone.WatchedAttributes.SetString(ClonePlayerUidKey, player.PlayerUID ?? string.Empty);
            clone.WatchedAttributes.SetString(ClonePlayerNameKey, player.PlayerName ?? string.Empty);

            clone.WatchedAttributes.SetBool("showHealthbar", false);

            clone.WatchedAttributes.MarkPathDirty(CloneFlagKey);
            clone.WatchedAttributes.MarkPathDirty(CloneOwnerIdKey);
            clone.WatchedAttributes.MarkPathDirty(ClonePlayerUidKey);
            clone.WatchedAttributes.MarkPathDirty(ClonePlayerNameKey);
            clone.WatchedAttributes.MarkPathDirty("showHealthbar");

            var tag = clone.WatchedAttributes.GetTreeAttribute("nametag") ?? new TreeAttribute();
            tag.SetString("name", player.PlayerName ?? "");
            clone.WatchedAttributes.SetAttribute("nametag", tag);
            clone.WatchedAttributes.MarkPathDirty("nametag");

            CopyAppearanceAttributes(player, clone);
        }

        private void CopyAppearanceAttributes(IPlayer player, Entity clone)
        {
            if (player?.Entity == null || clone?.WatchedAttributes == null) return;

            TryCopyTreeAttribute(player.Entity, clone, "wearablesInv");
            TryCopyTreeAttribute(player.Entity, clone, "skinConfig");
            TryCopyTreeAttribute(player.Entity, clone, "skinParts");
            TryCopyTreeAttribute(player.Entity, clone, "skinnableParts");

            // Some mods / versions store these keys with different casing.
            TryCopyTreeAttribute(player.Entity, clone, "wearablesinv");
            TryCopyTreeAttribute(player.Entity, clone, "skinconfig");
            TryCopyTreeAttribute(player.Entity, clone, "skinparts");
            TryCopyTreeAttribute(player.Entity, clone, "skinnableparts");
        }

        private void TryCopyTreeAttribute(Entity source, Entity target, string key)
        {
            if (source?.WatchedAttributes == null || target?.WatchedAttributes == null) return;

            var tree = source.WatchedAttributes.GetTreeAttribute(key);
            if (tree == null) return;

            target.WatchedAttributes.SetAttribute(key, tree.Clone());
            target.WatchedAttributes.MarkPathDirty(key);
        }

        private void CopyPlayerInventory(IPlayer player, Entity clone)
        {
            if (player?.Entity == null || clone == null) return;

            var invBehavior = clone.GetBehavior<EntityBehaviorSeraphInventory>();
            if (invBehavior?.Inventory == null) return;

            var targetInv = invBehavior.Inventory;
            var sourceInv = player.InventoryManager?.GetOwnInventory("character");

            if (sourceInv != null)
            {
                int count = Math.Min(targetInv.Count, sourceInv.Count);
                for (int i = 0; i < count; i++)
                {
                    var sourceSlot = sourceInv[i];
                    var targetSlot = targetInv[i];
                    if (targetSlot == null) continue;

                    if (sourceSlot?.Itemstack != null)
                    {
                        targetSlot.Itemstack = sourceSlot.Itemstack.Clone();
                    }
                    else
                    {
                        targetSlot.Itemstack = null;
                    }
                    targetSlot.MarkDirty();
                }
            }

            TrySetHandItem(targetInv, HandSlotRight, player.Entity.RightHandItemSlot);
            TrySetHandItem(targetInv, HandSlotLeft, player.Entity.LeftHandItemSlot);
        }

        private void TrySetHandItem(InventoryBase targetInv, int slotIndex, ItemSlot sourceSlot)
        {
            if (targetInv == null) return;
            if (slotIndex < 0 || slotIndex >= targetInv.Count) return;

            var targetSlot = targetInv[slotIndex];
            if (targetSlot == null) return;

            if (sourceSlot?.Itemstack != null)
            {
                targetSlot.Itemstack = sourceSlot.Itemstack.Clone();
            }
            else
            {
                targetSlot.Itemstack = null;
            }
            targetSlot.MarkDirty();
        }

        private void SyncClone()
        {
            var owner = GetCloneOwner();
            if (owner == null || !owner.Alive)
            {
                DespawnClone();
                return;
            }

            if (owner.TryGetHealth(out var healthTree, out float cur, out float max))
            {
                if (entity.TryGetHealth(out var cloneTree, out float cloneCur, out float cloneMax))
                {
                    if (cloneTree != null)
                    {
                        cloneTree.SetFloat("maxhealth", max);
                        cloneTree.SetFloat("currenthealth", cur);
                        entity.WatchedAttributes.MarkPathDirty("health");
                    }
                }
            }
        }

        private Entity GetCloneOwner()
        {
            long ownerId = entity?.WatchedAttributes?.GetLong(CloneOwnerIdKey, 0) ?? 0;
            if (ownerId <= 0 || Sapi == null) return null;
            return Sapi.World.GetEntityById(ownerId);
        }

        private bool IsCloneEntity()
        {
            return entity?.WatchedAttributes?.GetBool(CloneFlagKey, false) ?? false;
        }

        private void DespawnClone()
        {
            if (Sapi == null || entity == null) return;

            Sapi.World.DespawnEntity(entity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
        }

        private void CleanupClones()
        {
            if (Sapi == null) return;

            foreach (var pair in cloneByPlayerUid)
            {
                if (pair.Value <= 0) continue;

                var clone = Sapi.World.GetEntityById(pair.Value);
                if (clone != null)
                {
                    Sapi.World.DespawnEntity(clone, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                }
            }

            cloneByPlayerUid.Clear();
        }
    }
}
