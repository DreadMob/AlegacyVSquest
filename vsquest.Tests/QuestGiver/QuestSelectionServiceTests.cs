using System;
using System.Collections.Generic;
using Xunit;

namespace VsQuest.Tests
{
    public class QuestSelectionServiceTests
    {
        [Fact]
        public void IsExcluded_ReturnsTrue_ForEmptyQuestId()
        {
            var service = new QuestSelectionService(
                Array.Empty<string>(), Array.Empty<string>(), null,
                Array.Empty<string>(), Array.Empty<string>(),
                false, 1, 0, 1, false, false, 12345L);

            Assert.True(service.IsExcluded(""));
            Assert.True(service.IsExcluded(null));
            Assert.True(service.IsExcluded("   "));
        }

        [Fact]
        public void IsExcluded_ReturnsTrue_ForExcludedQuestId()
        {
            var service = new QuestSelectionService(
                Array.Empty<string>(), Array.Empty<string>(), null,
                new[] { "quest1", "quest2" }, Array.Empty<string>(),
                false, 1, 0, 1, false, false, 12345L);

            Assert.True(service.IsExcluded("quest1"));
            Assert.True(service.IsExcluded("QUEST1")); // Case insensitive
            Assert.False(service.IsExcluded("quest3"));
        }

        [Fact]
        public void IsExcluded_ReturnsTrue_ForExcludedPrefix()
        {
            var service = new QuestSelectionService(
                Array.Empty<string>(), Array.Empty<string>(), null,
                Array.Empty<string>(), new[] { "test_", "demo-" },
                false, 1, 0, 1, false, false, 12345L);

            Assert.True(service.IsExcluded("test_quest1"));
            Assert.True(service.IsExcluded("demo-quest2"));
            Assert.False(service.IsExcluded("real_quest"));
        }

        [Fact]
        public void BuildAllQuestIds_ReturnsAllNonExcludedQuests()
        {
            var service = new QuestSelectionService(
                new[] { "quest1", "quest2" },
                new[] { "always1" },
                new[] { "rotation1" },
                new[] { "quest2" }, // exclude quest2
                Array.Empty<string>(),
                false, 1, 0, 1, false, false, 12345L);

            var registry = new Dictionary<string, Quest>(StringComparer.OrdinalIgnoreCase);
            // Note: Quest objects would need proper mocking in real tests

            var result = service.BuildAllQuestIds(registry);

            Assert.Contains("quest1", result);
            Assert.DoesNotContain("quest2", result); // excluded
            Assert.Contains("always1", result);
            Assert.Contains("rotation1", result);
        }

        [Fact]
        public void GetOfferLimit_ReturnsRotationCount_WhenRotationEnabled()
        {
            var service = new QuestSelectionService(
                Array.Empty<string>(), Array.Empty<string>(), null,
                Array.Empty<string>(), Array.Empty<string>(),
                false, 1, 7, 3, false, false, 12345L);

            Assert.Equal(3, service.GetOfferLimit());
        }

        [Fact]
        public void GetOfferLimit_ReturnsMaxValue_WhenNoRotation()
        {
            var service = new QuestSelectionService(
                Array.Empty<string>(), Array.Empty<string>(), null,
                Array.Empty<string>(), Array.Empty<string>(),
                false, 1, 0, 1, false, false, 12345L);

            Assert.Equal(int.MaxValue, service.GetOfferLimit());
        }
    }
}
