using System;
using System.Collections.Generic;
using Godot;

namespace WitchApprentice;

/// <summary>Спрайтлисты (CC0): Kenney Tiny Dungeon, Kenney Tiny Town, Clint Bellanger Tiny Creatures.</summary>
public enum Sheet { Dungeon, Creatures, Town }

public readonly record struct SpriteRef(Sheet Sheet, int Index);

public sealed class SchoolDef
{
    public string Id, Name, Desc, Spell;
    public Color Light, Dark, Aura;
}

public sealed class SpellDef
{
    public string Id, Name;
    public int Max;
    public Color Color;
}

public sealed class FlowerDef
{
    public string Id, Name, Desc;
    public Color Color;
    public Action<Player> Apply;
}

public sealed class EnemyDef
{
    public string Id;
    public float Hp, Speed, R, Dmg, Xp, Scale;
    public SpriteRef Sprite;
    public Color Particle;
}

public static class Data
{
    public const int PagesToBoss = 3;
    public const float SpriteScale = 3f;

    // Ведьма: девушка из Tiny Dungeon + шляпа из Tiny Creatures, фиолетовое перекрашивается под школу.
    public static readonly SpriteRef WitchBody = new(Sheet.Dungeon, 99);
    public static readonly SpriteRef WitchHat = new(Sheet.Creatures, 66);
    public static readonly Color BaseDressLight = new(0.82f, 0.46f, 0.82f);
    public static readonly Color BaseDressDark = new(0.61f, 0.30f, 0.64f);

    public static readonly Dictionary<string, SchoolDef> Schools = new()
    {
        ["fire"] = new() { Id = "fire", Name = "Огонь", Spell = "fireball", Desc = "Огненный шар взрывается при попадании",
            Light = new Color("#f0703a"), Dark = new Color("#a8321f"), Aura = new Color("#ff7043") },
        ["ice"] = new() { Id = "ice", Name = "Лёд", Spell = "frost", Desc = "Ледяные осколки кружат вокруг и замедляют",
            Light = new Color("#7cc4f0"), Dark = new Color("#2f6fa8"), Aura = new Color("#85c1e9") },
        ["nature"] = new() { Id = "nature", Name = "Природа", Spell = "thorns", Desc = "Волна шипов отбрасывает врагов",
            Light = new Color("#6fd48a"), Dark = new Color("#2e8b57"), Aura = new Color("#82e0aa") },
        ["storm"] = new() { Id = "storm", Name = "Гроза", Spell = "lightning", Desc = "Молния бьёт цепью по нескольким врагам",
            Light = new Color("#f2d74e"), Dark = new Color("#b08a1c"), Aura = new Color("#f7dc6f") },
    };

    public static readonly Dictionary<string, SpellDef> Spells = new()
    {
        ["spark"] = new() { Id = "spark", Name = "Искра", Max = 6, Color = new Color("#e8d5ff") },
        ["fireball"] = new() { Id = "fireball", Name = "Огненный шар", Max = 5, Color = new Color("#ff7043") },
        ["frost"] = new() { Id = "frost", Name = "Ледяной вихрь", Max = 5, Color = new Color("#aed6f1") },
        ["thorns"] = new() { Id = "thorns", Name = "Волна шипов", Max = 5, Color = new Color("#82e0aa") },
        ["lightning"] = new() { Id = "lightning", Name = "Цепная молния", Max = 5, Color = new Color("#f7dc6f") },
    };

    public static readonly List<FlowerDef> Flowers = new()
    {
        new() { Id = "rose", Name = "Роза", Color = new Color("#e74c3c"), Desc = "+5% шанс крита", Apply = p => p.Crit += 0.05f },
        new() { Id = "lily", Name = "Лилия", Color = new Color("#fdfefe"), Desc = "+0.5 ОЗ/сек", Apply = p => p.Regen += 0.5f },
        new() { Id = "corn", Name = "Василёк", Color = new Color("#3498db"), Desc = "+6% скорость бега", Apply = p => p.Speed *= 1.06f },
        new() { Id = "sun", Name = "Подсолнух", Color = new Color("#f4d03f"), Desc = "+25% радиус сбора", Apply = p => p.Magnet *= 1.25f },
        new() { Id = "violet", Name = "Фиалка", Color = new Color("#a569bd"), Desc = "+6% урон", Apply = p => p.DmgMul *= 1.06f },
        new() { Id = "clover", Name = "Клевер", Color = new Color("#58d68d"), Desc = "+8 к макс. ОЗ", Apply = p => { p.MaxHp += 8; p.Hp += 8; } },
    };

    public static readonly Dictionary<string, EnemyDef> Enemies = new()
    {
        ["imp"] = new() { Id = "imp", Hp = 14, Speed = 88, R = 16, Dmg = 6, Xp = 1, Scale = 3, Sprite = new(Sheet.Creatures, 39), Particle = new Color("#c0392b") },
        ["bat"] = new() { Id = "bat", Hp = 8, Speed = 140, R = 14, Dmg = 4, Xp = 1, Scale = 2.6f, Sprite = new(Sheet.Dungeon, 120), Particle = new Color("#8e5a3c") },
        ["slime"] = new() { Id = "slime", Hp = 42, Speed = 58, R = 20, Dmg = 12, Xp = 3, Scale = 3.2f, Sprite = new(Sheet.Dungeon, 108), Particle = new Color("#52be80") },
        ["elite"] = new() { Id = "elite", Hp = 240, Speed = 78, R = 26, Dmg = 16, Xp = 12, Scale = 4.2f, Sprite = new(Sheet.Creatures, 123), Particle = new Color("#922b21") },
        ["boss"] = new() { Id = "boss", Hp = 2600, Speed = 68, R = 52, Dmg = 24, Xp = 0, Scale = 8f, Sprite = new(Sheet.Creatures, 38), Particle = new Color("#e74c3c") },
    };

    // Декор леса из Tiny Town.
    public static readonly int[] GrassTiles = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 2 };
    public static readonly int[] TreeTiles = { 4, 16, 28, 4, 16, 3, 15, 27 };
    public static readonly int[] SmallDecor = { 5, 5, 29 };

    // Снаряды.
    public static readonly SpriteRef SparkSprite = new(Sheet.Creatures, 89);
    public static readonly SpriteRef FireSprite = new(Sheet.Creatures, 55);
    public static readonly SpriteRef IceSprite = new(Sheet.Creatures, 59);
}
