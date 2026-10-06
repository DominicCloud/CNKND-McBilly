using System.Collections.Generic;
using UnityEngine;

namespace McBilly.Proto
{
    /// <summary>
    /// Floor: normal.  Pillar: hard obstacle, nothing gets through.
    /// Vault: blocks walking and dodging, but McBilly can vault over it (Space while facing it).
    /// Gap: a hole. Dodge over it; walk into it and you fall (lose 1 health, back to where you stepped from).
    /// Broken: floor that cracks when McBilly lands on it, blinks, then acts as a Gap until it repairs itself.
    /// </summary>
    public enum TileKind { Floor, Pillar, Vault, Gap, Broken }

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

        /// <summary>Special tiles. Anything not in here is plain floor.</summary>
        readonly Dictionary<Vector2Int, TileKind> special = new Dictionary<Vector2Int, TileKind>();
        readonly Dictionary<Vector2Int, BreakableTile> breakables = new Dictionary<Vector2Int, BreakableTile>();

        /// <summary>The most tiles of one special kind: 10% of the grid, rounded down.</summary>
        public static int MaxSpecialFor(int width, int height) => Mathf.FloorToInt(width * height * .1f);

        public void Build(int width, int height, bool showRail, int pillars, int vaults, int gaps, int broken, Vector2Int keepClear)
        {
            Width = width;
            Height = height;
            HasRail = showRail;
            BuildRing();
            PickSpecialTiles(pillars, vaults, gaps, broken, keepClear);

            var frame = Prims.NewSprite("Frame", Prims.Square, Palette.Frame, transform, -10);
            frame.transform.localScale = new Vector3(width + .4f, height + .4f, 1f);

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var c = new Vector2Int(x, y);
                switch (Kind(c))
                {
                    case TileKind.Pillar:
                        // A raised block: drop shadow + solid top with a lit edge.
                        Tile(c, $"Pillar {x},{y}", Palette.PillarShadow, -5, .94f, new Vector3(.06f, -.08f));
                        Tile(c, "PillarTop", Palette.Pillar, -4, .82f, new Vector3(-.02f, .04f));
                        Tile(c, "PillarEdge", Palette.PillarEdge, -3, 1f, new Vector3(-.02f, .4f), new Vector3(.82f, .07f));
                        break;

                    case TileKind.Vault:
                        // Floor with a low crate on it: you can't walk or dash through, but you can vault it.
                        Tile(c, $"Vault {x},{y}", (x + y) % 2 == 0 ? Palette.TileA : Palette.TileB, -5, .94f, Vector3.zero);
                        Tile(c, "Crate", Palette.Vault, -4, .7f, Vector3.zero);
                        Tile(c, "CrateInner", Palette.VaultDark, -3, .46f, Vector3.zero);
                        Tile(c, "CrateTop", Palette.VaultEdge, -2, 1f, new Vector3(0f, .31f), new Vector3(.7f, .08f));
                        break;

                    case TileKind.Gap:
                        // A hole in the floor: dark pit with a faint rim. Dash over it; walk in and you fall.
                        Tile(c, $"Gap {x},{y}", Palette.GapRim, -5, .94f, Vector3.zero);
                        Tile(c, "Pit", Palette.Gap, -4, .8f, Vector3.zero);
                        break;

                    case TileKind.Broken:
                    {
                        var bt = new GameObject($"Broken {x},{y}").AddComponent<BreakableTile>();
                        bt.transform.SetParent(transform, false);
                        bt.transform.position = ToWorld(c);
                        bt.Init((x + y) % 2 == 0 ? Palette.TileA : Palette.TileB);
                        breakables[c] = bt;
                        break;
                    }

                    default:
                        Tile(c, $"Tile {x},{y}", (x + y) % 2 == 0 ? Palette.TileA : Palette.TileB, -5, .94f, Vector3.zero);
                        break;
                }
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

        void Tile(Vector2Int c, string name, Color color, int order, float scale, Vector3 offset, Vector3? size = null)
        {
            var sr = Prims.NewSprite(name, Prims.Square, color, transform, order, scale);
            sr.transform.position = ToWorld(c) + offset;
            if (size.HasValue) sr.transform.localScale = new Vector3(size.Value.x, size.Value.y, 1f);
        }

        /// <summary>What the tile is right now. A broken tile that's currently down reports as Gap.</summary>
        public TileKind Kind(Vector2Int c)
        {
            if (!special.TryGetValue(c, out var k)) return TileKind.Floor;
            if (k == TileKind.Broken && breakables.TryGetValue(c, out var b) && b.IsDown) return TileKind.Gap;
            return k;
        }

        /// <summary>On the grid and plain floor (not a pillar, vault, gap or breakable tile). Enemies and coins use only these.</summary>
        public bool IsWalkable(Vector2Int c) => InBounds(c) && !special.ContainsKey(c);

        /// <summary>McBilly can stand here right now: plain floor, or a breakable tile that hasn't broken yet.</summary>
        public bool IsSolid(Vector2Int c)
        {
            if (!InBounds(c)) return false;
            var k = Kind(c);
            return k == TileKind.Floor || k == TileKind.Broken;
        }

        /// <summary>McBilly is standing here: if it's an intact breakable tile, start it cracking.</summary>
        public void Step(Vector2Int c)
        {
            if (breakables.TryGetValue(c, out var b)) b.Trigger();
        }

        /// <summary>Closest plain floor tile with no enemy on it (used to climb out after a fall).</summary>
        public Vector2Int NearestSafeCell(Vector2Int from)
        {
            var seen = new HashSet<Vector2Int> { from };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                if (Kind(c) == TileKind.Floor && InBounds(c) && EnemyAt(c) == null) return c;
                for (int i = 0; i < 8; i++)
                {
                    var n = c + Dir8.Vec(i);
                    if (InBounds(n) && seen.Add(n)) queue.Enqueue(n);
                }
            }
            return from;
        }

