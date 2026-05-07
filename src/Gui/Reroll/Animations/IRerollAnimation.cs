namespace VsQuest
{
    /// <summary>
    /// Interface for reroll animation styles.
    /// Allows different animation implementations (simple spin, CS:GO style, slots, etc.)
    /// </summary>
    public interface IRerollAnimation
    {
        /// <summary>
        /// Unique identifier for this animation type
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Initialize animation with item list and result
        /// </summary>
        /// <param name="itemIds">All possible item IDs that could appear</param>
        /// <param name="resultItemId">The actual result item ID</param>
        /// <param name="itemNames">Display names for items (parallel to itemIds)</param>
        /// <param name="itemCodes">Base item codes for icons (parallel to itemIds)</param>
        /// <param name="resultItemCode">Result item code for icon</param>
        void Initialize(string[] itemIds, string[] itemNames, string[] itemCodes, string resultItemId, string resultItemName, string resultItemCode);

        /// <summary>
        /// Update animation state
        /// </summary>
        /// <param name="deltaTime">Time since last update in seconds</param>
        void Update(float deltaTime);

        /// <summary>
        /// Get currently displayed item ID
        /// </summary>
        string GetCurrentItemId();

        /// <summary>
        /// Get currently displayed item name
        /// </summary>
        string GetCurrentItemName();

        /// <summary>
        /// Get currently displayed item code for icon
        /// </summary>
        string GetCurrentItemCode();

        /// <summary>
        /// Is animation complete?
        /// </summary>
        bool IsComplete { get; }

        /// <summary>
        /// Animation progress (0-1)
        /// </summary>
        float Progress { get; }
    }
}
