using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace WitchApprentice;

/// <summary>Корневой узел: состояние игры, ввод (клавиатура, мышь, DualSense), камера, свет и отрисовка мира.</summary>
public partial class Main : Node2D
{
    public GameWorld G;
    public GameState State = GameState.Title;
    public List<Choice> Choices = new();
    public float BestWin;
    public float UiTime;
    public bool UsingGamepad;

    Camera2D _camera;
    Sfx _sfx;
    Ui _ui;
    PointLight2D _light;
    Vector2 _titleCam;
    readonly Dictionary<(int, int), Painter.TreeGeom> _trees = new();

    const string SavePath = "user://save.cfg";
    const float TreeCell = 260;

    public override void _Ready()
    {
        SetupInput();
        LoadBest();
        RenderingServer.SetDefaultClearColor(new Color("#07060b"));

        _camera = new Camera2D();
        AddChild(_camera);
        _camera.MakeCurrent();

        // Ночь: весь мир затемнён, вокруг ведьмы — свет.
        AddChild(new CanvasModulate { Color = new Color(0.19f, 0.17f, 0.27f) });
        _light = new PointLight2D
        {
            Texture = RadialTexture(512, new Color(1, 1, 1, 1), new Color(1, 1, 1, 0), 0.0f),
            TextureScale = 1.45f,
            Energy = 1.5f,
            Color = new Color("#d9c8ff"),
            BlendMode = Light2D.BlendModeEnum.Add,
        };
        AddChild(_light);

        // Слой свечения поверх мира: не затемняется, смешивается сложением.
        var fxLayer = new CanvasLayer { Layer = 1, FollowViewportEnabled = true };
        AddChild(fxLayer);
        fxLayer.AddChild(new Glow(this) { Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add } });
        fxLayer.AddChild(new Overlay(this));

        _sfx = new Sfx();
        AddChild(_sfx);

        _ui = new Ui(this) { Layer = 10 };
        AddChild(_ui);
        _ui.ShowTitle();

