using System.Collections.Generic;

namespace VsQuest
{
    public class ReputationConfig
    {
        public Dictionary<string, ReputationDefinition> factions { get; set; } = new Dictionary<string, ReputationDefinition>();
        public Dictionary<string, ReputationDefinition> npcs { get; set; } = new Dictionary<string, ReputationDefinition>();
    }

    public class ReputationDefinition
    {
        public string titleLangKey { get; set; }
        public List<ReputationRank> ranks { get; set; } = new List<ReputationRank>();
    }

    public class ReputationRank
    {
        public int min { get; set; }
        public string rankLangKey { get; set; }
        public string rewardAction { get; set; }
        public string rewardOnceKey { get; set; }
    }
}
