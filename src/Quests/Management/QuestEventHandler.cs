using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsQuest
{
    public class QuestEventHandler
    {
        private readonly BossCombatService bossCombatService = new BossCombatService();

        private const string SummonedByEntityIdKey = "alegacyvsquest:bosssummonritual:summonedByEntityId";

        private readonly Dictionary<string, Quest> questRegistry;
        private readonly IQuestPersistenceManager persistenceManager;
        private readonly ICoreServerAPI sapi;
        private readonly IQuestEventDispatcher eventDispatcher;
        private QuestSystem questSystem;
        private Systems.Database.VsQuestSyncService dbSyncService;

        private void ApplyCoreConfig()
        {
            if (questSystem?.CoreConfig != null)
            {
                bossCombatService.ApplyConfig(questSystem.CoreConfig);
            }
        }

        public QuestEventHandler(IQuestPersistenceManager persistenceManager, ICoreServerAPI sapi, IQuestEventDispatcher eventDispatcher = null)
        {
            this.questRegistry = QuestRegistryService.QuestRegistry;
            this.persistenceManager = persistenceManager;
            this.sapi = sapi;
            this.eventDispatcher = eventDispatcher ?? new ActiveQuestEventDispatcher(sapi, new InteractPositionCache());
        }

        public void SetDbSyncService(Systems.Database.VsQuestSyncService syncService)
        {
            dbSyncService = syncService;
        }

        public void RegisterEventHandlers()
        {
            questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();

            ApplyCoreConfig();

            sapi.Event.GameWorldSave += OnGameWorldSave;
            sapi.Event.PlayerJoin += OnPlayerJoin;
            sapi.Event.PlayerDisconnect += OnPlayerDisconnect;
            sapi.Event.OnEntityDeath += OnEntityDeath;
            sapi.Event.DidBreakBlock += OnBlockBroken;
            sapi.Event.DidPlaceBlock += OnBlockPlaced;
            sapi.Event.RegisterGameTickListener(OnQuestTick, 5000);
        }

        private void OnPlayerJoin(IServerPlayer byPlayer)
        {
            if (byPlayer == null || byPlayer.Entity == null) return;

            // Delay to give vanilla ModJournal time to load the player's journal.
            sapi.Event.RegisterCallback(_ =>
            {
                try
                {
                    var epl = byPlayer.Entity;
                    if (epl?.Stats != null)
                    {
                        epl.Stats.Remove("walkspeed", "alegacyvsquest");
                        epl.Stats.Remove("walkspeed", "alegacyvsquest:bossgrab");
                        epl.Stats.Remove("walkspeed", "alegacyvsquest:bosshook");
                        epl.walkSpeed = epl.Stats.GetBlended("walkspeed");
                    }
                }
                catch (Exception ex)
                {
                    sapi.Logger.Warning("[QuestEventHandler] Failed to reset player walkspeed stats on join: {0}", ex.Message);
                }

                var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
                QuestSystemAdminUtils.ForgetOutdatedQuestsForPlayer(questSystem, byPlayer, sapi);
            }, 1000);
        }

        private void OnGameWorldSave()
        {
            persistenceManager.SaveAllPlayerQuests();
        }

        private void OnPlayerDisconnect(IServerPlayer byPlayer)
        {
            persistenceManager.UnloadPlayerQuests(byPlayer.PlayerUID);
            QuestTickUtil.ClearPlayerCache(byPlayer.PlayerUID);
            WalkDistanceObjective.ClearPlayerCache(byPlayer.PlayerUID);
        }

        private void OnEntityDeath(Entity entity, DamageSource damageSource)
        {
            var credited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                long summonedBy = 0;
                try
                {
                    summonedBy = entity?.WatchedAttributes?.GetLong(SummonedByEntityIdKey, 0) ?? 0;
                }
                catch (Exception ex)
                {
                    sapi.Logger.Warning("[QuestEventHandler] Failed to get SummonedByEntityId: {0}", ex.Message);
                    summonedBy = 0;
                }

                if (summonedBy != 0)
                {
                    sapi.Event.RegisterCallback(_ =>
                    {
                        try
                        {
                            if (entity == null) return;
                            if (entity.Alive) return;

                            sapi.World.DespawnEntity(entity, new EntityDespawnData { Reason = EnumDespawnReason.Removed });
                        }
                        catch (Exception ex)
                        {
                            sapi.Logger.Warning("[QuestEventHandler] Failed to despawn summoned entity: {0}", ex.Message);
                        }
                    }, 2000);
                }
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning("[QuestEventHandler] Failed to handle summoned entity death: {0}", ex.Message);
            }

            try
            {
                var killer = damageSource?.SourceEntity ?? damageSource?.CauseEntity;
                if (killer != null && killer.Alive && killer != entity && bossCombatService.IsBossEntity(killer))
                {
                    bossCombatService.TryHealBossOnKill(killer);
                }
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning("[QuestEventHandler] Failed to handle boss heal on kill: {0}", ex.Message);
            }

            if (damageSource?.SourceEntity is EntityPlayer player && !string.IsNullOrWhiteSpace(player.PlayerUID))
            {
                credited.Add(player.PlayerUID);
            }

            if (bossCombatService.IsBossEntity(entity) && bossCombatService.IsFinalBossStage(entity))
            {
                try
                {
                    var wa = entity.WatchedAttributes;
                    var dmgTree = wa.GetTreeAttribute(EntityBehaviorBossCombatMarker.BossCombatDamageByPlayerKey);
                    if (dmgTree != null)
                    {
                        double totalDmg = 0;
                        var attackers = wa.GetStringArray(EntityBehaviorBossCombatMarker.BossCombatAttackersKey, new string[0]) ?? new string[0];
                        foreach (var uid in attackers)
                        {
                            totalDmg += dmgTree.GetDouble(uid);
                        }

                        if (totalDmg > 0)
                        {
                            float maxHp = 1f;
                            var healthBh = entity.GetBehavior<EntityBehaviorHealth>();
                            if (healthBh != null)
                            {
                                maxHp = healthBh.MaxHealth;
                            }

                            var creditedUids = bossCombatService.GetCreditedPlayerUids(entity, totalDmg, maxHp);
                            foreach (var uid in creditedUids)
                            {
                                credited.Add(uid);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    sapi.Logger.Warning("[QuestEventHandler] Failed to calculate boss death credits: {0}", ex.Message);
                }
            }

            var creditedPlayers = new List<IServerPlayer>();

            foreach (var uid in credited)
            {
                if (string.IsNullOrWhiteSpace(uid)) continue;

                IPlayer iPlayer = null;
                try
                {
                    iPlayer = sapi?.World?.PlayerByUid(uid);
                }
                catch (Exception ex)
                {
                    sapi.Logger.Warning("[QuestEventHandler] Failed to get player by UID {0}: {1}", uid, ex.Message);
                    iPlayer = null;
                }

                var epl = iPlayer?.Entity as EntityPlayer;
                if (epl == null) continue;

                var serverPlayer = iPlayer as IServerPlayer;
                if (serverPlayer != null)
                {
                    creditedPlayers.Add(serverPlayer);
                }

                var quests = persistenceManager.GetPlayerQuests(uid);
                Systems.Management.DeathHandler.HandleEntityDeath(sapi, quests, epl, entity);
            }

            bossCombatService.AnnounceBossDeath(sapi, entity, creditedPlayers, damageSource);

            // Sync boss kills to MySQL
            if (dbSyncService != null && bossCombatService.IsBossEntity(entity) && bossCombatService.IsFinalBossStage(entity))
            {
                var qt = entity.GetBehavior<EntityBehaviorQuestTarget>();
                if (qt != null && !string.IsNullOrWhiteSpace(qt.TargetId))
                {
                    foreach (var creditedPlayer in creditedPlayers)
                    {
                        if (creditedPlayer == null) continue;
                        dbSyncService.QueueBossKill(creditedPlayer.PlayerUID, creditedPlayer.PlayerName, qt.TargetId);
                    }
                }
            }

            var victimPlayer = entity as EntityPlayer;
            if (victimPlayer != null)
            {
                var killer = damageSource?.SourceEntity ?? damageSource?.CauseEntity;
                if (killer != null && killer.IsQuestBoss())
                {
                    var serverVictim = victimPlayer.Player as IServerPlayer;
                    if (serverVictim != null)
                    {
                        var qs = sapi.ModLoader.GetModSystem<QuestSystem>();
                        if (qs?.Config == null || qs.Config.ShowCustomBossDeathMessage)
                        {
                            BossKillAnnouncementUtil.AnnouncePlayerKilledByBoss(sapi, serverVictim, killer);
                        }
                    }
                }
            }
        }

        private void OnBlockBroken(IServerPlayer byPlayer, int blockId, BlockSelection blockSel)
        {
            if (byPlayer == null || blockSel == null)
            {
                return;
            }

            var blockCode = sapi.World.GetBlock(blockId)?.Code.ToString();
            var position = new int[] { blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z };
            var playerQuests = persistenceManager.GetPlayerQuests(byPlayer.PlayerUID);
            if (playerQuests == null || playerQuests.Count == 0) return;

            QuestSystem qs = null;
            try
            {
                qs = sapi.ModLoader.GetModSystem<QuestSystem>();
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning("[QuestEventHandler] Failed to get QuestSystem in OnBlockBroken: {0}", ex.Message);
                qs = null;
            }

            int count = playerQuests.Count;
            for (int i = 0; i < count; i++)
            {
                if (i >= playerQuests.Count) break;
                var quest = playerQuests[i];
                if (quest == null || string.IsNullOrWhiteSpace(quest.questId)) continue;

                try
                {
                    if (qs?.QuestRegistry == null) continue;
                    if (!qs.QuestRegistry.TryGetValue(quest.questId, out var questDef) || questDef == null) continue;
                    if (questDef.blockBreakObjectives == null || questDef.blockBreakObjectives.Count == 0) continue;
                }
                catch (Exception ex)
                {
                    sapi.Logger.Warning("[QuestEventHandler] Failed to check block break objectives for quest {0}: {1}", quest.questId, ex.Message);
                }

                eventDispatcher.OnBlockBroken(quest, blockCode, position, byPlayer, null);
            }
        }

        private void OnBlockPlaced(IServerPlayer byPlayer, int oldBlockId, BlockSelection blockSel, ItemStack itemstack)
        {
            if (byPlayer == null || blockSel == null)
            {
                return;
            }

            var blockCode = sapi.World.BlockAccessor.GetBlock(blockSel.Position)?.Code.ToString();
            var position = new int[] { blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z };
            var playerQuests = persistenceManager.GetPlayerQuests(byPlayer.PlayerUID);
            if (playerQuests == null || playerQuests.Count == 0) return;

            QuestSystem qs = null;
            try
            {
                qs = sapi.ModLoader.GetModSystem<QuestSystem>();
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning("[QuestEventHandler] Failed to get QuestSystem in OnBlockPlaced: {0}", ex.Message);
                qs = null;
            }

            int count = playerQuests.Count;
            for (int i = 0; i < count; i++)
            {
                if (i >= playerQuests.Count) break;
                var quest = playerQuests[i];
                if (quest == null || string.IsNullOrWhiteSpace(quest.questId)) continue;

                try
                {
                    if (qs?.QuestRegistry == null) continue;
                    if (!qs.QuestRegistry.TryGetValue(quest.questId, out var questDef) || questDef == null) continue;
                    if (questDef.blockPlaceObjectives == null || questDef.blockPlaceObjectives.Count == 0) continue;
                }
                catch (Exception ex)
                {
                    sapi.Logger.Warning("[QuestEventHandler] Failed to check block place objectives for quest {0}: {1}", quest.questId, ex.Message);
                }

                eventDispatcher.OnBlockPlaced(quest, blockCode, position, byPlayer, null);
            }
        }

        private void OnQuestTick(float dt)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                OnQuestTickInternal(dt);
            }
            finally
            {
                sw.Stop();
                if (sw.ElapsedMilliseconds > 10)
                {
                    sapi.Logger.Warning("[QuestLagDebug] OnQuestTick took {0}ms for {1} players", sw.ElapsedMilliseconds, sapi.World.AllOnlinePlayers.Length);
                }
            }
        }

        private void OnQuestTickInternal(float dt)
        {
            questSystem ??= sapi.ModLoader.GetModSystem<QuestSystem>();
            if (questSystem == null) return;

            var players = sapi.World.AllOnlinePlayers;
            if (players == null || players.Length == 0) return;

            double missingLogThrottle = 1.0 / 60.0;
            double passiveThrottle = 1.0 / 3600.0;
            try
            {
                var cfg = questSystem.CoreConfig?.QuestTick;
                if (cfg != null)
                {
                    if (cfg.MissingQuestLogThrottleHours > 0) missingLogThrottle = cfg.MissingQuestLogThrottleHours;
                    if (cfg.PassiveCompletionThrottleHours > 0) passiveThrottle = cfg.PassiveCompletionThrottleHours;
                }
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning("[QuestEventHandler] Failed to read QuestTick config: {0}", ex.Message);
                missingLogThrottle = 1.0 / 60.0;
                passiveThrottle = 1.0 / 3600.0;
            }

            QuestTickUtil.HandleQuestTick(dt, questRegistry, questSystem.ActionObjectiveRegistry, players, persistenceManager.GetPlayerQuests, uid => persistenceManager.MarkDirty(uid), sapi, missingLogThrottle, passiveThrottle);
        }

        public void HandleVanillaBlockInteract(IServerPlayer player, VanillaBlockInteractMessage message)
        {
            if (player == null || message == null)
            {
                return;
            }

            if (message.BlockCode == "alegacyvsquest:cooldownplaceholder")
            {
                return;
            }

            var playerQuests = persistenceManager.GetPlayerQuests(player.PlayerUID);
            if (playerQuests == null || playerQuests.Count == 0) return;

            QuestSystem qs = questSystem ??= sapi.ModLoader.GetModSystem<QuestSystem>();
            int[] position = new int[] { message.Position.X, message.Position.Y, message.Position.Z };

            for (int i = 0; i < playerQuests.Count; i++)
            {
                var activeQuest = playerQuests[i];
                if (activeQuest == null || string.IsNullOrWhiteSpace(activeQuest.questId)) continue;

                if (qs?.QuestRegistry == null || !qs.QuestRegistry.TryGetValue(activeQuest.questId, out var questDef) || questDef == null) continue;

                // Get action objectives from current stage using centralized method
                var actionObjectivesToCheck = questDef.GetActionObjectives(activeQuest.currentStageIndex);
                
                // Get interact objectives from current stage (no centralized method yet, using GetStage)
                var currentStage = questDef.GetStage(activeQuest.currentStageIndex);
                var interactObjectivesToCheck = currentStage?.interactObjectives ?? questDef.interactObjectives;

                // Проверка: нужен ли этому квесту ивент взаимодействия
                bool needsInteract = (interactObjectivesToCheck != null && interactObjectivesToCheck.Count > 0);
                if (!needsInteract && actionObjectivesToCheck != null)
                {
                    for (int ao = 0; ao < actionObjectivesToCheck.Count; ao++)
                    {
                        var a = actionObjectivesToCheck[ao];
                        if (a != null && (a.id == "interactat" || a.id == "interactcount"))
                        {
                            needsInteract = true;
                            break;
                        }
                    }
                }

                if (needsInteract)
                {
                    eventDispatcher.OnBlockUsed(activeQuest, message.BlockCode, position, player, null);
                }
            }
        }
    }
}
