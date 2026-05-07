using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace VsQuest
{
    public class BlockEntityQuestSpawner : BlockEntity
    {
        private const string AttrEntityCode = "vsquest:questspawner:entityCode";
        private const string AttrKillId = "vsquest:questspawner:killId";
        private const string AttrEntries = "vsquest:questspawner:entries";
        private const string AttrMaxAlive = "vsquest:questspawner:maxAlive";
        private const string AttrSpawnIntervalSeconds = "vsquest:questspawner:spawnIntervalSeconds";
        private const string AttrSpawnRadius = "vsquest:questspawner:spawnRadius";
        private const string AttrLeashRange = "vsquest:questspawner:leashRange";
        private const string AttrYOffset = "vsquest:questspawner:yOffset";

        private const int PacketOpenGui = 1000;
        private const int PacketSave = 1001;
        private const int PacketKill = 1002;
        private const int PacketToggle = 1003;

        private string entityCode;
        private string killId;

        private string[] entriesRaw;

        private int maxAlive;
        private float spawnIntervalSeconds;
        private float spawnRadius;
        private float leashRange;
        private float yOffset;

        private bool ticking;
        private float elapsed;

        private int lastEnabledMaxAlive;

        private QuestSpawnerConfigGui dlg;

        private class SpawnEntry
        {
            public string entityCode;
            public string killId;
            public int weight;
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (Api?.Side != EnumAppSide.Server) return;

            if (!ticking)
            {
                ticking = true;
                RegisterGameTickListener(OnServerTick, 250);
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            entityCode = tree.GetString(AttrEntityCode, entityCode);
            killId = tree.GetString(AttrKillId, killId);

            var entriesJoined = tree.GetString(AttrEntries, null);
            if (entriesJoined != null)
            {
                entriesRaw = entriesJoined.Length == 0
                    ? Array.Empty<string>()
                    : entriesJoined.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
            }

            maxAlive = tree.GetInt(AttrMaxAlive, maxAlive);
            spawnIntervalSeconds = tree.GetFloat(AttrSpawnIntervalSeconds, spawnIntervalSeconds);
            spawnRadius = tree.GetFloat(AttrSpawnRadius, spawnRadius);
            leashRange = tree.GetFloat(AttrLeashRange, leashRange);
            yOffset = tree.GetFloat(AttrYOffset, yOffset);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            if (!string.IsNullOrWhiteSpace(entityCode)) tree.SetString(AttrEntityCode, entityCode);
            if (!string.IsNullOrWhiteSpace(killId)) tree.SetString(AttrKillId, killId);

            if (entriesRaw != null)
            {
                tree.SetString(AttrEntries, string.Join("\n", entriesRaw));
            }

            tree.SetInt(AttrMaxAlive, maxAlive);
            tree.SetFloat(AttrSpawnIntervalSeconds, spawnIntervalSeconds);
            tree.SetFloat(AttrSpawnRadius, spawnRadius);
            tree.SetFloat(AttrLeashRange, leashRange);
            tree.SetFloat(AttrYOffset, yOffset);
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);

            if (Api?.Side != EnumAppSide.Server) return;

            var attrs = Block?.Attributes;
            entityCode = attrs?["entityCode"].AsString(entityCode);
            killId = attrs?["killId"].AsString(killId);

            entriesRaw = attrs?["entries"].AsArray<string>(null) ?? entriesRaw;

            maxAlive = attrs?["maxAlive"].AsInt(3) ?? 3;
            spawnIntervalSeconds = attrs?["spawnIntervalSeconds"].AsFloat(10f) ?? 10f;
            spawnRadius = attrs?["spawnRadius"].AsFloat(4f) ?? 4f;
            leashRange = attrs?["leashRange"].AsFloat(12f) ?? 12f;
            yOffset = attrs?["yOffset"].AsFloat(0f) ?? 0f;

            if (lastEnabledMaxAlive <= 0)
            {
                lastEnabledMaxAlive = Math.Max(1, maxAlive);
            }

            MarkDirty(true);
        }

        internal void OnInteract(IPlayer byPlayer)
        {
            if (byPlayer == null) return;

            if (Api.Side == EnumAppSide.Server)
            {
                var sp = byPlayer as IServerPlayer;
                if (sp == null) return;

                var data = BuildConfigData();
                (Api as ICoreServerAPI).Network.SendBlockEntityPacket(sp, Pos, PacketOpenGui, SerializerUtil.Serialize(data));
                return;
            }

            dlg = new QuestSpawnerConfigGui(Pos, Api as Vintagestory.API.Client.ICoreClientAPI);
            dlg.Data = BuildConfigData();
            dlg.TryOpen();
            dlg.OnClosed += () =>
            {
                dlg?.Dispose();
                dlg = null;
            };
        }

        public override void OnReceivedServerPacket(int packetid, byte[] bytes)
        {
            if (packetid != PacketOpenGui) return;

            var data = SerializerUtil.Deserialize<QuestSpawnerConfigData>(bytes);

            // If GUI is open, update it.
            if (dlg != null && dlg.IsOpened())
            {
                dlg.UpdateFromServer(data);
            }
            else
            {
                // If GUI isn't open yet, store data so it opens populated.
                // (OnInteract() will open it on the client side.)
                ApplyConfigData(data, markDirty: false);
            }
        }

        public override void OnReceivedClientPacket(IPlayer fromPlayer, int packetid, byte[] bytes)
        {
            var sp = fromPlayer as IServerPlayer;
            if (sp == null || !sp.HasPrivilege(Privilege.controlserver)) return;

            if (packetid == PacketSave)
            {
                var data = SerializerUtil.Deserialize<QuestSpawnerConfigData>(bytes);
                ApplyConfigData(data, markDirty: true);
                return;
            }

            if (packetid == PacketKill)
            {
                KillAllSpawned(fromPlayer);
                return;
            }

            if (packetid == PacketToggle)
            {
                ToggleEnabledServer(fromPlayer);
                return;
            }
        }

        private void ToggleEnabledServer(IPlayer byPlayer)
        {
            if (Api?.Side != EnumAppSide.Server) return;

            var sp = byPlayer as IServerPlayer;
            if (sp == null) return;

            ToggleEnabled();
            MarkDirty(true);

            // Refresh GUI on client if it's open
            var data = BuildConfigData();
            (Api as ICoreServerAPI).Network.SendBlockEntityPacket(sp, Pos, PacketOpenGui, SerializerUtil.Serialize(data));
        }

        private void KillAllSpawned(IPlayer byPlayer)
        {
            if (Api?.Side != EnumAppSide.Server) return;

            var sapi = Api as ICoreServerAPI;
            if (sapi?.World?.LoadedEntities == null) return;

            var list = new System.Collections.Generic.List<Entity>();
            try
            {
                foreach (var e in sapi.World.LoadedEntities.Values)
                {
                    var wa = e?.WatchedAttributes;
                    if (wa == null) continue;

                    bool matchNew = wa.GetInt("alegacyvsquest:spawner:dim", int.MinValue) == Pos.dimension
                        && wa.GetInt("alegacyvsquest:spawner:x", int.MinValue) == Pos.X
                        && wa.GetInt("alegacyvsquest:spawner:y", int.MinValue) == Pos.Y
                        && wa.GetInt("alegacyvsquest:spawner:z", int.MinValue) == Pos.Z;

                    if (!matchNew) continue;

                    list.Add(e);
                }
            }
            catch
            {
                return;
            }

            foreach (var e in list)
            {
                try
                {
                    sapi.World.DespawnEntity(e, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                }
                catch (Exception ex)
                {
                    sapi.Logger.Warning("[BlockEntityQuestSpawner] Failed to despawn entity: {0}", ex.Message);
                }
            }

            MarkDirty(true);
        }

        private QuestSpawnerConfigData BuildConfigData()
        {
            return new QuestSpawnerConfigData
            {
                maxAlive = maxAlive,
                spawnIntervalSeconds = spawnIntervalSeconds,
                spawnRadius = spawnRadius,
                leashRange = leashRange,
                yOffset = yOffset,
                entries = entriesRaw == null ? "" : string.Join("\n", entriesRaw)
            };
        }

        private void ApplyConfigData(QuestSpawnerConfigData data, bool markDirty)
        {
            if (data == null) return;

            maxAlive = Math.Max(0, data.maxAlive);
            spawnIntervalSeconds = Math.Max(0.1f, data.spawnIntervalSeconds);
            spawnRadius = Math.Max(0f, data.spawnRadius);
            leashRange = Math.Max(0f, data.leashRange);
            yOffset = data.yOffset;

            if (entriesRaw == null) entriesRaw = Array.Empty<string>();

            var entriesJoined = data.entries ?? "";
            entriesRaw = entriesJoined.Length == 0
                ? Array.Empty<string>()
                : entriesJoined.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

            if (lastEnabledMaxAlive <= 0) lastEnabledMaxAlive = Math.Max(1, maxAlive);

            if (markDirty)
            {
                MarkDirty(true);
            }
        }

        public string Describe()
        {
            string entries = entriesRaw == null || entriesRaw.Length == 0 ? "(none)" : string.Join(", ", entriesRaw);
            return $"questspawner: maxAlive={maxAlive}, interval={spawnIntervalSeconds:0.##}s, radius={spawnRadius:0.##}, leash={leashRange:0.##}, entries={entries}";
        }

        public bool ToggleEnabled()
        {
            if (maxAlive > 0)
            {
                lastEnabledMaxAlive = maxAlive;
                maxAlive = 0;
                MarkDirty(true);
                return false;
            }

            maxAlive = lastEnabledMaxAlive > 0 ? lastEnabledMaxAlive : 1;
            MarkDirty(true);
            return true;
        }

        public bool TryApplyFromEntitySpawnerItem(ItemStack itemStack)
        {
            if (itemStack?.Attributes == null) return false;

            string typeCode = itemStack.Attributes.GetString("type", null);
            if (string.IsNullOrWhiteSpace(typeCode)) return false;

            string newEntityCode = typeCode;
            string newKillId = typeCode;

            bool changed = false;

            if (!string.Equals(entityCode, newEntityCode, StringComparison.OrdinalIgnoreCase))
            {
                entityCode = newEntityCode;
                changed = true;
            }

            if (!string.Equals(killId, newKillId, StringComparison.OrdinalIgnoreCase))
            {
                killId = newKillId;
                changed = true;
            }

            string entry = $"{newEntityCode}|{newKillId}|1";
            if (entriesRaw == null || entriesRaw.Length != 1 || !string.Equals(entriesRaw[0], entry, StringComparison.OrdinalIgnoreCase))
            {
                entriesRaw = new[] { entry };
                changed = true;
            }

            if (changed)
            {
                MarkDirty(true);
            }

            return changed;
        }

        public bool TryAppendFromEntitySpawnerItem(ItemStack itemStack)
        {
            if (itemStack?.Attributes == null) return false;

            string typeCode = itemStack.Attributes.GetString("type", null);
            if (string.IsNullOrWhiteSpace(typeCode)) return false;

            // Default: killId = entityCode, weight = 1
            string entry = typeCode;

            if (entriesRaw == null)
            {
                entriesRaw = new[] { entry };
                MarkDirty(true);
                return true;
            }

            for (int i = 0; i < entriesRaw.Length; i++)
            {
                if (string.Equals(entriesRaw[i], entry, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            var newArr = new string[entriesRaw.Length + 1];
            Array.Copy(entriesRaw, newArr, entriesRaw.Length);
            newArr[newArr.Length - 1] = entry;
            entriesRaw = newArr;

            MarkDirty(true);
            return true;
        }

        private void OnServerTick(float dt)
        {
            if (Api?.Side != EnumAppSide.Server) return;
            if (Pos == null) return;

            if (maxAlive <= 0) return;

            var entry = SelectSpawnEntry();
            if (entry == null) return;

            elapsed += dt;
            if (elapsed < spawnIntervalSeconds) return;
            elapsed = 0;

            var countResult = CountAliveInRangeAndCheckBossRespawn();
            if (countResult.blockSpawn) return;
            if (countResult.aliveCount >= maxAlive) return;

            TrySpawnOne(entry);
        }

        private (int aliveCount, bool blockSpawn) CountAliveInRangeAndCheckBossRespawn()
        {
            var world = Api?.World;
            if (world == null) return (0, false);

            // Extra safety for boss-like spawns: if this spawner is configured to set a killId/targetId,
            // never spawn another copy while a living entity with the same targetId exists anywhere loaded.
            // This avoids duplicates when multi-phase bosses temporarily lose/skip spawner anchor during transitions.
            string activeKillId = null;
            try
            {
                var entry = SelectSpawnEntry();
                activeKillId = entry?.killId;
            }
            catch
            {
                activeKillId = null;
            }

            // Prefer scanning loaded entities to avoid duplicates when spawned mobs wander away from the spawner radius.
            // (Radius-based counting can miss living mobs that moved far, causing the spawner to create extra copies.)
            var sapi = Api as ICoreServerAPI;
            var loaded = sapi?.World?.LoadedEntities?.Values;
            if (loaded != null)
            {
                int aliveCount = 0;
                bool blockSpawn = false;

                foreach (var e in loaded)
                {
                    var wa = e?.WatchedAttributes;
                    if (wa == null) continue;

                    if (!string.IsNullOrWhiteSpace(activeKillId) && e.Alive)
                    {
                        string tid = null;
                        try
                        {
                            tid = wa.GetString("alegacyvsquest:killaction:targetid", null);
                        }
                        catch
                        {
                            tid = null;
                        }
                        if (string.Equals(tid, activeKillId, StringComparison.OrdinalIgnoreCase))
                        {
                            return (maxAlive, true);
                        }
                    }

                    bool matchNew = wa.GetInt("alegacyvsquest:spawner:dim", int.MinValue) == Pos.dimension
                        && wa.GetInt("alegacyvsquest:spawner:x", int.MinValue) == Pos.X
                        && wa.GetInt("alegacyvsquest:spawner:y", int.MinValue) == Pos.Y
                        && wa.GetInt("alegacyvsquest:spawner:z", int.MinValue) == Pos.Z;

                    if (!matchNew) continue;

                    if (e.Alive)
                    {
                        aliveCount++;
                    }
                    else
                    {
                        double respawnAt = wa.GetDouble("alegacyvsquest:bossrespawnAtTotalHours", double.NaN);
                        if (!double.IsNaN(respawnAt))
                        {
                            blockSpawn = true;
                        }

                    }
                }

                return (aliveCount, blockSpawn);
            }

            var center = Pos.ToVec3d().Add(0.5, 0, 0.5);
            float range = Math.Max(leashRange, spawnRadius) + 6f;

            var entities = world.GetEntitiesAround(center, range, range, (Entity e) => e != null);
            if (entities == null) return (0, false);

            int aliveCountRadius = 0;
            bool blockSpawnRadius = false;

            foreach (var e in entities)
            {
                var wa = e?.WatchedAttributes;
                if (wa == null) continue;

                bool matchNewRadius = wa.GetInt("alegacyvsquest:spawner:dim", int.MinValue) == Pos.dimension
                    && wa.GetInt("alegacyvsquest:spawner:x", int.MinValue) == Pos.X
                    && wa.GetInt("alegacyvsquest:spawner:y", int.MinValue) == Pos.Y
                    && wa.GetInt("alegacyvsquest:spawner:z", int.MinValue) == Pos.Z;

                if (!matchNewRadius) continue;

                if (e.Alive)
                {
                    aliveCountRadius++;
                }

                // If there's a corpse with a boss respawn timer, do not spawn additional copies.
                // This avoids race conditions where the spawner spawns while bossrespawn also spawns.
                if (!e.Alive)
                {
                    double respawnAt = wa.GetDouble("alegacyvsquest:bossrespawnAtTotalHours", double.NaN);
                    if (!double.IsNaN(respawnAt))
                    {
                        blockSpawnRadius = true;
                    }

                }
            }

            return (aliveCountRadius, blockSpawnRadius);
        }

        private SpawnEntry SelectSpawnEntry()
        {
            var list = GetSpawnEntries();
            if (list == null || list.Length == 0) return null;

            int total = 0;
            for (int i = 0; i < list.Length; i++)
            {
                int w = list[i].weight;
                if (w <= 0) continue;
                total += w;
            }

            if (total <= 0) return null;

            int roll = Api.World.Rand.Next(total);
            for (int i = 0; i < list.Length; i++)
            {
                int w = list[i].weight;
                if (w <= 0) continue;
                roll -= w;
                if (roll < 0) return list[i];
            }

            return list[0];
        }

        private SpawnEntry[] GetSpawnEntries()
        {
            if (entriesRaw != null && entriesRaw.Length > 0)
            {
                var parsed = new System.Collections.Generic.List<SpawnEntry>(entriesRaw.Length);

                for (int i = 0; i < entriesRaw.Length; i++)
                {
                    if (TryParseEntry(entriesRaw[i], out var e))
                    {
                        parsed.Add(e);
                    }
                }

                return parsed.ToArray();
            }

            if (string.IsNullOrWhiteSpace(entityCode)) return null;

            return new[]
            {
                new SpawnEntry
                {
                    entityCode = entityCode,
                    killId = killId,
                    weight = 1
                }
            };
        }

        private bool TryParseEntry(string raw, out SpawnEntry entry)
        {
            // entityCode|killId|weight
            entry = null;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            var parts = raw.Split('|');
            if (parts.Length < 1) return false;

            string code = parts[0]?.Trim();
            if (string.IsNullOrWhiteSpace(code)) return false;

            // Allow simplified format: "entityCode" only
            // If killId is omitted, don't set it at all (lets entity's own questtarget.targetId apply)
            string id = parts.Length >= 2 ? parts[1]?.Trim() : null;

            int weight = 1;
            if (parts.Length >= 3)
            {
                int.TryParse(parts[2]?.Trim(), out weight);
            }
            if (weight <= 0) weight = 0;

            entry = new SpawnEntry
            {
                entityCode = code,
                killId = id,
                weight = weight
            };

            return true;
        }

        private void TrySpawnOne(SpawnEntry entry)
        {
            if (entry == null) return;

            var world = Api?.World;
            if (world == null) return;

            var type = world.GetEntityType(new AssetLocation(entry.entityCode));
            if (type == null) return;

            Entity entity = world.ClassRegistry.CreateEntity(type);
            if (entity == null) return;

            double angle = world.Rand.NextDouble() * Math.PI * 2;
            double radius = world.Rand.NextDouble() * spawnRadius;

            double x = Pos.X + 0.5 + Math.Cos(angle) * radius;
            double z = Pos.Z + 0.5 + Math.Sin(angle) * radius;

            int surfaceY = Api.World.BlockAccessor.GetRainMapHeightAt((int)x, (int)z);
            double y = surfaceY + yOffset;

            entity.Pos.X = x;
            entity.Pos.Y = y;
            entity.Pos.Z = z;
            entity.Pos.Dimension = Pos.dimension;
            entity.Pos.SetFrom(entity.Pos);

            EntityBehaviorQuestTarget.SetSpawnerAnchor(entity, Pos);

            if (!string.IsNullOrWhiteSpace(entry.killId))
            {
                entity.WatchedAttributes.SetString("alegacyvsquest:killaction:targetid", entry.killId);
                entity.WatchedAttributes.MarkPathDirty("alegacyvsquest:killaction:targetid");
            }

            world.SpawnEntity(entity);
        }
    }
}
