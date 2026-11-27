using System.Collections.Generic;
using Godot;

namespace WitchApprentice;

/// <summary>
/// Векторная отрисовка мира в духе «3D в 2D»: объёмные фигуры, тени от луны вправо-вниз,
/// светящиеся части рисуются отдельно в слое свечения (см. Fx).
/// </summary>
public static class Painter
{
    // Направление теней (луна слева сверху).
    public static readonly Vector2 ShadowDir = new(0.55f, 0.22f);

    static readonly Dictionary<(float, float, int), Vector2[]> EllipseCache = new();

    public static Vector2[] EllipsePts(float rx, float ry, int n = 22)
    {
        var key = (rx, ry, n);
        if (EllipseCache.TryGetValue(key, out var pts)) return pts;
        pts = new Vector2[n];
        for (int i = 0; i < n; i++) pts[i] = new Vector2(Mathf.Cos(i * Mathf.Tau / n) * rx, Mathf.Sin(i * Mathf.Tau / n) * ry);
        if (EllipseCache.Count < 4000) EllipseCache[key] = pts;
        return pts;
    }

    public static void Ellipse(CanvasItem ci, Vector2 c, float rx, float ry, Color col)
    {
        var src = EllipsePts(rx, ry);
        var pts = new Vector2[src.Length];
        for (int i = 0; i < src.Length; i++) pts[i] = src[i] + c;
        ci.DrawColoredPolygon(pts, col);
    }

    public static void Poly(CanvasItem ci, Color col, params Vector2[] pts) => ci.DrawColoredPolygon(pts, col);

    static Vector2 V(float x, float y) => new(x, y);

    /// <summary>Тень под объектом, вытянутая в сторону от луны.</summary>
    public static void GroundShadow(CanvasItem ci, Vector2 feet, float w, float alpha = 0.5f)
    {
        Ellipse(ci, feet + new Vector2(w * 0.45f, 2), w * 1.25f, w * 0.38f, new Color(0, 0, 0, alpha * 0.6f));
        Ellipse(ci, feet + new Vector2(w * 0.15f, 1), w * 0.8f, w * 0.28f, new Color(0, 0, 0, alpha));
    }

    // ---------------- Ведьма ----------------

    public static readonly Color Skin = new("#ffe6da");
    public static readonly Color Hair = new("#6b4cc9");
    public static readonly Color HairDark = new("#3f2a86");
    public static readonly Color DressBase = new("#ece6f7");
    public static readonly Color DressShade = new("#b9addb");
    public static readonly Color TrimBase = new("#8e6cc9");
    public static readonly Color CapeBase = new("#2c2050");
    public static readonly Color HatBase = new("#f1ecfa");
    public static readonly Color HatUnder = new("#4b2f94");

