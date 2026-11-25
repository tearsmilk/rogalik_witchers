using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace WitchApprentice;

/// <summary>Корневой узел: состояние игры, ввод, камера и отрисовка мира.</summary>
public partial class Main : Node2D
{
    public GameWorld G;
    public GameState State = GameState.Title;
    public List<Choice> Choices = new();
    public float BestWin;
    public float UiTime;

    Camera2D _camera;
    Sfx _sfx;
    Ui _ui;
    Vector2 _titleCam;

    const string SavePath = "user://save.cfg";

    public override void _Ready()
    {
        SetupInput();
        Art.Load();
        LoadBest();

        _camera = new Camera2D();
        AddChild(_camera);
        _camera.MakeCurrent();

        _sfx = new Sfx();
        AddChild(_sfx);

        _ui = new Ui(this);
        AddChild(_ui);
        _ui.ShowTitle();

        var testDir = Autotest.DirFromArgs();
        if (!string.IsNullOrEmpty(testDir)) AddChild(new Autotest(this, testDir));
    }

    static void SetupInput()
    {
        void Keys(string action, params Key[] keys)
        {
            if (!InputMap.HasAction(action)) InputMap.AddAction(action);
            foreach (var k in keys) InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = k });
        }
        Keys("move_left", Key.A, Key.Left);
        Keys("move_right", Key.D, Key.Right);
        Keys("move_up", Key.W, Key.Up);
        Keys("move_down", Key.S, Key.Down);
        Keys("dash", Key.Space);
        Keys("pause", Key.Escape, Key.P);
        Keys("confirm", Key.Enter, Key.KpEnter);
        Keys("choose_1", Key.Key1);
        Keys("choose_2", Key.Key2);
        Keys("choose_3", Key.Key3);
        Keys("fullscreen", Key.F11);
    }

    void LoadBest()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(SavePath) == Error.Ok) BestWin = (float)cfg.GetValue("stats", "best_win", 0f);
    }

    void SaveBest()
    {
        var cfg = new ConfigFile();
        cfg.SetValue("stats", "best_win", BestWin);
        cfg.Save(SavePath);
    }

    public void StartRun()
    {
        G = new GameWorld { ViewSize = GetViewportRect().Size };
        G.Sound += _sfx.Play;
        G.Banner += _ui.ShowBanner;
        G.Start();
        State = GameState.Play;
        _ui.ShowHud();
    }

    public void ToTitle()
    {
        G = null;
        State = GameState.Title;
        _ui.ShowTitle();
    }

    public void SetPaused(bool paused)
    {
        if (G == null) return;
        if (paused && State == GameState.Play) { State = GameState.Pause; _ui.ShowPause(); }
        else if (!paused && State == GameState.Pause) { State = GameState.Play; _ui.ShowHud(); }
    }

    public void ApplyChoice(int i)
    {
        if (i < 0 || i >= Choices.Count) return;
        bool page = State == GameState.PageChoice;
        Choices[i].Apply();
        _sfx.Play("pick");
        if (page) G.AfterPageChosen();
        State = GameState.Play;
        _ui.ShowHud();
        OpenNextModal();
    }

    void OpenNextModal()
    {
        if (G.PageQueue > 0)
        {
            G.PageQueue--;
            Choices = G.PageChoices();
            if (Choices.Count > 0)
            {
                State = GameState.PageChoice;
                _ui.ShowChoices("Страница гримуара", "Новая школа магии меняет и заклинания, и твой образ", true);
                return;
            }
            G.AfterPageChosen();
        }
        if (G.LevelUpQueue > 0)
        {
            G.LevelUpQueue--;
            Choices = G.LevelUpChoices();
            State = GameState.LevelUp;
            _ui.ShowChoices("Новый уровень!", "Выбери улучшение (клик или 1–3)", false);
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusOut) SetPaused(true);
    }

    public override void _Process(double delta)
    {
        float dt = Mathf.Min((float)delta, 0.05f);
        UiTime += dt;

        if (Input.IsActionJustPressed("fullscreen"))
        {
            bool fs = DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen;
            DisplayServer.WindowSetMode(fs ? DisplayServer.WindowMode.Windowed : DisplayServer.WindowMode.Fullscreen);
        }

        switch (State)
        {
            case GameState.Title:
                if (Input.IsActionJustPressed("confirm") || Input.IsActionJustPressed("dash")) StartRun();
                _titleCam += new Vector2(18, 6) * dt;
                _camera.Position = _titleCam;
                break;
            case GameState.Play:
                if (Input.IsActionJustPressed("pause")) { SetPaused(true); break; }
                G.ViewSize = GetViewportRect().Size;
                var input = new InputState
                {
                    Move = Input.GetVector("move_left", "move_right", "move_up", "move_down"),
                    Dash = Input.IsActionJustPressed("dash"),
                    AimAtCursor = Input.IsMouseButtonPressed(MouseButton.Left) && !_ui.MouseOverUi(),
                    Cursor = GetGlobalMousePosition(),
                };
                bool win = G.Update(dt, input);
                if (win) Win();
                else if (G.PlayerDead) { State = GameState.Over; _ui.ShowEnd(false); }
                else OpenNextModal();
                break;
            case GameState.Pause:
                if (Input.IsActionJustPressed("pause")) SetPaused(false);
                break;
            case GameState.LevelUp:
            case GameState.PageChoice:
                for (int i = 0; i < 3; i++)
                    if (Input.IsActionJustPressed($"choose_{i + 1}")) { ApplyChoice(i); break; }
                break;
            case GameState.Over:
            case GameState.Win:
                if (Input.IsActionJustPressed("confirm")) StartRun();
                break;
        }

        if (G != null)
        {
            var shake = G.Shake > 0 ? new Vector2((float)GD.RandRange(-6, 6), (float)GD.RandRange(-6, 6)) * G.Shake * 3 : Vector2.Zero;
            _camera.Position = G.P.Pos + new Vector2(0, -20);
            _camera.Offset = shake;
        }
        QueueRedraw();
        _ui.Tick(dt);
    }

    void Win()
    {
        State = GameState.Win;
        if (BestWin <= 0 || G.T < BestWin) { BestWin = G.T; SaveBest(); }
        _sfx.Play("win");
        _ui.ShowEnd(true);
    }

    // ---------- Отрисовка мира ----------
    static float Hash(int x, int y, int s = 0)
    {
        unchecked
        {
            uint h = (uint)x * 0x8da6b343u ^ (uint)y * 0xd8163841u ^ (uint)(s + 1) * 0xcb1ab31fu;
            h ^= h >> 16; h *= 0x7feb352du;
            h ^= h >> 15; h *= 0x846ca68bu;
            h ^= h >> 16;
            return h / 4294967296f;
        }
    }

    static readonly Color GroundTint = new(0.62f, 0.74f, 0.64f);
    static readonly Color TreeTint = new(0.72f, 0.82f, 0.74f);

    public Vector2 CameraCenter => _camera.Position;

    public override void _Draw()
    {
        var view = GetViewportRect().Size;
        var center = _camera.Position;
        var tl = center - view / 2;
        const float S = Data.SpriteScale;
        const float tile = 16 * S;

        // Трава
        int x0 = (int)Mathf.Floor(tl.X / tile) - 1, y0 = (int)Mathf.Floor(tl.Y / tile) - 1;
        int x1 = (int)Mathf.Floor((tl.X + view.X) / tile) + 1, y1 = (int)Mathf.Floor((tl.Y + view.Y) / tile) + 1;
        var town = Art.Tex(Sheet.Town);
        for (int cx = x0; cx <= x1; cx++)
        for (int cy = y0; cy <= y1; cy++)
        {
            int idx = Data.GrassTiles[(int)(Hash(cx, cy) * Data.GrassTiles.Length)];
            DrawTextureRectRegion(town, new Rect2(cx * tile, cy * tile, tile + 0.5f, tile + 0.5f), Art.Region(new SpriteRef(Sheet.Town, idx)), GroundTint);
        }
        // Мелкий декор (кусты, грибы)
        for (int cx = x0; cx <= x1; cx++)
        for (int cy = y0; cy <= y1; cy++)
        {
            if (Hash(cx, cy, 40) > 0.035f) continue;
            int idx = Data.SmallDecor[(int)(Hash(cx, cy, 41) * Data.SmallDecor.Length)];
            Art.Draw(this, new SpriteRef(Sheet.Town, idx), new Vector2(cx * tile + tile / 2, cy * tile + tile), S * 0.8f, mod: GroundTint);
        }

        if (G == null)
        {
            DrawTrees(tl, view, null);
            return;
        }

        foreach (var r in G.Rings)
            DrawArc(r.Pos + new Vector2(0, -10), r.R, 0, Mathf.Tau, 48, new Color(r.C, Mathf.Clamp(r.T * 3, 0, 1)), 6);
        foreach (var g in G.Gems) DrawGem(g);
        foreach (var f in G.Flowers) DrawFlower(f);
        foreach (var pg in G.Pages) DrawPage(pg);

        DrawTrees(tl, view, G);

        // Ледяные осколки
        if (G.P.Spells.TryGetValue("frost", out int fl))
        {
            int n = GameWorld.FrostCount(fl);
            float R = GameWorld.FrostRadius(fl);
            for (int i = 0; i < n; i++)
            {
                float a = G.FrostAngle + i * Mathf.Tau / n;
                Art.DrawCentered(this, Data.IceSprite, G.P.Pos + new Vector2(0, -20) + Vector2.FromAngle(a) * R, 2.2f, a + Mathf.Pi / 2);
            }
        }

        foreach (var b in G.Bullets)
        {
            if (b.Kind == "fireball")
            {
                DrawCircle(b.Pos, 20, new Color(1f, 0.45f, 0.2f, 0.25f));
                Art.DrawCentered(this, Data.FireSprite, b.Pos, 2.4f, G.T * 8);
            }
            else
            {
                DrawCircle(b.Pos, 14, new Color(0.9f, 0.8f, 1f, 0.25f));
                Art.DrawCentered(this, Data.SparkSprite, b.Pos, 1.3f);
            }
        }
        foreach (var b in G.FoeBullets)
        {
            DrawCircle(b.Pos, b.R + 3, new Color(0.3f, 0, 0, 0.6f));
            DrawCircle(b.Pos, b.R, new Color("#ff5252"));
            DrawCircle(b.Pos, b.R * 0.45f, new Color("#ffcdd2"));
        }
        foreach (var bl in G.Bolts)
        {
            var pts = new List<Vector2>();
            for (int i = 0; i < bl.Pts.Count; i++)
            {
                if (i > 0)
                    for (int k = 1; k < 4; k++)
                        pts.Add(bl.Pts[i - 1].Lerp(bl.Pts[i], k / 4f) + new Vector2((float)GD.RandRange(-10, 10), (float)GD.RandRange(-10, 10)));
                pts.Add(bl.Pts[i]);
            }
            var c = new Color("#fff9c4", Mathf.Clamp(bl.T * 8, 0, 1));
            DrawPolyline(pts.ToArray(), new Color("#f7dc6f", c.A * 0.5f), 8);
            DrawPolyline(pts.ToArray(), c, 3);
        }
        foreach (var q in G.Particles)
            DrawRect(new Rect2(q.Pos - Vector2.One * q.S / 2, Vector2.One * q.S), new Color(q.C, Mathf.Clamp(q.T * 3, 0, 1)));

        var font = ThemeDB.FallbackFont;
        foreach (var t in G.Texts)
        {
            int size = t.Big ? 18 : t.Crit ? 20 : 15;
            float w = font.GetStringSize(t.Text, HorizontalAlignment.Left, -1, size).X;
            var pos = t.Pos - new Vector2(w / 2, 0);
            var col = new Color(t.C, Mathf.Clamp(t.T * 2, 0, 1));
            DrawStringOutline(font, pos, t.Text, HorizontalAlignment.Left, -1, size, 4, new Color(0, 0, 0, col.A));
            DrawString(font, pos, t.Text, HorizontalAlignment.Left, -1, size, col);
        }
    }

    /// <summary>Деревья, враги и ведьма рисуются вместе, отсортированные по Y, чтобы правильно перекрываться.</summary>
    void DrawTrees(Vector2 tl, Vector2 view, GameWorld g)
    {
        const float S = Data.SpriteScale;
        const float C = 150;
        var items = new List<(float y, Action draw)>();
        int x0 = (int)Mathf.Floor(tl.X / C) - 1, y0 = (int)Mathf.Floor(tl.Y / C) - 1;
        int x1 = (int)Mathf.Floor((tl.X + view.X) / C) + 1, y1 = (int)Mathf.Floor((tl.Y + view.Y) / C) + 3;
        for (int cy = y0; cy <= y1; cy++)
        for (int cx = x0; cx <= x1; cx++)
        {
            if (Hash(cx, cy, 30) > 0.30f) continue;
            var pos = new Vector2(cx * C + Hash(cx, cy, 31) * C, cy * C + Hash(cx, cy, 32) * C);
            int idx = Data.TreeTiles[(int)(Hash(cx, cy, 33) * Data.TreeTiles.Length)];
            float sc = S * (1.1f + Hash(cx, cy, 34) * 0.6f);
            items.Add((pos.Y, () =>
            {
                float alpha = 1;
                if (g != null)
                {
                    var p = g.P.Pos;
                    if (pos.Y > p.Y && pos.Y - p.Y < 16 * sc && Mathf.Abs(pos.X - p.X) < 8 * sc) alpha = 0.45f;
                }
                Art.Draw(this, new SpriteRef(Sheet.Town, idx), pos, sc, mod: new Color(TreeTint, alpha));
            }));
        }
        if (g != null)
        {
            foreach (var e in g.Enemies)
            {
                var en = e;
                if (Mathf.Abs(en.Pos.X - _camera.Position.X) > view.X / 2 + 100 || Mathf.Abs(en.Pos.Y - _camera.Position.Y) > view.Y / 2 + 150) continue;
                items.Add((en.Pos.Y, () => DrawEnemy(en)));
            }
            items.Add((g.P.Pos.Y, () =>
            {
                bool blink = g.P.IFrames > 0 && (int)(g.T * 20) % 2 == 0;
                Art.DrawWitch(this, g.P.Look, g.P.Pos, S, g.P.Face < 0, g.P.Anim, blink);
            }));
        }
        foreach (var it in items.OrderBy(i => i.y)) it.draw();
    }

    void DrawEnemy(Enemy e)
    {
        var d = e.Def;
        bool faceLeft = G.P.Pos.X < e.Pos.X;
        float hop = 0;
        var squash = Vector2.One;
        if (d.Id == "bat")
        {
            hop = 10 + Mathf.Sin(G.T * 10 + e.Phase) * 6;
            squash = new Vector2(1 + Mathf.Sin(G.T * 20 + e.Phase) * 0.15f, 1);
        }
        else if (d.Id == "slime")
        {
            float s = Mathf.Sin(G.T * 6 + e.Phase) * 0.1f;
            squash = new Vector2(1 + s, 1 - s);
        }
        else hop = Mathf.Abs(Mathf.Sin(G.T * 8 + e.Phase)) * 4;

        // тень
        DrawSetTransform(e.Pos, 0, new Vector2(1, 0.4f));
        DrawCircle(Vector2.Zero, e.R * 0.9f, new Color(0, 0, 0, 0.3f));
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);

        if (e.IsElite || e.IsBoss)
        {
            var glow = e.IsBoss ? new Color(1, 0.2f, 0.2f, 0.12f) : new Color(1, 0.85f, 0.2f, 0.14f);
            for (int i = 1; i <= 3; i++) DrawCircle(e.Pos - new Vector2(0, e.R), e.R * (0.8f + i * 0.4f), glow);
        }
        Color mod = e.HitFlash > 0 ? new Color(3, 3, 3) : e.Slow > 0 ? new Color(0.65f, 0.85f, 1.3f) : Colors.White;
        Art.Draw(this, d.Sprite, e.Pos - new Vector2(0, hop), d.Scale, faceLeft, mod, squash: squash);

        if (e.IsElite)
        {
            var pp = e.Pos - new Vector2(0, d.Scale * 16 + 18 + Mathf.Sin(G.T * 3) * 3);
            DrawPageIcon(pp, 1.4f);
        }
        if (e.IsBoss)
        {
            // корона
            var top = e.Pos - new Vector2(0, d.Scale * 16 + 4 + hop);
            var crown = new[] { top + new Vector2(-22, 0), top + new Vector2(-22, -18), top + new Vector2(-11, -8), top + new Vector2(0, -24),
                top + new Vector2(11, -8), top + new Vector2(22, -18), top + new Vector2(22, 0) };
            DrawColoredPolygon(crown, new Color("#f4d03f"));
            DrawPolyline(crown.Append(crown[0]).ToArray(), new Color("#3f2631"), 3);
        }
        if ((e.IsElite || d.Id == "slime") && e.Hp < e.MaxHp)
        {
            var bp = e.Pos - new Vector2(22, d.Scale * 16 + 10);
            DrawRect(new Rect2(bp, new Vector2(44, 5)), new Color(0, 0, 0, 0.6f));
            DrawRect(new Rect2(bp, new Vector2(44 * e.Hp / e.MaxHp, 5)), new Color("#e74c3c"));
        }
    }

    void DrawGem(Gem g)
    {
        var c = g.V >= 3 ? new Color("#f5b7b1") : new Color("#a9cce3");
        var p = g.Pos;
        DrawColoredPolygon(new[] { p + new Vector2(0, -9), p + new Vector2(6, 0), p + new Vector2(0, 9), p + new Vector2(-6, 0) }, new Color("#3f2631"));
        DrawColoredPolygon(new[] { p + new Vector2(0, -6), p + new Vector2(3.5f, 0), p + new Vector2(0, 6), p + new Vector2(-3.5f, 0) }, c);
    }

    void DrawFlower(Flower f)
    {
        var p = f.Pos + new Vector2(0, Mathf.Sin(f.Bob) * 2);
        DrawCircle(p - new Vector2(0, 14), 26, new Color(f.Kind.Color, 0.15f));
        const float px = 3;
        void Px(int x, int y, Color c) => DrawRect(new Rect2(p + new Vector2(x * px, y * px), new Vector2(px, px)), c);
        var stem = new Color("#2e7d32");
        for (int y = -4; y <= 0; y++) Px(0, y, stem);
        Px(1, -2, stem); Px(2, -3, stem);
        var outline = new Color("#3f2631");
        int[,] petals = { { 0, -8 }, { -2, -6 }, { 2, -6 }, { -1, -4 }, { 1, -4 } };
        for (int i = 0; i < petals.GetLength(0); i++)
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                Px(petals[i, 0] + dx, petals[i, 1] + dy, outline);
        for (int i = 0; i < petals.GetLength(0); i++) Px(petals[i, 0], petals[i, 1], f.Kind.Color);
        Px(0, -6, f.Kind.Color); Px(-1, -6, f.Kind.Color); Px(1, -6, f.Kind.Color); Px(0, -7, f.Kind.Color); Px(0, -5, f.Kind.Color);
        Px(0, -6, new Color("#fff59d"));
    }

    void DrawPage(Page pg)
    {
        var p = pg.Pos + new Vector2(0, -16 + Mathf.Sin(pg.Bob) * 4);
        for (int i = 3; i >= 1; i--) DrawCircle(p, 14 * i, new Color(1, 0.9f, 0.6f, 0.1f));
        DrawPageIcon(p, 2f);
    }

    void DrawPageIcon(Vector2 p, float s)
    {
        DrawRect(new Rect2(p - new Vector2(7, 9) * s, new Vector2(14, 18) * s), new Color("#3f2631"));
        DrawRect(new Rect2(p - new Vector2(6, 8) * s, new Vector2(12, 16) * s), new Color("#f8f1dc"));
        for (int i = 0; i < 4; i++)
            DrawRect(new Rect2(p + new Vector2(-4, -5 + i * 3.5f) * s, new Vector2(i == 3 ? 5 : 8, 1.2f) * s), new Color("#8d6e63"));
    }
}
