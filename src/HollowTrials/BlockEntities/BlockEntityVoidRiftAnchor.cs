using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class BlockEntityVoidRiftAnchor : BlockEntity
    {
        private const string AttrTrialKey = "alegacyvsquest:voidrift:trialKey";
        private const string AttrAnchorId = "alegacyvsquest:voidrift:anchorId";
        private const string AttrYOffset = "alegacyvsquest:voidrift:yOffset";

        private string trialKey;
        private string anchorId;
        private float yOffset;

        public string TrialKey => trialKey;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (Api?.Side == EnumAppSide.Server)
            {
                TryRegisterAnchor();
            }
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);

            if (Api?.Side != EnumAppSide.Server) return;

            // Read defaults from block JSON attributes
            var attrs = Block?.Attributes;
            trialKey = attrs?["trialKey"].AsString(trialKey);
            yOffset = attrs?["yOffset"].AsFloat(yOffset) ?? yOffset;

            TryRegisterAnchor();
            MarkDirty(true);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            trialKey = tree.GetString(AttrTrialKey, trialKey);
            anchorId = tree.GetString(AttrAnchorId, anchorId);
            yOffset = tree.GetFloat(AttrYOffset, yOffset);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            if (!string.IsNullOrWhiteSpace(trialKey)) tree.SetString(AttrTrialKey, trialKey);
            if (!string.IsNullOrWhiteSpace(anchorId)) tree.SetString(AttrAnchorId, anchorId);
            tree.SetFloat(AttrYOffset, yOffset);
        }

        internal void OnInteract(IPlayer byPlayer)
        {
            if (byPlayer == null || Api?.Side != EnumAppSide.Server) return;

            var sp = byPlayer as IServerPlayer;
            if (sp == null || !sp.HasPrivilege(Privilege.controlserver)) return;

            // Simple chat-based config for now (GUI can be added later)
            sp.SendMessage(0,
                $"[VoidRift] trialKey: '{trialKey ?? "(none)"}', anchorId: '{anchorId ?? "(auto)"}', yOffset: {yOffset}",
                EnumChatType.Notification);
        }

        internal void OnRemovedServerSide()
        {
            if (Api?.Side != EnumAppSide.Server) return;
            if (string.IsNullOrWhiteSpace(trialKey)) return;

            try
            {
                var system = Api.ModLoader.GetModSystem<HollowTrialSystem>();
                system?.UnsetAnchorPoint(trialKey, anchorId, new BlockPos(Pos.X, Pos.Y, Pos.Z, Pos.dimension));
            }
            catch (Exception ex)
            {
                Api?.Logger?.Error($"[VoidRiftAnchor] Exception in UnsetAnchorPoint: {ex}");
            }
        }

        /// <summary>
        /// Set the trial key for this anchor (used by admin commands or GUI).
        /// </summary>
        public void SetTrialKey(string newTrialKey)
        {
            trialKey = newTrialKey;
            TryRegisterAnchor();
            MarkDirty(true);
        }

        private void TryRegisterAnchor()
        {
            if (Api?.Side != EnumAppSide.Server) return;
            if (string.IsNullOrWhiteSpace(trialKey)) return;

            if (string.IsNullOrWhiteSpace(anchorId))
            {
                anchorId = $"alegacyvsquest:voidrift:{Pos.dimension}:{Pos.X}:{Pos.Y}:{Pos.Z}";
                MarkDirty(true);
            }

            try
            {
                var system = Api.ModLoader.GetModSystem<HollowTrialSystem>();
                system?.SetAnchorPoint(trialKey, anchorId, new BlockPos(Pos.X, Pos.Y, Pos.Z, Pos.dimension), yOffset);
            }
            catch (Exception ex)
            {
                Api?.Logger?.Error($"[VoidRiftAnchor] Exception in TryRegisterAnchor: {ex}");
            }
        }
    }
}
