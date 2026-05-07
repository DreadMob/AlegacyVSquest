using System;
using Xunit;

namespace VsQuest.Tests
{
    public class QuestGiverConstantsTests
    {
        [Fact]
        public void LastAcceptedKey_ReturnsCorrectFormat()
        {
            var key = QuestGiverConstants.LastAcceptedKey("test-quest");
            Assert.Equal("alegacyvsquest:lastaccepted-test-quest", key);
        }

        [Fact]
        public void ChainCooldownKey_ReturnsCorrectFormat()
        {
            var key = QuestGiverConstants.ChainCooldownKey(12345L);
            Assert.Equal("vsquest:questgiver:lastcompleted-12345", key);
        }

        [Fact]
        public void AccessQuestsLangKey_HasCorrectValue()
        {
            Assert.Equal("alegacyvsquest:access-quests", QuestGiverConstants.AccessQuestsLangKey);
        }

        [Fact]
        public void DialogTriggerOpenQuests_HasCorrectValue()
        {
            Assert.Equal("openquests", QuestGiverConstants.DialogTriggerOpenQuests);
        }

        [Fact]
        public void DialogTriggerOpenServerInfo_HasCorrectValue()
        {
            Assert.Equal("openserverinfo", QuestGiverConstants.DialogTriggerOpenServerInfo);
        }
    }
}
