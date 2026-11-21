using System;
using UnityEngine;

namespace HrdnLab.FpsWave.Core
{
    /// <summary>
    /// Impostazioni del giocatore, salvate in PlayerPrefs e applicate subito.
    /// Letti dal gioco: sensibilità (PlayerInputReader), FOV (PlayerLook), numeri di danno e FPS (HUD).
    /// Applicati qui: volume generale, schermo intero, VSync, qualità grafica.
    /// </summary>
    public static class GameSettings
    {
        public const float MinSensitivity = 0.02f, MaxSensitivity = 0.5f, DefaultSensitivity = 0.12f;
        public const float MinFov = 70f, MaxFov = 110f, DefaultFov = 90f;

        private const string Prefix = "fpswave.settings.";

        private static bool s_loaded;
        private static float s_sensitivity = DefaultSensitivity;
        private static float s_fov = DefaultFov;
        private static float s_volume = 1f;
        private static bool s_fullscreen = true;
        private static bool s_vsync = true;
        private static int s_quality = -1;
        private static bool s_showFps = true;
        private static bool s_damageNumbers = true;

        /// <summary>Qualunque impostazione è cambiata (già salvata e applicata).</summary>
        public static event Action Changed;

        public static float MouseSensitivity { get { Load(); return s_sensitivity; } set => Set(ref s_sensitivity, Mathf.Clamp(value, MinSensitivity, MaxSensitivity)); }
        public static float FieldOfView { get { Load(); return s_fov; } set => Set(ref s_fov, Mathf.Clamp(value, MinFov, MaxFov)); }
        public static float MasterVolume { get { Load(); return s_volume; } set => Set(ref s_volume, Mathf.Clamp01(value)); }
        public static bool Fullscreen { get { Load(); return s_fullscreen; } set => Set(ref s_fullscreen, value); }
        public static bool VSync { get { Load(); return s_vsync; } set => Set(ref s_vsync, value); }
        public static int QualityLevel { get { Load(); return s_quality; } set => Set(ref s_quality, Mathf.Clamp(value, 0, Mathf.Max(0, QualitySettings.names.Length - 1))); }
        public static bool ShowFps { get { Load(); return s_showFps; } set => Set(ref s_showFps, value); }
        public static bool ShowDamageNumbers { get { Load(); return s_damageNumbers; } set => Set(ref s_damageNumbers, value); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_loaded = false;
            Changed = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyOnStartup()
        {
            Load();
            Apply();
        }

        private static void Load()
        {
            if (s_loaded)
                return;
            s_loaded = true;
            s_sensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "sensitivity", DefaultSensitivity), MinSensitivity, MaxSensitivity);
            s_fov = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "fov", DefaultFov), MinFov, MaxFov);
            s_volume = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "volume", 1f));
            s_fullscreen = PlayerPrefs.GetInt(Prefix + "fullscreen", 1) == 1;
            s_vsync = PlayerPrefs.GetInt(Prefix + "vsync", 1) == 1;
            s_quality = PlayerPrefs.GetInt(Prefix + "quality", QualitySettings.GetQualityLevel());
            s_showFps = PlayerPrefs.GetInt(Prefix + "showFps", 1) == 1;
            s_damageNumbers = PlayerPrefs.GetInt(Prefix + "damageNumbers", 1) == 1;
        }

        private static void Set<T>(ref T field, T value)
        {
            Load();
            if (Equals(field, value))
                return;
            field = value;
            Save();
            Apply();
            Changed?.Invoke();
        }

        private static void Save()
        {
            PlayerPrefs.SetFloat(Prefix + "sensitivity", s_sensitivity);
            PlayerPrefs.SetFloat(Prefix + "fov", s_fov);
            PlayerPrefs.SetFloat(Prefix + "volume", s_volume);
            PlayerPrefs.SetInt(Prefix + "fullscreen", s_fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "vsync", s_vsync ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "quality", s_quality);
            PlayerPrefs.SetInt(Prefix + "showFps", s_showFps ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "damageNumbers", s_damageNumbers ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>Applica le impostazioni di sistema (le altre le leggono i componenti di gioco).</summary>
        public static void Apply()
        {
            AudioListener.volume = s_volume;
            QualitySettings.vSyncCount = s_vsync ? 1 : 0;
            if (s_quality >= 0 && s_quality < QualitySettings.names.Length && QualitySettings.GetQualityLevel() != s_quality)
                QualitySettings.SetQualityLevel(s_quality, true);
            if (!Application.isEditor)
                Screen.fullScreenMode = s_fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        }

        /// <summary>Riporta tutto ai valori predefiniti.</summary>
        public static void ResetToDefaults()
        {
            Load();
            s_sensitivity = DefaultSensitivity;
            s_fov = DefaultFov;
            s_volume = 1f;
            s_fullscreen = true;
            s_vsync = true;
            s_showFps = true;
            s_damageNumbers = true;
            Save();
            Apply();
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Stato condiviso tra menu e gameplay: con un menu aperto il player non riceve input e il cursore è libero.
    /// </summary>
    public static class UiState
    {
        /// <summary>Un menu (pausa, impostazioni...) è aperto sopra il gioco.</summary>
        public static bool MenuOpen { get; set; }

        /// <summary>C'è un menu che gestisce Esc da sé (altrimenti Esc libera solo il cursore, come prima).</summary>
        public static bool MenuHandlesEscape { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            MenuOpen = false;
            MenuHandlesEscape = false;
        }
    }
}
