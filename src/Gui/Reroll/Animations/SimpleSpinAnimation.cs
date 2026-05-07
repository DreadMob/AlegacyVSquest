using System;

namespace VsQuest
{
    /// <summary>
    /// Simple spin animation - items cycle rapidly then slow down and stop on result.
    /// </summary>
    public class SimpleSpinAnimation : IRerollAnimation
    {
        public string Id => "simplespin";

        private string[] itemIds;
        private string[] itemNames;
        private string resultItemId;
        private string resultItemName;

        private int currentIndex;
        private float elapsed;
        private float spinSpeed; // items per second
        private float timeSinceLastChange;

        private const float InitialSpeed = 20f; // 20 items per second at start
        private const float MinSpeed = 2f; // 2 items per second at end
        private const float TotalDuration = 18f; // 18 seconds total

        public bool IsComplete => elapsed >= TotalDuration;
        public float Progress => Math.Min(1f, elapsed / TotalDuration);

        public void Initialize(string[] itemIds, string[] itemNames, string resultItemId, string resultItemName)
        {
            this.itemIds = itemIds;
            this.itemNames = itemNames;
            this.resultItemId = resultItemId;
            this.resultItemName = resultItemName;
            this.elapsed = 0f;
            this.currentIndex = 0;
            this.timeSinceLastChange = 0f;
            this.spinSpeed = InitialSpeed;
        }

        public void Update(float deltaTime)
        {
            if (IsComplete) return;

            elapsed += deltaTime;

            // Calculate current speed (decelerate over time)
            float t = Progress;
            // Ease out - start fast, slow down
            spinSpeed = InitialSpeed - (InitialSpeed - MinSpeed) * (t * t);

            // Check if we need to change item
            timeSinceLastChange += deltaTime;
            float changeInterval = 1f / spinSpeed;

            while (timeSinceLastChange >= changeInterval && !IsComplete)
            {
                timeSinceLastChange -= changeInterval;
                currentIndex = (currentIndex + 1) % itemIds.Length;
            }

            // At completion, ensure we land on result
            if (IsComplete)
            {
                // Find result index
                for (int i = 0; i < itemIds.Length; i++)
                {
                    if (itemIds[i] == resultItemId)
                    {
                        currentIndex = i;
                        break;
                    }
                }
            }
        }

        public string GetCurrentItemId()
        {
            if (IsComplete) return resultItemId;
            return itemIds?[currentIndex] ?? "";
        }

        public string GetCurrentItemName()
        {
            if (IsComplete) return resultItemName;
            return itemNames?[currentIndex] ?? "";
        }
    }
}
