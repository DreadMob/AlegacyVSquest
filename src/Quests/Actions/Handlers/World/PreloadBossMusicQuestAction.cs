using System;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class PreloadBossMusicQuestAction : PlayerActionBase
    {
        protected override int MinArgs => 1;
        protected override string ActionName => "preloadbossmusic";

        protected override void Execute(ICoreServerAPI sapi, IServerPlayer byPlayer, string[] args)
        {
            if (sapi == null) return;

            for (int i = 0; i < args.Length; i++)
            {
                var url = args[i];
                if (string.IsNullOrWhiteSpace(url)) continue;

                try
                {
                    sapi.Network.GetChannel("alegacyvsquest").SendPacket(new PreloadBossMusicMessage { Url = url }, byPlayer);
                }
                catch (Exception ex)
                {
                    sapi.Logger.Warning("[PreloadBossMusicQuestAction] Failed to send preload music packet for URL {0}: {1}", url, ex.Message);
                }
            }
        }
    }
}
