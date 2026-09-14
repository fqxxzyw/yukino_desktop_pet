using NAudio.Vorbis;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;

namespace YukinoPet
{
    internal enum VoiceTrigger
    {
        Click,
        Idle,
        Pomodoro
    }

    internal static class EmbeddedAssemblyLoader
    {
        private static bool installed;

        public static void Install()
        {
            if (installed) return;
            installed = true;
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        }

        private static Assembly Resolve(object sender, ResolveEventArgs args)
        {
            string simpleName = new AssemblyName(args.Name).Name;
            string resource = simpleName == "NAudio" ? "YukinoPet.NAudio.dll" :
                              simpleName == "NAudio.Vorbis" ? "YukinoPet.NAudio.Vorbis.dll" :
                              simpleName == "NVorbis" ? "YukinoPet.NVorbis.dll" : null;
            if (resource == null) return null;
            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
                {
                    if (stream == null) return null;
                    byte[] bytes = new byte[stream.Length];
                    int offset = 0;
                    while (offset < bytes.Length)
                    {
                        int read = stream.Read(bytes, offset, bytes.Length - offset);
                        if (read <= 0) break;
                        offset += read;
                    }
                    return Assembly.Load(bytes);
                }
            }
            catch { return null; }
        }
    }

    /// <summary>从 EXE 内置语音库异步播放，任何解码或设备错误都只会静默跳过。</summary>
    internal sealed class AudioManager : IDisposable
    {
        private readonly object sync = new object();
        private readonly Random random;
        private readonly Stream archiveStream;
        private readonly ZipArchive archive;
        private readonly List<ZipArchiveEntry> voices;
        private IWavePlayer output;
        private VorbisWaveReader reader;
        private WaveChannel32 volumeStream;
        private MemoryStream currentAudio;
        private DateTime nextAllowed = DateTime.MinValue;
        private int lastVoiceIndex = -1;
        private bool disposed;

        public AudioManager(Random random)
        {
            this.random = random;
            try
            {
                archiveStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("YukinoPet.Voices.zip");
                if (archiveStream != null)
                {
                    archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, false);
                    voices = archive.Entries.Where(x => x.Name.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)).ToList();
                }
            }
            catch { voices = new List<ZipArchiveEntry>(); }
            if (voices == null) voices = new List<ZipArchiveEntry>();
        }

        public bool IsPlaying
        {
            get { lock (sync) return output != null; }
        }

        public int VoiceCount { get { return voices.Count; } }

        public bool TryPlayRandom(VoiceTrigger trigger, bool enabled, double volume)
        {
            lock (sync)
            {
                if (disposed || !enabled || voices.Count == 0) return false;
                bool userRequested = trigger == VoiceTrigger.Click;
                if (!userRequested && (output != null || DateTime.UtcNow < nextAllowed)) return false;
                if (userRequested && output != null) CleanupPlayback();
                try
                {
                    int voiceIndex = random.Next(voices.Count);
                    if (voices.Count > 1 && voiceIndex == lastVoiceIndex)
                        voiceIndex = (voiceIndex + 1 + random.Next(voices.Count - 1)) % voices.Count;
                    lastVoiceIndex = voiceIndex;
                    ZipArchiveEntry entry = voices[voiceIndex];
                    currentAudio = new MemoryStream((int)Math.Min(Int32.MaxValue, entry.Length));
                    using (Stream source = entry.Open()) source.CopyTo(currentAudio);
                    currentAudio.Position = 0;
                    reader = new VorbisWaveReader(currentAudio);
                    volumeStream = new WaveChannel32(reader);
                    volumeStream.Volume = (float)Math.Max(0, Math.Min(1, volume));
                    WaveOutEvent waveOut = new WaveOutEvent { DesiredLatency = 180, NumberOfBuffers = 3 };
                    waveOut.PlaybackStopped += OnPlaybackStopped;
                    output = waveOut;
                    waveOut.Init(volumeStream);
                    waveOut.Play();
                    nextAllowed = DateTime.UtcNow.AddSeconds(trigger == VoiceTrigger.Click ? 0.2 : trigger == VoiceTrigger.Pomodoro ? 8 : 35);
                    return true;
                }
                catch
                {
                    CleanupPlayback();
                    nextAllowed = DateTime.UtcNow.AddSeconds(8);
                    return false;
                }
            }
        }

        public void Stop()
        {
            lock (sync)
            {
                try { if (output != null) output.Stop(); } catch { CleanupPlayback(); }
            }
        }

        private void OnPlaybackStopped(object sender, EventArgs e)
        {
            lock (sync) CleanupPlayback();
        }

        private void CleanupPlayback()
        {
            IWavePlayer oldOutput = output;
            output = null;
            try { if (oldOutput != null) oldOutput.PlaybackStopped -= OnPlaybackStopped; } catch { }
            try { if (oldOutput != null) oldOutput.Dispose(); } catch { }
            try { if (volumeStream != null) volumeStream.Dispose(); } catch { }
            try { if (reader != null) reader.Dispose(); } catch { }
            try { if (currentAudio != null) currentAudio.Dispose(); } catch { }
            reader = null;
            volumeStream = null;
            currentAudio = null;
        }

        public void Dispose()
        {
            lock (sync)
            {
                disposed = true;
                CleanupPlayback();
                try { if (archive != null) archive.Dispose(); } catch { }
                try { if (archiveStream != null) archiveStream.Dispose(); } catch { }
            }
        }
    }
}
