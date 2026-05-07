using System.Collections.Generic;

namespace VsQuest
{
    public interface IQuestPersistenceManager
    {
        List<ActiveQuest> GetPlayerQuests(string playerUID);
        void SavePlayerQuests(string playerUID, List<ActiveQuest> activeQuests);
        void MarkDirty(string playerUID);
        void UnloadPlayerQuests(string playerUID);
        void SaveAllPlayerQuests();
        void SetDbSyncService(Systems.Database.VsQuestSyncService syncService);
        void Dispose();
    }
}
