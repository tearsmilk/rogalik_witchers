using System.Collections.Generic;
using Godot;

namespace WitchApprentice;

public sealed class Player
{
    public Vector2 Pos;
    public float R = 16, Face = 1, Anim;
    public float Hp = 100, MaxHp = 100, Regen, Speed = 210;
    public float Magnet = 110, DmgMul = 1, RateMul = 1, Crit = 0.05f;
    public int ExtraProj;
    public float IFrames, DashCd, DashT;
    public Vector2 DashDir;
    public int Level = 1;
    public float Xp, XpNext = 6;
    public readonly Dictionary<string, int> Spells = new() { ["spark"] = 1 };
    public readonly Dictionary<string, float> Timers = new();
    public readonly List<string> Schools = new();
    public readonly WitchLook Look = new();
    public readonly List<FlowerDef> Bouquet = new();
}

public sealed class Enemy
{
    public EnemyDef Def;
    public Vector2 Pos, Kb;
    public float R, Hp, MaxHp, Speed, Dmg, Xp;
    public float HitFlash, Slow, Phase, ShootCd = 3, FrostHitAt = -9;
    public bool IsBoss => Def.Id == "boss";
    public bool IsElite => Def.Id == "elite";
}

public sealed class Bullet
{
    public string Kind;
    public Vector2 Pos, Vel;
    public float R, T, Dmg, Radius;
    public int Pierce;
    public readonly HashSet<Enemy> Hit = new();
}

public sealed class FoeBullet { public Vector2 Pos, Vel; public float R, T, Dmg; }
public sealed class Gem { public Vector2 Pos; public float V; public bool Pulled, Got; }
public sealed class Flower { public Vector2 Pos; public FlowerDef Kind; public float Bob; public bool Got; }
public sealed class Page { public Vector2 Pos; public float Bob; public bool Got; }
public sealed class Particle { public Vector2 Pos, Vel; public float T, S; public Color C; }
public sealed class FloatText { public Vector2 Pos; public string Text; public float T; public Color C; public bool Big, Crit; }
public sealed class Bolt { public List<Vector2> Pts; public float T; }
public sealed class Ring { public Vector2 Pos; public float R, MaxR, T; public Color C; }

public sealed class Choice
{
    public string Title, Desc;
    public Color Color;
    public System.Action Apply;
}

public enum GameState { Title, Play, LevelUp, PageChoice, Pause, Over, Win }
