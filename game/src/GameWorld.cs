using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace WitchApprentice;

public struct InputState
{
    public Vector2 Move;
    public bool Dash;
    public bool AimAtCursor;
    public Vector2 Cursor;
}

/// <summary>Вся игровая логика одного забега. Не зависит от сцены — только данные и шаг симуляции.</summary>
public sealed class GameWorld
{
    public readonly Random Rng = new();
    public float T;
    public int Kills;
    public readonly Player P = new();
    public readonly List<Enemy> Enemies = new();
    public readonly List<Bullet> Bullets = new();
    public readonly List<FoeBullet> FoeBullets = new();
    public readonly List<Gem> Gems = new();
    public readonly List<Flower> Flowers = new();
    public readonly List<Page> Pages = new();
    public readonly List<Particle> Particles = new();
    public readonly List<FloatText> Texts = new();
    public readonly List<Bolt> Bolts = new();
    public readonly List<Ring> Rings = new();
    public float SpawnAcc, NextElite = 35, FrostAngle, Shake;
    public int PagesFound, LevelUpQueue, PageQueue;
    public bool BossSpawned;
    public Enemy Boss;
    public float WinAt = -1;
    public Vector2 ViewSize = new(1280, 800);

    /// <summary>Звуковые события: имя эффекта.</summary>
    public event Action<string> Sound;
    /// <summary>Крупная надпись по центру экрана: заголовок и подзаголовок.</summary>
    public event Action<string, string, float> Banner;

    public GameWorld()
    {
        for (int i = 0; i < 6; i++) SpawnFlower(true);
    }

    public void Start() => Banner?.Invoke("Найди страницы гримуара", "Бесы утащили их в Проклятый лес", 3);

    float Rand(float a, float b) => a + (float)Rng.NextDouble() * (b - a);
    T Pick<T>(IList<T> list) => list[Rng.Next(list.Count)];
    float ViewRadius => ViewSize.Length() / 2 + 60;

    // ---------- Спавн ----------
    public Enemy SpawnEnemy(string type, Vector2? at = null)
    {
        var d = Data.Enemies[type];
        float mult = type == "boss" ? 1 : 1 + T / 110f;
        Vector2 pos = at ?? P.Pos + Vector2.FromAngle(Rand(0, Mathf.Tau)) * ViewRadius;
        var e = new Enemy
        {
            Def = d, Pos = pos, R = d.R, Hp = d.Hp * mult, MaxHp = d.Hp * mult, Speed = d.Speed * Rand(0.9f, 1.1f),
            Dmg = d.Dmg, Xp = d.Xp, Phase = Rand(0, 6.28f),
        };
        Enemies.Add(e);
        return e;
    }

    void SpawnFlower(bool initial)
    {
        float m = Mathf.Max(ViewSize.X, ViewSize.Y);
        float dist = initial ? Rand(200, 700) : Rand(m * 0.6f, m * 0.9f);
        Flowers.Add(new Flower { Pos = P.Pos + Vector2.FromAngle(Rand(0, Mathf.Tau)) * dist, Kind = Pick(Data.Flowers), Bob = Rand(0, 6) });
    }

    string EnemyTypeForTime()
    {
        double r = Rng.NextDouble();
        if (T > 60 && r < 0.18) return "slime";
        if (T > 25 && r < 0.45) return "bat";
        return "imp";
    }

    // ---------- Урон ----------
    void DamageEnemy(Enemy e, float baseDmg, float kb = 0, Vector2? from = null, float slow = 0)
    {
        bool crit = Rng.NextDouble() < P.Crit;
        float dmg = baseDmg * P.DmgMul * (crit ? 2 : 1);
        e.Hp -= dmg;
        e.HitFlash = 0.1f;
        if (kb > 0)
        {
            var dir = (e.Pos - (from ?? P.Pos)).Normalized();
            float k = e.IsBoss ? 0.1f : e.IsElite ? 0.4f : 1;
            e.Kb += dir * kb * k;
        }
        if (slow > 0) e.Slow = Mathf.Max(e.Slow, slow);
        Texts.Add(new FloatText { Pos = e.Pos + new Vector2(Rand(-6, 6), -Painter.TopHeight(e) - 4), Text = Mathf.RoundToInt(dmg).ToString(), T = 0.7f, Crit = crit,
            C = crit ? new Color("#ffd54f") : Colors.White });
        Sound?.Invoke("hit");
    }