        var testDir = Autotest.DirFromArgs();
        if (!string.IsNullOrEmpty(testDir)) AddChild(new Autotest(this, testDir));
    }

    public static GradientTexture2D RadialTexture(int size, Color inner, Color outer, float innerOffset)
    {
        var grad = new Gradient();
        grad.SetColor(0, inner);
        grad.SetColor(1, outer);
        grad.SetOffset(0, innerOffset);
        return new GradientTexture2D
        {
            Gradient = grad, Width = size, Height = size,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f),
        };
    }

    static void SetupInput()
    {
        void Ensure(string action, float deadzone = 0.5f)
        {
            if (!InputMap.HasAction(action)) InputMap.AddAction(action, deadzone);
        }
        void Keys(string action, params Key[] keys)
        {
            Ensure(action);
            foreach (var k in keys) InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = k });
        }
        void Pad(string action, params JoyButton[] buttons)
        {
            Ensure(action);
            foreach (var b in buttons) InputMap.ActionAddEvent(action, new InputEventJoypadButton { ButtonIndex = b });
        }
        void Axis(string action, JoyAxis axis, float dir)
        {
            Ensure(action, 0.2f);
            InputMap.ActionSetDeadzone(action, 0.2f);
            InputMap.ActionAddEvent(action, new InputEventJoypadMotion { Axis = axis, AxisValue = dir });
        }

        Keys("move_left", Key.A, Key.Left);
        Keys("move_right", Key.D, Key.Right);
        Keys("move_up", Key.W, Key.Up);
        Keys("move_down", Key.S, Key.Down);
        // DualSense: левый стик и крестовина — бег, правый стик — прицел.
        Axis("move_left", JoyAxis.LeftX, -1); Axis("move_right", JoyAxis.LeftX, 1);
        Axis("move_up", JoyAxis.LeftY, -1); Axis("move_down", JoyAxis.LeftY, 1);
        Pad("move_left", JoyButton.DpadLeft); Pad("move_right", JoyButton.DpadRight);
        Pad("move_up", JoyButton.DpadUp); Pad("move_down", JoyButton.DpadDown);
        Axis("aim_left", JoyAxis.RightX, -1); Axis("aim_right", JoyAxis.RightX, 1);
        Axis("aim_up", JoyAxis.RightY, -1); Axis("aim_down", JoyAxis.RightY, 1);

        Keys("dash", Key.Space);
        Pad("dash", JoyButton.A, JoyButton.RightShoulder);             // Cross / R1
        Keys("pause", Key.Escape, Key.P);
        Pad("pause", JoyButton.Start);                                  // Options
        Keys("choose_1", Key.Key1);
        Keys("choose_2", Key.Key2);
        Keys("choose_3", Key.Key3);
        Keys("fullscreen", Key.F11);

        // Пробел — это рывок, пусть он не нажимает кнопки меню.
        foreach (var ev in InputMap.ActionGetEvents("ui_accept"))
            if (ev is InputEventKey k && (k.Keycode == Key.Space || k.PhysicalKeycode == Key.Space))
                InputMap.ActionEraseEvent("ui_accept", ev);
    }

    public override void _Input(InputEvent e)
    {
        if (e is InputEventJoypadButton || (e is InputEventJoypadMotion m && Mathf.Abs(m.AxisValue) > 0.3f))
        {
            if (!UsingGamepad) { UsingGamepad = true; Input.MouseMode = Input.MouseModeEnum.Hidden; }
        }
        else if (e is InputEventMouseMotion mm && mm.Relative.Length() > 2 || e is InputEventMouseButton)
        {
            if (UsingGamepad) { UsingGamepad = false; Input.MouseMode = Input.MouseModeEnum.Visible; }
        }
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

    void OnSound(string name)
    {
        _sfx.Play(name);
        if (!UsingGamepad) return;
        switch (name)
        {
            case "hurt": Rumble(0.4f, 0.7f, 0.22f); break;
            case "boss": Rumble(0.6f, 1f, 0.7f); break;
            case "bossShot": Rumble(0.2f, 0.35f, 0.15f); break;
            case "page": Rumble(0.5f, 0.2f, 0.25f); break;
            case "lvl": Rumble(0.3f, 0f, 0.12f); break;
            case "dash": Rumble(0.15f, 0f, 0.08f); break;
        }
    }

    static void Rumble(float weak, float strong, float dur)
    {
        foreach (var id in Input.GetConnectedJoypads()) Input.StartJoyVibration(id, weak, strong, dur);
    }

    public void StartRun()
    {
        G = new GameWorld { ViewSize = GetViewportRect().Size };
        G.Sound += OnSound;
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
            _ui.ShowChoices("Новый уровень!", "Выбери улучшение", false);
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
                _titleCam += new Vector2(14, 5) * dt;
                _camera.Position = _titleCam;
                _light.Position = _titleCam + new Vector2(260, 40);
                break;
            case GameState.Play:
                if (Input.IsActionJustPressed("pause")) { SetPaused(true); break; }
                G.ViewSize = GetViewportRect().Size;
                G.Update(dt, ReadInput());
                if (G.WinAt > 0 && G.T >= G.WinAt) Win();
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
        }

        if (G != null)
        {
            var shake = G.Shake > 0 ? new Vector2((float)GD.RandRange(-6, 6), (float)GD.RandRange(-6, 6)) * G.Shake * 3 : Vector2.Zero;
            _camera.Position = G.P.Pos + new Vector2(0, -30);
            _camera.Offset = shake;
            _light.Position = G.P.Pos + new Vector2(0, -40);
            _light.Color = (G.P.Look.Aura ?? new Color("#d9c8ff")).Lerp(Colors.White, 0.45f);
        }
        QueueRedraw();
        _ui.Tick(dt);
    }

    InputState ReadInput()
    {
        var move = Input.GetVector("move_left", "move_right", "move_up", "move_down");
        var aim = Input.GetVector("aim_left", "aim_right", "aim_up", "aim_down");
        var s = new InputState { Move = move, Dash = Input.IsActionJustPressed("dash") };
        if (aim.Length() > 0.35f)
        {
            // правый стик: стрельба в направлении стика
            s.AimAtCursor = true;
            s.Cursor = G.CastOrigin + aim.Normalized() * 300;
        }
        else if (UsingGamepad)
        {
            s.AimAtCursor = false;
            s.Cursor = G.P.Pos + (move != Vector2.Zero ? move : new Vector2(G.P.Face, 0)) * 200;
        }
        else
        {
            s.AimAtCursor = Input.IsMouseButtonPressed(MouseButton.Left);
            s.Cursor = GetGlobalMousePosition();
        }
        return s;
    }

    void Win()
    {
        State = GameState.Win;
        if (BestWin <= 0 || G.T < BestWin) { BestWin = G.T; SaveBest(); }
        _sfx.Play("win");
        _ui.ShowEnd(true);
    }

    // ---------- Отрисовка мира (затемняется ночью, освещается светом ведьмы) ----------
    public static float Hash(int x, int y, int s = 0)
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

    public Vector2 CameraCenter => _camera.Position;

    static readonly Color[] Leaves = { new("#8a4a26"), new("#6e3a22"), new("#a65f2c"), new("#5b2f1f"), new("#7d2e3a") };

    public override void _Draw()
    {
        var view = GetViewportRect().Size;
        var center = _camera.Position;
        var tl = center - view / 2 - new Vector2(40, 40);
        var size = view + new Vector2(80, 80);
        DrawRect(new Rect2(tl, size), new Color("#4a4556"));

        const float cell = 96;
        int x0 = (int)Mathf.Floor(tl.X / cell), y0 = (int)Mathf.Floor(tl.Y / cell);
        int x1 = (int)Mathf.Floor((tl.X + size.X) / cell), y1 = (int)Mathf.Floor((tl.Y + size.Y) / cell);
        for (int cx = x0; cx <= x1; cx++)
        for (int cy = y0; cy <= y1; cy++)
        {
            var o = new Vector2(cx * cell, cy * cell);
            if (Hash(cx, cy, 1) < 0.35f)
                Painter.Ellipse(this, o + new Vector2(Hash(cx, cy, 11) * cell, Hash(cx, cy, 12) * cell), 60 + Hash(cx, cy, 13) * 50, 26 + Hash(cx, cy, 14) * 20, new Color("#38333f"));
            if (Hash(cx, cy, 2) < 0.09f)
            {
                int n = 2 + (int)(Hash(cx, cy, 21) * 4);
                for (int k = 0; k < n; k++)
                {
                    var c = o + new Vector2(Hash(cx, cy, 30 + k) * cell, Hash(cx, cy, 40 + k) * cell);
                    float w = 16 + Hash(cx, cy, 50 + k) * 12, h = w * 0.55f;
                    var pts = new[] { c + new Vector2(-w, 0), c + new Vector2(-w * 0.3f, -h), c + new Vector2(w * 0.8f, -h * 0.8f), c + new Vector2(w, h * 0.2f), c + new Vector2(w * 0.2f, h) };
                    DrawColoredPolygon(pts.Select(p => p + new Vector2(2, 3)).ToArray(), new Color("#26222c"));
                    DrawColoredPolygon(pts, new Color("#57525e"));
                    DrawColoredPolygon(new[] { pts[0], pts[1], pts[2], c }, new Color("#66606c"));
                }
            }
            if (Hash(cx, cy, 3) < 0.5f)
            {
                int n = 3 + (int)(Hash(cx, cy, 60) * 10);
                for (int k = 0; k < n; k++)
                {
                    var c = o + new Vector2(Hash(cx, cy, 70 + k) * cell, Hash(cx, cy, 90 + k) * cell);
                    float r = 2.5f + Hash(cx, cy, 110 + k) * 2.5f;
                    DrawSetTransform(c, Hash(cx, cy, 130 + k) * Mathf.Tau, Vector2.One);
                    DrawColoredPolygon(Painter.EllipsePts(r, r * 0.5f, 8), Leaves[(int)(Hash(cx, cy, 150 + k) * Leaves.Length)]);
                    DrawSetTransform(Vector2.Zero, 0, Vector2.One);
                }
            }
            if (Hash(cx, cy, 4) < 0.4f)
            {
                var c = o + new Vector2(Hash(cx, cy, 170) * cell, Hash(cx, cy, 171) * cell);
                var col = new Color("#3d4a35");
                for (int k = -2; k <= 2; k++) DrawLine(c + new Vector2(k * 2, 0), c + new Vector2(k * 3.5f, -7 - Mathf.Abs(k) * -1.5f), col, 1.5f);
            }
        }

        // Деревья (тени — отдельно и раньше всех, чтобы ложились на землю)
        var trees = VisibleTrees(tl, size).ToList();
        foreach (var t in trees) Painter.TreeShadow(this, t);

        var items = new List<(float y, Action draw)>();
        foreach (var t in trees)
        {
            var tree = t;
            items.Add((tree.Base.Y, () =>
            {
                float alpha = 1;
                if (G != null)
                {
                    var p = G.P.Pos;
                    if (tree.Base.Y > p.Y && tree.Base.Y - p.Y < tree.Height && Mathf.Abs(tree.Base.X - p.X) < 70) alpha = 0.4f;
                }
                // ворох листьев у корней
                for (int k = 0; k < 14; k++)
                {
                    float a = k * 2.4f, r = 10 + (k * 7) % 30;
                    Painter.Ellipse(this, tree.Base + new Vector2(Mathf.Cos(a) * r * 1.4f, Mathf.Sin(a) * r * 0.45f + 2), 4, 2, Leaves[k % Leaves.Length]);
                }
                Painter.Tree(this, tree, alpha);
            }));
        }

        if (G != null)
        {
            foreach (var e in G.Enemies)
            {
                var en = e;
                if (Mathf.Abs(en.Pos.X - center.X) > view.X / 2 + 160 || Mathf.Abs(en.Pos.Y - center.Y) > view.Y / 2 + 220) continue;
                items.Add((en.Pos.Y, () => Painter.Enemy(this, en, G.T, G.P.Pos.X < en.Pos.X)));
            }
            items.Add((G.P.Pos.Y, () =>
            {
                bool blink = G.P.IFrames > 0 && (int)(G.T * 20) % 2 == 0;
                Painter.Witch(this, G.P.Look, G.P.Pos, Data.WitchScale, G.P.Face < 0, G.P.Anim, blink);
            }));
        }
        foreach (var it in items.OrderBy(i => i.y)) it.draw();
    }

    IEnumerable<Painter.TreeGeom> VisibleTrees(Vector2 tl, Vector2 size)
    {
        int x0 = (int)Mathf.Floor(tl.X / TreeCell) - 1, y0 = (int)Mathf.Floor(tl.Y / TreeCell) - 1;
        int x1 = (int)Mathf.Floor((tl.X + size.X) / TreeCell) + 1, y1 = (int)Mathf.Floor((tl.Y + size.Y) / TreeCell) + 2;
        if (_trees.Count > 3000) _trees.Clear();
        for (int cy = y0; cy <= y1; cy++)
        for (int cx = x0; cx <= x1; cx++)
        {
            if (Hash(cx, cy, 300) > 0.55f) continue;
            if (!_trees.TryGetValue((cx, cy), out var t))
            {
                var basePos = new Vector2(cx * TreeCell + Hash(cx, cy, 301) * TreeCell, cy * TreeCell + Hash(cx, cy, 302) * TreeCell);
                t = Painter.BuildTree(basePos, new Random((int)(Hash(cx, cy, 303) * int.MaxValue)));
                _trees[(cx, cy)] = t;
            }
            yield return t;
        }
    }
}

