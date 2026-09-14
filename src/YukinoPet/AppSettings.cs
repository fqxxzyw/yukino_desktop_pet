using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace YukinoPet
{
    internal sealed class AppSettings
    {
        public double Left = Double.NaN;
        public double Top = Double.NaN;
        public double Scale = 1.0;
        public double Opacity = 1.0;
        public bool Topmost = true;
        public bool AnimationPaused;
        public bool InteractionPaused;
        public bool RandomQuotes = true;
        public bool InteractionQuotes = true;
        public bool NightReminders = true;
        public bool ShowBubbles = true;
        public bool QuietMode;
        public bool SoundEnabled = true;
        public double SoundVolume = 0.35;
        private bool voiceInitialized;

        private readonly string path;

        public AppSettings(string path)
        {
            this.path = path;
        }

        public void Load()
        {
            if (!File.Exists(path)) return;
            try
            {
                Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string raw in File.ReadAllLines(path))
                {
                    int separator = raw.IndexOf('=');
                    if (separator <= 0) continue;
                    values[raw.Substring(0, separator).Trim()] = raw.Substring(separator + 1).Trim();
                }
                Left = ReadDouble(values, "left", Left);
                Top = ReadDouble(values, "top", Top);
                Scale = Clamp(ReadDouble(values, "scale", Scale), 0.60, 2.00);
                Opacity = Clamp(ReadDouble(values, "opacity", Opacity), 0.35, 1.00);
                Topmost = ReadBool(values, "topmost", Topmost);
                AnimationPaused = ReadBool(values, "paused", AnimationPaused);
                InteractionPaused = ReadBool(values, "interactionPaused", InteractionPaused);
                RandomQuotes = ReadBool(values, "randomQuotes", RandomQuotes);
                InteractionQuotes = ReadBool(values, "interactionQuotes", InteractionQuotes);
                NightReminders = ReadBool(values, "nightReminders", NightReminders);
                ShowBubbles = ReadBool(values, "showBubbles", ShowBubbles);
                QuietMode = ReadBool(values, "quietMode", QuietMode);
                SoundEnabled = ReadBool(values, "soundEnabled", SoundEnabled);
                SoundVolume = Clamp(ReadDouble(values, "soundVolume", SoundVolume), 0, 1);
                voiceInitialized = ReadBool(values, "voiceInitialized", false);
                if (!voiceInitialized)
                {
                    SoundEnabled = true;
                    voiceInitialized = true;
                }
            }
            catch { }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllLines(path, new string[]
                {
                    "left=" + Left.ToString(CultureInfo.InvariantCulture),
                    "top=" + Top.ToString(CultureInfo.InvariantCulture),
                    "scale=" + Scale.ToString(CultureInfo.InvariantCulture),
                    "opacity=" + Opacity.ToString(CultureInfo.InvariantCulture),
                    "topmost=" + Topmost,
                    "paused=" + AnimationPaused,
                    "interactionPaused=" + InteractionPaused,
                    "randomQuotes=" + RandomQuotes,
                    "interactionQuotes=" + InteractionQuotes,
                    "nightReminders=" + NightReminders,
                    "showBubbles=" + ShowBubbles,
                    "quietMode=" + QuietMode,
                    "soundEnabled=" + SoundEnabled,
                    "soundVolume=" + SoundVolume.ToString(CultureInfo.InvariantCulture),
                    "voiceInitialized=True"
                });
            }
            catch { }
        }

        private static double ReadDouble(Dictionary<string, string> values, string key, double fallback)
        {
            string value;
            double number;
            return values.TryGetValue(key, out value) && Double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number) ? number : fallback;
        }

        private static bool ReadBool(Dictionary<string, string> values, string key, bool fallback)
        {
            string value;
            bool result;
            return values.TryGetValue(key, out value) && Boolean.TryParse(value, out result) ? result : fallback;
        }

        private static double Clamp(double value, double min, double max)
        {
            return Math.Max(min, Math.Min(max, value));
        }
    }
}
