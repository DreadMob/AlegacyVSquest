using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace VsQuest
{
    public class ActionItemCastController : IDisposable
    {
        private readonly ICoreClientAPI capi;
        private readonly IClientNetworkChannel clientChannel;
        private readonly ActionItemAttributeResolver attributeResolver;
        private readonly string castActionItemId;
        private readonly float castDurationSec;
        private readonly float castSlowdown;
        private readonly string castSpeedStatKey;
        private readonly AssetLocation castLoopSound;
        private readonly AssetLocation castCompleteSound;
        private readonly float castSoundVolume;
        private readonly float castSoundRange;
        private readonly float castCompleteSoundRange;
        private readonly float castCompleteSoundVolume;

        private bool isCastingActionItem;
        private long actionItemCastStartMs;
        private string actionItemCastId;
        private ILoadedSound actionItemCastSound;
        private bool wasRightMouseDown;
        private IProgressBar castProgressBar;

        public ActionItemCastController(
            ICoreClientAPI capi,
            IClientNetworkChannel clientChannel,
            ActionItemAttributeResolver attributeResolver,
            string castActionItemId,
            float castDurationSec,
            float castSlowdown,
            string castSpeedStatKey,
            AssetLocation castLoopSound,
            AssetLocation castCompleteSound,
            float castSoundVolume,
            float castSoundRange,
            float castCompleteSoundRange,
            float castCompleteSoundVolume)
        {
            this.capi = capi;
            this.clientChannel = clientChannel;
            this.attributeResolver = attributeResolver;
            this.castActionItemId = castActionItemId;
            this.castDurationSec = castDurationSec;
            this.castSlowdown = castSlowdown;
            this.castSpeedStatKey = castSpeedStatKey;
            this.castLoopSound = castLoopSound;
            this.castCompleteSound = castCompleteSound;
            this.castSoundVolume = castSoundVolume;
            this.castSoundRange = castSoundRange;
            this.castCompleteSoundRange = castCompleteSoundRange;
            this.castCompleteSoundVolume = castCompleteSoundVolume;
        }

        public bool TryHandleMouseDown(MouseEvent args)
        {
            if (args.Button != EnumMouseButton.Right) return false;
            if (capi?.World?.Player?.InventoryManager == null) return false;

            var slot = capi.World.Player.InventoryManager.ActiveHotbarSlot;
            if (slot?.Itemstack == null) return false;
            if (!attributeResolver.EnsureActionItemAttributes(slot)) return false;

            var attributes = slot.Itemstack.Attributes;
            if (attributes == null) return false;

            if (!attributeResolver.TryGetActionItemActionsFromAttributes(attributes, out var actions, out string sourceQuestId))
            {
                return false;
            }

            string actionItemId = attributes.GetString(ItemAttributeUtils.ActionItemIdKey);
            if (!ShouldUseActionItemCast(actionItemId)) return false;

            StartActionItemCast(slot, actionItemId);
            args.Handled = true;
            return true;
        }

        public void HandleMouseUp(MouseEvent args)
        {
            if (args.Button != EnumMouseButton.Right) return;
            if (!isCastingActionItem) return;

            CancelActionItemCast();
            wasRightMouseDown = false;
        }

        public void HandleClientTick(float dt)
        {
            if (capi?.World?.Player?.InventoryManager == null)
            {
                CancelActionItemCast();
                wasRightMouseDown = false;
                return;
            }

            bool inWorldRight = capi.Input?.InWorldMouseButton?.Right ?? false;
            bool anyRight = capi.Input?.MouseButton?.Right ?? false;
            bool rightDown = inWorldRight || anyRight;

            if (!isCastingActionItem)
            {
                if (rightDown && !wasRightMouseDown)
                {
                    var slot = capi.World.Player.InventoryManager.ActiveHotbarSlot;
                    var stack = slot?.Itemstack;
                    if (stack?.Attributes != null
                        && attributeResolver.EnsureActionItemAttributes(slot)
                        && attributeResolver.TryGetActionItemActionsFromAttributes(stack.Attributes, out var actions, out string sourceQuestId))
                    {
                        string actionItemId = stack.Attributes.GetString(ItemAttributeUtils.ActionItemIdKey);
                        if (ShouldUseActionItemCast(actionItemId))
                        {
                            StartActionItemCast(slot, actionItemId);
                        }
                    }
                }

                wasRightMouseDown = rightDown;
                return;
            }

            if (!rightDown)
            {
                CancelActionItemCast();
                wasRightMouseDown = false;
                return;
            }

            var activeSlot = capi.World.Player.InventoryManager.ActiveHotbarSlot;
            var activeStack = activeSlot?.Itemstack;
            if (activeStack?.Attributes == null || !attributeResolver.EnsureActionItemAttributes(activeSlot))
            {
                CancelActionItemCast();
                wasRightMouseDown = rightDown;
                return;
            }

            if (!attributeResolver.TryGetActionItemActionsFromAttributes(activeStack.Attributes, out var activeActions, out string activeSourceQuestId))
            {
                CancelActionItemCast();
                wasRightMouseDown = rightDown;
                return;
            }

            string currentActionItemId = activeStack.Attributes.GetString(ItemAttributeUtils.ActionItemIdKey);
            if (!string.Equals(currentActionItemId, actionItemCastId, StringComparison.OrdinalIgnoreCase))
            {
                CancelActionItemCast();
                wasRightMouseDown = rightDown;
                return;
            }

            long elapsedMs = capi.InWorldEllapsedMilliseconds - actionItemCastStartMs;
            if (elapsedMs >= castDurationSec * 1000f)
            {
                CompleteActionItemCast();
                wasRightMouseDown = rightDown;
                return;
            }

            UpdateActionItemCastSoundPosition();
            UpdateActionItemCastProgress(elapsedMs);
            wasRightMouseDown = rightDown;
        }

        private void StartActionItemCastProgress()
        {
            if (capi?.Side != EnumAppSide.Client) return;

            var progressSystem = capi.ModLoader.GetModSystem<ModSystemProgressBar>();
            if (progressSystem == null) return;

            progressSystem.RemoveProgressbar(castProgressBar);
            castProgressBar = progressSystem.AddProgressbar();
            if (castProgressBar != null)
            {
                castProgressBar.Progress = 0f;
            }
        }

        private void UpdateActionItemCastProgress(long elapsedMs)
        {
            if (castProgressBar == null) return;

            float progress = castDurationSec <= 0f ? 1f : elapsedMs / (castDurationSec * 1000f);
            castProgressBar.Progress = Math.Clamp(progress, 0f, 1f);
        }

        private void StopActionItemCastProgress()
        {
            if (capi?.Side != EnumAppSide.Client) return;

            var progressSystem = capi.ModLoader.GetModSystem<ModSystemProgressBar>();
            progressSystem?.RemoveProgressbar(castProgressBar);
            castProgressBar = null;
        }

        private void StartActionItemCast(ItemSlot slot, string actionItemId)
        {
            if (isCastingActionItem) return;

            isCastingActionItem = true;
            actionItemCastStartMs = capi.InWorldEllapsedMilliseconds;
            actionItemCastId = actionItemId;

            ApplyActionItemCastSlowdown(true);
            StartActionItemCastSound();
            StartActionItemCastProgress();
        }

        private void CompleteActionItemCast()
        {
            string completedActionItemId = actionItemCastId;

            isCastingActionItem = false;
            actionItemCastStartMs = 0;
            actionItemCastId = null;

            ApplyActionItemCastSlowdown(false);
            StopActionItemCastSound();
            StopActionItemCastProgress();

            actionItemCastId = completedActionItemId;
            PlayActionItemCastCompleteSound();
            actionItemCastId = null;

            clientChannel.SendPacket(new ExecuteActionItemPacket());
        }

        private void CancelActionItemCast()
        {
            isCastingActionItem = false;
            actionItemCastStartMs = 0;
            actionItemCastId = null;

            ApplyActionItemCastSlowdown(false);
            StopActionItemCastSound();
            StopActionItemCastProgress();
        }

        private void ApplyActionItemCastSlowdown(bool enabled)
        {
            var playerEntity = capi?.World?.Player?.Entity;
            if (playerEntity?.Stats == null) return;

            if (enabled)
            {
                playerEntity.Stats.Set("walkspeed", castSpeedStatKey, castSlowdown, true);
            }
            else
            {
                playerEntity.Stats.Remove("walkspeed", castSpeedStatKey);
            }

            playerEntity.UpdatePlayerWalkSpeed();
        }

        private void StartActionItemCastSound()
        {
            if (capi?.World == null) return;
            if (!ShouldUseActionItemCast(actionItemCastId)) return;

            actionItemCastSound?.Stop();
            actionItemCastSound?.Dispose();
            actionItemCastSound = capi.World.LoadSound(new SoundParams
            {
                Location = castLoopSound,
                ShouldLoop = true,
                RelativePosition = false,
                DisposeOnFinish = false,
                Volume = castSoundVolume,
                Pitch = 1f,
                Range = castSoundRange
            });

            actionItemCastSound?.Start();
            UpdateActionItemCastSoundPosition();
        }

        private void UpdateActionItemCastSoundPosition()
        {
            if (actionItemCastSound == null) return;

            var playerEntity = capi?.World?.Player?.Entity;
            if (playerEntity == null) return;

            if (actionItemCastSound.HasStopped)
            {
                // If a loop has stopped unexpectedly, recreate it once to avoid repeated Start() churn.
                StartActionItemCastSound();
                return;
            }

            actionItemCastSound.SetPosition((float)playerEntity.Pos.X, (float)playerEntity.Pos.InternalY, (float)playerEntity.Pos.Z);
        }

        private void StopActionItemCastSound()
        {
            if (actionItemCastSound == null) return;

            actionItemCastSound.FadeOutAndStop(0.25f);
            actionItemCastSound.Dispose();
            actionItemCastSound = null;
        }

        private void PlayActionItemCastCompleteSound()
        {
            if (!ShouldUseActionItemCast(actionItemCastId)) return;

            var player = capi?.World?.Player;
            if (player?.Entity == null) return;

            float pitch = (float)capi.World.Rand.NextDouble() * 0.5f + 0.75f;
            capi.World.PlaySoundAt(castCompleteSound, player.Entity, null, pitch, castCompleteSoundRange, castCompleteSoundVolume);
        }

        private bool ShouldUseActionItemCast(string actionItemId)
        {
            return string.Equals(actionItemId, castActionItemId, StringComparison.OrdinalIgnoreCase);
        }

        public void Dispose()
        {
            CancelActionItemCast();
            actionItemCastSound?.Stop();
            actionItemCastSound?.Dispose();
            actionItemCastSound = null;
        }
    }
}