/// <summary>Свечение: заклинания, глаза врагов, аура, опыт, страницы. Слой не затемняется ночью.</summary>
public partial class Glow : Node2D
{
    readonly Main _main;
    static GradientTexture2D _soft;

    public Glow(Main main) { _main = main; }

    public override void _Ready() =>
        _soft ??= Main.RadialTexture(128, new Color(1, 1, 1, 1), new Color(1, 1, 1, 0), 0f);

    public override void _Process(double delta) => QueueRedraw();

    void Soft(Vector2 at, float radius, Color c) => DrawTextureRect(_soft, new Rect2(at - Vector2.One * radius, Vector2.One * radius * 2), false, c);

    public override void _Draw()
    {
        var g = _main.G;
        if (g == null)
        {
            // светлячки на титульном экране
            var cam = _main.CameraCenter;
            for (int i = 0; i < 40; i++)
            {
                float t = _main.UiTime * 0.3f + i * 1.7f;
                var p = cam + new Vector2(Mathf.Sin(t * 0.7f + i) * 600, Mathf.Cos(t * 0.5f + i * 2.1f) * 380);
                float a = 0.4f + 0.4f * Mathf.Sin(_main.UiTime * 3 + i);
                Soft(p, 14, new Color(0.75f, 1f, 0.45f, a));
                Soft(p, 3, new Color(1, 1, 0.8f, a));
            }
            return;
        }
        var P = g.P;

        // аура и навершие посоха
        var aura = P.Look.Aura;
        if (aura.HasValue) Soft(P.Pos - new Vector2(0, 50), 110, new Color(aura.Value, 0.35f));
        var tip = Painter.StaffTip(P.Pos, Data.WitchScale, P.Face < 0, P.Anim);
        var gem = P.Look.DressLight ?? new Color("#b388ff");
        Soft(tip, 26, new Color(gem, 0.8f));
        Soft(tip, 7, new Color(1, 1, 1, 0.9f));
        // магический круг под ногами
        DrawSetTransform(P.Pos, 0, new Vector2(1, 0.42f));
        DrawArc(Vector2.Zero, 48, 0, Mathf.Tau, 48, new Color(gem, 0.35f), 2.5f);
        DrawArc(Vector2.Zero, 38, g.T, g.T + Mathf.Tau * 0.7f, 32, new Color(gem, 0.25f), 1.5f);
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);

