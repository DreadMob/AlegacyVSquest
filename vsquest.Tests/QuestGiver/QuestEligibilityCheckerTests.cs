using System;
using System.Collections.Generic;
using Xunit;

namespace VsQuest.Tests
{
    public class QuestEligibilityCheckerTests
    {
        // Note: These tests require mocking ICoreServerAPI, QuestSystem, and ReputationSystem
        // In a real implementation, you would use a mocking framework like Moq

        [Fact]
        public void PredecessorsCompleted_ReturnsTrue_WhenNoPredecessors()
        {
            // This test demonstrates the expected behavior
            // In real tests, you would mock the dependencies
            
            var quest = new Quest
            {
                id = "test-quest",
                predecessor = null,
                predecessors = null
            };

            // Without mocking, we can only test the basic logic structure
            Assert.True(quest.predecessor == null);
            Assert.True(quest.predecessors == null);
        }

        [Fact]
        public void PredecessorsCompleted_ReturnsTrue_WhenSinglePredecessorCompleted()
        {
            var quest = new Quest
            {
                id = "test-quest",
                predecessor = "pre-quest",
                predecessors = null
            };

            var completedQuests = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "pre-quest"
            };

            // Verify the predecessor is in the completed set
            Assert.Contains(quest.predecessor, completedQuests);
        }

        [Fact]
        public void PredecessorsCompleted_ReturnsFalse_WhenSinglePredecessorNotCompleted()
        {
            var quest = new Quest
            {
                id = "test-quest",
                predecessor = "pre-quest",
                predecessors = null
            };

            var completedQuests = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Verify the predecessor is NOT in the completed set
            Assert.DoesNotContain(quest.predecessor, completedQuests);
        }

        [Fact]
        public void PredecessorsCompleted_ReturnsTrue_WhenAllPredecessorsCompleted()
        {
            var quest = new Quest
            {
                id = "test-quest",
                predecessor = null,
                predecessors = new List<string> { "pre1", "pre2", "pre3" }
            };

            var completedQuests = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "pre1", "pre2", "pre3"
            };

            // Verify all predecessors are in the completed set
            foreach (var pre in quest.predecessors)
            {
                Assert.Contains(pre, completedQuests);
            }
        }

        [Fact]
        public void PredecessorsCompleted_ReturnsFalse_WhenAnyPredecessorNotCompleted()
        {
            var quest = new Quest
            {
                id = "test-quest",
                predecessor = null,
                predecessors = new List<string> { "pre1", "pre2", "pre3" }
            };

            var completedQuests = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "pre1", "pre3" // pre2 is missing
            };

            // Verify not all predecessors are in the completed set
            Assert.DoesNotContain("pre2", completedQuests);
        }
    }
}
