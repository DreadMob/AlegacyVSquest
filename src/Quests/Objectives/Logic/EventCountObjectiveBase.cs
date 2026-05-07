using System;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace VsQuest
{
    public abstract class EventCountObjectiveBase : ActionObjectiveBase
    {
        private readonly string eventName;

        protected EventCountObjectiveBase(string eventName)
        {
            this.eventName = eventName;
        }

        public string CountKey(string questId, int slot) => $"alegacyvsquest:{eventName}:{questId}:slot{slot}:count";

        public override bool IsCompletable(IPlayer byPlayer, params string[] args)
        {
            if (!TryParseArgs(args, out _, out _, out int needed)) return false;
            var progress = GetProgress(byPlayer, args);
            return progress[0] >= needed;
        }

        public override List<int> GetProgress(IPlayer byPlayer, params string[] args)
        {
            if (byPlayer?.Entity?.WatchedAttributes == null) return new List<int> { 0, 0 };
            if (!TryParseArgs(args, out string questId, out int slot, out int needed)) return new List<int> { 0, 0 };

            int have = byPlayer.Entity.WatchedAttributes.GetInt(CountKey(questId, slot), 0);
            return new List<int> { have, needed };
        }

        public void Increment(IPlayer byPlayer, string questId, int slot, int needed)
        {
            var wa = byPlayer.Entity.WatchedAttributes;
            string key = CountKey(questId, slot);
            int have = wa.GetInt(key, 0);
            if (have < needed)
            {
                wa.SetInt(key, have + 1);
                wa.MarkPathDirty(key);
            }
        }

        public void Reset(IPlayer byPlayer, string questId, int slot)
        {
            var wa = byPlayer.Entity.WatchedAttributes;
            string key = CountKey(questId, slot);
            wa.RemoveAttribute(key);
            wa.MarkPathDirty(key);
        }

        protected bool TryParseArgs(string[] args, out string questId, out int slot, out int needed)
        {
            questId = null;
            slot = 0;
            needed = 0;

            if (args == null || args.Length < 2) return false;

            questId = args[0];
            if (string.IsNullOrWhiteSpace(questId)) return false;

            if (args.Length >= 3 && int.TryParse(args[1], out int parsedSlot))
            {
                slot = parsedSlot;
                if (!int.TryParse(args[2], out needed)) needed = 0;
            }
            else
            {
                slot = 0;
                if (!int.TryParse(args[1], out needed)) needed = 0;
            }

            return true;
        }
    }
}
