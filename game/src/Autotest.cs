using System.Linq;
using Godot;

namespace WitchApprentice;

/// <summary>
/// Автопрогон для проверки сборки: запуск с пользовательским аргументом
/// <c>-- --autotest=&lt;папка&gt;</c> проходит забег по сценарию и сохраняет скриншоты.
/// В обычной игре не создаётся.
/// </summary>
public partial class Autotest : Node
{
    readonly Main _main;
    readonly string _dir;
    float _t;
    int _step;
    string _log = "";

    public Autotest(Main main, string dir) { _main = main; _dir = dir; }

    public static string DirFromArgs() =>
        OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--autotest="))?.Substring("--autotest=".Length);

    void Shot(string name)
    {
        var img = GetViewport().GetTexture().GetImage();
        img.SavePng($"{_dir}/{name}.png");
        Note($"shot {name} state={_main.State}");
    }

    void Note(string s)
    {
        _log += $"[{_t:0.0}] {s}\n";
        GD.Print("AUTOTEST " + s);
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        var g = _main.G;
        switch (_step)
        {
            case 0 when _t > 1.0f: Shot("1-title"); _main.StartRun(); _step++; break;
            case 1:
                // бег по кругу
                string[] dirs = { "move_right", "move_down", "move_left", "move_up" };
                int k = (int)(_t / 0.6f) % 4;
                foreach (var d in dirs) Input.ActionRelease(d);
                Input.ActionPress(dirs[k]);
                if (_main.State is GameState.LevelUp or GameState.PageChoice) { Shot("2-levelup"); _main.ApplyChoice(0); }
                if (_t > 9) { foreach (var d in dirs) Input.ActionRelease(d); Shot("3-play"); _step++; }
                break;
            case 2:
                g.P.Hp = g.P.MaxHp = 9999;
                for (int i = 0; i < Data.PagesToBoss; i++) g.Pages.Add(new Page { Pos = g.P.Pos });
                _step++;
                break;
            case 3:
                if (_main.State == GameState.PageChoice)
                {
                    if (g.P.Schools.Count == 1) Shot("4-page");
                    _main.ApplyChoice(0);
                }
                else if (_main.State == GameState.LevelUp) _main.ApplyChoice(0);
                if (g.Boss != null && _main.State == GameState.Play) { _step++; _t = 100; }
                break;
            case 4 when _t > 102.5f:
                Shot("5-boss");
                Note($"schools={string.Join(",", g.P.Schools)} spells={string.Join(",", g.P.Spells.Select(s => s.Key + s.Value))} enemies={g.Enemies.Count} kills={g.Kills} lvl={g.P.Level}");
                g.Boss.Hp = 0;
                _step++;
                break;
            case 5:
                if (_main.State is GameState.LevelUp or GameState.PageChoice) _main.ApplyChoice(0);
                if (_main.State == GameState.Win) { _step++; _t = 200; }
                else if (_t > 160) { Note($"FAIL: no win screen, state={_main.State} boss={g.Boss?.Hp} winAt={g.WinAt} t={g.T} fps={Engine.GetFramesPerSecond()}"); Finish(1); }
                break;
            case 6 when _t > 201.5f: Shot("6-win"); Note($"fps={Engine.GetFramesPerSecond()}"); Finish(0); break;
        }
    }

    void Finish(int code)
    {
        Note(code == 0 ? "PASS" : "FAIL");
        using var f = FileAccess.Open($"{_dir}/autotest.log", FileAccess.ModeFlags.Write);
        f.StoreString(_log);
        GetTree().Quit(code);
    }
}
