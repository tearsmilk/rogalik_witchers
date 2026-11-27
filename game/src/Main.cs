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
    /// <summary>Какие подсказки показывать: по выбору в настройках или по последнему устройству ввода.</summary>
    public bool ShowPadPrompts => Settings.Device == InputDevice.PlayStation || (Settings.Device == InputDevice.Auto && UsingGamepad);

    Camera2D _camera;
    Sfx _sfx;
    Ui _ui;
    public Vector2 LightPos;
    public Color LightColor = new("#d9c8ff");
    Vector2 _titleCam;

    const string SavePath = "user://save.cfg";
    const float TreeCell = 260;

    public override void _Ready()
    {
        SetupInput();
        LoadBest();
        Settings.Load();
        Settings.ApplyWindow();
        RenderingServer.SetDefaultClearColor(new Color("#07060b"));

        _camera = new Camera2D();
        AddChild(_camera);
        _camera.MakeCurrent();

        // Слой свечения поверх мира: не затемняется, смешивается сложением.
        var fxLayer = new CanvasLayer { Layer = 1, FollowViewportEnabled = true };
        AddChild(fxLayer);
        // Ночь: дешёвая «маска темноты» с прозрачным кругом вокруг ведьмы (вместо 2D-света).
        fxLayer.AddChild(new Darkness(this));
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

    public void PlayTestSound() => _sfx.Play("pick");
    public void OpenSettings() => _ui.ShowSettings(_ui.ShowTitle);

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
            Settings.Fullscreen = DisplayServer.WindowGetMode() != DisplayServer.WindowMode.Fullscreen;
            Settings.ApplyWindow();
            Settings.Save();
        }

        switch (State)
        {
            case GameState.Title:
                _titleCam += new Vector2(14, 5) * dt;
                _camera.Position = _titleCam;
                LightPos = _titleCam + new Vector2(260, 40);
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
            LightPos = G.P.Pos + new Vector2(0, -40);
            LightColor = G.P.Look.Aura ?? new Color("#d9c8ff");
        }
        SyncWorld();
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

    // ---------- Мир: узлы с кэшированной отрисовкой ----------
    // Земля и деревья рисуются один раз (Godot кэширует команды отрисовки узла),
    // каждый кадр перерисовываются только враги и ведьма. Глубину сортирует движок (Y-sort).
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

    Node2D _groundRoot, _actors;
    readonly Dictionary<(int, int), GroundChunk> _chunks = new();
    readonly Dictionary<(int, int), TreeView> _treeViews = new();
    readonly List<EnemyView> _enemyViews = new();
    WitchView _witchView;

    void EnsureWorldNodes()
    {
        if (_groundRoot != null) return;
        _groundRoot = new Node2D { ZIndex = -100 };
        AddChild(_groundRoot);
        _actors = new Node2D { YSortEnabled = true };
        AddChild(_actors);
        _witchView = new WitchView(this) { Visible = false };
        _actors.AddChild(_witchView);
    }

    void SyncWorld()
    {
        EnsureWorldNodes();
        var view = GetViewportRect().Size;
        var center = _camera.Position;
        var tl = center - view / 2;

        // земля: куски 768×768 вокруг камеры
        const float CS = GroundChunk.Size;
        int x0 = (int)Mathf.Floor((tl.X - 100) / CS), y0 = (int)Mathf.Floor((tl.Y - 100) / CS);
        int x1 = (int)Mathf.Floor((tl.X + view.X + 100) / CS), y1 = (int)Mathf.Floor((tl.Y + view.Y + 100) / CS);
        var needed = new HashSet<(int, int)>();
        for (int cx = x0; cx <= x1; cx++)
        for (int cy = y0; cy <= y1; cy++)
        {
            needed.Add((cx, cy));
            if (!_chunks.ContainsKey((cx, cy)))
            {
                var ch = new GroundChunk(cx, cy) { Position = new Vector2(cx * CS, cy * CS) };
                _groundRoot.AddChild(ch);
                _chunks[(cx, cy)] = ch;
            }
        }
        foreach (var k in _chunks.Keys.Where(k => !needed.Contains(k)).ToList()) { _chunks[k].QueueFree(); _chunks.Remove(k); }

        // деревья
        int tx0 = (int)Mathf.Floor((tl.X - 300) / TreeCell), ty0 = (int)Mathf.Floor((tl.Y - 200) / TreeCell);
        int tx1 = (int)Mathf.Floor((tl.X + view.X + 300) / TreeCell), ty1 = (int)Mathf.Floor((tl.Y + view.Y + 450) / TreeCell);
        var neededTrees = new HashSet<(int, int)>();
        for (int cy = ty0; cy <= ty1; cy++)
        for (int cx = tx0; cx <= tx1; cx++)
        {
            if (Hash(cx, cy, 300) > 0.55f) continue;
            neededTrees.Add((cx, cy));
            if (_treeViews.ContainsKey((cx, cy))) continue;
            var basePos = new Vector2(cx * TreeCell + Hash(cx, cy, 301) * TreeCell, cy * TreeCell + Hash(cx, cy, 302) * TreeCell);
            var geom = Painter.BuildTree(Vector2.Zero, new Random((int)(Hash(cx, cy, 303) * int.MaxValue)));
            var tv = new TreeView(geom) { Position = basePos };
            _actors.AddChild(tv);
            _treeViews[(cx, cy)] = tv;
        }
        foreach (var k in _treeViews.Keys.Where(k => !neededTrees.Contains(k)).ToList()) { _treeViews[k].QueueFree(); _treeViews.Remove(k); }

        // прозрачность дерева, если ведьма за ним
        if (G != null)
            foreach (var tv in _treeViews.Values)
            {
                var p = G.P.Pos;
                bool behind = tv.Position.Y > p.Y && tv.Position.Y - p.Y < tv.Geom.Height && Mathf.Abs(tv.Position.X - p.X) < 70;
                tv.Modulate = new Color(1, 1, 1, behind ? 0.4f : 1);
            }

        // враги: пул узлов, перерисовка каждый кадр
        int used = 0;
        if (G != null)
        {
            foreach (var e in G.Enemies)
            {
                if (Mathf.Abs(e.Pos.X - center.X) > view.X / 2 + 160 || Mathf.Abs(e.Pos.Y - center.Y) > view.Y / 2 + 260) continue;
                if (used == _enemyViews.Count)
                {
                    var ev = new EnemyView(this);
                    _actors.AddChild(ev);
                    _enemyViews.Add(ev);
                }
                var v = _enemyViews[used++];
                v.Enemy = e;
                v.Position = e.Pos;
                v.Visible = true;
                v.QueueRedraw();
            }
        }
        for (int i = used; i < _enemyViews.Count; i++) if (_enemyViews[i].Visible) { _enemyViews[i].Visible = false; _enemyViews[i].Enemy = null; }

        _witchView.Visible = G != null;
        if (G != null)
        {
            _witchView.Position = G.P.Pos;
            _witchView.QueueRedraw();
        }
    }
}

