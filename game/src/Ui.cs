using System.Linq;
using Godot;

namespace WitchApprentice;

/// <summary>Интерфейс: HUD поверх мира и меню (титул, выбор улучшений, пауза, итог).</summary>
public partial class Ui : CanvasLayer
{
    static readonly Color Gold = new("#f8e7a1");
    static readonly Color Ink = new("#241d36");

    readonly Main _main;
    Hud _hud;
    Control _modal;
    string _bannerTitle, _bannerSub;
    float _bannerT, _bannerDur;

    public Ui(Main main) { _main = main; }

    public override void _Ready()
    {
        var vignette = new Vignette { MouseFilter = Control.MouseFilterEnum.Ignore };
        AddChild(vignette);
        vignette.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _hud = new Hud(_main, this) { MouseFilter = Control.MouseFilterEnum.Ignore };
        AddChild(_hud);
        _hud.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
    }

    public bool MouseOverUi() => false;

    public void Tick(float dt)
    {
        if (_bannerT > 0) _bannerT -= dt;
        _hud.QueueRedraw();
    }

    public void ShowBanner(string title, string sub, float dur)
    {
        _bannerTitle = title; _bannerSub = sub; _bannerT = dur; _bannerDur = dur;
    }

    public (string title, string sub, float alpha)? Banner =>
        _bannerT > 0 ? (_bannerTitle, _bannerSub, Mathf.Clamp(Mathf.Min(_bannerT, _bannerDur - _bannerT) * 3, 0, 1)) : null;