    /// <summary>Ведьма в локальных координатах ~64 px ростом. feet — точка между ступнями.</summary>
    public static void Witch(CanvasItem ci, WitchLook look, Vector2 feet, float s, bool flip, float anim, bool flash)
    {
        GroundShadow(ci, feet, 13 * s);
        ci.DrawSetTransform(feet, 0, new Vector2(flip ? -s : s, s));

        var trim = look.DressLight ?? TrimBase;
        var trimDark = look.DressDark ?? CapeBase;
        var hat = look.HatLight ?? HatBase;
        var hatUnder = look.HatDark ?? HatUnder;
        Color F(Color c) => flash ? c.Lerp(Colors.White, 0.7f) : c;

        float bob = -Mathf.Abs(Mathf.Sin(anim)) * 1.4f;
        float sway = Mathf.Sin(anim) * 1.8f;
        float step = Mathf.Sin(anim) * 2.2f;

        // плащ и волосы сзади
        Poly(ci, F(trimDark), V(-6, -41 + bob), V(6, -41 + bob), V(13, -4), V(-17 - sway, -2), V(-14, -24 + bob));
        Poly(ci, F(HairDark), V(-9, -52 + bob), V(8, -52 + bob), V(9, -34 + bob), V(5, -16 + bob), V(-6 - sway, -12 + bob), V(-14 - sway, -18 + bob), V(-12, -36 + bob));

        // ботинки
        Ellipse(ci, V(-4 + step * 0.4f, -1.5f), 3.2f, 1.8f, F(new Color("#2a1f35")));
        Ellipse(ci, V(4 - step * 0.4f, -1.5f), 3.2f, 1.8f, F(new Color("#2a1f35")));

        // юбка
        Poly(ci, F(DressBase), V(-7, -30 + bob), V(7, -30 + bob), V(15 + sway * 0.3f, -3), V(-15 + sway * 0.3f, -3));
        Poly(ci, F(DressShade), V(-7, -30 + bob), V(-2, -30 + bob), V(-6 + sway * 0.3f, -3), V(-15 + sway * 0.3f, -3));
        Poly(ci, F(trim), V(-15 + sway * 0.3f, -6), V(15 + sway * 0.3f, -6), V(15 + sway * 0.3f, -3), V(-15 + sway * 0.3f, -3));
        // корсет и воротник
        Poly(ci, F(trim), V(-6, -42 + bob), V(6, -42 + bob), V(7, -29 + bob), V(-7, -29 + bob));
        Poly(ci, F(DressBase), V(-4, -43 + bob), V(4, -43 + bob), V(0, -37 + bob));
        ci.DrawLine(V(-3, -36 + bob), V(3, -32 + bob), F(DressBase), 0.8f);
        ci.DrawLine(V(3, -36 + bob), V(-3, -32 + bob), F(DressBase), 0.8f);

        // посох в руке
        ci.DrawLine(V(11, -55 + bob), V(13, -1), F(new Color("#5a3b2e")), 2.2f);
        Ellipse(ci, V(9.5f, -34 + bob), 2.4f, 2.4f, F(Skin));
        Poly(ci, F(DressBase), V(5, -41 + bob), V(8, -40 + bob), V(11, -35 + bob), V(8, -33 + bob));

        // голова
        Ellipse(ci, V(0, -50 + bob), 9, 9.5f, F(Skin));
        // глаза (аниме: крупные, с бликом)
        foreach (var ex in new[] { 1.8f, 6.6f })
        {
            Ellipse(ci, V(ex, -49.5f + bob), 1.9f, 2.7f, F(new Color("#b0468f")));
            Ellipse(ci, V(ex, -48.6f + bob), 1.3f, 1.5f, F(new Color("#3a1240")));
            Ellipse(ci, V(ex - 0.6f, -50.6f + bob), 0.7f, 0.8f, Colors.White);
            ci.DrawLine(V(ex - 2.2f, -52.4f + bob), V(ex + 2.0f, -52.6f + bob), F(new Color("#2a1638")), 0.9f);
        }
        Ellipse(ci, V(0.5f, -46 + bob), 1.6f, 0.8f, new Color(1, 0.55f, 0.65f, 0.5f));
        Ellipse(ci, V(7.5f, -46 + bob), 1.4f, 0.7f, new Color(1, 0.55f, 0.65f, 0.5f));
        ci.DrawLine(V(4, -44.2f + bob), V(5.4f, -44.4f + bob), F(new Color("#a0505a")), 0.7f);

        // чёлка и прядь
        Poly(ci, F(Hair), V(-9.5f, -51 + bob), V(-8, -58 + bob), V(0, -61 + bob), V(8, -58.5f + bob), V(10, -52 + bob), V(7, -54.5f + bob),
            V(4.5f, -52.5f + bob), V(2, -55.5f + bob), V(-1, -53 + bob), V(-4, -55 + bob), V(-7, -50 + bob));
        Poly(ci, F(Hair), V(8.5f, -55 + bob), V(11, -49 + bob), V(10.5f, -38 + bob + sway * 0.3f), V(8, -45 + bob));
        Poly(ci, F(Hair), V(-9.5f, -53 + bob), V(-11, -44 + bob), V(-10, -32 + bob + sway * 0.3f), V(-7.5f, -44 + bob));

        // шляпа: широкие поля, загнутый конус, лента и роза
        Ellipse(ci, V(0, -57.5f + bob), 19, 5, F(hatUnder));
        Ellipse(ci, V(0, -58.5f + bob), 18.5f, 4.2f, F(hat));
        Poly(ci, F(hat), V(-9, -59 + bob), V(9, -59 + bob), V(6, -67 + bob), V(3, -76 + bob), V(-2, -84 + bob + sway * 0.4f),
            V(-9, -83 + bob + sway * 0.4f), V(-5, -79 + bob), V(-6, -69 + bob));
        Poly(ci, F(hatUnder), V(-9, -59 + bob), V(-6, -69 + bob), V(-5, -79 + bob), V(-9, -83 + bob + sway * 0.4f), V(-8, -72 + bob), V(-10, -62 + bob));
        Poly(ci, F(trim), V(-9.4f, -59.5f + bob), V(9.4f, -59.5f + bob), V(8.6f, -63 + bob), V(-8.6f, -63 + bob));
        Ellipse(ci, V(6.5f, -62 + bob), 2.8f, 2.6f, F(new Color("#f6d6ea")));
        Ellipse(ci, V(6.5f, -62 + bob), 1.4f, 1.3f, F(new Color("#d98ab8")));
        Ellipse(ci, V(9.5f, -61 + bob), 2.0f, 1.9f, F(new Color("#e9e2ff")));

        ci.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }

    /// <summary>Точка навершия посоха в мировых координатах (для свечения).</summary>
    public static Vector2 StaffTip(Vector2 feet, float s, bool flip, float anim) =>
        feet + new Vector2((flip ? -11 : 11) * s, (-56 - Mathf.Abs(Mathf.Sin(anim)) * 1.4f) * s);

    // ---------------- Враги ----------------

    public static void Enemy(CanvasItem ci, Enemy e, float t, bool faceLeft, Vector2 feet)
    {
        float s = e.Def.Scale;
        bool flash = e.HitFlash > 0;
        Color F(Color c) => flash ? c.Lerp(Colors.White, 0.75f) : e.Slow > 0 ? c.Lerp(new Color("#9fd3ff"), 0.4f) : c;

        switch (e.Def.Id)
        {
            case "bat":
            {
                GroundShadow(ci, feet, 6 * s, 0.35f);
                float hover = 22 + Mathf.Sin(t * 6 + e.Phase) * 4;
                float flap = Mathf.Sin(t * 22 + e.Phase);
                ci.DrawSetTransform(feet - new Vector2(0, hover * s / 3), 0, new Vector2(faceLeft ? -s : s, s));
                Poly(ci, F(new Color("#3d2a5c")), V(-2, -2), V(-16, -6 - flap * 7), V(-12, 1), V(-7, -1), V(-4, 3));
                Poly(ci, F(new Color("#3d2a5c")), V(2, -2), V(16, -6 - flap * 7), V(12, 1), V(7, -1), V(4, 3));
                Ellipse(ci, V(0, 0), 5, 5.5f, F(new Color("#251a38")));
                Poly(ci, F(new Color("#251a38")), V(-4, -3), V(-3, -8), V(-1, -4));
                Poly(ci, F(new Color("#251a38")), V(4, -3), V(3, -8), V(1, -4));
                break;
            }
            case "slime":
            {
                GroundShadow(ci, feet, 9 * s, 0.45f);
                float q = Mathf.Sin(t * 6 + e.Phase) * 0.12f;
                ci.DrawSetTransform(feet, 0, new Vector2((faceLeft ? -s : s) * (1 + q), s * (1 - q)));
                Poly(ci, F(new Color("#5a2384")), V(-11, 0), V(-10, -7), V(-6, -12), V(0, -14), V(6, -12), V(10, -7), V(11, 0));
                Poly(ci, F(new Color("#7c3bb0")), V(-8, -2), V(-7, -8), V(-3, -11), V(2, -11), V(-2, -7), V(-4, -2));
                Ellipse(ci, V(-4, -10), 2, 1.2f, new Color(1, 1, 1, 0.5f));
                break;
            }
            case "boss":
            {
                GroundShadow(ci, feet, 16 * s, 0.6f);
                float hop = Mathf.Abs(Mathf.Sin(t * 3 + e.Phase)) * 1.2f;
                ci.DrawSetTransform(feet, 0, new Vector2(faceLeft ? -s : s, s));
                // плащ
                Poly(ci, F(new Color("#160812")), V(-14, -30 - hop), V(14, -30 - hop), V(20, 0), V(-22, 0));
                // ноги
                Ellipse(ci, V(-6, -3), 4, 3, F(new Color("#24070f")));
                Ellipse(ci, V(6, -3), 4, 3, F(new Color("#24070f")));
                // тело
                Ellipse(ci, V(0, -20 - hop), 13, 15, F(new Color("#4a0f1e")));
                Ellipse(ci, V(2, -17 - hop), 8, 10, F(new Color("#6b1a2c")));
                // руки-когти
                Poly(ci, F(new Color("#3a0a16")), V(10, -26 - hop), V(19, -18 - hop), V(21, -10 - hop), V(16, -12 - hop), V(9, -18 - hop));
                Poly(ci, F(new Color("#3a0a16")), V(-10, -26 - hop), V(-19, -18 - hop), V(-21, -10 - hop), V(-16, -12 - hop), V(-9, -18 - hop));
                // голова и рога
                Ellipse(ci, V(1, -38 - hop), 8, 7.5f, F(new Color("#5a1424")));
                Poly(ci, F(new Color("#d9c6a0")), V(-5, -42 - hop), V(-15, -52 - hop), V(-12, -40 - hop));
                Poly(ci, F(new Color("#d9c6a0")), V(6, -42 - hop), V(16, -52 - hop), V(12, -40 - hop));
                // корона
                Poly(ci, F(new Color("#e8c14a")), V(-6, -44 - hop), V(-6, -50 - hop), V(-3, -47 - hop), V(0, -52 - hop), V(3, -47 - hop), V(7, -50 - hop), V(7, -44 - hop));
                ci.DrawLine(V(-4, -34 - hop), V(6, -34 - hop), F(new Color("#1a0508")), 1.2f);
                break;
            }
            default: // imp и elite — бесы
            {
                bool elite = e.Def.Id == "elite";
                GroundShadow(ci, feet, 8 * s, 0.5f);
                float hop = Mathf.Abs(Mathf.Sin(t * 9 + e.Phase)) * 2.2f;
                float leg = Mathf.Sin(t * 9 + e.Phase) * 1.5f;
                ci.DrawSetTransform(feet, 0, new Vector2(faceLeft ? -s : s, s));
                var body = elite ? new Color("#3d0c1a") : new Color("#7a1f33");
                var belly = elite ? new Color("#5e1528") : new Color("#a33a4a");
                var horn = elite ? new Color("#e8c14a") : new Color("#2a1016");
                // хвост
                ci.DrawPolyline(new[] { V(-6, -8 - hop), V(-11, -6 - hop), V(-13, -11 - hop), V(-11, -14 - hop) }, F(body), 1.4f);
                Poly(ci, F(body), V(-11, -14 - hop), V(-13, -17 - hop), V(-9, -15 - hop));
                // ноги
                Ellipse(ci, V(-3 + leg, -1.5f), 2.6f, 1.8f, F(new Color("#2a0d14")));
                Ellipse(ci, V(3 - leg, -1.5f), 2.6f, 1.8f, F(new Color("#2a0d14")));
                // крылышки
                Poly(ci, F(new Color("#2b0b15")), V(-4, -15 - hop), V(-12, -22 - hop), V(-10, -14 - hop), V(-6, -12 - hop));
                // тело
                Ellipse(ci, V(0, -10 - hop), 7.5f, 8.5f, F(body));
                Ellipse(ci, V(1.5f, -8 - hop), 4.5f, 5.5f, F(belly));
                // голова
                Ellipse(ci, V(1, -18 - hop), 6.5f, 5.8f, F(body));
                Poly(ci, F(horn), V(-3, -21 - hop), V(-6, -29 - hop), V(-0.5f, -23 - hop));
                Poly(ci, F(horn), V(4, -21 - hop), V(7, -29 - hop), V(2.5f, -23 - hop));
                ci.DrawLine(V(1, -14.5f - hop), V(6, -15 - hop), F(new Color("#1a0508")), 0.9f);
                if (elite)
                {
                    // свиток-страница в лапе
                    Poly(ci, F(new Color("#f3e8c8")), V(6, -14 - hop), V(13, -15 - hop), V(14, -6 - hop), V(7, -5 - hop));
                    for (int i = 0; i < 3; i++) ci.DrawLine(V(8, -12 + i * 2.2f - hop), V(12.5f, -12.4f + i * 2.2f - hop), F(new Color("#8d6e63")), 0.5f);
                }
                break;
            }
        }
        ci.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }

    /// <summary>Точки глаз врага в мире — их рисует слой свечения.</summary>
    public static IEnumerable<Vector2> EnemyEyes(Enemy e, float t, bool faceLeft)
    {
        float s = e.Def.Scale;
        float dir = faceLeft ? -1 : 1;
        switch (e.Def.Id)
        {
            case "bat":
            {
                float hover = 22 + Mathf.Sin(t * 6 + e.Phase) * 4;
                var c = e.Pos - new Vector2(0, hover * s / 3);
                yield return c + new Vector2(dir * 1.6f * s, -1 * s);
                yield return c + new Vector2(dir * -1.6f * s, -1 * s);
                break;
            }
            case "slime":
                yield return e.Pos + new Vector2(dir * 2 * s, -8 * s);
                yield return e.Pos + new Vector2(dir * 6 * s, -8 * s);
                break;
            case "boss":
            {
                float hop = Mathf.Abs(Mathf.Sin(t * 3 + e.Phase)) * 1.2f;
                yield return e.Pos + new Vector2(dir * -2 * s, (-39 - hop) * s);
                yield return e.Pos + new Vector2(dir * 4.5f * s, (-39 - hop) * s);
                break;
            }
            default:
            {
                float hop = Mathf.Abs(Mathf.Sin(t * 9 + e.Phase)) * 2.2f;
                yield return e.Pos + new Vector2(dir * 0 * s, (-19 - hop) * s);
                yield return e.Pos + new Vector2(dir * 4 * s, (-19 - hop) * s);
                break;
            }
        }
    }

