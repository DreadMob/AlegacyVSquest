using System;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class NotifyQuestAction : PlayerActionBase
    {
        protected override int MinArgs => 1;
        protected override string ActionName => "notify";

        protected override void Execute(ICoreServerAPI api, IServerPlayer byPlayer, string[] args)
        {
            var notification = new ShowNotificationMessage() { Notification = args[0] };

            if (string.Equals(args[0], "albase:bosshunt-rotation-info", StringComparison.OrdinalIgnoreCase))
            {
                var bossSystem = api?.ModLoader?.GetModSystem<BossHuntSystem>();
                if (bossSystem != null && bossSystem.TryGetBossHuntStatus(out _, out _, out double hoursUntilRotation))
                {
                    if (hoursUntilRotation > 0)
                    {
                        int daysLeft = (int)Math.Ceiling(hoursUntilRotation / 24.0);
                        if (daysLeft < 0) daysLeft = 0;
                        notification.Need = daysLeft;
                    }
                }
            }

            api.Network.GetChannel("alegacyvsquest").SendPacket(notification, byPlayer);
        }
    }
}
