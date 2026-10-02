using System.Collections.Generic;
using UnityEngine;

namespace McBilly.Proto
{
    /// <summary>The arena: tile visuals, cell/world conversion and enemy occupancy.</summary>
    public class Board : MonoBehaviour
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        /// <summary>True when the enemy rail (one tile outside the grid) is in use and drawn.</summary>
        public bool HasRail { get; private set; }

        /// <summary>The ring of cells just outside the grid, in loop order (counter-clockwise).</summary>
        public readonly List<Vector2Int> Ring = new List<Vector2Int>();
        readonly Dictionary<Vector2Int, int> ringIndex = new Dictionary<Vector2Int, int>();

        readonly Dictionary<Vector2Int, Enemy> occupants = new Dictionary<Vector2Int, Enemy>();

        /// <summary>Missing tiles: nobody can stand on, walk into or dash through them. Attacks still pass over.</summary>
        readonly HashSet<Vector2Int> missing = new HashSet<Vector2Int>();
        public int MissingCount => missing.Count;

        /// <summary>The most tiles that may be missing: 10% of the grid, rounded down.</summary>
        public static int MaxMissingFor(int width, int height) => Mathf.FloorToInt(width * height * .1f);

        public void Build(int width, int height, bool showRail = false, int missingTiles = 0, Vector2Int keepClear = default)
        {
            Width = width;
            Height = height;
            HasRail = showRail;
            BuildRing();
            PickMissingTiles(missingTiles, keepClear);

            var frame = Prims.NewSprite("Frame", Prims.Square, Palette.Frame, transform, -10);
            frame.transform.localScale = new Vector3(width + .4f, height + .4f, 1f);

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var c = new Vector2Int(x, y);
                if (missing.Contains(c))
                {
                    // A hole: no floor, just a dark pit with a faint cross.
                    var pit = Prims.NewSprite($"Missing {x},{y}", Prims.Square, Palette.Frame, transform, -5, .94f);
                    pit.transform.position = ToWorld(c);
                    for (int k = 0; k < 2; k++)
                    {
                        var bar = Prims.NewSprite("X", Prims.Square, Palette.Rail, transform, -4);
                        bar.transform.position = ToWorld(c);
                        bar.transform.rotation = Quaternion.Euler(0, 0, k == 0 ? 45f : -45f);
                        bar.transform.localScale = new Vector3(.75f, .05f, 1f);
                    }
                    continue;
                }
                var tile = Prims.NewSprite($"Tile {x},{y}", Prims.Square, (x + y) % 2 == 0 ? Palette.TileA : Palette.TileB, transform, -5, .94f);
                tile.transform.position = ToWorld(c);
            }

