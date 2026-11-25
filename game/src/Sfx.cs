using System;
using System.Collections.Generic;
using Godot;

namespace WitchApprentice;

/// <summary>Простые синтезированные звуки (без файлов): генерируются один раз при запуске.</summary>
public partial class Sfx : Node
{
    enum Wave { Square, Triangle, Saw, Sine }

    const int Rate = 22050;
    readonly Dictionary<string, AudioStreamWav> _sounds = new();
    readonly Dictionary<string, ulong> _last = new();
    readonly List<AudioStreamPlayer> _players = new();
    int _next;

    public override void _Ready()
    {
        Add("hit", 220, 0.05f, Wave.Square, 0.25f);
        Add("hurt", 160, 0.2f, Wave.Saw, 0.5f, -100);
        Add("spark", 880, 0.06f, Wave.Triangle, 0.25f, 400);
        Add("fire", 200, 0.25f, Wave.Saw, 0.35f, -120);
        Add("thorns", 120, 0.2f, Wave.Triangle, 0.5f, 80);
        Add("zap", 1200, 0.12f, Wave.Saw, 0.25f, -900);
        Add("dash", 400, 0.12f, Wave.Triangle, 0.35f, 400);
        Add("xp", 1320, 0.04f, Wave.Sine, 0.15f);
        Add("lvl", 784, 0.3f, Wave.Triangle, 0.5f, 400);
        Add("flower", 990, 0.2f, Wave.Sine, 0.5f, 300);
        Add("page", 523, 0.4f, Wave.Triangle, 0.6f, 520);
        Add("elite", 140, 0.4f, Wave.Saw, 0.4f, -80);
        Add("boss", 90, 1.0f, Wave.Saw, 0.6f, -40);
        Add("bossShot", 110, 0.3f, Wave.Square, 0.4f, -50);
        Add("pick", 660, 0.15f, Wave.Triangle, 0.5f, 300);
        Add("win", 523, 0.6f, Wave.Triangle, 0.6f, 500);
        for (int i = 0; i < 12; i++)
        {
            var p = new AudioStreamPlayer { VolumeDb = -14 };
            AddChild(p);
            _players.Add(p);
        }
    }

    void Add(string name, float freq, float dur, Wave wave, float vol, float slide = 0)
    {
        int n = (int)(Rate * dur);
        var data = new byte[n * 2];
        double phase = 0;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            double f = Math.Max(30, freq + slide * t);
            phase += f / Rate;
            double ph = phase - Math.Floor(phase);
            double s = wave switch
            {
                Wave.Square => ph < 0.5 ? 1 : -1,
                Wave.Triangle => 1 - 4 * Math.Abs(ph - 0.5),
                Wave.Saw => 2 * ph - 1,
                _ => Math.Sin(phase * Math.Tau),
            };
            double env = Math.Pow(1 - t, 2) * Math.Min(1, i / (Rate * 0.004));
            short v = (short)(s * env * vol * short.MaxValue);
            data[i * 2] = (byte)(v & 0xff);
            data[i * 2 + 1] = (byte)((v >> 8) & 0xff);
        }
        _sounds[name] = new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = Rate, Stereo = false, Data = data };
    }

    public void Play(string name)
    {
        if (!_sounds.TryGetValue(name, out var s)) return;
        ulong now = Time.GetTicksMsec();
        if (_last.TryGetValue(name, out var last) && now - last < 45) return;
        _last[name] = now;
        var p = _players[_next];
        _next = (_next + 1) % _players.Count;
        p.Stream = s;
        p.Play();
    }
}