    void KillEnemy(Enemy e)
    {
        Kills++;
        for (int i = 0; i < 8; i++)
            Particles.Add(new Particle { Pos = e.Pos - new Vector2(0, Painter.BodyHeight(e)), Vel = new Vector2(Rand(-120, 120), Rand(-120, 120)), T = Rand(0.3f, 0.6f), C = e.Def.Particle, S = Rand(3, 6) });
        if (e.Xp > 0)
        {
            int n = e.IsElite ? 5 : 1;
            for (int i = 0; i < n; i++) Gems.Add(new Gem { Pos = e.Pos + new Vector2(Rand(-8, 8), Rand(-8, 8)), V = e.Xp / n });
        }
        if (e.IsElite)
        {
            Pages.Add(new Page { Pos = e.Pos });
            Sound?.Invoke("elite");
        }
        if (e.IsBoss)
        {
            Boss = null;
            Shake = 0.8f;
            for (int i = 0; i < 60; i++)
                Particles.Add(new Particle { Pos = e.Pos - new Vector2(0, 90), Vel = new Vector2(Rand(-300, 300), Rand(-300, 300)), T = Rand(0.6f, 1.4f),
                    C = Pick(new[] { new Color("#f7dc6f"), new Color("#ff7043"), new Color("#d7bde2") }), S = Rand(4, 8) });
            WinAt = T + 1.2f;
        }
    }

    public bool PlayerDead => P.Hp <= 0;

    void HurtPlayer(float dmg)
    {
        if (P.IFrames > 0 || P.DashT > 0 || P.Hp <= 0) return;
        P.Hp = Mathf.Max(0, P.Hp - dmg);
        P.IFrames = 0.8f;
        Shake = Mathf.Max(Shake, 0.25f);
        Sound?.Invoke("hurt");
    }

    // ---------- Выбор улучшений ----------
    public List<Choice> LevelUpChoices()
    {
        var pool = new List<Choice>();
        foreach (var (id, lv) in P.Spells.ToList())
        {
            var s = Data.Spells[id];
            if (lv < s.Max)
                pool.Add(new Choice { Title = $"{s.Name} — ур. {lv + 1}", Desc = SpellUpgradeText(id, lv + 1), Color = s.Color, Apply = () => P.Spells[id]++ });
        }
        pool.Add(new Choice { Title = "Острый ум", Desc = "+12% урон всех заклинаний", Color = new Color("#f5b7b1"), Apply = () => P.DmgMul *= 1.12f });
        pool.Add(new Choice { Title = "Быстрые пальцы", Desc = "+10% скорость заклинаний", Color = new Color("#f9e79f"), Apply = () => P.RateMul *= 1.10f });
        pool.Add(new Choice { Title = "Лёгкие туфельки", Desc = "+10% скорость бега", Color = new Color("#abebc6"), Apply = () => P.Speed *= 1.10f });
        pool.Add(new Choice { Title = "Крепкий чай", Desc = "+20 к макс. ОЗ и лечение", Color = new Color("#fadbd8"), Apply = () => { P.MaxHp += 20; P.Hp = Mathf.Min(P.MaxHp, P.Hp + 40); } });
        pool.Add(new Choice { Title = "Карманная луна", Desc = "+35% радиус сбора опыта", Color = new Color("#d6eaf8"), Apply = () => P.Magnet *= 1.35f });
        if (P.ExtraProj < 2)
            pool.Add(new Choice { Title = "Двойное эхо", Desc = "+1 снаряд у Искры и Огненного шара", Color = new Color("#e8daef"), Apply = () => P.ExtraProj++ });
        return pool.OrderBy(_ => Rng.Next()).Take(3).ToList();
    }