            if (showRail)
            {
                // The rail enemies patrol: small dots so it reads as "not floor".
                foreach (var c in Ring)
                {
                    var dot = Prims.NewSprite($"Rail {c.x},{c.y}", Prims.Diamond, Palette.Rail, transform, -6, .22f);
                    dot.transform.position = ToWorld(c);
                }
            }
        }

        void BuildRing()
        {
            Ring.Clear();
            ringIndex.Clear();
            int w = Width, h = Height;
            for (int x = -1; x <= w; x++) Ring.Add(new Vector2Int(x, -1));     // bottom, left -> right
            for (int y = 0; y <= h; y++) Ring.Add(new Vector2Int(w, y));       // right, bottom -> top
            for (int x = w - 1; x >= -1; x--) Ring.Add(new Vector2Int(x, h));  // top, right -> left
            for (int y = h - 1; y >= 0; y--) Ring.Add(new Vector2Int(-1, y));  // left, top -> bottom
            for (int i = 0; i < Ring.Count; i++) ringIndex[Ring[i]] = i;
        }

        public bool IsRing(Vector2Int c) => ringIndex.ContainsKey(c);

        public bool IsMissing(Vector2Int c) => missing.Contains(c);

        /// <summary>On the grid and not a missing tile.</summary>
        public bool IsWalkable(Vector2Int c) => InBounds(c) && !missing.Contains(c);

        /// <summary>
        /// Randomly removes up to <paramref name="count"/> tiles (capped at 10% of the grid), never the
        /// player's start tile or its neighbours, and never in a way that cuts the floor into separate islands.
        /// </summary>
        void PickMissingTiles(int count, Vector2Int keepClear)
        {
            missing.Clear();
            count = Mathf.Clamp(count, 0, MaxMissingFor(Width, Height));
            if (count == 0) return;

            var candidates = new List<Vector2Int>();
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                var c = new Vector2Int(x, y);
                if (Chebyshev(c, keepClear) > 1) candidates.Add(c);
            }
            for (int i = candidates.Count - 1; i > 0; i--) // shuffle
            {
                int j = Random.Range(0, i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }

            foreach (var c in candidates)
            {
                if (missing.Count >= count) break;
                missing.Add(c);
                if (!FloorIsConnected(keepClear)) missing.Remove(c);
            }
        }

        bool FloorIsConnected(Vector2Int start)
        {
            var seen = new HashSet<Vector2Int> { start };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            Vector2Int[] steps = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                foreach (var s in steps)
                {
                    var n = c + s;
                    if (IsWalkable(n) && seen.Add(n)) queue.Enqueue(n);
                }
            }
            return seen.Count == Width * Height - missing.Count;
        }

        /// <summary>Rail enemies stand on the ring; grid enemies stand on the grid. They never cross over.</summary>
        public bool IsEnemyCell(Vector2Int c, bool rail) => rail ? IsRing(c) : IsWalkable(c);

        /// <summary>Steps around the ring, signed, shortest way (positive = counter-clockwise).</summary>
        public int RingDelta(Vector2Int from, Vector2Int to)
        {
            int n = Ring.Count;
            int d = ((ringIndex[to] - ringIndex[from]) % n + n) % n;
            return d > n / 2 ? d - n : d;
        }

        /// <summary>The neighbouring ring cell one step from <paramref name="from"/> toward <paramref name="to"/>.</summary>
        public Vector2Int RingStep(Vector2Int from, Vector2Int to)
        {
            int d = RingDelta(from, to);
            if (d == 0) return from;
            int n = Ring.Count;
            return Ring[((ringIndex[from] + (d > 0 ? 1 : -1)) % n + n) % n];
        }

        public Vector3 ToWorld(Vector2Int c) => new Vector3(c.x - (Width - 1) * .5f, c.y - (Height - 1) * .5f, 0f);

        public bool InBounds(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < Width && c.y < Height;

        public bool InWorldBounds(Vector3 p, float margin) =>
            Mathf.Abs(p.x) <= Width * .5f + margin && Mathf.Abs(p.y) <= Height * .5f + margin;

        public Enemy EnemyAt(Vector2Int c) => occupants.TryGetValue(c, out var e) ? e : null;

        public void SetOccupant(Vector2Int c, Enemy e) => occupants[c] = e;

        public void Vacate(Vector2Int c, Enemy e)
        {
            if (occupants.TryGetValue(c, out var cur) && cur == e) occupants.Remove(c);
        }

        /// <summary>A cell an enemy may stand on, with no enemy, and (optionally) not the player's tile.</summary>
        public bool IsFree(Vector2Int c, bool rail, bool blockPlayerTile = true)
        {
            if (!IsEnemyCell(c, rail) || EnemyAt(c) != null) return false;
            return !blockPlayerTile || McBillyGame.I.Player.Cell != c;
        }

        public static int Chebyshev(Vector2Int a, Vector2Int b) => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
    }

    /// <summary>
    /// A telegraphed tile. The inner square grows from small to full size over the wind-up,
    /// so the fill itself is the countdown: when it touches the edges, it hits.
    /// The owning enemy decides what happens on resolve and destroys the warning.
    /// </summary>
    public class TileWarning : MonoBehaviour
    {
        public Vector2Int Cell { get; private set; }
        public Enemy Owner { get; private set; }
        public float Remaining => duration - t;

        SpriteRenderer outline, fill;
        float duration, t;

        public static TileWarning Create(Enemy owner, Vector2Int cell, float duration)
        {
            var g = McBillyGame.I;
            var go = new GameObject("Warning");
            go.transform.SetParent(g.Root, false);
            go.transform.position = g.Board.ToWorld(cell);
            var w = go.AddComponent<TileWarning>();
            w.Owner = owner;
            w.Cell = cell;
            w.duration = Mathf.Max(.01f, duration);
            w.outline = Prims.NewSprite("Outline", Prims.Square, Palette.Warning.WithAlpha(.18f), go.transform, -2, .94f);
            w.fill = Prims.NewSprite("Fill", Prims.Square, Palette.Warning.WithAlpha(.55f), go.transform, -1, .1f);
            g.Warnings.Add(w);
            return w;
        }

        void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            fill.transform.localScale = Vector3.one * Mathf.Lerp(.15f, .94f, k);
            float blink = k > .7f ? (Mathf.Sin(Time.time * 60f) > 0f ? 1f : .6f) : 1f;
            fill.color = Palette.Warning.WithAlpha(Mathf.Lerp(.35f, .8f, k) * blink);
        }

        void OnDestroy()
        {
            if (McBillyGame.I != null) McBillyGame.I.Warnings.Remove(this);
        }
    }

    /// <summary>Fire-and-forget scale/alpha tween used for sparks, afterimages, beams, slams.</summary>
    public class FadeFx : MonoBehaviour
    {
        SpriteRenderer sr;
        Vector3 fromScale, toScale;
        float fromAlpha, duration, t;
        bool unscaled;

        public static FadeFx Spawn(Sprite sprite, Color color, Vector3 pos, float rotationDeg,
                                   Vector3 fromScale, Vector3 toScale, float duration, int order = 40, bool unscaled = false)
        {
            var sr = Prims.NewSprite("Fx", sprite, color, McBillyGame.I.Root, order);
            sr.transform.position = pos;
            sr.transform.rotation = Quaternion.Euler(0, 0, rotationDeg);
            sr.transform.localScale = fromScale;
            var fx = sr.gameObject.AddComponent<FadeFx>();
            fx.sr = sr;
            fx.fromScale = fromScale;
            fx.toScale = toScale;
            fx.fromAlpha = color.a;
            fx.duration = duration;
            fx.unscaled = unscaled;
            return fx;
        }

        void Update()
        {
            t += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            transform.localScale = Vector3.LerpUnclamped(fromScale, toScale, Ease.OutCubic(k));
            sr.color = sr.color.WithAlpha(fromAlpha * (1f - k));
            if (k >= 1f) Destroy(gameObject);
        }
    }
}
