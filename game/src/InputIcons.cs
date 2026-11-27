using Godot;

namespace WitchApprentice;

/// <summary>
/// Иконки клавиш и кнопок геймпада (пак «Controller &amp; Keyboard Icons» от Vryell, куплен владельцем проекта).
/// Пак нельзя распространять, поэтому атлас <c>res://assets/input_icons.png</c> не хранится в репозитории
/// (см. tools/make_input_icons.py). Если атласа нет — вместо иконок выводятся текстовые подписи.
/// </summary>
public static class InputIcons
{
    public enum Id { W, A, S, D, Space, Esc, MouseLeft, Key1, Key2, Key3, Cross, R1, Dpad, StickL, StickR, Options, Enter }

    static readonly string[] Fallback = { "W", "A", "S", "D", "Пробел", "Esc", "ЛКМ", "1", "2", "3", "✕", "R1", "D-pad", "L", "R", "Options", "Enter" };

    static Texture2D _atlas;
    static bool _loaded;

    public static Texture2D Atlas
    {
        get
        {
            if (!_loaded)
            {
                _loaded = true;
                const string p = "res://assets/input_icons.png";
                if (ResourceLoader.Exists(p)) _atlas = GD.Load<Texture2D>(p);
            }
            return _atlas;
        }
    }

    /// <summary>Рисует иконку (или текст) и возвращает её ширину.</summary>
    public static float Draw(CanvasItem ci, Id id, Vector2 pos, float scale = 2)
    {
        if (Atlas != null)
        {
            ci.DrawTextureRectRegion(Atlas, new Rect2(pos, new Vector2(16, 16) * scale), new Rect2((int)id * 16, 0, 16, 16));
            return 16 * scale;
        }
        var font = ThemeDB.FallbackFont;
        string t = Fallback[(int)id];
        int size = (int)(7 * scale);
        float w = font.GetStringSize(t, HorizontalAlignment.Left, -1, size).X + 8;
        ci.DrawRect(new Rect2(pos + new Vector2(0, 2), new Vector2(w, 12 * scale)), new Color(0.15f, 0.12f, 0.22f, 0.9f));
        ci.DrawRect(new Rect2(pos + new Vector2(0, 2), new Vector2(w, 12 * scale)), new Color(0.8f, 0.75f, 0.95f), false, 1.5f);
        ci.DrawString(font, pos + new Vector2(4, 10 * scale), t, HorizontalAlignment.Left, -1, size, Colors.White);
        return w;
    }
}
