using Godot;

namespace WitchApprentice;

public enum InputDevice { Auto, Keyboard, PlayStation }

/// <summary>Настройки игрока: управление, звук, качество эффектов. Хранятся в user://settings.cfg.</summary>
public static class Settings
{
    const string Path = "user://settings.cfg";

    public static InputDevice Device = InputDevice.Auto;
    public static float Master = 0.8f;   // 0..1
    public static float Effects = 0.8f;  // 0..1
    public static bool Muted;
    public static bool HighFx;           // false — облегчённые эффекты (по умолчанию)
    public static bool Fullscreen;

    public static void Load()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(Path) != Error.Ok) return;
        Device = (InputDevice)(int)cfg.GetValue("input", "device", 0);
        Master = (float)cfg.GetValue("audio", "master", Master);
        Effects = (float)cfg.GetValue("audio", "effects", Effects);
        Muted = (bool)cfg.GetValue("audio", "muted", false);
        HighFx = (bool)cfg.GetValue("video", "high_fx", false);
        Fullscreen = (bool)cfg.GetValue("video", "fullscreen", false);
    }

    public static void Save()
    {
        var cfg = new ConfigFile();
        cfg.SetValue("input", "device", (int)Device);
        cfg.SetValue("audio", "master", Master);
        cfg.SetValue("audio", "effects", Effects);
        cfg.SetValue("audio", "muted", Muted);
        cfg.SetValue("video", "high_fx", HighFx);
        cfg.SetValue("video", "fullscreen", Fullscreen);
        cfg.Save(Path);
    }

    /// <summary>Применяет громкость к шинам Master и SFX.</summary>
    public static void ApplyAudio()
    {
        int sfx = AudioServer.GetBusIndex("SFX");
        AudioServer.SetBusMute(0, Muted);
        AudioServer.SetBusVolumeDb(0, Mathf.LinearToDb(Mathf.Max(Master, 0.0001f)));
        if (sfx >= 0) AudioServer.SetBusVolumeDb(sfx, Mathf.LinearToDb(Mathf.Max(Effects, 0.0001f)));
    }

    public static void ApplyWindow()
    {
        var want = Fullscreen ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed;
        if (DisplayServer.WindowGetMode() != want) DisplayServer.WindowSetMode(want);
    }
}
