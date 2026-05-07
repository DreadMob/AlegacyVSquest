using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public interface IChatCommandSubRegistry
    {
        void Register(IChatCommand avq, ICoreServerAPI sapi);
    }
}
