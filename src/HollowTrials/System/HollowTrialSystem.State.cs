using System;
using System.Collections.Generic;
using ProtoBuf;

namespace VsQuest
{
    public partial class HollowTrialSystem
    {
        private void LoadState()
        {
            try
            {
                state = sapi.WorldManager.SaveGame.GetData<HollowTrialWorldState>(SaveKey, null);
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning("[HollowTrialSystem] Failed to load state: {0}", ex.Message);
            }

            if (state == null)
            {
                state = new HollowTrialWorldState();
                // First launch — perform initial rotation
                double nowHours = sapi.World?.Calendar?.TotalHours ?? 0;
                PerformRotation(nowHours);
            }

            state.entries ??= new List<HollowTrialStateEntry>();
            state.activeTrialKeys ??= new List<string>();
            state.rotationIndexPerTier ??= new Dictionary<int, int>();
        }

        private void OnWorldSave()
        {
            SaveStateIfDirty();
            SaveReputationData();
        }

        private void SaveStateIfDirty()
        {
            if (!stateDirty || sapi == null) return;

            _stateLock.EnterReadLock();
            try
            {
                sapi.WorldManager.SaveGame.StoreData(SaveKey, state);
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning("[HollowTrialSystem] Failed to save state: {0}", ex.Message);
            }
            finally
            {
                _stateLock.ExitReadLock();
            }

            stateDirty = false;
        }

        private HollowTrialStateEntry GetOrCreateEntry(string trialKey)
        {
            if (string.IsNullOrWhiteSpace(trialKey)) return null;

            state.entries ??= new List<HollowTrialStateEntry>();

            for (int i = 0; i < state.entries.Count; i++)
            {
                if (string.Equals(state.entries[i].trialKey, trialKey, StringComparison.OrdinalIgnoreCase))
                    return state.entries[i];
            }

            var entry = new HollowTrialStateEntry
            {
                trialKey = trialKey,
                anchorPoints = new List<HollowTrialAnchorPoint>()
            };
            state.entries.Add(entry);
            stateDirty = true;
            return entry;
        }
    }
}