    public static Color EyeColor(Enemy e) => e.Def.Id switch
    {
        "bat" => new Color("#ff3b5c"),
        "slime" => new Color("#e6b3ff"),
        "boss" => new Color("#ff2a2a"),
        "elite" => new Color("#ffd54f"),
        _ => new Color("#ffcf40"),
    };

    /// <summary>Высота врага над землёй (центр тела) — для попаданий и полосок здоровья.</summary>
    public static float BodyHeight(Enemy e) => e.Def.Id switch
    {
        "bat" => 22 * e.Def.Scale / 3 + 2,
        "slime" => 7 * e.Def.Scale,
        "boss" => 24 * e.Def.Scale,
        _ => 12 * e.Def.Scale,
    };

    public static float TopHeight(Enemy e) => e.Def.Id switch
    {
        "bat" => 22 * e.Def.Scale / 3 + 10 * e.Def.Scale,
        "slime" => 15 * e.Def.Scale,
        "boss" => 54 * e.Def.Scale,
        _ => 30 * e.Def.Scale,
    };

    // ---------------- Лес ----------------

    public sealed class TreeGeom
    {
        public Vector2 Base;
        public readonly List<(Vector2 a, Vector2 b, float w)> Segs = new();
        public float Height;
    }

    public static TreeGeom BuildTree(Vector2 basePos, System.Random rng)
    {
        var g = new TreeGeom { Base = basePos };
        float h = 110 + (float)rng.NextDouble() * 110;
        g.Height = h;
        float lean = ((float)rng.NextDouble() - 0.5f) * 0.5f;
        var p = basePos;
        int segs = 5;
        float w = 11 + (float)rng.NextDouble() * 5;
        for (int i = 0; i < segs; i++)
        {
            float ang = -Mathf.Pi / 2 + lean + ((float)rng.NextDouble() - 0.5f) * 0.35f;
            var q = p + Vector2.FromAngle(ang) * (h / segs);
            float w2 = w * 0.82f;
            g.Segs.Add((p, q, w));
            if (i >= 2) Branch(g, q, ang + (rng.NextDouble() < 0.5 ? -1 : 1) * (0.6f + (float)rng.NextDouble() * 0.5f), h * 0.35f, w2 * 0.6f, 2, rng);
            p = q; w = w2;
        }
        Branch(g, p, -Mathf.Pi / 2 - 0.5f, h * 0.3f, w * 0.8f, 2, rng);
        Branch(g, p, -Mathf.Pi / 2 + 0.6f, h * 0.28f, w * 0.8f, 2, rng);
        // корни
        for (int i = 0; i < 3; i++)
        {
            float a = (i - 1) * 0.9f + ((float)rng.NextDouble() - 0.5f) * 0.4f;
            g.Segs.Add((basePos + new Vector2(0, -4), basePos + new Vector2(Mathf.Sin(a) * 22, 3 + (float)rng.NextDouble() * 4), 5));
        }
        return g;
    }