        foreach (var r in g.Rings)
        {
            var c = new Color(r.C, Mathf.Clamp(r.T * 3, 0, 1));
            DrawSetTransform(r.Pos, 0, new Vector2(1, 0.55f));
            DrawArc(Vector2.Zero, r.R, 0, Mathf.Tau, 64, c, 8);
            DrawArc(Vector2.Zero, r.R * 0.85f, 0, Mathf.Tau, 64, new Color(c, c.A * 0.4f), 4);
            DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        }

        // ледяные осколки
        if (P.Spells.TryGetValue("frost", out int fl))
        {
            int n = GameWorld.FrostCount(fl);
            float R = GameWorld.FrostRadius(fl);
            for (int i = 0; i < n; i++)
            {
                float a = g.FrostAngle + i * Mathf.Tau / n;
                var c = P.Pos + new Vector2(0, -45) + Vector2.FromAngle(a) * R;
                Soft(c, 26, new Color(0.5f, 0.8f, 1f, 0.6f));
                var d = Vector2.FromAngle(a + Mathf.Pi / 2);
                var nrm = new Vector2(-d.Y, d.X);
                DrawColoredPolygon(new[] { c + d * 12, c + nrm * 4, c - d * 12, c - nrm * 4 }, new Color(0.8f, 0.95f, 1f, 0.95f));
            }
        }

