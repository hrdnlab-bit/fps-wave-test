using System;
using System.Collections.Generic;
using System.IO;
using HrdnLab.FpsWave.Audio;
using UnityEditor;
using UnityEngine;

namespace HrdnLab.FpsWave.Editor
{
    /// <summary>
    /// FpsWave/Setup/9. Build Audio: prepara i suoni e la libreria del gioco.
    /// - Gli spari (registrazioni a 96 kHz con lunghe code, The Free Firearm Sound Library, CC0) vengono tagliati
    ///   dall'attacco, sfumati, ricampionati a 48 kHz e normalizzati in Assets/ThirdParty/_Own/Audio
    ///   (versione "vicina" stereo per chi spara, "lontana" mono per gli altri).
    /// - Gli altri suoni (Kenney Impact, Sci-fi e Interface, CC0) si usano così come sono.
    /// - Crea Assets/_Project/Resources/SoundLibrary.asset con una voce per ogni <see cref="SoundId"/>.
    /// I file sorgente arrivano da `tools/setup-audio-assets.ps1` (fuori dal repo, come gli asset Fab).
    /// Ripetibile: si può rieseguire quando si cambiano i file o i volumi qui sotto.
    /// </summary>
    public static class AudioContentBuilder
    {
        private const string SourceDir = "Assets/ThirdParty/Audio";
        private const string OwnDir = "Assets/ThirdParty/_Own/Audio";
        private const string LibraryPath = "Assets/_Project/Resources/SoundLibrary.asset";
        private const int OutputRate = 48000;

        private static readonly (string source, string output, float seconds, bool mono)[] Gunshots =
        {
            ("FirearmLibrary/AR15_near.wav", "rifle_close", 1.1f, false),
            ("FirearmLibrary/AR15_mid.wav", "rifle_far", 1.6f, true),
            ("FirearmLibrary/PPQ_near.wav", "pistol_close", 0.9f, false),
            ("FirearmLibrary/PPQ_mid.wav", "pistol_far", 1.4f, true),
        };

