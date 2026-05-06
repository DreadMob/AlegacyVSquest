using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class HealPlayerAction : PlayerActionBase
    {
        protected override int MinArgs => 0;
        protected override string ActionName => "healplayer";

        protected override void Execute(ICoreServerAPI sapi, IServerPlayer player, string[] args)
        {
            float amount = 1000f;
            if (args.Length > 0 && !float.TryParse(args[0], out amount))
            {
                sapi.Logger.Error($"[vsquest] 'healplayer' action has an invalid amount '{args[0]}'. Defaulting to full heal.");
                amount = 1000f;
            }
            player.Entity.ReceiveDamage(new DamageSource() { Type = EnumDamageType.Heal }, amount);
        }
    }
}