    static string SpellUpgradeText(string id, int lv) => id switch
    {
        "spark" => lv % 2 == 0 ? "+1 снаряд и урон" : lv >= 4 ? "Пробивает врагов, урон выше" : "Больше урона и скорости",
        "fireball" => "Больше урона и радиус взрыва",
        "frost" => "+1 осколок, сильнее холод",
        "thorns" => "Шире волна, больше урона",
        "lightning" => "+1 цель в цепи, больше урона",
        _ => "",
    };

    public List<Choice> PageChoices()
    {
        var free = Data.Schools.Keys.Where(k => !P.Schools.Contains(k)).OrderBy(_ => Rng.Next()).Take(3);
        return free.Select(k =>
        {
            var s = Data.Schools[k];
            return new Choice
            {
                Title = "Школа: " + s.Name, Desc = s.Desc, Color = s.Light,
                Apply = () =>
                {
                    P.Schools.Add(k);
                    P.Spells[s.Spell] = 1;
                    // Каждая страница меняет часть образа: платье → шляпа → аура.
                    int n = P.Schools.Count;
                    if (n == 1) { P.Look.DressLight = s.Light; P.Look.DressDark = s.Dark; }
                    else if (n == 2) { P.Look.HatLight = s.Light; P.Look.HatDark = s.Dark; }
                    else P.Look.Aura = s.Aura;
                    P.Hp = Mathf.Min(P.MaxHp, P.Hp + 25);
                },
            };
        }).ToList();
    }

    /// <summary>Вызывается после выбора на странице гримуара: если страницы собраны — появляется босс.</summary>
    public void AfterPageChosen()
    {
        if (PageQueue == 0 && PagesFound >= Data.PagesToBoss && !BossSpawned)
        {
            BossSpawned = true;
            Boss = SpawnEnemy("boss", P.Pos + Vector2.FromAngle(Rand(0, Mathf.Tau)) * 520);
            Banner?.Invoke("Король бесов", "Он не отдаст последнюю тайну гримуара без боя!", 3.5f);
            Sound?.Invoke("boss");
        }
    }

    // ---------- Заклинания ----------
    Enemy Nearest(Vector2 from, float maxD)
    {
        Enemy best = null;
        float bd = maxD * maxD;
        foreach (var e in Enemies)
        {
            float d = from.DistanceSquaredTo(e.Pos);
            if (d < bd) { bd = d; best = e; }
        }
        return best;
    }

    public static float FrostRadius(int lv) => 78 + 4 * lv;
    public static int FrostCount(int lv) => 1 + lv;
    public Vector2 CastOrigin => P.Pos + new Vector2(P.Face * 19, -95);