        foreach (var b in g.Bullets)
        {
            if (b.Kind == "fireball")
            {
                Soft(b.Pos, 46, new Color(1f, 0.45f, 0.15f, 0.8f));
                Soft(b.Pos, 14, new Color(1f, 0.9f, 0.6f, 1f));
            }
            else
            {
                var tail = b.Pos - b.Vel.Normalized() * 26;
                DrawLine(tail, b.Pos, new Color(0.7f, 0.45f, 1f, 0.6f), 6);
                Soft(b.Pos, 26, new Color(0.75f, 0.45f, 1f, 0.85f));
                Soft(b.Pos, 7, new Color(1, 1, 1, 1));
            }
        }
        foreach (var b in g.FoeBullets)
        {
            Soft(b.Pos, b.R * 3, new Color(1f, 0.15f, 0.25f, 0.75f));
            Soft(b.Pos, b.R, new Color(1f, 0.8f, 0.8f, 1f));
        }
        foreach (var bl in g.Bolts)
        {
            var pts = new List<Vector2>();
            for (int i = 0; i < bl.Pts.Count; i++)
            {
                if (i > 0)
                    for (int k = 1; k < 4; k++)
                        pts.Add(bl.Pts[i - 1].Lerp(bl.Pts[i], k / 4f) + new Vector2((float)GD.RandRange(-10, 10), (float)GD.RandRange(-10, 10)));
                pts.Add(bl.Pts[i]);
            }
            float a = Mathf.Clamp(bl.T * 8, 0, 1);
            DrawPolyline(pts.ToArray(), new Color(1f, 0.85f, 0.3f, a * 0.5f), 10);
            DrawPolyline(pts.ToArray(), new Color(1f, 1f, 0.85f, a), 3);
        }
        foreach (var q in g.Particles)
            Soft(q.Pos, q.S * 1.6f, new Color(q.C, Mathf.Clamp(q.T * 3, 0, 1) * 0.9f));

