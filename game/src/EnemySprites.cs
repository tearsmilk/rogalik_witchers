using System.Collections.Generic;
using Godot;

namespace WitchApprentice;

/// <summary>
/// Кадры анимации врагов, заранее нарисованные в текстуры при запуске.
/// В бою враг — обычный Sprite2D: движок рисует их пачкой вместо сотен векторных фигур на каждого.
/// </summary>
public partial class EnemySprites : Node
{
    public const int Frames = 8;

    public sealed class Sheet
    {
        public Texture2D Texture;
        public Vector2 Cell, Origin;
        public float Freq;
    }

    public static readonly Dictionary<string, Sheet> Sheets = new();

    static float FreqOf(string id) => id switch { "bat" => 22, "slime" => 6, "boss" => 3, _ => 9 };

    public override void _Ready()
    {
        foreach (var def in Data.Enemies.Values)
        {
            float s = def.Scale;
            var cell = new Vector2(Mathf.Ceil(60 * s), Mathf.Ceil(72 * s));
            var origin = new Vector2(Mathf.Floor(cell.X / 2), cell.Y - 10 * s);
            var vp = new SubViewport
            {
                Size = new Vector2I((int)cell.X * Frames, (int)cell.Y), TransparentBg = true, Disable3D = true,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Once, RenderTargetClearMode = SubViewport.ClearMode.Once,
            };
            var d = def;
            vp.AddChild(new Painter2(ci =>
            {
                for (int k = 0; k < Frames; k++)
                {
                    var fake = new Enemy { Def = d, Phase = k * Mathf.Tau / Frames };
                    Painter.Enemy(ci, fake, 0, false, new Vector2(k * cell.X + origin.X, origin.Y));
                }
            }));
            AddChild(vp);
            Sheets[def.Id] = new Sheet { Texture = vp.GetTexture(), Cell = cell, Origin = origin, Freq = FreqOf(def.Id) };
        }
    }

    public static int FrameOf(Enemy e, float t)
    {
        var sh = Sheets[e.Def.Id];
        float ph = Mathf.PosMod(t * sh.Freq + e.Phase, Mathf.Tau);
        return Mathf.Clamp((int)(ph / Mathf.Tau * Frames), 0, Frames - 1);
    }

    partial class Painter2 : Node2D
    {
        readonly System.Action<CanvasItem> _paint;
        public Painter2(System.Action<CanvasItem> paint) { _paint = paint; }
        public override void _Draw() => _paint(this);
    }
}
