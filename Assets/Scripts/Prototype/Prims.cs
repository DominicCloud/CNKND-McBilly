using System;
using UnityEngine;

namespace McBilly.Proto
{
    /// <summary>
    /// Runtime-generated primitive sprites, so the prototype needs zero art assets.
    /// Every sprite is 1 world unit (= 1 grid tile) across.
    /// </summary>
    public static class Prims
    {
        static Sprite square, triangle, circle, ring, diamond;
        static Material material;

        public static Sprite Square => square != null ? square : (square = Make(16, (u, v) => 1f, 1));
        /// <summary>Points along +X (right). Rotate by the facing angle.</summary>
        public static Sprite Triangle => triangle != null ? triangle : (triangle = Make(128, TriangleCoverage));
        public static Sprite Circle => circle != null ? circle : (circle = Make(128, (u, v) => Sq(u - .5f) + Sq(v - .5f) <= .2401f ? 1f : 0f));
        public static Sprite Ring => ring != null ? ring : (ring = Make(128, (u, v) =>
        {
            float d = Sq(u - .5f) + Sq(v - .5f);
            return d <= .2401f && d >= .1600f ? 1f : 0f;
        }));
        public static Sprite Diamond => diamond != null ? diamond : (diamond = Make(128, (u, v) => Mathf.Abs(u - .5f) + Mathf.Abs(v - .5f) <= .48f ? 1f : 0f));

        /// <summary>Unlit sprite material, so nothing depends on 2D lights being in the scene.</summary>
        public static Material Material
        {
            get
            {
                if (material == null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                    if (shader == null) shader = Shader.Find("Sprites/Default");
                    material = new Material(shader) { hideFlags = HideFlags.DontSave };
                }
                return material;
            }
        }

        public static SpriteRenderer NewSprite(string name, Sprite sprite, Color color, Transform parent, int order, float scale = 1f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            sr.sharedMaterial = Material;
            return sr;
        }

        static float Sq(float x) => x * x;

        static float TriangleCoverage(float u, float v)
        {
            // Tip at the right edge, flat back on the left. Counter-clockwise winding.
            var p = new Vector2(u, v);
            var a = new Vector2(.95f, .50f);
            var b = new Vector2(.12f, .86f);
            var c = new Vector2(.12f, .14f);
            return Edge(a, b, p) >= 0f && Edge(b, c, p) >= 0f && Edge(c, a, p) >= 0f ? 1f : 0f;
        }

        static float Edge(Vector2 a, Vector2 b, Vector2 p) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);

        static Sprite Make(int size, Func<float, float, float> coverage, int supersample = 4)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            var px = new Color32[size * size];
            float inv = 1f / (supersample * supersample);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float a = 0f;
                for (int sy = 0; sy < supersample; sy++)
                for (int sx = 0; sx < supersample; sx++)
                    a += coverage((x + (sx + .5f) / supersample) / size, (y + (sy + .5f) / supersample) / size);
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * inv * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size, 0, SpriteMeshType.FullRect);
            s.hideFlags = HideFlags.DontSave;
            return s;
        }
    }

    /// <summary>8-way directions. Index i points at i*45 degrees (0 = right, counter-clockwise).</summary>
    public static class Dir8
    {
        static readonly Vector2Int[] Vecs =
        {
            new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(0, 1), new Vector2Int(-1, 1),
            new Vector2Int(-1, 0), new Vector2Int(-1, -1), new Vector2Int(0, -1), new Vector2Int(1, -1)
        };

        public static Vector2Int Vec(int i) => Vecs[Wrap(i)];
        public static Vector2 Unit(int i) => ((Vector2)Vecs[Wrap(i)]).normalized;
        public static float Angle(int i) => Wrap(i) * 45f;
        public static int Opposite(int i) => Wrap(i + 4);
        static int Wrap(int i) => ((i % 8) + 8) % 8;

        public static int FromVector(Vector2 v)
        {
            float a = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
            return Wrap(Mathf.RoundToInt(a / 45f));
        }

        /// <summary>How many 45-degree steps apart two directions are (0..4).</summary>
        public static int Steps(int a, int b)
        {
            int d = Mathf.Abs(Wrap(a) - Wrap(b));
            return d > 4 ? 8 - d : d;
        }
    }

    public static class Ease
    {
        public static float OutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            float f = 1f - t;
            return 1f - f * f * f;
        }

        public static float OutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
    }

    public static class Palette
    {
        public static readonly Color Background = Hex(0x14121A);
        public static readonly Color TileA = Hex(0x2A2633);
        public static readonly Color TileB = Hex(0x25222D);
        public static readonly Color Frame = Hex(0x0C0B10);
        public static readonly Color Player = Hex(0x7FE3FF);
        public static readonly Color Dodge = Hex(0x7FE3FF, .35f);
        public static readonly Color FacingTile = new Color(1f, 1f, 1f, .07f);
        public static readonly Color Shield = Color.white;
        public static readonly Color Warning = Hex(0xFF4D4D);
        public static readonly Color Projectile = Hex(0xFFB347);
        public static readonly Color Shooter = Hex(0xFF8A3D);
        public static readonly Color Brute = Hex(0xB26BFF);
        public static readonly Color Caster = Hex(0x4DE38A);
        public static readonly Color Patience = Hex(0xF2D14B);
        public static readonly Color Rail = new Color(1f, 1f, 1f, .1f);
        public static readonly Color BarBg = new Color(0f, 0f, 0f, .65f);

        public static Color Hex(int rgb, float a = 1f) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, a);

        public static Color WithAlpha(this Color c, float a) { c.a = a; return c; }
    }
}