/// <summary>Кусок земли: плиты, листья, трава. Рисуется один раз.</summary>
public partial class GroundChunk : Node2D
{
    public const float Size = 768;
    const float Cell = 96;
    readonly int _cx, _cy;
    static readonly Color[] Leaves = { new("#8a4a26"), new("#6e3a22"), new("#a65f2c"), new("#5b2f1f"), new("#7d2e3a") };

    public GroundChunk(int cx, int cy) { _cx = cx; _cy = cy; }

    public override void _Draw()
    {
        DrawRect(new Rect2(0, 0, Size + 1, Size + 1), new Color("#4a4556"));
        int n = (int)(Size / Cell);
        for (int ix = 0; ix < n; ix++)
        for (int iy = 0; iy < n; iy++)
        {
            int cx = _cx * n + ix, cy = _cy * n + iy;
            var o = new Vector2(ix * Cell, iy * Cell);
            float H(int s) => Main.Hash(cx, cy, s);
            if (H(1) < 0.35f)
                Painter.Ellipse(this, o + new Vector2(H(11) * Cell, H(12) * Cell), 60 + H(13) * 50, 26 + H(14) * 20, new Color("#38333f"));
            if (H(2) < 0.09f)
            {
                int k2 = 2 + (int)(H(21) * 4);
                for (int k = 0; k < k2; k++)
                {
                    var c = o + new Vector2(H(30 + k) * Cell, H(40 + k) * Cell);
                    float w = 16 + H(50 + k) * 12, h = w * 0.55f;
                    var pts = new[] { c + new Vector2(-w, 0), c + new Vector2(-w * 0.3f, -h), c + new Vector2(w * 0.8f, -h * 0.8f), c + new Vector2(w, h * 0.2f), c + new Vector2(w * 0.2f, h) };
                    DrawColoredPolygon(pts.Select(p => p + new Vector2(2, 3)).ToArray(), new Color("#26222c"));
                    DrawColoredPolygon(pts, new Color("#57525e"));
                    DrawColoredPolygon(new[] { pts[0], pts[1], pts[2], c }, new Color("#66606c"));
                }
            }
            if (H(3) < 0.5f)
            {
                int k2 = 3 + (int)(H(60) * 10);
                for (int k = 0; k < k2; k++)
                {
                    var c = o + new Vector2(H(70 + k) * Cell, H(90 + k) * Cell);
                    float r = 2.5f + H(110 + k) * 2.5f;
                    float a = H(130 + k) * Mathf.Tau;
                    var pts = Painter.EllipsePts(r, r * 0.5f, 8).Select(p => c + p.Rotated(a)).ToArray();
                    DrawColoredPolygon(pts, Leaves[(int)(H(150 + k) * Leaves.Length)]);
                }
            }
            if (H(4) < 0.4f)
            {
                var c = o + new Vector2(H(170) * Cell, H(171) * Cell);
                var col = new Color("#3d4a35");
                for (int k = -2; k <= 2; k++) DrawLine(c + new Vector2(k * 2, 0), c + new Vector2(k * 3.5f, -7 + Mathf.Abs(k) * 1.5f), col, 1.5f);
            }
        }
    }
}