    void CastSpells(float dt, InputState input)
    {
        foreach (var id in P.Spells.Keys) P.Timers[id] = P.Timers.GetValueOrDefault(id) - dt * P.RateMul;
        var lv = P.Spells;

        if (lv.TryGetValue("spark", out int sl) && P.Timers["spark"] <= 0)
        {
            P.Timers["spark"] = 0.5f / (1 + 0.1f * (sl - 1));
            float ang = (input.Cursor - CastOrigin).Angle();
            if (!input.AimAtCursor)
            {
                var e = Nearest(P.Pos, 600);
                if (e != null) ang = (e.Pos - new Vector2(0, Painter.BodyHeight(e)) - CastOrigin).Angle();
            }
            int n = 1 + sl / 2 + P.ExtraProj;
            for (int i = 0; i < n; i++)
            {
                float a = ang + (i - (n - 1) / 2f) * 0.16f;
                Bullets.Add(new Bullet { Kind = "spark", Pos = CastOrigin, Vel = Vector2.FromAngle(a) * 540, R = 8, T = 1.2f, Dmg = 10 + 3 * (sl - 1), Pierce = sl >= 4 ? 2 : 0 });
            }
            Sound?.Invoke("spark");
        }

        if (lv.TryGetValue("fireball", out int fl) && P.Timers["fireball"] <= 0)
        {
            P.Timers["fireball"] = 1.7f;
            var e = Nearest(P.Pos, 700);
            if (e != null)
            {
                int n = 1 + P.ExtraProj;
                float baseA = (e.Pos - new Vector2(0, Painter.BodyHeight(e)) - CastOrigin).Angle();
                for (int i = 0; i < n; i++)
                {
                    float a = baseA + (i - (n - 1) / 2f) * 0.35f;
                    Bullets.Add(new Bullet { Kind = "fireball", Pos = CastOrigin, Vel = Vector2.FromAngle(a) * 360, R = 12, T = 2, Dmg = 22 + 9 * (fl - 1), Radius = 70 + 12 * fl });
                }
                Sound?.Invoke("fire");
            }
            else P.Timers["fireball"] = 0.2f;
        }

        if (lv.TryGetValue("thorns", out int tl) && P.Timers["thorns"] <= 0)
        {
            P.Timers["thorns"] = 2.4f;
            float R = 110 + 18 * tl;
            Rings.Add(new Ring { Pos = P.Pos, R = 10, MaxR = R, T = 0.35f, C = Data.Spells["thorns"].Color });
            foreach (var e in Enemies)
                if (P.Pos.DistanceSquaredTo(e.Pos) < (R + e.R) * (R + e.R)) DamageEnemy(e, 14 + 6 * (tl - 1), kb: 420);
            Sound?.Invoke("thorns");
        }

        if (lv.TryGetValue("lightning", out int ll) && P.Timers["lightning"] <= 0)
        {
            P.Timers["lightning"] = 1.4f;
            var near = Enemies.Where(e => P.Pos.DistanceSquaredTo(e.Pos) < 480 * 480).ToList();
            if (near.Count > 0)
            {
                var cur = Pick(near);
                var pts = new List<Vector2> { CastOrigin };
                var hit = new HashSet<Enemy>();
                for (int i = 0; i < 1 + ll && cur != null; i++)
                {
                    hit.Add(cur);
                    pts.Add(cur.Pos - new Vector2(0, Painter.BodyHeight(cur)));
                    DamageEnemy(cur, 20 + 7 * (ll - 1));
                    Enemy nx = null;
                    float nd = 170 * 170;
                    foreach (var e in Enemies)
                    {
                        if (hit.Contains(e)) continue;
                        float d = cur.Pos.DistanceSquaredTo(e.Pos);
                        if (d < nd) { nd = d; nx = e; }
                    }
                    cur = nx;
                }
                Bolts.Add(new Bolt { Pts = pts, T = 0.18f });
                Sound?.Invoke("zap");
            }
            else P.Timers["lightning"] = 0.2f;
        }

        if (lv.TryGetValue("frost", out int il))
        {
            FrostAngle += dt * (2.2f + 0.2f * il);
            int n = FrostCount(il);
            float R = FrostRadius(il);
            for (int i = 0; i < n; i++)
            {
                var s = P.Pos + new Vector2(0, -45) + Vector2.FromAngle(FrostAngle + i * Mathf.Tau / n) * R;
                foreach (var e in Enemies)
                {
                    if (s.DistanceSquaredTo(e.Pos - new Vector2(0, Painter.BodyHeight(e))) < (e.R + 10) * (e.R + 10) && T - e.FrostHitAt > 0.45f)
                    {
                        e.FrostHitAt = T;
                        DamageEnemy(e, 7 + 3 * (il - 1), kb: 120, slow: 1.2f);
                    }
                }
            }
        }
    }