    // ---------- Конструкторы виджетов ----------
    static Label MakeLabel(string text, int size, Color color, bool wrap = false)
    {
        var l = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center };
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", color);
        l.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.85f));
        l.AddThemeConstantOverride("outline_size", size >= 28 ? 8 : 4);
        if (wrap) { l.AutowrapMode = TextServer.AutowrapMode.WordSmart; l.CustomMinimumSize = new Vector2(640, 0); }
        return l;
    }

    static StyleBoxFlat Box(Color bg, Color border, int bw)
    {
        var sb = new StyleBoxFlat { BgColor = bg, BorderColor = border };
        sb.SetBorderWidthAll(bw);
        sb.SetCornerRadiusAll(6);
        sb.SetContentMarginAll(14);
        return sb;
    }

    static Button MakeButton(string text, Color accent, Vector2 size, System.Action onPress, int fontSize = 18)
    {
        var b = new Button { Text = text, CustomMinimumSize = size, AutowrapMode = TextServer.AutowrapMode.WordSmart, FocusMode = Control.FocusModeEnum.All };
        b.AddThemeStyleboxOverride("normal", Box(Ink, accent, 2));
        b.AddThemeStyleboxOverride("hover", Box(new Color("#3a2f55"), accent, 3));
        b.AddThemeStyleboxOverride("pressed", Box(new Color("#4a3d6b"), accent, 3));
        b.AddThemeStyleboxOverride("focus", Box(new Color(1, 1, 1, 0.08f), Colors.White, 3));
        b.AddThemeStyleboxOverride("disabled", Box(Ink, accent, 2));
        b.AddThemeColorOverride("font_disabled_color", new Color("#eeeeee"));
        b.AddThemeFontSizeOverride("font_size", fontSize);
        b.AddThemeColorOverride("font_color", new Color("#eeeeee"));
        b.AddThemeColorOverride("font_hover_color", Colors.White);
        b.Pressed += onPress;
        return b;
    }

    VBoxContainer NewModal(float dim)
    {
        _modal?.QueueFree();
        var root = new ColorRect { Color = new Color(0.03f, 0.02f, 0.07f, dim) };
        AddChild(root);
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var center = new CenterContainer();
        root.AddChild(center);
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var v = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        v.AddThemeConstantOverride("separation", 14);
        center.AddChild(v);
        _modal = root;
        Callable.From(() => FocusFirst(root)).CallDeferred();
        return v;
    }

    /// <summary>Фокус на первую кнопку (для геймпада/Enter) и короткая блокировка от случайного нажатия.</summary>
    void FocusFirst(Control root)
    {
        if (!IsInstanceValid(root)) return;
        var buttons = new System.Collections.Generic.List<Button>();
        void Walk(Node n) { foreach (var c in n.GetChildren()) { if (c is Button b) buttons.Add(b); Walk(c); } }
        Walk(root);
        if (buttons.Count == 0) return;
        foreach (var b in buttons) b.Disabled = true;
        GetTree().CreateTimer(0.35).Timeout += () =>
        {
            foreach (var b in buttons) if (IsInstanceValid(b)) b.Disabled = false;
            if (IsInstanceValid(buttons[0])) buttons[0].GrabFocus();
        };
    }

    void CloseModal()
    {
        _modal?.QueueFree();
        _modal = null;
    }

    // ---------- Экраны ----------
    public void ShowHud()
    {
        CloseModal();
        _hud.Visible = true;
    }

    public void ShowTitle()
    {
        _hud.Visible = false;
        _modal?.QueueFree();
        var root = new Control();
        AddChild(root);
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _modal = root;

        var bg = new TextureRect { Texture = GD.Load<Texture2D>("res://assets/forest_bg.svg"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = Control.MouseFilterEnum.Ignore };
        root.AddChild(bg);
        bg.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var portrait = new TextureRect { Texture = GD.Load<Texture2D>("res://assets/witch_portrait.svg"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore };
        root.AddChild(portrait);
        portrait.AnchorLeft = 0.48f; portrait.AnchorRight = 1f; portrait.AnchorTop = 0.02f; portrait.AnchorBottom = 1f;

        var left = new MarginContainer();
        root.AddChild(left);
        left.AnchorLeft = 0; left.AnchorRight = 0.52f; left.AnchorTop = 0; left.AnchorBottom = 1;
        left.AddThemeConstantOverride("margin_left", 70);
        var v = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        v.AddThemeConstantOverride("separation", 16);
        left.AddChild(v);

        var title = MakeLabel("Ведьма-ученица", 66, Gold);
        title.HorizontalAlignment = HorizontalAlignment.Left;
        v.AddChild(title);
        var story = MakeLabel("Юная ведьма проспала экзамен, а бесы растащили гримуар наставницы по Проклятому лесу. " +
            $"Найди {Data.PagesToBoss} страницы, собери букет, выбери школы магии и одолей Короля бесов!", 18, new Color("#ddd6ee"), wrap: true);
        story.HorizontalAlignment = HorizontalAlignment.Left;
        story.CustomMinimumSize = new Vector2(480, 0);
        v.AddChild(story);
        v.AddChild(new Control { CustomMinimumSize = new Vector2(0, 10) });
        v.AddChild(LeftAligned(MakeButton("Начать забег", Gold, new Vector2(300, 60), () => _main.StartRun(), 22)));
        v.AddChild(LeftAligned(MakeButton("Выйти", new Color("#9a8fb5"), new Vector2(300, 48), () => _main.GetTree().Quit())));
        v.AddChild(new Control { CustomMinimumSize = new Vector2(0, 10) });
        var keys = MakeLabel("Клавиатура: WASD — бег · ЛКМ — стрельба в курсор · Пробел — рывок · Esc — пауза\n" +
            "DualSense: левый стик — бег · правый стик — прицел · ✕ или R1 — рывок · Options — пауза", 14, new Color("#a99fc4"));
        keys.HorizontalAlignment = HorizontalAlignment.Left;
        v.AddChild(keys);
        if (_main.BestWin > 0)
        {
            var best = MakeLabel("Лучшее время победы: " + Fmt(_main.BestWin), 14, new Color("#a99fc4"));
            best.HorizontalAlignment = HorizontalAlignment.Left;
            v.AddChild(best);
        }
        Callable.From(() => FocusFirst(root)).CallDeferred();
    }

    static Control LeftAligned(Control c)
    {
        var row = new HBoxContainer();
        row.AddChild(c);
        return row;
    }

    public void ShowChoices(string title, string subtitle, bool isPage)
    {
        var v = NewModal(0.72f);
        v.AddChild(MakeLabel(title, 40, Gold));
        v.AddChild(MakeLabel(subtitle, 16, new Color("#cccccc")));
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 20);
        for (int i = 0; i < _main.Choices.Count; i++)
        {
            int idx = i;
            var c = _main.Choices[i];
            row.AddChild(MakeButton($"{c.Title}\n\n{c.Desc}\n\n[{i + 1}]", c.Color, new Vector2(250, 180), () => _main.ApplyChoice(idx), 17));
        }
        v.AddChild(row);
        if (isPage) v.AddChild(new WitchPreview(_main, 1.8f, new Vector2(170, 170)));
    }

    public void ShowPause()
    {
        var v = NewModal(0.7f);
        v.AddChild(MakeLabel("Пауза", 44, Gold));
        v.AddChild(Centered(MakeButton("Продолжить", Gold, new Vector2(260, 56), () => _main.SetPaused(false))));
        v.AddChild(Centered(MakeButton("В главное меню", new Color("#bbbbbb"), new Vector2(260, 56), () => _main.ToTitle())));
        v.AddChild(Centered(MakeButton("Выйти из игры", new Color("#e57373"), new Vector2(260, 56), () => _main.GetTree().Quit())));
    }

    public void ShowEnd(bool win)
    {
        var g = _main.G;
        var v = NewModal(0.78f);
        string title, text;
        if (win)
        {
            bool fast = g.T < 8 * 60;
            title = fast ? "Экзамен сдан с отличием!" : "Экзамен сдан!";
            text = fast ? "Наставница впечатлена: гримуар собран, а ты ещё и успела к чаю." : "Гримуар снова цел. Наставница ворчит, но ставит «зачёт».";
        }
        else
        {
            title = "Ведьма выбилась из сил…";
            text = "Совы вынесли тебя из леса. Экзамен придётся пересдавать.";
        }
        v.AddChild(MakeLabel(title, 44, win ? Gold : new Color("#e57373")));
        v.AddChild(MakeLabel(text, 18, new Color("#dddddd")));
        v.AddChild(MakeLabel($"Время: {Fmt(g.T)}   ·   Уровень: {g.P.Level}   ·   Побеждено бесов: {g.Kills}   ·   Страницы: {g.PagesFound}", 17, new Color("#dddddd")));
        v.AddChild(new WitchPreview(_main, 1.8f, new Vector2(170, 170)));
        v.AddChild(Centered(MakeButton("Новый забег", Gold, new Vector2(260, 60), () => _main.StartRun(), 20)));
        v.AddChild(Centered(MakeButton("В главное меню", new Color("#bbbbbb"), new Vector2(260, 50), () => _main.ToTitle())));
    }

    static Control Centered(Control c)
    {
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddChild(c);
        return row;
    }

    public static string Fmt(float t) => $"{(int)(t / 60)}:{(int)(t % 60):00}";
}