/// <summary>Мёртвое дерево: ствол рисуется один раз; тень — отдельным узлом под всеми.</summary>
public partial class TreeView : Node2D
{
    public readonly Painter.TreeGeom Geom;
    static readonly Color[] Leaves = { new("#8a4a26"), new("#6e3a22"), new("#a65f2c"), new("#5b2f1f"), new("#7d2e3a") };

    public TreeView(Painter.TreeGeom geom) { Geom = geom; }

    public override void _Ready() => AddChild(new TreeShadowView(Geom) { ZIndex = -50 });

    public override void _Draw()
    {
        for (int k = 0; k < 14; k++)
        {
            float a = k * 2.4f, r = 10 + (k * 7) % 30;
            Painter.Ellipse(this, new Vector2(Mathf.Cos(a) * r * 1.4f, Mathf.Sin(a) * r * 0.45f + 2), 4, 2, Leaves[k % Leaves.Length]);
        }
        Painter.Tree(this, Geom, 1);
    }
}

public partial class TreeShadowView : Node2D
{
    readonly Painter.TreeGeom _geom;
    public TreeShadowView(Painter.TreeGeom geom) { _geom = geom; }
    public override void _Draw() => Painter.TreeShadow(this, _geom);
}

public partial class EnemyView : Node2D
{
    readonly Main _main;
    public Enemy Enemy;
    public EnemyView(Main main) { _main = main; }
    public override void _Draw()
    {
        if (Enemy == null || _main.G == null) return;
        Painter.Enemy(this, Enemy, _main.G.T, _main.G.P.Pos.X < Enemy.Pos.X, Vector2.Zero);
    }
}