        foreach (var gm in g.Gems) Soft(gm.Pos, 16, gm.V >= 3 ? new Color(1f, 0.5f, 0.7f, 0.5f) : new Color(0.4f, 0.9f, 1f, 0.5f));
        foreach (var f in g.Flowers) Soft(f.Pos - new Vector2(0, 14), 34, new Color(f.Kind.Color, 0.35f));
        foreach (var pg in g.Pages) Soft(pg.Pos - new Vector2(0, 18), 60, new Color(1f, 0.85f, 0.5f, 0.55f));

        // глаза врагов
        foreach (var e in g.Enemies)
        {
            bool faceLeft = P.Pos.X < e.Pos.X;
            var col = Painter.EyeColor(e);
            float r = e.IsBoss ? 7 : e.IsElite ? 5 : 3.2f;
            foreach (var eye in Painter.EnemyEyes(e, g.T, faceLeft))
            {
                Soft(eye, r * 3.2f, new Color(col, 0.7f));
                Soft(eye, r, new Color(1, 1, 0.9f, 1));
            }
            if (e.IsElite) Soft(e.Pos - new Vector2(0, Painter.BodyHeight(e)), 70, new Color(1f, 0.75f, 0.2f, 0.18f));
            if (e.IsBoss)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(g.T * 4);
                Soft(e.Pos - new Vector2(0, Painter.BodyHeight(e)), 160, new Color(1f, 0.1f, 0.15f, 0.15f + 0.1f * pulse));
                DrawSetTransform(e.Pos, 0, new Vector2(1, 0.4f));
                DrawArc(Vector2.Zero, 90, g.T, g.T + Mathf.Tau, 64, new Color(1f, 0.2f, 0.3f, 0.5f * pulse), 3);
                DrawSetTransform(Vector2.Zero, 0, Vector2.One);
            }
            if (e.Def.Id == "slime") Soft(e.Pos - new Vector2(0, 10), 34, new Color(0.6f, 0.2f, 1f, 0.3f));
        }
    }
}