        /// <summary>
        /// Randomly places pillars, vaults, gaps, then breakable tiles (each capped at 10% of the grid), never on
        /// the player's start tile or its neighbours, and never in a way that cuts the plain floor
        /// into separate islands, so every floor tile can always be reached on foot.
        /// </summary>
        void PickSpecialTiles(int pillars, int vaults, int gaps, int broken, Vector2Int keepClear)
        {
            special.Clear();
            breakables.Clear();
            int cap = MaxSpecialFor(Width, Height);

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

            int next = 0;
            Place(TileKind.Pillar, Mathf.Clamp(pillars, 0, cap));
            Place(TileKind.Vault, Mathf.Clamp(vaults, 0, cap));
            Place(TileKind.Gap, Mathf.Clamp(gaps, 0, cap));
            Place(TileKind.Broken, Mathf.Clamp(broken, 0, cap));

            void Place(TileKind kind, int count)
            {
                int placed = 0;
                while (placed < count && next < candidates.Count)
                {
                    var c = candidates[next++];
                    special[c] = kind;
                    if (FloorIsConnected(keepClear)) placed++;
                    else special.Remove(c);
                }
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
            return seen.Count == Width * Height - special.Count;
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

    /// <summary>
    /// A breakable floor tile. Intact: walkable, with cracks drawn on it. Stepped on: blinks for
    /// McBillyGame.brokenCrackTime seconds (blinking faster toward the end). Then it breaks and acts
    /// as a gap for McBillyGame.brokenDownTime seconds, then pops back to intact.
    /// </summary>
    public class BreakableTile : MonoBehaviour
    {
        enum State { Intact, Cracking, Down }

        State state;
        float t;
        Color floorColor;
        SpriteRenderer floor, rim, pit;
        SpriteRenderer[] cracks;

        public bool IsDown => state == State.Down;

        public void Init(Color floorColor)
        {
            this.floorColor = floorColor;
            floor = Prims.NewSprite("Floor", Prims.Square, floorColor, transform, -5, .94f);
            cracks = new SpriteRenderer[3];
            float[] angles = { 35f, -50f, 110f };
            Vector3[] offsets = { new Vector3(-.12f, .1f), new Vector3(.12f, -.06f), new Vector3(.02f, -.2f) };
            for (int i = 0; i < cracks.Length; i++)
            {
                cracks[i] = Prims.NewSprite("Crack", Prims.Square, Palette.Crack, transform, -4);
                cracks[i].transform.localPosition = offsets[i];
                cracks[i].transform.localRotation = Quaternion.Euler(0, 0, angles[i]);
                cracks[i].transform.localScale = new Vector3(.38f, .035f, 1f);
            }
            rim = Prims.NewSprite("Rim", Prims.Square, Palette.GapRim, transform, -5, .94f);
            pit = Prims.NewSprite("Pit", Prims.Square, Palette.Gap, transform, -4, .8f);
            ShowIntact();
        }

        /// <summary>Start cracking (only from intact).</summary>
        public void Trigger()
        {
            if (state != State.Intact) return;
            state = State.Cracking;
            t = 0f;
        }

        void Update()
        {
            var g = McBillyGame.I;
            if (g == null) return;
            float dt = Time.deltaTime;

            switch (state)
            {
                case State.Cracking:
                {
                    t += dt;
                    float crack = Mathf.Max(.05f, g.brokenCrackTime);
                    float k = Mathf.Clamp01(t / crack);
                    // Blink, speeding up as it's about to go.
                    float freq = Mathf.Lerp(4f, 14f, k);
                    bool lit = Mathf.Repeat(t * freq, 1f) < .5f;
                    floor.color = lit ? Color.Lerp(floorColor, Palette.Warning, .55f) : floorColor;
                    foreach (var c in cracks) c.color = lit ? Color.white.WithAlpha(.6f) : Palette.Crack;
                    floor.transform.localPosition = (Vector3)(Random.insideUnitCircle * .025f * k); // tremble
                    if (t >= crack) Break();
                    break;
                }
                case State.Down:
                    t += dt;
                    if (t >= Mathf.Max(.05f, g.brokenDownTime)) Restore();
                    break;
            }
        }

        void Break()
        {
            state = State.Down;
            t = 0f;
            floor.enabled = false;
            foreach (var c in cracks) c.enabled = false;
            rim.enabled = pit.enabled = true;
            // Debris falling in.
            for (int i = 0; i < 5; i++)
            {
                Vector3 p = transform.position + (Vector3)(Random.insideUnitCircle * .3f);
                FadeFx.Spawn(Prims.Square, floorColor, p, Random.Range(0f, 90f),
                    Vector3.one * Random.Range(.12f, .22f), Vector3.zero, Random.Range(.25f, .4f), -3);
            }
        }

        void Restore()
        {
            state = State.Intact;
            t = 0f;
            ShowIntact();
            FadeFx.Spawn(Prims.Square, Color.white.WithAlpha(.35f), transform.position, 0f,
                Vector3.one * .94f, Vector3.one * 1.1f, .2f, -3);
        }

        void ShowIntact()
        {
            floor.enabled = true;
            floor.color = floorColor;
            floor.transform.localPosition = Vector3.zero;
            foreach (var c in cracks) { c.enabled = true; c.color = Palette.Crack; }
            rim.enabled = pit.enabled = false;
        }
    }
}