    static void Branch(TreeGeom g, Vector2 from, float ang, float len, float w, int depth, System.Random rng)
    {
        var to = from + Vector2.FromAngle(ang) * len;
        var mid = from.Lerp(to, 0.5f) + new Vector2(((float)rng.NextDouble() - 0.5f) * len * 0.25f, 0);
        g.Segs.Add((from, mid, w));
        g.Segs.Add((mid, to, w * 0.7f));
        if (depth <= 0 || w < 1.2f) return;
        Branch(g, to, ang - 0.5f - (float)rng.NextDouble() * 0.3f, len * 0.6f, w * 0.6f, depth - 1, rng);
        Branch(g, to, ang + 0.4f + (float)rng.NextDouble() * 0.3f, len * 0.55f, w * 0.6f, depth - 1, rng);
        if (rng.NextDouble() < 0.5) Branch(g, mid, ang + 0.7f, len * 0.4f, w * 0.5f, depth - 1, rng);
    }

    static Vector2 ShadowOf(Vector2 p, Vector2 basePos)
    {
        float up = basePos.Y - p.Y;
        return new Vector2(p.X + up * ShadowDir.X, basePos.Y + up * ShadowDir.Y);
    }

    public static Rect2 TreeBounds(TreeGeom g)
    {
        var r = new Rect2(g.Base, Vector2.Zero);
        foreach (var (a, b, w) in g.Segs) { r = r.Expand(a).Expand(b); }
        return r.Grow(16);
    }

    public static Rect2 TreeShadowBounds(TreeGeom g)
    {
        var r = new Rect2(g.Base, Vector2.Zero);
        foreach (var (a, b, w) in g.Segs) { r = r.Expand(ShadowOf(a, g.Base)).Expand(ShadowOf(b, g.Base)); }
        return r.Grow(16);
    }

    public static void TreeShadow(CanvasItem ci, TreeGeom g)
    {
        var c = new Color(0, 0, 0, 0.32f);
        foreach (var (a, b, w) in g.Segs) ci.DrawLine(ShadowOf(a, g.Base), ShadowOf(b, g.Base), c, w * 0.9f);
    }

    public static void Tree(CanvasItem ci, TreeGeom g, float alpha)
    {
        var dark = new Color("#211a26", alpha);
        var mid = new Color("#3a2f3f", alpha);
        var hi = new Color("#5c4b5e", alpha * 0.8f);
        foreach (var (a, b, w) in g.Segs) ci.DrawLine(a, b, dark, w + 2);
        foreach (var (a, b, w) in g.Segs)
        {
            ci.DrawLine(a, b, mid, w);
            if (w > 3) ci.DrawLine(a + new Vector2(-w * 0.25f, 0), b + new Vector2(-w * 0.25f, 0), hi, Mathf.Max(1, w * 0.25f));
        }
        foreach (var (a, _, w) in g.Segs) if (w > 2) ci.DrawCircle(a, w / 2, mid);
    }
}