    void Explode(Vector2 at, float radius, float dmg)
    {
        Rings.Add(new Ring { Pos = at, R = 6, MaxR = radius, T = 0.3f, C = new Color("#ff8a50") });
        for (int i = 0; i < 14; i++)
            Particles.Add(new Particle { Pos = at, Vel = new Vector2(Rand(-200, 200), Rand(-200, 200)), T = Rand(0.2f, 0.5f),
                C = Pick(new[] { new Color("#ff7043"), new Color("#f5b041"), new Color("#fdebd0") }), S = Rand(3, 7) });
        foreach (var e in Enemies)
        {
            var body = e.Pos - new Vector2(0, Painter.BodyHeight(e));
            if (at.DistanceSquaredTo(body) < (radius + e.R) * (radius + e.R)) DamageEnemy(e, dmg, kb: 160, from: at);
        }
        Shake = Mathf.Max(Shake, 0.08f);
    }

    // ---------- Шаг симуляции ----------
    /// <summary>Возвращает true, если пора показать экран победы.</summary>
    public bool Update(float dt, InputState input)
    {
        T += dt;

        // Движение и рывок
        var move = input.Move.LengthSquared() > 1 ? input.Move.Normalized() : input.Move;
        P.DashCd -= dt;
        if (input.Dash && P.DashCd <= 0)
        {
            P.DashDir = move != Vector2.Zero ? move.Normalized() : new Vector2(P.Face, 0);
            P.DashT = 0.16f;
            P.DashCd = 1.4f;
            Sound?.Invoke("dash");
        }
        if (P.DashT > 0)
        {
            P.DashT -= dt;
            P.Pos += P.DashDir * 820 * dt;
            Particles.Add(new Particle { Pos = P.Pos - new Vector2(0, 50), T = 0.3f, C = P.Look.Aura ?? P.Look.DressLight ?? new Color("#b39ddb"), S = 9 });
        }
        else P.Pos += move * P.Speed * dt;
        if (Mathf.Abs(input.Cursor.X - P.Pos.X) > 4) P.Face = input.Cursor.X > P.Pos.X ? 1 : -1;
        P.Anim += dt * (move != Vector2.Zero ? 10 : 2.5f);
        P.IFrames -= dt;
        if (P.Regen > 0) P.Hp = Mathf.Min(P.MaxHp, P.Hp + P.Regen * dt);

        // Спавн врагов
        if (Boss == null || Enemies.Count < 60)
        {
            SpawnAcc += dt * (0.8f + T * 0.025f) * (Boss != null ? 0.4f : 1);
            while (SpawnAcc >= 1)
            {
                SpawnAcc -= 1;
                if (Enemies.Count >= 320) continue;
                var type = EnemyTypeForTime();
                if (type == "bat" && Rng.NextDouble() < 0.3)
                {
                    var c = P.Pos + Vector2.FromAngle(Rand(0, Mathf.Tau)) * ViewRadius;
                    for (int i = 0; i < 5; i++) SpawnEnemy("bat", c + new Vector2(Rand(-40, 40), Rand(-40, 40)));
                }
                else SpawnEnemy(type);
            }
        }
        int elites = Enemies.Count(e => e.IsElite);
        if (T >= NextElite && !BossSpawned && PagesFound + Pages.Count + elites < Data.PagesToBoss)
        {
            NextElite = T + 45;
            SpawnEnemy("elite");
            Banner?.Invoke("Бес-хранитель", "У него страница гримуара!", 2.2f);
        }

        CastSpells(dt, input);

        // Снаряды
        foreach (var b in Bullets)
        {
            b.Pos += b.Vel * dt;
            b.T -= dt;
            if (b.Kind == "fireball" && Rng.NextDouble() < 0.6)
                Particles.Add(new Particle { Pos = b.Pos, Vel = new Vector2(Rand(-30, 30), Rand(-30, 30)), T = 0.3f, C = Pick(new[] { new Color("#ff7043"), new Color("#f5b041") }), S = Rand(3, 5) });
            foreach (var e in Enemies)
            {
                if (b.T <= 0) break;
                if (b.Hit.Contains(e)) continue;
                var body = e.Pos - new Vector2(0, Painter.BodyHeight(e));
                if (b.Pos.DistanceSquaredTo(body) < (b.R + e.R) * (b.R + e.R))
                {
                    b.Hit.Add(e);
                    if (b.Kind == "fireball") { Explode(b.Pos, b.Radius, b.Dmg); b.T = 0; break; }
                    DamageEnemy(e, b.Dmg, kb: 60, from: b.Pos - b.Vel);
                    if (b.Pierce-- <= 0) b.T = 0;
                }
            }
        }
        Bullets.RemoveAll(b => b.T <= 0);

        // Враги
        float far = ViewSize.Length() * 1.3f;
        foreach (var e in Enemies)
        {
            e.HitFlash -= dt;
            e.Slow -= dt;
            var to = P.Pos - e.Pos;
            float d = Mathf.Max(to.Length(), 0.001f);
            var v = to / d * e.Speed * (e.Slow > 0 ? 0.5f : 1);
            if (e.Def.Id == "bat")
            {
                e.Phase += dt * 6;
                v += new Vector2(Mathf.Cos(e.Phase), Mathf.Sin(e.Phase)) * 60;
            }
            if (e.IsBoss)
            {
                e.ShootCd -= dt;
                if (e.ShootCd <= 0)
                {
                    bool rage = e.Hp < e.MaxHp / 2;
                    e.ShootCd = rage ? 2 : 3;
                    int n = rage ? 20 : 14;
                    float off = Rand(0, 1);
                    var origin = e.Pos - new Vector2(0, Painter.BodyHeight(e));
                    for (int i = 0; i < n; i++)
                        FoeBullets.Add(new FoeBullet { Pos = origin, Vel = Vector2.FromAngle(off + i * Mathf.Tau / n) * 170, R = 9, T = 5, Dmg = 12 });
                    for (int i = 0; i < 4; i++) SpawnEnemy("imp", e.Pos + new Vector2(Rand(-60, 60), Rand(-60, 60)));
                    Sound?.Invoke("bossShot");
                }
            }
            e.Pos += (v + e.Kb) * dt;
            e.Kb *= Mathf.Pow(0.0005f, dt);
            if (d < e.R + P.R) HurtPlayer(e.Dmg);
            if (d > far && !e.IsBoss && !e.IsElite)
                e.Pos = P.Pos + Vector2.FromAngle(Rand(0, Mathf.Tau)) * ViewRadius;
        }
        SeparateEnemies();
        var dead = Enemies.Where(e => e.Hp <= 0).ToList();
        Enemies.RemoveAll(e => e.Hp <= 0);
        dead.ForEach(KillEnemy);

        // Снаряды босса
        foreach (var b in FoeBullets)
        {
            b.Pos += b.Vel * dt;
            b.T -= dt;
            if (b.Pos.DistanceSquaredTo(P.Pos - new Vector2(0, 45)) < (b.R + P.R) * (b.R + P.R)) { HurtPlayer(b.Dmg); b.T = 0; }
        }
        FoeBullets.RemoveAll(b => b.T <= 0);

        // Опыт
        foreach (var g in Gems)
        {
            float d2 = g.Pos.DistanceSquaredTo(P.Pos);
            if (d2 < P.Magnet * P.Magnet) g.Pulled = true;
            if (!g.Pulled) continue;
            float d = Mathf.Sqrt(d2);
            g.Pos += (P.Pos - g.Pos) / Mathf.Max(d, 0.001f) * 480 * dt;
            if (d < 18) { g.Got = true; GainXp(g.V); }
        }
        Gems.RemoveAll(g => g.Got);
        if (Gems.Count > 400) Gems.RemoveRange(0, Gems.Count - 400);

        // Цветы в букет
        foreach (var f in Flowers)
        {
            f.Bob += dt * 3;
            float d2 = f.Pos.DistanceSquaredTo(P.Pos);
            if (d2 < 30 * 30)
            {
                f.Got = true;
                f.Kind.Apply(P);
                P.Bouquet.Add(f.Kind);
                Texts.Add(new FloatText { Pos = P.Pos - new Vector2(0, 175), Text = $"{f.Kind.Name}: {f.Kind.Desc}", T = 1.6f, C = f.Kind.Color, Big = true });
                Sound?.Invoke("flower");
            }
            else if (d2 > 1600 * 1600) f.Got = true;
        }
        Flowers.RemoveAll(f => f.Got);
        while (Flowers.Count < 5) SpawnFlower(false);

        // Страницы гримуара
        foreach (var pg in Pages)
        {
            pg.Bob += dt * 3;
            if (pg.Pos.DistanceSquaredTo(P.Pos) < 36 * 36)
            {
                pg.Got = true;
                PagesFound++;
                PageQueue++;
                Banner?.Invoke($"Страница гримуара {PagesFound}/{Data.PagesToBoss}", "Выбери школу магии", 2);
                Sound?.Invoke("page");
            }
        }
        Pages.RemoveAll(pg => pg.Got);

        // Эффекты
        foreach (var q in Particles) { q.Pos += q.Vel * dt; q.Vel *= 0.92f; q.T -= dt; }
        Particles.RemoveAll(q => q.T <= 0);
        int maxParticles = Settings.HighFx ? 900 : 250;
        if (Particles.Count > maxParticles) Particles.RemoveRange(0, Particles.Count - maxParticles);
        foreach (var t in Texts) { t.Pos.Y -= 40 * dt; t.T -= dt; }
        Texts.RemoveAll(t => t.T <= 0);
        if (Texts.Count > 120) Texts.RemoveRange(0, Texts.Count - 120);
        foreach (var b in Bolts) b.T -= dt;
        Bolts.RemoveAll(b => b.T <= 0);
        foreach (var r in Rings) { r.T -= dt; r.R += (r.MaxR - r.R) * Mathf.Min(1, dt * 18); }
        Rings.RemoveAll(r => r.T <= 0);
        Shake = Mathf.Max(0, Shake - dt);

        return WinAt > 0 && T >= WinAt;
    }