/// <summary>Ведьма крупным планом в меню (в текущем образе).</summary>
public partial class WitchPreview : Control
{
    readonly Main _main;
    readonly float _scale;

    public WitchPreview(Main main, float scale, Vector2 size)
    {
        _main = main; _scale = scale;
        CustomMinimumSize = size;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        var look = _main.G?.P.Look ?? new WitchLook();
        Painter.Witch(this, look, new Vector2(Size.X / 2, Size.Y - 8), _scale, false, _main.UiTime * 3, false);
    }
}

/// <summary>Тёмный туман по краям экрана (радиальный градиент).</summary>
public partial class Vignette : TextureRect
{
    public Vignette()
    {
        var grad = new Gradient();
        grad.SetColor(0, new Color(0.02f, 0.04f, 0.03f, 0f));
        grad.SetColor(1, new Color(0.02f, 0.04f, 0.03f, 0.8f));
        grad.SetOffset(0, 0.45f);
        grad.SetOffset(1, 1.0f);
        Texture = new GradientTexture2D
        {
            Gradient = grad, Width = 256, Height = 256,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1.05f, 1.05f),
        };
        ExpandMode = ExpandModeEnum.IgnoreSize;
        StretchMode = StretchModeEnum.Scale;
        TextureFilter = TextureFilterEnum.Linear;
    }
}

/// <summary>Полоски здоровья/опыта, таймер, заклинания, букет, полоса босса, баннеры и стрелки к целям.</summary>
public partial class Hud : Control
{
    readonly Main _main;
    readonly Ui _ui;
    static readonly Color Gold = new("#f8e7a1");

    public Hud(Main main, Ui ui) { _main = main; _ui = ui; }

    void Text(string s, Vector2 pos, int size, Color c, HorizontalAlignment align = HorizontalAlignment.Left, float width = -1)
    {
        var font = ThemeDB.FallbackFont;
        if (align != HorizontalAlignment.Left && width < 0)
        {
            float w = font.GetStringSize(s, HorizontalAlignment.Left, -1, size).X;
            pos.X -= align == HorizontalAlignment.Center ? w / 2 : w;
        }
        DrawStringOutline(font, pos, s, HorizontalAlignment.Left, -1, size, 5, new Color(0, 0, 0, c.A * 0.9f));
        DrawString(font, pos, s, HorizontalAlignment.Left, -1, size, c);
    }