/// <summary>Яркие предметы, полоски здоровья и всплывающий урон поверх ночи (обычное смешивание).</summary>
public partial class Overlay : Node2D
{
    readonly Main _main;
    public Overlay(Main main) { _main = main; }
    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        var g = _main.G;
        if (g == null) return;

        foreach (var gm in g.Gems)
        {
            var p = gm.Pos;
            var c = gm.V >= 3 ? new Color("#ff9ec4") : new Color("#8ee9ff");
            DrawColoredPolygon(new[] { p + new Vector2(0, -7), p + new Vector2(4.5f, 0), p + new Vector2(0, 7), p + new Vector2(-4.5f, 0) }, c);
            DrawColoredPolygon(new[] { p + new Vector2(0, -7), p + new Vector2(4.5f, 0), p }, new Color(1, 1, 1, 0.6f));
        }
        foreach (var f in g.Flowers)
        {
            var p = f.Pos + new Vector2(0, Mathf.Sin(f.Bob) * 2);
            DrawLine(p, p - new Vector2(0, 12), new Color("#4c8a4a"), 2);
            for (int i = 0; i < 5; i++)
            {
                var off = Vector2.FromAngle(i * Mathf.Tau / 5 + f.Bob * 0.2f) * 4.5f;
                DrawCircle(p - new Vector2(0, 16) + off, 3.6f, f.Kind.Color);
            }
            DrawCircle(p - new Vector2(0, 16), 2.4f, new Color("#fff59d"));
        }
        foreach (var pg in g.Pages)
        {
            var p = pg.Pos + new Vector2(0, -22 + Mathf.Sin(pg.Bob) * 4);
            DrawSetTransform(p, Mathf.Sin(pg.Bob * 0.7f) * 0.2f, Vector2.One * 1.6f);
            DrawRect(new Rect2(-7, -9, 14, 18), new Color("#3f2631"));
            DrawRect(new Rect2(-6, -8, 12, 16), new Color("#f8f1dc"));
            for (int i = 0; i < 4; i++) DrawRect(new Rect2(-4, -5 + i * 3.5f, i == 3 ? 5 : 8, 1.2f), new Color("#8d6e63"));
            DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        }

        foreach (var e in g.Enemies)
        {
            if (!(e.IsElite || e.Def.Id == "slime") || e.Hp >= e.MaxHp) continue;
            var bp = e.Pos - new Vector2(22, Painter.TopHeight(e) + 10);
            DrawRect(new Rect2(bp, new Vector2(44, 5)), new Color(0, 0, 0, 0.7f));
            DrawRect(new Rect2(bp, new Vector2(44 * e.Hp / e.MaxHp, 5)), new Color("#ff4d6d"));
        }
        // полоска здоровья ведьмы над головой
        var hp = g.P.Pos - new Vector2(20, 160);
        DrawRect(new Rect2(hp, new Vector2(40, 5)), new Color(0, 0, 0, 0.7f));
        DrawRect(new Rect2(hp, new Vector2(40 * g.P.Hp / g.P.MaxHp, 5)), new Color("#7cf08a"));

        var font = ThemeDB.FallbackFont;
        foreach (var t in g.Texts)
        {
            int size = t.Big ? 18 : t.Crit ? 22 : 16;
            float w = font.GetStringSize(t.Text, HorizontalAlignment.Left, -1, size).X;
            var pos = t.Pos - new Vector2(w / 2, 0);
            var col = new Color(t.C, Mathf.Clamp(t.T * 2, 0, 1));
            DrawStringOutline(font, pos, t.Text, HorizontalAlignment.Left, -1, size, 4, new Color(0, 0, 0, col.A));
            DrawString(font, pos, t.Text, HorizontalAlignment.Left, -1, size, col);
        }
    }
}