        [MenuItem("FpsWave/Setup/9. Build Audio")]
        public static void BuildMenu() => Build();

        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(SourceDir))
            {
                Debug.LogWarning($"[FpsWave] {SourceDir} mancante: eseguire tools/setup-audio-assets.ps1 (Unity chiuso), poi Setup/9. Il gioco resta muto.");
                return;
            }
            Directory.CreateDirectory(OwnDir);
            int processed = 0;
            foreach (var (source, output, seconds, mono) in Gunshots)
            {
                string src = Path.Combine(SourceDir, source);
                if (!File.Exists(src))
                {
                    Debug.LogWarning($"[FpsWave] Sparo sorgente mancante: {src}");
                    continue;
                }
                try
                {
                    WavIo.Read(src, out float[][] channels, out int rate);
                    float[][] shot = ShotEdit.Prepare(channels, rate, OutputRate, seconds, mono);
                    WavIo.Write(Path.Combine(OwnDir, output + ".wav"), shot, OutputRate);
                    processed++;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[FpsWave] Errore preparando {src}: {e.Message}");
                }
            }
            AssetDatabase.Refresh();
            ConfigureImporters();
            BuildLibrary();
            Debug.Log($"[FpsWave] Audio pronto: {processed} spari rifiniti, libreria in {LibraryPath}.");
        }

        private static void ConfigureImporters()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { SourceDir, OwnDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not AudioImporter importer)
                    continue;
                // Le registrazioni originali a 96 kHz non vengono usate a runtime: compresse al minimo indispensabile.
                bool far = path.Contains("_far");
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.7f;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = far;
                importer.loadInBackground = false;
                importer.SaveAndReimport();
            }
        }

        private static void BuildLibrary()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath));
            var library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<SoundLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            var cues = new List<SoundCue>
            {
                Cue(SoundId.RifleShotClose, Own("rifle_close"), 0.85f, 0.96f, 1.04f),
                Cue(SoundId.RifleShotFar, Own("rifle_far"), 1f, 0.94f, 1.04f, 160f),
                Cue(SoundId.PistolShotClose, Own("pistol_close"), 0.8f, 0.96f, 1.05f),
                Cue(SoundId.PistolShotFar, Own("pistol_far"), 1f, 0.94f, 1.05f, 140f),
                Cue(SoundId.ImpactSurface, Clips("KenneyImpact", "impactGeneric_light_"), 0.45f, 0.9f, 1.15f, 30f, 0.025f),
                Cue(SoundId.ImpactMetal, Clips("KenneyImpact", "impactMetal_light_"), 0.5f, 0.9f, 1.1f, 35f, 0.025f),
                Cue(SoundId.ImpactFlesh, Clips("KenneyImpact", "impactSoft_medium_"), 0.5f, 0.9f, 1.1f, 30f, 0.03f),
                Cue(SoundId.HitMarker, Clips("KenneyInterface", "tick_002"), 0.45f, 1f, 1.08f, 0f, 0.03f),
                Cue(SoundId.HitMarkerPrecision, Clips("KenneyInterface", "tick_004"), 0.6f, 1.12f, 1.2f, 0f, 0.03f),
                Cue(SoundId.KillConfirm, Clips("KenneyInterface", "confirmation_001"), 0.55f, 1f, 1f),
                Cue(SoundId.ArmorPlateHit, Clips("KenneyImpact", "impactPlate_light_"), 0.55f, 0.9f, 1.1f, 40f, 0.03f),
                Cue(SoundId.ArmorPlateBreak, Merge(Clips("KenneyImpact", "impactPlate_heavy_"), Clips("KenneyImpact", "impactMetal_heavy_")), 1f, 0.85f, 1f, 60f, 0.05f),
                Cue(SoundId.Footstep, Clips("KenneyImpact", "footstep_concrete_"), 0.35f, 0.92f, 1.06f),
                Cue(SoundId.Land, Clips("KenneyImpact", "impactSoft_heavy_"), 0.6f, 0.9f, 1f),
                Cue(SoundId.AbilityOvercharge, Clips("KenneySciFi", "thrusterFire_"), 0.6f, 1.1f, 1.2f, 40f),
                Cue(SoundId.AbilityArmor, Clips("KenneySciFi", "forceField_"), 0.7f, 0.9f, 1f, 40f),
                Cue(SoundId.AbilityScan, Clips("KenneySciFi", "computerNoise_"), 0.7f, 0.95f, 1.05f, 50f),
                Cue(SoundId.AbilityDenied, Clips("KenneyInterface", "error_004"), 0.5f, 1f, 1f),
                Cue(SoundId.AbilityReady, Clips("KenneyInterface", "confirmation_002"), 0.4f, 1f, 1f),
                Cue(SoundId.SuitCallout, Clips("KenneyInterface", "bong_001"), 0.35f, 1f, 1f, 0f, 0.15f),
                Cue(SoundId.SuitAlarm, Clips("KenneyInterface", "error_006"), 0.5f, 1f, 1f, 0f, 0.3f),
                Cue(SoundId.Ping, Clips("KenneyInterface", "select_003"), 0.6f, 1f, 1f, 0f, 0.1f),
                Cue(SoundId.ReviveTick, Clips("KenneyInterface", "toggle_001"), 0.5f, 1f, 1f),
                Cue(SoundId.ReviveDone, Clips("KenneyInterface", "confirmation_003"), 0.6f, 1f, 1f),
                Cue(SoundId.Downed, Clips("KenneyInterface", "glitch_002"), 0.7f, 0.9f, 0.9f),
                Cue(SoundId.EnemyHitPlayer, Clips("KenneyImpact", "impactPunch_heavy_"), 0.6f, 0.9f, 1.05f, 0f, 0.06f),
                Cue(SoundId.BruteWindup, Clips("KenneySciFi", "spaceEngineLow_"), 1f, 0.55f, 0.65f, 50f),
                Cue(SoundId.BruteChargeImpact, Clips("KenneySciFi", "explosionCrunch_"), 1f, 0.7f, 0.8f, 60f, 0.1f),
            };
            library.EditorSetCues(cues.ToArray());
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
        }

        private static SoundCue Cue(SoundId id, AudioClip[] clips, float volume, float pitchMin, float pitchMax,
            float maxDistance = 60f, float minInterval = 0f)
        {
            if (clips.Length == 0)
                Debug.LogWarning($"[FpsWave] Nessun clip per {id}.");
            return new SoundCue
            {
                Id = id,
                Clips = clips,
                Volume = volume,
                Pitch = new Vector2(pitchMin, pitchMax),
                MaxDistance = maxDistance > 0f ? maxDistance : 60f,
                MinInterval = minInterval,
            };
        }

        private static AudioClip[] Own(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{OwnDir}/{name}.wav");
            return clip != null ? new[] { clip } : Array.Empty<AudioClip>();
        }

        private static AudioClip[] Clips(string folder, string prefix)
        {
            string dir = $"{SourceDir}/{folder}";
            if (!AssetDatabase.IsValidFolder(dir))
                return Array.Empty<AudioClip>();
            var result = new List<AudioClip>();
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { dir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    result.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(path));
            }
            result.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return result.ToArray();
        }

        private static AudioClip[] Merge(AudioClip[] a, AudioClip[] b)
        {
            var all = new AudioClip[a.Length + b.Length];
            a.CopyTo(all, 0);
            b.CopyTo(all, a.Length);
            return all;
        }
    }

    /// <summary>Rifinitura di uno sparo registrato (logica pura, coperta da test).</summary>
    public static class ShotEdit
    {
        /// <summary>
        /// Taglia dall'attacco (primo campione oltre l'8% del picco, con 3 ms di anticipo), tiene <paramref name="seconds"/>
        /// secondi con una sfumatura sull'ultimo 45%, ricampiona a <paramref name="outRate"/> (interpolazione lineare)
        /// e normalizza il picco a 0.95. Con <paramref name="mono"/> i canali vengono mediati.
        /// </summary>
        public static float[][] Prepare(float[][] channels, int inRate, int outRate, float seconds, bool mono)
        {
            int frames = channels[0].Length;
            float peak = 0f;
            for (int c = 0; c < channels.Length; c++)
                for (int i = 0; i < frames; i++)
                    peak = Mathf.Max(peak, Mathf.Abs(channels[c][i]));
            if (peak <= 0f)
                throw new InvalidOperationException("file silenzioso");

            int onset = 0;
            float threshold = peak * 0.08f;
            for (int i = 0; i < frames && onset == 0; i++)
                for (int c = 0; c < channels.Length; c++)
                    if (Mathf.Abs(channels[c][i]) >= threshold) { onset = Mathf.Max(0, i - (int)(inRate * 0.003f)); break; }

            int outFrames = Mathf.Max(1, Mathf.RoundToInt(seconds * outRate));
            int outChannels = mono ? 1 : channels.Length;
            var output = new float[outChannels][];
            for (int c = 0; c < outChannels; c++)
                output[c] = new float[outFrames];

            double step = (double)inRate / outRate;
            float fadeStart = outFrames * 0.55f;
            float outPeak = 0f;
            for (int i = 0; i < outFrames; i++)
            {
                double srcPos = onset + i * step;
                int i0 = (int)srcPos;
                if (i0 + 1 >= frames)
                    break;
                float t = (float)(srcPos - i0);
                float fade = i < fadeStart ? 1f : 1f - (i - fadeStart) / (outFrames - fadeStart);
                fade *= fade;
                for (int c = 0; c < outChannels; c++)
                {
                    float v;
                    if (mono)
                    {
                        v = 0f;
                        for (int k = 0; k < channels.Length; k++)
                            v += Mathf.Lerp(channels[k][i0], channels[k][i0 + 1], t);
                        v /= channels.Length;
                    }
                    else
                    {
                        v = Mathf.Lerp(channels[c][i0], channels[c][i0 + 1], t);
                    }
                    v *= fade;
                    output[c][i] = v;
                    outPeak = Mathf.Max(outPeak, Mathf.Abs(v));
                }
            }
            if (outPeak > 0f)
            {
                float gain = 0.95f / outPeak;
                for (int c = 0; c < outChannels; c++)
                    for (int i = 0; i < outFrames; i++)
                        output[c][i] *= gain;
            }
            return output;
        }
    }

    /// <summary>Lettura/scrittura WAV PCM (16/24/32 bit intero o 32 bit float) senza dipendenze.</summary>
    public static class WavIo
    {
        public static void Read(string path, out float[][] channels, out int sampleRate)
        {
            using var reader = new BinaryReader(File.OpenRead(path));
            if (new string(reader.ReadChars(4)) != "RIFF")
                throw new InvalidDataException("non è un WAV");
            reader.ReadInt32();
            if (new string(reader.ReadChars(4)) != "WAVE")
                throw new InvalidDataException("non è un WAV");
            int format = 1, channelCount = 2, bits = 16;
            sampleRate = 44100;
            while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
            {
                string id = new string(reader.ReadChars(4));
                int size = reader.ReadInt32();
                long next = reader.BaseStream.Position + size + (size & 1);
                if (id == "fmt ")
                {
                    format = reader.ReadInt16();
                    channelCount = reader.ReadInt16();
                    sampleRate = reader.ReadInt32();
                    reader.ReadInt32();
                    reader.ReadInt16();
                    bits = reader.ReadInt16();
                    if (format == -2 && size >= 26) // WAVE_FORMAT_EXTENSIBLE: il sottoformato è nei primi 2 byte del GUID
                    {
                        reader.ReadInt16();
                        reader.ReadInt16();
                        reader.ReadInt32();
                        format = reader.ReadInt16();
                    }
                }
                else if (id == "data")
                {
                    int bytesPerSample = bits / 8;
                    int frames = size / (bytesPerSample * channelCount);
                    channels = new float[channelCount][];
                    for (int c = 0; c < channelCount; c++)
                        channels[c] = new float[frames];
                    for (int i = 0; i < frames; i++)
                        for (int c = 0; c < channelCount; c++)
                            channels[c][i] = ReadSample(reader, format, bits);
                    return;
                }
                reader.BaseStream.Position = next;
            }
            throw new InvalidDataException("blocco data mancante");
        }

        private static float ReadSample(BinaryReader reader, int format, int bits)
        {
            if (format == 3 && bits == 32)
                return reader.ReadSingle();
            switch (bits)
            {
                case 16: return reader.ReadInt16() / 32768f;
                case 24:
                    int b0 = reader.ReadByte(), b1 = reader.ReadByte(), b2 = reader.ReadByte();
                    int v = (b2 << 24) | (b1 << 16) | (b0 << 8);
                    return (v >> 8) / 8388608f;
                case 32: return reader.ReadInt32() / 2147483648f;
                default: throw new InvalidDataException($"{bits} bit non supportati");
            }
        }

        public static void Write(string path, float[][] channels, int sampleRate)
        {
            int channelCount = channels.Length;
            int frames = channels[0].Length;
            int dataSize = frames * channelCount * 2;
            using var writer = new BinaryWriter(File.Create(path));
            writer.Write("RIFF".ToCharArray());
            writer.Write(36 + dataSize);
            writer.Write("WAVE".ToCharArray());
            writer.Write("fmt ".ToCharArray());
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channelCount);
            writer.Write(sampleRate);
            writer.Write(sampleRate * channelCount * 2);
            writer.Write((short)(channelCount * 2));
            writer.Write((short)16);
            writer.Write("data".ToCharArray());
            writer.Write(dataSize);
            for (int i = 0; i < frames; i++)
                for (int c = 0; c < channelCount; c++)
                    writer.Write((short)Mathf.Clamp(Mathf.RoundToInt(channels[c][i] * 32767f), -32768, 32767));
        }
    }
}
