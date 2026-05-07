using System;
using Vintagestory.API.Client;
using Vintagestory.GameContent;

namespace VsQuest
{
    public class ServerInfoGuiManager
    {
        private ServerInfoGui gui;

        public void HandleShowServerInfoMessage(ShowServerInfoMessage message, ICoreClientAPI capi)
        {
            TryCloseOpenDialogue(capi);

            try
            {
                if (gui != null && gui.IsOpened())
                {
                    gui.TryClose();
                }
            }
            catch (Exception ex)
            {
                capi.Logger.Warning("[ServerInfoGuiManager] Failed to close existing GUI: {0}", ex.Message);
            }

            gui = new ServerInfoGui(capi, message?.startTab ?? 0);
            gui.OnClosed += () =>
            {
                if (gui != null && !gui.IsOpened())
                {
                    gui = null;
                }
            };
            gui.TryOpen();
        }

        private static void TryCloseOpenDialogue(ICoreClientAPI capi)
        {
            try
            {
                var opened = capi?.Gui?.OpenedGuis;
                if (opened == null) return;

                for (int i = opened.Count - 1; i >= 0; i--)
                {
                    if (opened[i] is GuiDialogueDialog dlg && dlg.IsOpened())
                    {
                        dlg.TryClose();
                    }
                }
            }
            catch (Exception ex)
            {
                capi.Logger.Warning("[ServerInfoGuiManager] Failed to close open dialogues: {0}", ex.Message);
            }
        }
    }
}
