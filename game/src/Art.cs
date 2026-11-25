using System.Collections.Generic;
using Godot;

namespace WitchApprentice;

/// <summary>Текстуры спрайтлистов и перекраска ведьмы под выбранные школы.</summary>
public static class Art
{
    const int Tile = 16;
    static readonly Dictionary<Sheet, Texture2D> Textures = new();
    static readonly Dictionary<Sheet, int> Columns = new() { [Sheet.Dungeon] = 12, [Sheet.Creatures] = 10, [Sheet.Town] = 12 };
    static readonly Dictionary<string, ImageTexture> Recolored = new();

    public static void Load()
    {
        Textures[Sheet.Dungeon] = GD.Load<Texture2D>("res://assets/dungeon.png");
        Textures[Sheet.Creatures] = GD.Load<Texture2D>("res://assets/creatures.png");
        Textures[Sheet.Town] = GD.Load<Texture2D>("res://assets/town.png");
    }

    public static Texture2D Tex(Sheet s) => Textures[s];

    public static Rect2 Region(SpriteRef r)
    {
        int cols = Columns[r.Sheet];
        return new Rect2(r.Index % cols * Tile, r.Index / cols * Tile, Tile, Tile);
    }

    /// <summary>Рисует спрайт так, что pos — точка «ног» (низ по центру).</summary>
    public static void Draw(CanvasItem ci, SpriteRef r, Vector2 pos, float scale, bool flip = false, Color? mod = null, float rot = 0, Vector2? squash = null)
    {
        var sq = squash ?? Vector2.One;
        ci.DrawSetTransform(pos, rot, new Vector2((flip ? -scale : scale) * sq.X, scale * sq.Y));
        ci.DrawTextureRectRegion(Textures[r.Sheet], new Rect2(-8, -16, 16, 16), Region(r), mod ?? Colors.White);
        ci.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }

    /// <summary>Рисует спрайт по центру.</summary>
    public static void DrawCentered(CanvasItem ci, SpriteRef r, Vector2 pos, float scale, float rot = 0, Color? mod = null)
    {
        ci.DrawSetTransform(pos, rot, new Vector2(scale, scale));
        ci.DrawTextureRectRegion(Textures[r.Sheet], new Rect2(-8, -8, 16, 16), Region(r), mod ?? Colors.White);
        ci.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }

    /// <summary>Тайл спрайтлиста с перекрашенными фиолетовыми пикселями (платье/шляпа).</summary>
    public static ImageTexture RecoloredTile(SpriteRef r, Color? light, Color? dark)
    {
        string key = $"{r.Sheet}:{r.Index}:{light?.ToHtml()}:{dark?.ToHtml()}";
        if (Recolored.TryGetValue(key, out var cached)) return cached;
        var rect = Region(r);
        var img = Textures[r.Sheet].GetImage().GetRegion(new Rect2I((int)rect.Position.X, (int)rect.Position.Y, Tile, Tile));
        img.Convert(Image.Format.Rgba8);
        if (light.HasValue && dark.HasValue)
        {
            for (int y = 0; y < Tile; y++)
            for (int x = 0; x < Tile; x++)
            {
                var c = img.GetPixel(x, y);
                if (c.A < 0.5f) continue;
                if (Near(c, Data.BaseDressLight)) img.SetPixel(x, y, light.Value);
                else if (Near(c, Data.BaseDressDark)) img.SetPixel(x, y, dark.Value);
            }
        }
        var tex = ImageTexture.CreateFromImage(img);
        Recolored[key] = tex;
        return tex;
    }

    static bool Near(Color a, Color b) =>
        Mathf.Abs(a.R - b.R) < 0.06f && Mathf.Abs(a.G - b.G) < 0.06f && Mathf.Abs(a.B - b.B) < 0.06f;

    /// <summary>Ведьма: тело + шляпа, перекрашенные по образу. pos — точка ног.</summary>
    public static void DrawWitch(CanvasItem ci, WitchLook look, Vector2 pos, float scale, bool flip, float anim, bool flash)
    {
        if (look.Aura.HasValue)
        {
            var a = look.Aura.Value;
            for (int i = 4; i >= 1; i--)
                ci.DrawCircle(pos + new Vector2(0, -8 * scale), 6 * scale * i / 2.2f, new Color(a, 0.07f));
        }
        ci.DrawSetTransform(pos + new Vector2(0, -1), 0, new Vector2(scale, scale * 0.45f));
        ci.DrawCircle(Vector2.Zero, 6, new Color(0, 0, 0, 0.35f));
        ci.DrawSetTransform(Vector2.Zero, 0, Vector2.One);

        float bob = Mathf.Abs(Mathf.Sin(anim)) * 1.2f;
        var squash = new Vector2(1 + Mathf.Sin(anim * 2) * 0.03f, 1 - Mathf.Sin(anim * 2) * 0.03f);
        var body = RecoloredTile(Data.WitchBody, look.DressLight, look.DressDark);
        var hat = RecoloredTile(Data.WitchHat, look.HatLight, look.HatDark);
        var mod = flash ? new Color(4, 4, 4) : Colors.White;
        var sx = (flip ? -scale : scale) * squash.X;
        var sy = scale * squash.Y;
        ci.DrawSetTransform(pos + new Vector2(0, -bob * scale), 0, new Vector2(sx, sy));
        ci.DrawTexture(body, new Vector2(-8, -16), mod);
        ci.DrawTexture(hat, new Vector2(-8, -25 + Mathf.Sin(anim * 0.5f) * 0.4f), mod);
        ci.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }
}

/// <summary>Внешний вид ведьмы: каждая страница гримуара меняет его часть.</summary>
public sealed class WitchLook
{
    public Color? DressLight, DressDark, HatLight, HatDark, Aura;
}
