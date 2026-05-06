using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public enum DiscordBroadcastKind
    {
        Generic = 0,
        QuestCompleted = 1,
        BossDefeated = 2
    }

    public static class GlobalChatBroadcastUtil
    {
        private static event System.Action<string, DiscordBroadcastKind> DiscordBroadcast;

        public static void SubscribeDiscordBroadcast(System.Action<string, DiscordBroadcastKind> handler)
        {
            if (handler == null) return;
            DiscordBroadcast += handler;
        }

        public static void UnsubscribeDiscordBroadcast(System.Action<string, DiscordBroadcastKind> handler)
        {
            if (handler == null) return;
            DiscordBroadcast -= handler;
        }

        public static void Broadcast(ICoreServerAPI sapi, int chatGroupId, string message, EnumChatType chatType)
        {
            if (sapi == null || string.IsNullOrWhiteSpace(message)) return;

            foreach (var p in sapi.World.AllOnlinePlayers)
            {
                if (p is IServerPlayer sp)
                {
                    sapi.SendMessage(sp, chatGroupId, message, chatType);
                }
            }
        }

        public static void BroadcastGeneralChat(ICoreServerAPI sapi, string message, EnumChatType chatType = EnumChatType.Notification)
        {
            Broadcast(sapi, Vintagestory.API.Config.GlobalConstants.GeneralChatGroup, message, chatType);
        }

        public static void BroadcastGeneralChatWithDiscord(
            ICoreServerAPI sapi,
            string message,
            string discordMessage,
            EnumChatType chatType = EnumChatType.Notification,
            DiscordBroadcastKind kind = DiscordBroadcastKind.Generic)
        {
            // Broadcast to in-game chat
            BroadcastGeneralChat(sapi, message, chatType);

            // Send to Discord via callback if registered
            try
            {
                DiscordBroadcast?.Invoke(discordMessage, kind);
            }
            catch (System.Exception ex)
            {
                sapi.Logger.Warning($"[alegacyvsquest] Failed to send Discord broadcast: {ex.Message}");
            }
        }
    }
}
