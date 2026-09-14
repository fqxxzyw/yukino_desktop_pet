using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace YukinoPet
{
    internal sealed class QuoteManager
    {
        private readonly List<QuoteEntry> entries;
        private readonly Dictionary<string, DateTime> lastByText = new Dictionary<string, DateTime>();
        private readonly Dictionary<string, DateTime> lastByCategory = new Dictionary<string, DateTime>();
        private readonly Random random;
        private DateTime lastGlobal = DateTime.MinValue;

        public QuoteManager(string path, Random random)
        {
            this.random = random;
            entries = Load(path);
        }

        public bool TryPick(QuoteTrigger trigger, CharacterState state, DateTime now, bool highPriority, string requiredAction, out QuoteEntry selected)
        {
            selected = null;
            double globalCooldown = highPriority ? 1.5 : 30.0;
            if ((now - lastGlobal).TotalSeconds < globalCooldown) return false;

            List<QuoteEntry> candidates = new List<QuoteEntry>();
            foreach (QuoteEntry entry in entries)
            {
                if (!String.Equals(entry.TriggerType, trigger.ToString(), StringComparison.OrdinalIgnoreCase)) continue;
                if (!String.IsNullOrEmpty(entry.RequiredState) && !String.Equals(entry.RequiredState, "Any", StringComparison.OrdinalIgnoreCase) && !String.Equals(entry.RequiredState, state.CurrentMood.ToString(), StringComparison.OrdinalIgnoreCase)) continue;
                double max = entry.MaxAffection <= 0 ? 100 : entry.MaxAffection;
                if (state.Affection < entry.MinAffection || state.Affection > max) continue;
                if (!String.IsNullOrEmpty(entry.RequiredAction) && !String.Equals(entry.RequiredAction, "Any", StringComparison.OrdinalIgnoreCase) && !String.Equals(entry.RequiredAction, requiredAction, StringComparison.OrdinalIgnoreCase)) continue;
                if (!AllowedAtHour(entry, now.Hour)) continue;

                DateTime last;
                if (lastByText.TryGetValue(entry.Text, out last) && !entry.AllowRepeat && (now - last).TotalSeconds < Math.Max(90, entry.CooldownSeconds)) continue;
                if (lastByCategory.TryGetValue(entry.Category ?? String.Empty, out last) && (now - last).TotalSeconds < Math.Max(4, entry.CooldownSeconds / 3.0)) continue;
                candidates.Add(entry);
            }
            if (candidates.Count == 0) return false;

            int totalWeight = candidates.Sum(x => Math.Max(1, x.Weight));
            int roll = random.Next(totalWeight);
            foreach (QuoteEntry entry in candidates)
            {
                roll -= Math.Max(1, entry.Weight);
                if (roll >= 0) continue;
                selected = entry;
                lastGlobal = now;
                lastByText[entry.Text] = now;
                lastByCategory[entry.Category ?? String.Empty] = now;
                state.LastQuoteUtc = DateTime.UtcNow;
                return true;
            }
            return false;
        }

        private static bool AllowedAtHour(QuoteEntry entry, int hour)
        {
            if (entry.StartHour == entry.EndHour) return true;
            if (entry.StartHour < entry.EndHour) return hour >= entry.StartHour && hour < entry.EndHour;
            return hour >= entry.StartHour || hour < entry.EndHour;
        }

        private static List<QuoteEntry> Load(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    List<QuoteEntry> parsed = serializer.Deserialize<List<QuoteEntry>>(File.ReadAllText(path));
                    if (parsed != null && parsed.Count > 0) return parsed;
                }
            }
            catch { }
            return new List<QuoteEntry>
            {
                new QuoteEntry { Text = "桌面很安静。这样也不错。", Category = "IdleQuotes", TriggerType = "Idle", Weight = 1, CooldownSeconds = 90, RequiredState = "Any" },
                new QuoteEntry { Text = "你是有什么事吗？", Category = "MouseQuotes", TriggerType = "Mouse", Weight = 1, CooldownSeconds = 45, RequiredState = "Any" },
                new QuoteEntry { Text = "适可而止。", Category = "AnnoyedQuotes", TriggerType = "Annoyed", Weight = 1, CooldownSeconds = 20, RequiredState = "Any" },
                new QuoteEntry { Text = "今天也辛苦了。", Category = "AffectionQuotes", TriggerType = "Affection", Weight = 1, CooldownSeconds = 120, MinAffection = 45, RequiredState = "Any" }
            };
        }
    }
}
