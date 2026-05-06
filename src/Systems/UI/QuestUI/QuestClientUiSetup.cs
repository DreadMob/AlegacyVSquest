using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace VsQuest
{
    public static class QuestClientUiSetup
    {
        public static VsQuestDiscoveryHud Initialize(ICoreClientAPI capi)
        {
            if (capi == null) return null;

            capi.RegisterVtmlTagConverter("qhover", (clientApi, token, fontStack, onClick) =>
            {
                if (token == null) return null;

                string displayText = token.ContentText;
                string hoverText = null;
                if (token.Attributes != null && token.Attributes.TryGetValue("text", out var attrText))
                {
                    hoverText = attrText;
                }

                if (string.IsNullOrWhiteSpace(hoverText))
                {
                    return new RichTextComponent(clientApi, displayText, fontStack.Peek());
                }

                return new RichTextComponentQuestHover(clientApi, displayText, hoverText, fontStack.Peek());
            });

            try
            {
                return new VsQuestDiscoveryHud(capi);
            }
            catch
            {
                return null;
            }
        }
    }
}