public partial class WitchView : Node2D
{
    readonly Main _main;
    public WitchView(Main main) { _main = main; }
    public override void _Draw()
    {
        var g = _main.G;
        if (g == null) return;
        bool blink = g.P.IFrames > 0 && (int)(g.T * 20) % 2 == 0;
        Painter.Witch(this, g.P.Look, Vector2.Zero, Data.WitchScale, g.P.Face < 0, g.P.Anim, blink);
    }
}

/// <summary>Ночь: тёмный слой с мягким светлым кругом вокруг ведьмы и лёгким цветным оттенком ауры.</summary>
public partial class Darkness : Node2D
{
    readonly Main _main;
    static GradientTexture2D _hole;
    static readonly Color Night = new(0.03f, 0.02f, 0.07f);
    public Darkness(Main main) { _main = main; }

    public override void _Ready()
    {
        if (_hole != null) return;
        var grad = new Gradient();
        grad.SetColor(0, new Color(Night, 0.30f));
        grad.SetColor(1, new Color(Night, 0.86f));
        grad.SetOffset(0, 0.18f);
        grad.SetOffset(1, 1.0f);
        _hole = new GradientTexture2D { Gradient = grad, Width = 256, Height = 256, Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f) };
    }

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        var view = GetViewportRect().Size;
        var cam = _main.CameraCenter;
        var tl = cam - view / 2 - new Vector2(50, 50);
        var br = tl + view + new Vector2(100, 100);
        float R = 470;
        var c = _main.LightPos;
        var hole = new Rect2(c - new Vector2(R, R), new Vector2(R * 2, R * 2));
        var dark = new Color(Night, 0.86f);
        // всё вне круга — тёмное
        if (hole.Position.Y > tl.Y) DrawRect(new Rect2(tl.X, tl.Y, br.X - tl.X, hole.Position.Y - tl.Y), dark);
        if (hole.End.Y < br.Y) DrawRect(new Rect2(tl.X, hole.End.Y, br.X - tl.X, br.Y - hole.End.Y), dark);
        if (hole.Position.X > tl.X) DrawRect(new Rect2(tl.X, hole.Position.Y, hole.Position.X - tl.X, hole.Size.Y), dark);
        if (hole.End.X < br.X) DrawRect(new Rect2(hole.End.X, hole.Position.Y, br.X - hole.End.X, hole.Size.Y), dark);
        DrawTextureRect(_hole, hole, false);
        // общий лунный оттенок
        DrawRect(new Rect2(tl, br - tl), new Color(0.25f, 0.18f, 0.45f, 0.18f));
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
        bool hi = Settings.HighFx;
        if (g == null)
        {
            // светлячки на титульном экране
            var cam = _main.CameraCenter;
            for (int i = 0; i < (hi ? 40 : 12); i++)
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
        if (aura.HasValue && hi) Soft(P.Pos - new Vector2(0, 50), 110, new Color(aura.Value, 0.35f));
        var tip = Painter.StaffTip(P.Pos, Data.WitchScale, P.Face < 0, P.Anim);
        var gem = P.Look.DressLight ?? new Color("#b388ff");
        Soft(tip, hi ? 26 : 14, new Color(gem, 0.8f));
        // магический круг под ногами
        DrawSetTransform(P.Pos, 0, new Vector2(1, 0.42f));
        DrawArc(Vector2.Zero, 48, 0, Mathf.Tau, 48, new Color(gem, 0.35f), 2.5f);
        if (hi) DrawArc(Vector2.Zero, 38, g.T, g.T + Mathf.Tau * 0.7f, 32, new Color(gem, 0.25f), 1.5f);
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);

        foreach (var r in g.Rings)
        {
            var c = new Color(r.C, Mathf.Clamp(r.T * 3, 0, 1));
            DrawSetTransform(r.Pos, 0, new Vector2(1, 0.55f));
            DrawArc(Vector2.Zero, r.R, 0, Mathf.Tau, 64, c, 8);
            if (hi) DrawArc(Vector2.Zero, r.R * 0.85f, 0, Mathf.Tau, 64, new Color(c, c.A * 0.4f), 4);
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
                if (hi) Soft(c, 26, new Color(0.5f, 0.8f, 1f, 0.6f));
                var d = Vector2.FromAngle(a + Mathf.Pi / 2);
                var nrm = new Vector2(-d.Y, d.X);
                DrawColoredPolygon(new[] { c + d * 12, c + nrm * 4, c - d * 12, c - nrm * 4 }, new Color(0.8f, 0.95f, 1f, 0.95f));
            }
        }

        foreach (var b in g.Bullets)
        {
            if (b.Kind == "fireball")
            {
                if (hi) Soft(b.Pos, 46, new Color(1f, 0.45f, 0.15f, 0.8f));
                Soft(b.Pos, hi ? 14 : 18, new Color(1f, 0.9f, 0.6f, 1f));
            }
            else
            {
                var tail = b.Pos - b.Vel.Normalized() * 26;
                DrawLine(tail, b.Pos, new Color(0.7f, 0.45f, 1f, 0.6f), 6);
                if (hi) Soft(b.Pos, 26, new Color(0.75f, 0.45f, 1f, 0.85f));
                Soft(b.Pos, hi ? 7 : 10, new Color(0.95f, 0.85f, 1, 1));
            }
        }
        foreach (var b in g.FoeBullets)
        {
            if (hi) Soft(b.Pos, b.R * 3, new Color(1f, 0.15f, 0.25f, 0.75f));
            Soft(b.Pos, b.R * (hi ? 1 : 1.5f), new Color(1f, 0.55f, 0.6f, 1f));
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
            if (hi) DrawPolyline(pts.ToArray(), new Color(1f, 0.85f, 0.3f, a * 0.5f), 10);
            DrawPolyline(pts.ToArray(), new Color(1f, 1f, 0.85f, a), 3);
        }
        if (hi)
        {
            foreach (var q in g.Particles)
                Soft(q.Pos, q.S * 1.6f, new Color(q.C, Mathf.Clamp(q.T * 3, 0, 1) * 0.9f));
            foreach (var gm in g.Gems) Soft(gm.Pos, 16, gm.V >= 3 ? new Color(1f, 0.5f, 0.7f, 0.5f) : new Color(0.4f, 0.9f, 1f, 0.5f));
            foreach (var f in g.Flowers) Soft(f.Pos - new Vector2(0, 14), 34, new Color(f.Kind.Color, 0.35f));
        }
        foreach (var pg in g.Pages) Soft(pg.Pos - new Vector2(0, 18), 60, new Color(1f, 0.85f, 0.5f, 0.55f));

        // глаза врагов
        foreach (var e in g.Enemies)
        {
            bool faceLeft = P.Pos.X < e.Pos.X;
            var col = Painter.EyeColor(e);
            float r = e.IsBoss ? 7 : e.IsElite ? 5 : 3.2f;
            foreach (var eye in Painter.EnemyEyes(e, g.T, faceLeft))
            {
                if (hi) Soft(eye, r * 3.2f, new Color(col, 0.7f));
                Soft(eye, r * (hi ? 1 : 1.6f), hi ? new Color(1, 1, 0.9f, 1) : col);
            }
            if (!hi) continue;
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

        if (!Settings.HighFx)
            foreach (var q in g.Particles)
                DrawRect(new Rect2(q.Pos - Vector2.One * q.S / 2, Vector2.One * q.S), new Color(q.C, Mathf.Clamp(q.T * 3, 0, 1)));
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