    void SeparateEnemies()
    {
        const float cell = 56;
        var grid = new Dictionary<(int, int), List<Enemy>>();
        foreach (var e in Enemies)
        {
            var k = ((int)Mathf.Floor(e.Pos.X / cell), (int)Mathf.Floor(e.Pos.Y / cell));
            if (!grid.TryGetValue(k, out var list)) grid[k] = list = new List<Enemy>();
            list.Add(e);
        }
        foreach (var e in Enemies)
        {
            int cx = (int)Mathf.Floor(e.Pos.X / cell), cy = (int)Mathf.Floor(e.Pos.Y / cell);
            for (int ox = -1; ox <= 1; ox++)
            for (int oy = -1; oy <= 1; oy++)
            {
                if (!grid.TryGetValue((cx + ox, cy + oy), out var list)) continue;
                foreach (var o in list)
                {
                    if (o == e) continue;
                    var dv = e.Pos - o.Pos;
                    float rr = (e.R + o.R) * 0.8f;
                    float d2 = dv.LengthSquared();
                    if (d2 > 0 && d2 < rr * rr)
                    {
                        float d = Mathf.Sqrt(d2);
                        e.Pos += dv / d * (rr - d) * 0.25f * (e.IsBoss ? 0.05f : 1);
                    }
                }
            }
        }
    }

    void GainXp(float v)
    {
        P.Xp += v;
        Sound?.Invoke("xp");
        while (P.Xp >= P.XpNext)
        {
            P.Xp -= P.XpNext;
            P.Level++;
            P.XpNext = Mathf.Round(6 + P.Level * 4 + P.Level * P.Level * 0.35f);
            LevelUpQueue++;
            Sound?.Invoke("lvl");
        }
    }
}