    void Bar(Rect2 r, float frac, Color fg)
    {
        DrawRect(r.Grow(2), new Color("#3f2631"));
        DrawRect(r, new Color(0, 0, 0, 0.6f));
        DrawRect(new Rect2(r.Position, new Vector2(r.Size.X * Mathf.Clamp(frac, 0, 1), r.Size.Y)), fg);
    }

    public override void _Draw()
    {
        var g = _main.G;
        if (g == null) return;
        var p = g.P;
        var W = Size.X; var H = Size.Y;

        Bar(new Rect2(0, 0, W, 8), p.Xp / p.XpNext, new Color("#8e7cc3"));
        Bar(new Rect2(18, 22, 230, 18), p.Hp / p.MaxHp, new Color("#e74c3c"));
        Text($"ОЗ {Mathf.CeilToInt(p.Hp)} / {p.MaxHp}", new Vector2(26, 37), 15, Colors.White);
        Text($"Уровень {p.Level}", new Vector2(18, 64), 16, Colors.White);

        Text(Ui.Fmt(g.T), new Vector2(W / 2, 42), 26, Colors.White, HorizontalAlignment.Center);
        Text($"Страницы гримуара: {g.PagesFound} / {Data.PagesToBoss}", new Vector2(W - 18, 36), 16, Gold, HorizontalAlignment.Right);
        Text($"Побеждено: {g.Kills}", new Vector2(W - 18, 58), 15, new Color("#dddddd"), HorizontalAlignment.Right);

        float y = 92;
        foreach (var (id, lv) in p.Spells)
        {
            var s = Data.Spells[id];
            DrawCircle(new Vector2(26, y - 5), 6, s.Color);
            Text($"{s.Name} {lv}", new Vector2(38, y), 14, new Color("#eeeeee"));
            y += 21;
        }
        Text(p.DashCd > 0 ? $"Рывок: {p.DashCd:0.0}" : "Рывок готов [Пробел]", new Vector2(18, y + 4), 14, p.DashCd > 0 ? new Color("#888888") : new Color("#cceeee"));

        if (p.Bouquet.Count > 0)
        {
            Text("Букет:", new Vector2(18, H - 20), 14, new Color("#dddddd"));
            int i = 0;
            foreach (var f in p.Bouquet.TakeLast(24))
            {
                var c = new Vector2(78 + i * 15, H - 25);
                DrawCircle(c, 6, new Color("#3f2631"));
                DrawCircle(c, 4.5f, f.Color);
                i++;
            }
        }

        if (g.Boss != null)
        {
            float w = Mathf.Min(520, W - 80);
            Text("Король бесов", new Vector2(W / 2, H - 56), 15, Colors.White, HorizontalAlignment.Center);
            Bar(new Rect2(W / 2 - w / 2, H - 46, w, 14), g.Boss.Hp / g.Boss.MaxHp, new Color("#c0392b"));
        }

        if (_ui.Banner is { } b)
        {
            var c1 = new Color(Gold, b.alpha);
            Text(b.title, new Vector2(W / 2, H * 0.22f), 34, c1, HorizontalAlignment.Center);
            Text(b.sub, new Vector2(W / 2, H * 0.22f + 32), 18, new Color(1, 1, 1, b.alpha), HorizontalAlignment.Center);
        }

        // Стрелки к страницам, хранителям и боссу за пределами экрана
        var cam = _main.CameraCenter;
        var targets = g.Pages.Select(pg => (pg.Pos, new Color("#f8e7a1")))
            .Concat(g.Enemies.Where(e => e.IsElite || e.IsBoss).Select(e => (e.Pos, e.IsBoss ? new Color("#ff5252") : new Color("#f4d03f"))));
        foreach (var (pos, col) in targets)
        {
            var sp = pos - cam + Size / 2;
            if (sp.X > 0 && sp.Y > 0 && sp.X < W && sp.Y < H) continue;
            float a = (sp - Size / 2).Angle();
            var ap = new Vector2(Mathf.Clamp(W / 2 + Mathf.Cos(a) * W, 30, W - 30), Mathf.Clamp(H / 2 + Mathf.Sin(a) * H, 30, H - 30));
            var pts = new[] { new Vector2(16, 0), new Vector2(-9, -10), new Vector2(-9, 10) }.Select(v => ap + v.Rotated(a)).ToArray();
            DrawColoredPolygon(pts, col);
            DrawPolyline(pts.Append(pts[0]).ToArray(), new Color("#3f2631"), 2);
        }
    }
}
