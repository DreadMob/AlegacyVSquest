using Vintagestory.API.Client;

namespace VsQuest
{
    public class QuestNotificationHandler
    {
        private readonly VsQuestDiscoveryHud discoveryHud;

        public QuestNotificationHandler(VsQuestDiscoveryHud discoveryHud)
        {
            this.discoveryHud = discoveryHud;
        }

        public void HandleNotificationMessage(ShowNotificationMessage message, ICoreClientAPI capi)
        {
            if (message == null)
            {
                capi.ShowChatMessage(null);
                return;
            }

            string text = NotificationTextUtil.Build(message, capi.Logger);
            capi.ShowChatMessage(text);
        }

        public void HandleDiscoveryMessage(ShowDiscoveryMessage message, ICoreClientAPI capi)
        {
            if (message == null)
            {
                return;
            }

            string text = NotificationTextUtil.Build(new ShowNotificationMessage
            {
                Notification = message.Notification,
                Template = message.Template,
                Need = message.Need,
                MobCode = message.MobCode
            }, capi.Logger);

            if (discoveryHud != null)
            {
                discoveryHud.Show(text);
                return;
            }

            capi.TriggerIngameDiscovery(null, "alegacyvsquest", text);
        }
    }
}
