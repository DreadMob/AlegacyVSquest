using System;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace VsQuest
{
    public class BlockEntityVoidRiftAnchor : BlockEntity
    {
        private const string AttrTier = "alegacyvsquest:voidrift:tier";
        private const string AttrAnchorId = "alegacyvsquest:voidrift:anchorId";
        private const string AttrYOffset = "alegacyvsquest:voidrift:yOffset";
        private const string AttrLeashRange = "alegacyvsquest:voidrift:leashRange";
        private const string AttrTrialKey = "alegacyvsquest:voidrift:trialKey";
        private const string AttrDeadUntil = "alegacyvsquest:voidrift:deadUntil";
        private const string AttrSpawnedEntityId = "alegacyvsquest:voidrift:spawnedEntityId";

        private const int PacketOpenGui = 3000;
        private const int PacketSave = 3001;

        private int tier;
        private string anchorId;
        private string storedTrialKey;
        private float yOffset;
        private float leashRange = 60f;

        // Per-anchor cooldown (independent of global state)
        private double deadUntilTotalHours;
        // Entity ID of the boss this anchor spawned (0 = none)
        private long spawnedEntityId;

        // Particle tick
        private long particleListenerId;
        // Periodic re-registration interval
        private long lastRegisterMs;

        // Collapse animation state
        private bool collapsing;
        private long collapseStartMs;
        private const int CollapseDurationMs = 2500;

        // Summoning animation state
        private bool summoning;
        private long summonStartMs;
        private const int SummonDurationMs = 15000; // 15 seconds
        private const float SummonActivationRange = 10f;
        private bool summonTriggeredThisCycle;

        private VoidRiftAnchorConfigGui dlg;

        /// <summary>
        /// The tier this anchor serves (1, 2, or 3).
        /// The system resolves which boss is active for this tier from the current rotation.
        /// </summary>
        public int Tier => tier;

        /// <summary>
        /// Per-anchor cooldown: total hours until boss can be resummoned.
        /// </summary>
        public double DeadUntilTotalHours => deadUntilTotalHours;

        /// <summary>
        /// Returns the trial key assigned to this anchor.
        /// Each anchor has its own boss assigned during rotation.
        /// </summary>
        public string GetActiveTrialKey()
        {
            return string.IsNullOrWhiteSpace(storedTrialKey) ? null : storedTrialKey;
        }

        /// <summary>
        /// Assign a random boss from the full pool to this anchor.
        /// Called during rotation or skip, or on first interaction if no boss assigned.
        /// </summary>
        public void AssignRandomBoss()
        {
            if (Api?.Side != EnumAppSide.Server) return;

            try
            {
                var system = Api.ModLoader.GetModSystem<HollowTrialSystem>();
                if (system == null) return;

                var allConfigs = system.GetAllConfigs();
                if (allConfigs == null || allConfigs.Count == 0) return;

                // Pick random boss from full pool (only used as fallback — normally boss is assigned by ReassignAllAnchors)
                var rand = Api.World.Rand;
                var chosen = allConfigs[rand.Next(allConfigs.Count)];
                storedTrialKey = chosen.trialKey;
                TryRegisterAnchor();
                MarkDirty(true);

                (Api as ICoreServerAPI)?.Logger?.Notification("[VoidRiftAnchor] Assigned boss '{0}' (tier {1}) to anchor at {2}", storedTrialKey, tier, Pos);
            }
            catch (Exception ex)
            {
                (Api as ICoreServerAPI)?.Logger?.Warning("[VoidRiftAnchor] AssignRandomBoss failed: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Clear the assigned boss (forces reassignment on next interaction).
        /// Called by system during rotation/skip.
        /// </summary>
        public void ClearAssignedBoss()
        {
            // Unregister from old trial key before clearing
            if (!string.IsNullOrWhiteSpace(storedTrialKey) && !string.IsNullOrWhiteSpace(anchorId) && Api?.Side == EnumAppSide.Server)
            {
                try
                {
                    var system = Api.ModLoader.GetModSystem<HollowTrialSystem>();
                    system?.UnsetAnchorPoint(storedTrialKey, anchorId, new BlockPos(Pos.X, Pos.Y, Pos.Z, Pos.dimension));
                }
                catch { }
            }

            storedTrialKey = null;
            spawnedEntityId = 0;
            MarkDirty(true);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api.Side == EnumAppSide.Server)
            {
                TryRegisterAnchor();
            }

            // Both sides: tick for particles
            particleListenerId = api.World.RegisterGameTickListener(OnTick, 250);
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            if (particleListenerId != 0 && Api?.World != null)
            {
                Api.World.UnregisterGameTickListener(particleListenerId);
                particleListenerId = 0;
            }
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);

            if (Api?.Side != EnumAppSide.Server) return;

            var attrs = Block?.Attributes;
            tier = attrs?["tier"].AsInt(tier) ?? tier;
            yOffset = attrs?["yOffset"].AsFloat(yOffset) ?? yOffset;

            TryRegisterAnchor();
            MarkDirty(true);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            tier = tree.GetInt(AttrTier, tier);
            anchorId = tree.GetString(AttrAnchorId, anchorId);
            storedTrialKey = tree.GetString(AttrTrialKey, storedTrialKey);
            yOffset = tree.GetFloat(AttrYOffset, yOffset);
            leashRange = tree.GetFloat(AttrLeashRange, leashRange);
            deadUntilTotalHours = tree.GetDouble(AttrDeadUntil, deadUntilTotalHours);
            spawnedEntityId = tree.GetLong(AttrSpawnedEntityId, spawnedEntityId);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetInt(AttrTier, tier);
            if (!string.IsNullOrWhiteSpace(anchorId)) tree.SetString(AttrAnchorId, anchorId);
            if (!string.IsNullOrWhiteSpace(storedTrialKey)) tree.SetString(AttrTrialKey, storedTrialKey);
            tree.SetFloat(AttrYOffset, yOffset);
            tree.SetFloat(AttrLeashRange, leashRange);
            tree.SetDouble(AttrDeadUntil, deadUntilTotalHours);
            tree.SetLong(AttrSpawnedEntityId, spawnedEntityId);
        }

        internal void OnInteract(IPlayer byPlayer)
        {
            if (byPlayer == null) return;

            if (Api.Side == EnumAppSide.Server)
            {
                var sp = byPlayer as IServerPlayer;
                if (sp == null || !sp.HasPrivilege(Privilege.controlserver)) return;

                var data = BuildConfigData();
                (Api as ICoreServerAPI).Network.SendBlockEntityPacket(sp, Pos, PacketOpenGui, SerializerUtil.Serialize(data));
                return;
            }
        }

        /// <summary>
        /// Called when any player right-clicks the anchor to summon the boss.
        /// Validates quest, cooldown, and boss state before starting the summoning animation.
        /// </summary>
        public void OnPlayerSummonRequest(IPlayer byPlayer)
        {
            if (byPlayer == null || Api?.Side != EnumAppSide.Server) return;

            var sp = byPlayer as IServerPlayer;
            if (sp == null) return;

            var sapi = Api as ICoreServerAPI;

            // Check if player has an active trial quest (any quest containing "trial-tier")
            bool hasTrialQuest = false;
            try
            {
                var questSystem = Api.ModLoader.GetModSystem<QuestSystem>();
                if (questSystem != null)
                {
                    var playerQuests = questSystem.GetPlayerQuests(sp.PlayerUID);
                    if (playerQuests != null)
                    {
                        foreach (var aq in playerQuests)
                        {
                            if (aq?.questId != null && aq.questId.Contains("trial-tier"))
                            {
                                hasTrialQuest = true;
                                break;
                            }
                        }
                    }
                }
            }
            catch { }

            if (!hasTrialQuest)
            {
                sapi.SendMessage(sp, Vintagestory.API.Config.GlobalConstants.GeneralChatGroup,
                    "<font color=\"#6B7280\">Разлом молчит. Тебе нужно испытание, чтобы пробудить его.</font>", EnumChatType.Notification);
                return;
            }

            // Check if boss is already alive
            string activeKey = GetActiveTrialKey();
            if (string.IsNullOrWhiteSpace(activeKey))
            {
                // No boss assigned — assign one now
                AssignRandomBoss();
                activeKey = GetActiveTrialKey();
                if (string.IsNullOrWhiteSpace(activeKey))
                {
                    sapi.SendMessage(sp, Vintagestory.API.Config.GlobalConstants.GeneralChatGroup,
                        "<font color=\"#6B7280\">Разлом пуст. Ни одно порождение не привязано к этому якорю.</font>", EnumChatType.Notification);
                    return;
                }
            }
            else
            {
                // Verify the stored key is still valid (might be stale after rotation/skip)
                try
                {
                    var system = Api.ModLoader.GetModSystem<HollowTrialSystem>();
                    if (system != null)
                    {
                        var cfg = system.FindConfig(activeKey);
                        if (cfg == null)
                        {
                            // Config no longer exists — reassign
                            AssignRandomBoss();
                            activeKey = GetActiveTrialKey();
                            if (string.IsNullOrWhiteSpace(activeKey))
                            {
                                sapi.SendMessage(sp, Vintagestory.API.Config.GlobalConstants.GeneralChatGroup,
                                    "<font color=\"#6B7280\">Разлом пуст. Ни одно порождение не привязано к этому якорю.</font>", EnumChatType.Notification);
                                return;
                            }
                        }
                    }
                }
                catch { }
            }

            try
            {
                // Check if THIS anchor's boss is still alive (by entity ID)
                if (spawnedEntityId > 0)
                {
                    var sapi3 = Api as ICoreServerAPI;
                    var existingEntity = sapi3?.World?.GetEntityById(spawnedEntityId);
                    if (existingEntity != null && existingEntity.Alive)
                    {
                        sapi.SendMessage(sp, Vintagestory.API.Config.GlobalConstants.GeneralChatGroup,
                            "<font color=\"#EF4444\">Порождение уже пробуждено. Оно ждёт тебя.</font>", EnumChatType.Notification);
                        return;
                    }
                    else
                    {
                        // Entity dead or despawned — clear reference
                        spawnedEntityId = 0;
                        MarkDirty(true);
                    }
                }
            }
            catch { }

            // Check per-anchor cooldown
            double nowHours = Api.World.Calendar.TotalHours;
            if (deadUntilTotalHours > nowHours)
            {
                double hoursLeft = deadUntilTotalHours - nowHours;
                sapi.SendMessage(sp, Vintagestory.API.Config.GlobalConstants.GeneralChatGroup,
                    $"<font color=\"#A78BFA\">Разлом восстанавливается... Пустота вернётся через {hoursLeft:0.0} ч.</font>", EnumChatType.Notification);
                return;
            }

            // Already summoning
            if (summoning)
            {
                sapi.SendMessage(sp, Vintagestory.API.Config.GlobalConstants.GeneralChatGroup,
                    "<font color=\"#A78BFA\">Разлом уже разрывается... Приготовься.</font>", EnumChatType.Notification);
                return;
            }

            // Start the summoning animation
            StartSummoning(Api.World.ElapsedMilliseconds);
        }

        public override void OnReceivedServerPacket(int packetid, byte[] bytes)
        {
            if (packetid != PacketOpenGui) return;

            var data = SerializerUtil.Deserialize<VoidRiftAnchorConfigData>(bytes);
            var capi = Api as Vintagestory.API.Client.ICoreClientAPI;

            if (dlg == null || !dlg.IsOpened())
            {
                dlg = new VoidRiftAnchorConfigGui(Pos, capi);
                dlg.Data = data;
                dlg.TryOpen();
                dlg.OnClosed += () => { dlg = null; };
            }
            else
            {
                dlg.UpdateFromServer(data);
            }
        }

        public override void OnReceivedClientPacket(IPlayer fromPlayer, int packetid, byte[] bytes)
        {
            var sp = fromPlayer as IServerPlayer;
            if (sp == null || !sp.HasPrivilege(Privilege.controlserver)) return;
            if (packetid != PacketSave) return;

            var data = SerializerUtil.Deserialize<VoidRiftAnchorConfigData>(bytes);
            if (data == null) return;

            tier = data.tier;
            yOffset = data.yOffset;
            leashRange = data.leashRange > 0 ? data.leashRange : 60f;

            TryRegisterAnchor();
            MarkDirty(true);

            // Send refreshed data back
            var refreshed = BuildConfigData();
            (Api as ICoreServerAPI).Network.SendBlockEntityPacket(sp, Pos, PacketOpenGui, SerializerUtil.Serialize(refreshed));
        }

        private VoidRiftAnchorConfigData BuildConfigData()
        {
            return new VoidRiftAnchorConfigData
            {
                tier = tier,
                yOffset = yOffset,
                leashRange = leashRange
            };
        }

        internal void OnRemovedServerSide()
        {
            if (Api?.Side != EnumAppSide.Server) return;

            if (string.IsNullOrWhiteSpace(anchorId))
            {
                anchorId = $"alegacyvsquest:voidrift:{Pos.dimension}:{Pos.X}:{Pos.Y}:{Pos.Z}";
            }

            try
            {
                var system = Api.ModLoader.GetModSystem<HollowTrialSystem>();
                system?.UnsetAnchorPointByAnchorId(anchorId);
            }
            catch (Exception ex)
            {
                Api?.Logger?.Error($"[VoidRiftAnchor] Exception in OnRemovedServerSide: {ex}");
            }
        }

        /// <summary>
        /// Trigger collapse animation when boss dies. Called by HollowTrialSystem on death.
        /// </summary>
        public void TriggerCollapseAnimation()
        {
            if (Api?.Side != EnumAppSide.Server) return;
            collapsing = true;
            collapseStartMs = Api.World.ElapsedMilliseconds;
        }

        /// <summary>
        /// Get the entity ID of the boss this anchor spawned (for death notification matching).
        /// </summary>
        public long GetSpawnedEntityId() => spawnedEntityId;

        /// <summary>
        /// Called when this anchor's boss is killed. Sets per-anchor cooldown.
        /// </summary>
        public void OnBossKilled(double deadUntilHours)
        {
            deadUntilTotalHours = deadUntilHours;
            spawnedEntityId = 0;
            MarkDirty(true);
        }

        public void SetTrialKey(string newTrialKey)
        {
            storedTrialKey = newTrialKey;
            TryRegisterAnchor();
            MarkDirty(true);
        }

        private void TryRegisterAnchor()
        {
            if (Api?.Side != EnumAppSide.Server) return;

            if (string.IsNullOrWhiteSpace(anchorId))
            {
                anchorId = $"alegacyvsquest:voidrift:{Pos.dimension}:{Pos.X}:{Pos.Y}:{Pos.Z}";
                MarkDirty(true);
            }

            // Register under the stored trial key, or under a placeholder if no boss assigned yet
            string activeKey = GetActiveTrialKey();
            string registerKey = !string.IsNullOrWhiteSpace(activeKey) ? activeKey : "__unassigned__";

            try
            {
                var system = Api.ModLoader.GetModSystem<HollowTrialSystem>();
                system?.SetAnchorPoint(registerKey, anchorId, new BlockPos(Pos.X, Pos.Y, Pos.Z, Pos.dimension), yOffset);
            }
            catch (Exception ex)
            {
                Api?.Logger?.Error($"[VoidRiftAnchor] Exception in TryRegisterAnchor: {ex}");
            }
        }

        private void OnTick(float dt)
        {
            if (Api?.World == null) return;
            if (Api.Side != EnumAppSide.Server) return;

            long nowMs = Api.World.ElapsedMilliseconds;

            // Periodic re-registration (every 60s) to keep state in sync after skip/rotation
            if (nowMs - lastRegisterMs > 60000)
            {
                lastRegisterMs = nowMs;
                TryRegisterAnchor();
            }

            // Collapsing animation (boss died)
            if (collapsing)
            {
                long elapsed = nowMs - collapseStartMs;
                if (elapsed >= CollapseDurationMs)
                {
                    collapsing = false;
                    return;
                }
                SpawnCollapseParticles(elapsed / (double)CollapseDurationMs);
                return;
            }

            // Summoning animation (boss spawning)
            if (summoning)
            {
                long elapsed = nowMs - summonStartMs;
                if (elapsed >= SummonDurationMs)
                {
                    summoning = false;
                    summonTriggeredThisCycle = true;
                    // Final explosion burst
                    SpawnSummonExplosion();
                    // Signal system to actually spawn the boss now
                    RequestBossSpawn();
                    return;
                }
                SpawnSummoningParticles(elapsed / (double)SummonDurationMs);
                // Sound during summoning (every 2 sec, pitch increases)
                if (elapsed % 2000 < 300)
                {
                    float pitch = 0.6f + (float)(elapsed / (double)SummonDurationMs) * 0.8f;
                    Api.World.PlaySoundAt(new AssetLocation("game:sounds/effect/translocate-active"),
                        Pos.X + 0.5, Pos.Y + 1, Pos.Z + 0.5, null, true, 20f, pitch * 0.4f);
                }
                return;
            }

            // Determine state: alive / dead / waiting
            bool isAlive = false;
            bool bossExists = false;

            if (spawnedEntityId > 0)
            {
                try
                {
                    var sapi3 = Api as ICoreServerAPI;
                    var entity = sapi3?.World?.GetEntityById(spawnedEntityId);
                    bossExists = entity != null;
                    isAlive = entity != null && entity.Alive;

                    if (!isAlive && spawnedEntityId > 0)
                    {
                        // Entity gone or dead — clear reference (only once)
                        spawnedEntityId = 0;
                        MarkDirty(true);
                    }
                }
                catch { }
            }

            if (isAlive)
            {
                SpawnAliveParticles();
                TryPlayAmbientSound(true);
                summonTriggeredThisCycle = false;
            }
            else if (!summonTriggeredThisCycle)
            {
                // Boss doesn't exist — check if on cooldown
                double nowHoursCheck = Api.World.Calendar.TotalHours;
                bool onCooldown = deadUntilTotalHours > nowHoursCheck;

                if (onCooldown)
                {
                    SpawnDormantParticles();
                    TryPlayAmbientSound(false);
                }
                else
                {
                    SpawnReadyParticles();
                    TryPlayAmbientSound(false);
                }
            }
            else
            {
                SpawnDormantParticles();
                TryPlayAmbientSound(false);
            }
        }

        private void CheckProximityForSummon(long nowMs)
        {
            // Proximity auto-spawn disabled — boss is summoned via RMB click on anchor
        }

        private void StartSummoning(long nowMs)
        {
            summoning = true;
            summonStartMs = nowMs;

            // Initial activation sound
            Api.World.PlaySoundAt(new AssetLocation("game:sounds/effect/translocate-breakdimension"),
                Pos.X + 0.5, Pos.Y + 1, Pos.Z + 0.5, null, true, 32f, 0.5f);

            // Initial burst
            var sapi = Api as ICoreServerAPI;
            if (sapi != null)
            {
                ParticleUtils.SpawnAuraRing(sapi, new Vec3d(Pos.X + 0.5, Pos.Y + 1, Pos.Z + 0.5),
                    8f, ParticleUtils.Colors.Void, 30, 0.8f);
            }
        }

        private void SpawnSummoningParticles(double progress)
        {
            // progress: 0.0 → 1.0 over 15 seconds
            // Particles start far away and large, converge to center and shrink
            double cx = Pos.X + 0.5;
            double cy = Pos.Y + 1.5;
            double cz = Pos.Z + 0.5;

            var rand = Api.World.Rand;
            float radius = (float)(8.0 * (1.0 - progress * 0.9)); // 8 → 0.8 blocks
            float size = (float)(1.0 * (1.0 - progress * 0.7)); // 1.0 → 0.3
            int count = 4 + (int)(progress * 10); // 4 → 14 particles per tick
            float speed = 0.2f + (float)progress * 0.6f; // faster as they converge

            // Color shifts from deep purple to bright violet
            int alpha = (int)(180 + progress * 75);
            int r = (int)(100 + progress * 67);
            int g = (int)(50 + progress * 89);
            int b = (int)(180 + progress * 70);
            int color = ColorUtil.ToRgba(alpha, r, g, b);

            for (int i = 0; i < count; i++)
            {
                double angle = rand.NextDouble() * Math.PI * 2;
                // Rotating offset based on time
                double timeAngle = (Api.World.ElapsedMilliseconds / 500.0) + (i * Math.PI * 2 / count);
                double x = cx + Math.Cos(angle + timeAngle) * radius;
                double z = cz + Math.Sin(angle + timeAngle) * radius;
                double y = cy + (rand.NextDouble() - 0.5) * 2.0 * (1.0 - progress);

                // Velocity toward center
                float vx = (float)((cx - x) * speed);
                float vz = (float)((cz - z) * speed);
                float vy = (float)((cy - y) * speed * 0.5);

                Api.World.SpawnParticles(new SimpleParticleProperties(
                    1, 2, color,
                    new Vec3d(x, y, z), new Vec3d(x + 0.05, y + 0.05, z + 0.05),
                    new Vec3f(vx, vy, vz), new Vec3f(vx * 1.2f, vy + 0.05f, vz * 1.2f),
                    0.6f + (float)progress * 0.4f, 0f,
                    size * 0.8f, size,
                    EnumParticleModel.Quad
                ));
            }

            // Inner glow at center (grows with progress)
            if (progress > 0.3)
            {
                float innerSize = (float)(progress - 0.3) * 2f;
                Api.World.SpawnParticles(new SimpleParticleProperties(
                    2, 4, ColorUtil.ToRgba(255, 167, 139, 250),
                    new Vec3d(cx - 0.2, cy - 0.2, cz - 0.2),
                    new Vec3d(cx + 0.2, cy + 0.2, cz + 0.2),
                    new Vec3f(0, 0.05f, 0), new Vec3f(0, 0.15f, 0),
                    0.4f, -0.02f, innerSize * 0.5f, innerSize
                ));
            }
        }

        private void SpawnSummonExplosion()
        {
            var sapi = Api as ICoreServerAPI;
            if (sapi == null) return;

            Vec3d center = new Vec3d(Pos.X + 0.5, Pos.Y + 1.5, Pos.Z + 0.5);

            // Big shockwave
            ParticleUtils.SpawnShockwave(sapi, center, 6f, ParticleUtils.Colors.Void, 40, 0.8f);
            // Shadow explosion
            ParticleUtils.SpawnShadowExplosion(sapi, center, 4f, 3);
            // Pillar of light
            ParticleUtils.SpawnPillar(sapi, center, 5f, 1.5f, ParticleUtils.Colors.ArcaneBright, 30);
            // Spiral
            ParticleUtils.SpawnSpiral(sapi, center, 3f, 4f, ParticleUtils.Colors.Void, 30, 0.5f);

            // Explosion sound
            Api.World.PlaySoundAt(new AssetLocation("game:sounds/effect/translocate-breakdimension"),
                center.X, center.Y, center.Z, null, true, 48f, 0.8f);
            Api.World.PlaySoundAt(new AssetLocation("game:sounds/weather/lightning-verynear"),
                center.X, center.Y, center.Z, null, true, 48f, 0.4f);
        }

        private void RequestBossSpawn()
        {
            // Trigger the system to spawn the boss at this anchor
            if (Api?.Side != EnumAppSide.Server) return;

            var sapi2 = Api as ICoreServerAPI;

            try
            {
                string activeKey = GetActiveTrialKey();
                if (string.IsNullOrWhiteSpace(activeKey))
                {
                    sapi2?.Logger?.Warning("[VoidRiftAnchor] RequestBossSpawn: no active trial key");
                    return;
                }

                var system = Api.ModLoader.GetModSystem<HollowTrialSystem>();
                if (system == null) { sapi2?.Logger?.Warning("[VoidRiftAnchor] RequestBossSpawn: system null"); return; }

                var cfg = system.FindConfig(activeKey);
                if (cfg == null) { sapi2?.Logger?.Warning("[VoidRiftAnchor] RequestBossSpawn: config not found for '{0}'", activeKey); return; }

                // Ensure anchor is registered before spawning
                TryRegisterAnchor();

                // Find nearest player
                var sapi = Api as ICoreServerAPI;
                var players = sapi.World.AllOnlinePlayers;
                IServerPlayer nearestPlayer = null;
                double nearestDistSq = double.MaxValue;

                for (int i = 0; i < players.Length; i++)
                {
                    if (players[i] is not IServerPlayer sp) continue;
                    var pe = sp.Entity;
                    if (pe?.Pos == null) continue;

                    double dx = pe.Pos.X - (Pos.X + 0.5);
                    double dy = pe.Pos.Y - (Pos.Y + 1.0);
                    double dz = pe.Pos.Z - (Pos.Z + 0.5);
                    double distSq = dx * dx + dy * dy + dz * dz;

                    if (distSq < nearestDistSq)
                    {
                        nearestDistSq = distSq;
                        nearestPlayer = sp;
                    }
                }

                // Use block position directly for spawn point
                var point = new Vintagestory.API.MathTools.Vec3d(Pos.X + 0.5, Pos.Y + yOffset + 1, Pos.Z + 0.5);
                var anchor = new HollowTrialSystem.HollowTrialAnchorPoint
                {
                    anchorId = anchorId ?? $"alegacyvsquest:voidrift:{Pos.dimension}:{Pos.X}:{Pos.Y}:{Pos.Z}",
                    x = Pos.X,
                    y = Pos.Y,
                    z = Pos.Z,
                    dim = Pos.dimension,
                    yOffset = yOffset
                };

                sapi2?.Logger?.Notification("[VoidRiftAnchor] Calling SpawnBossFromAnchorDirect: key={0}, pos={1}, tier={2}", activeKey, point, tier);
                long entityId = system.SpawnBossFromAnchorDirect(activeKey, point, Pos.dimension, anchor, nearestPlayer, tier > 0 ? tier : 1);

                if (entityId > 0)
                {
                    spawnedEntityId = entityId;
                    MarkDirty(true);
                }
                else
                {
                    // Spawn failed — reset summon flag so player can try again
                    summonTriggeredThisCycle = false;
                    sapi2?.Logger?.Warning("[VoidRiftAnchor] SpawnBossFromAnchorDirect returned 0 for '{0}'", activeKey);
                }
            }
            catch (Exception ex)
            {
                Api?.Logger?.Error($"[VoidRiftAnchor] RequestBossSpawn failed: {ex}");
            }
        }

        private long lastSoundMs;
        private static readonly AssetLocation VoidSoundLoc = new AssetLocation("alegacyvsquest:sounds/void");

        private void TryPlayAmbientSound(bool isAlive)
        {
            long now = Api.World.ElapsedMilliseconds;
            // Repeat sound on a loop: every 1.9s when alive, every ~5s when dormant
            // (adjust interval to match your wav duration for seamless loop)
            int intervalMs = isAlive ? 1900 : 5000;
            if (now - lastSoundMs < intervalMs) return;
            lastSoundMs = now;

            double cx = Pos.X + 0.5;
            double cy = Pos.Y + 1.0;
            double cz = Pos.Z + 0.5;

            float volume = isAlive ? 0.7f : 0.25f;
            float range = isAlive ? 14f : 10f;

            Api.World.PlaySoundAt(VoidSoundLoc, cx, cy, cz, null, true, range, volume);
        }

        private void SpawnAliveParticles()
        {
            // Bright purple particles rising 3-4 blocks
            double cx = Pos.X + 0.5;
            double cy = Pos.Y + 1.0;
            double cz = Pos.Z + 0.5;

            Api.World.SpawnParticles(new SimpleParticleProperties(
                minQuantity: 4, maxQuantity: 8,
                color: ColorUtil.ToRgba(255, 167, 139, 250),
                minPos: new Vec3d(cx - 0.4, cy, cz - 0.4),
                maxPos: new Vec3d(cx + 0.4, cy + 0.5, cz + 0.4),
                minVelocity: new Vec3f(-0.05f, 0.4f, -0.05f),
                maxVelocity: new Vec3f(0.05f, 0.7f, 0.05f),
                lifeLength: 4.0f,
                gravityEffect: -0.01f,
                minSize: 0.6f,
                maxSize: 1.35f
            ));
        }

        private void SpawnDormantParticles()
        {
            // Dim particles rising 1-2 blocks (30% opacity) — boss on cooldown
            double cx = Pos.X + 0.5;
            double cy = Pos.Y + 1.0;
            double cz = Pos.Z + 0.5;

            Api.World.SpawnParticles(new SimpleParticleProperties(
                minQuantity: 1, maxQuantity: 3,
                color: ColorUtil.ToRgba(80, 167, 139, 250),
                minPos: new Vec3d(cx - 0.3, cy, cz - 0.3),
                maxPos: new Vec3d(cx + 0.3, cy + 0.3, cz + 0.3),
                minVelocity: new Vec3f(-0.02f, 0.15f, -0.02f),
                maxVelocity: new Vec3f(0.02f, 0.3f, 0.02f),
                lifeLength: 2.5f,
                gravityEffect: -0.005f,
                minSize: 0.45f,
                maxSize: 0.9f
            ));
        }

        /// <summary>
        /// Boss is available for summon — bright pulsing particles, more active than dormant.
        /// </summary>
        private void SpawnReadyParticles()
        {
            double cx = Pos.X + 0.5;
            double cy = Pos.Y + 1.0;
            double cz = Pos.Z + 0.5;

            // Bright purple particles rising higher, more quantity, pulsing
            Api.World.SpawnParticles(new SimpleParticleProperties(
                minQuantity: 3, maxQuantity: 6,
                color: ColorUtil.ToRgba(220, 180, 100, 255),
                minPos: new Vec3d(cx - 0.5, cy, cz - 0.5),
                maxPos: new Vec3d(cx + 0.5, cy + 0.5, cz + 0.5),
                minVelocity: new Vec3f(-0.04f, 0.3f, -0.04f),
                maxVelocity: new Vec3f(0.04f, 0.6f, 0.04f),
                lifeLength: 3.0f,
                gravityEffect: -0.015f,
                minSize: 0.5f,
                maxSize: 1.1f
            ));

            // Small orbiting sparks around the anchor
            var rand = Api.World.Rand;
            double angle = (Api.World.ElapsedMilliseconds / 400.0) % (Math.PI * 2);
            double ox = cx + Math.Cos(angle) * 0.8;
            double oz = cz + Math.Sin(angle) * 0.8;

            Api.World.SpawnParticles(new SimpleParticleProperties(
                1, 2, ColorUtil.ToRgba(255, 200, 150, 255),
                new Vec3d(ox - 0.1, cy + 0.5, oz - 0.1),
                new Vec3d(ox + 0.1, cy + 1.0, oz + 0.1),
                new Vec3f(0, 0.1f, 0), new Vec3f(0, 0.2f, 0),
                0.8f, -0.01f, 0.3f, 0.5f
            ));
        }

        private void SpawnCollapseParticles(double progress)
        {
            // Particles converge inward toward center
            double cx = Pos.X + 0.5;
            double cy = Pos.Y + 1.0;
            double cz = Pos.Z + 0.5;

            // Spawn at radius which shrinks over time
            double radius = 2.0 * (1.0 - progress);
            var rand = Api.World.Rand;

            for (int i = 0; i < 8; i++)
            {
                double angle = rand.NextDouble() * Math.PI * 2;
                double x = cx + Math.Cos(angle) * radius;
                double z = cz + Math.Sin(angle) * radius;
                double y = cy + rand.NextDouble() * 2.0;

                // Velocity points toward center (negative direction from outside)
                float vx = (float)((cx - x) * 0.4);
                float vz = (float)((cz - z) * 0.4);

                Api.World.SpawnParticles(new SimpleParticleProperties(
                    minQuantity: 1, maxQuantity: 2,
                    color: ColorUtil.ToRgba(200, 167, 139, 250),
                    minPos: new Vec3d(x, y, z),
                    maxPos: new Vec3d(x + 0.05, y + 0.05, z + 0.05),
                    minVelocity: new Vec3f(vx, -0.1f, vz),
                    maxVelocity: new Vec3f(vx, 0f, vz),
                    lifeLength: 1.0f,
                    gravityEffect: 0f,
                    minSize: 0.6f,
                    maxSize: 1.2f
                ));
            }
        }
    }
}
