using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace VsQuest
{
    public class EntityBehaviorBossMusicUrlController : EntityBehaviorBossBase
    {
        private const string QuestlandMusicAttributeKey = "alegacyvsquest:questlandmusic";
        private ICoreClientAPI clientApi;

        private float range;
        private float startRange;
        private float keepRange;
        private int combatTimeoutMs;
        private bool usePhases;
        private bool phaseSwitching;

        private float fadeOutSeconds;
        private float startAtSeconds;

        private class MusicPhase
        {
            public float whenHealthRelBelow;
            public string url;
            public float startAtSeconds;
            public float startAtRel;
        }

        private readonly System.Collections.Generic.List<MusicPhase> phases = new System.Collections.Generic.List<MusicPhase>();

        private string musicKey;
        private string musicUrl;

        private bool lastShouldPlay;

        private bool wasInCombat;
        private long combatEndGraceUntilMs;
        private const int CombatEndGraceMs = 2000;

        private long outOfRangeSinceMs;
        private const int OutOfRangeStopDelayMs = 800;

        private long lastResolveMs;
        private const int ResolveThrottleMs = 400;
        private string lastResolvedUrl;
        private float lastResolvedOffset;

        public EntityBehaviorBossMusicUrlController(Entity entity) : base(entity)
        {
            clientApi = entity?.Api as ICoreClientAPI;
        }

        public override string PropertyName() => "bossmusicurl";

        public override void Initialize(EntityProperties properties, Vintagestory.API.Datastructures.JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            range = attributes?["range"].AsFloat(60f) ?? 60f;
            startRange = attributes?["startRange"].AsFloat(0f) ?? 0f;
            keepRange = attributes?["keepRange"].AsFloat(45f) ?? 45f;
            combatTimeoutMs = attributes?["combatTimeoutMs"].AsInt(20000) ?? 20000;
            usePhases = attributes?["usePhases"].AsBool(true) ?? true;
            phaseSwitching = attributes?["phaseSwitching"].AsBool(true) ?? true;

            fadeOutSeconds = attributes?["fadeOutSeconds"].AsFloat(4f) ?? 4f;
            if (fadeOutSeconds < 0f) fadeOutSeconds = 0f;

            startAtSeconds = attributes?["startAtSeconds"].AsFloat(0f) ?? 0f;
            if (startAtSeconds < 0f) startAtSeconds = 0f;

            musicKey = attributes?["musicKey"].AsString(null);
            musicUrl = attributes?["musicUrl"].AsString(null);

            phases.Clear();
            try
            {
                if (attributes?["phases"]?.Exists == true)
                {
                    foreach (var ph in attributes["phases"].AsArray())
                    {
                        if (ph == null || !ph.Exists) continue;
                        phases.Add(new MusicPhase
                        {
                            whenHealthRelBelow = ph["whenHealthRelBelow"].AsFloat(1f),
                            url = ph["musicUrl"].AsString(null),
                            startAtSeconds = ph["startAtSeconds"].AsFloat(0f),
                            startAtRel = ph["startAtRel"].AsFloat(-1f)
                        });
                    }
                }
            }
            catch
            {
                phases.Clear();
            }

            if (range < 1f) range = 1f;
            if (startRange < 0f) startRange = 0f;
            if (startRange > 0f && startRange < 1f) startRange = 1f;
            if (keepRange < 1f) keepRange = 1f;
            if (combatTimeoutMs < 0) combatTimeoutMs = 0;

            if (keepRange > range) keepRange = range;

            wasInCombat = false;
            combatEndGraceUntilMs = 0;
        }

        private const float DeathFadeOutSeconds = 2f;

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            if (clientApi == null || entity == null || !entity.Alive)
            {
                ApplyShouldPlay(false, fadeOutSeconds > 0f ? fadeOutSeconds : DeathFadeOutSeconds);
                return;
            }

            var playerEntity = clientApi.World?.Player?.Entity;
            if (playerEntity == null || !playerEntity.Alive)
            {
                ApplyShouldPlay(false, fadeOutSeconds);
                return;
            }

            double distanceToPlayer = playerEntity.Pos.DistanceTo(entity.Pos);
            bool inRange = (float)distanceToPlayer <= range;

            if (!inRange)
            {
                if (playerEntity.WatchedAttributes.HasAttribute(QuestlandMusicAttributeKey))
                {
                    return;
                }

                long nowMs = Environment.TickCount64;
                if (outOfRangeSinceMs <= 0) outOfRangeSinceMs = nowMs;

                if (nowMs - outOfRangeSinceMs >= OutOfRangeStopDelayMs)
                {
                    ApplyShouldPlay(false, fadeOutSeconds);
                }

                wasInCombat = false;
                combatEndGraceUntilMs = 0;
                return;
            }

            outOfRangeSinceMs = 0;

            long lastDamageMs = entity.WatchedAttributes.GetLong(EntityBehaviorBossHuntCombatMarker.BossHuntLastDamageMsKey, 0);
            if (lastDamageMs <= 0)
            {
                lastDamageMs = entity.WatchedAttributes.GetLong(EntityBehaviorBossCombatMarker.BossCombatLastDamageMsKey, 0);
            }

            bool hasTrigger = lastDamageMs > 0;
            bool inStartRange = startRange > 0f ? distanceToPlayer <= startRange : inRange;
            bool inKeepRange = distanceToPlayer <= keepRange;

            bool aiHasTarget = entity.WatchedAttributes.GetBool(BossBehaviorUtils.HasTargetKey, false);

            bool recentDamage = false;
            if (combatTimeoutMs <= 0)
            {
                recentDamage = hasTrigger;
            }
            else if (hasTrigger)
            {
                long dtMs = clientApi.World.ElapsedMilliseconds - lastDamageMs;
                recentDamage = dtMs >= 0 && dtMs <= combatTimeoutMs;
            }

            bool combatCore = aiHasTarget || recentDamage;

            // If combat has already started, keep playing as long as player stays in keepRange.
            if (wasInCombat && inKeepRange)
            {
                combatCore = true;
            }

            bool combatNow = combatCore && (wasInCombat || inStartRange);

            long tickNow = Environment.TickCount64;
            if (wasInCombat && !combatNow)
            {
                if (combatEndGraceUntilMs <= 0)
                {
                    combatEndGraceUntilMs = tickNow + CombatEndGraceMs;
                }

                if (tickNow < combatEndGraceUntilMs)
                {
                    combatNow = true;
                }
            }
            else if (combatNow)
            {
                combatEndGraceUntilMs = 0;
            }

            ApplyShouldPlay(combatNow);
            wasInCombat = combatNow;
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            ApplyShouldPlay(false, fadeOutSeconds);
            base.OnEntityDespawn(despawn);
        }

        private void ApplyShouldPlay(bool shouldPlay, float fadeOutSeconds = -1f)
        {
            try
            {
                var sys = clientApi?.ModLoader?.GetModSystem<BossMusicUrlSystem>();
                if (sys == null) return;

                if (shouldPlay)
                {
                    bool playbackStopped = sys.IsActive && !sys.IsPlaybackRunning;

                    long now = Environment.TickCount64;
                    bool allowResolve = !lastShouldPlay || phaseSwitching || playbackStopped;
                    if (allowResolve && (!lastShouldPlay || playbackStopped || now - lastResolveMs >= ResolveThrottleMs))
                    {
                        lastResolveMs = now;

                        ResolveDesiredMusic(sys, out var url, out var offset);

                        bool changed = !string.Equals(lastResolvedUrl ?? "", url ?? "", StringComparison.OrdinalIgnoreCase)
                                       || Math.Abs(lastResolvedOffset - offset) > 0.01f;

                        if (!lastShouldPlay || changed || playbackStopped)
                        {
                            lastResolvedUrl = url;
                            lastResolvedOffset = offset;
                            sys.Start(musicKey, url, offset);
                        }
                    }
                }
                else
                {
                    if (lastShouldPlay)
                    {
                        lastResolvedUrl = null;
                        lastResolvedOffset = 0f;
                        sys.Stop(fadeOutSeconds);
                    }
                }
            }
            catch
            {
            }

            lastShouldPlay = shouldPlay;
        }

        private void ResolveDesiredMusic(BossMusicUrlSystem sys, out string url, out float offset)
        {
            bool preferKey = !string.IsNullOrWhiteSpace(musicKey);
            url = preferKey ? null : musicUrl;
            offset = startAtSeconds;

            if (preferKey && sys != null)
            {
                url = sys.ResolveUrl(musicKey);
            }

            if (usePhases && phases.Count > 0)
            {
                if (TryGetHealthFraction(out float frac))
                {
                    MusicPhase best = null;
                    float bestThr = 999f;
                    for (int i = 0; i < phases.Count; i++)
                    {
                        var ph = phases[i];
                        if (ph == null) continue;
                        if (frac <= ph.whenHealthRelBelow && ph.whenHealthRelBelow < bestThr)
                        {
                            best = ph;
                            bestThr = ph.whenHealthRelBelow;
                        }
                    }

                    if (best != null)
                    {
                        if (!preferKey && !string.IsNullOrWhiteSpace(best.url)) url = best.url;
                        offset = best.startAtSeconds;

                        if (best.startAtRel >= 0f)
                        {
                            float rel = GameMath.Clamp(best.startAtRel, 0f, 1f);

                            if (!string.IsNullOrWhiteSpace(url) && sys != null && sys.TryGetDurationSeconds(url, out var seconds) && seconds > 0.01f)
                            {
                                offset = seconds * rel;
                            }
                        }
                    }
                }
            }

            if (offset < 0f) offset = 0f;
        }
    }
}
