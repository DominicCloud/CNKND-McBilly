using System.Collections.Generic;
using UnityEngine;

namespace McBilly.Proto
{
    /// <summary>
    /// SHOOTER (orange circle). Lines up with the player on a row, column or diagonal,
    /// shows a dotted aim line, then fires a projectile down it.
    /// Answer: step out of the lane, parry while facing it, or dodge through it.
    /// On the rail: slides along the rail to find a lane instead.
    /// </summary>
    public class ShooterEnemy : Enemy
    {
        enum State { Idle, Windup }
        State state;
        float timer;
        int aimDir;
        readonly List<GameObject> aimDots = new List<GameObject>();

        protected override float PoseScale => state == State.Windup ? 1f + .25f * Charge : 1f;
        protected override float Charge => state == State.Windup ? 1f - Mathf.Clamp01(timer / G.enemy.shooterWindup) : 0f;

        protected override void OnInit() => timer = Random.Range(.2f, 1f);

        protected override void Think(float dt)
        {
            timer -= dt;
            if (timer > 0f) return;

            if (state == State.Idle)
            {
                if (OnRail)
                {
                    // Slide along the rail until we have a clean lane into the grid.
                    if (!CanFireFrom(Cell))
                    {
                        var spot = BestRailSpot();
                        if (spot != Cell) TryStep(Board.RingStep(Cell, spot));
                        timer = G.enemy.railStepInterval;
                        return;
                    }
                }
                else if (!Aligned(Cell) && TryAlign()) { timer = .3f; return; }
                StartWindup();
            }
            else Fire();
        }

        /// <summary>On the rail: lined up with the player, not point-blank, and the shot enters the grid.</summary>
        bool CanFireFrom(Vector2Int c)
        {
            if (!Aligned(c) || Board.Chebyshev(c, OldPlayer.Cell) < 2) return false;
            return Board.InBounds(c + Dir8.Vec(Dir8.FromVector((Vector2)(OldPlayer.Cell - c))));
        }

        Vector2Int BestRailSpot()
        {
            Vector2Int best = Cell;
            int bestDist = int.MaxValue;
            foreach (var r in Board.Ring)
            {
                if (r != Cell && Board.EnemyAt(r) != null) continue;
                if (!CanFireFrom(r)) continue;
                int d = Mathf.Abs(Board.RingDelta(Cell, r));
                if (d < bestDist) { bestDist = d; best = r; }
            }
            return best;
        }

        bool Aligned(Vector2Int c)
        {
            Vector2Int d = OldPlayer.Cell - c;
            return d.x == 0 || d.y == 0 || Mathf.Abs(d.x) == Mathf.Abs(d.y);
        }

        bool TryAlign()
        {
            // Look one step in every direction for a cell that lines up with the player.
            int start = Random.Range(0, 8);
            for (int i = 0; i < 8; i++)
            {
                var c = Cell + Dir8.Vec(start + i);
                if (Board.IsFree(c, OnRail) && Aligned(c) && Board.Chebyshev(c, OldPlayer.Cell) >= 2) return TryStep(c);
            }
            return false;
        }

        void StartWindup()
        {
            state = State.Windup;
            timer = G.enemy.shooterWindup;
            aimDir = Dir8.FromVector((Vector2)(OldPlayer.Cell - Cell));

            var step = Dir8.Vec(aimDir);
            for (var c = Cell + step; Board.InBounds(c); c += step)
            {
                var dot = Prims.NewSprite("Aim", Prims.Diamond, Palette.Projectile.WithAlpha(.45f), G.Root, 2, .16f);
                dot.transform.position = Board.ToWorld(c);
                aimDots.Add(dot.gameObject);
            }
        }

        void Fire()
        {
            ClearAim();
            Projectile.Spawn(this, World + (Vector3)(Dir8.Unit(aimDir) * .45f), aimDir, G.enemy.projectileSpeed);
            FadeFx.Spawn(Prims.Ring, Palette.Projectile, World, 0f, Vector3.one * .5f, Vector3.one * 1.1f, .15f);
            state = State.Idle;
            timer = Random.Range(G.enemy.shooterCooldown.x, G.enemy.shooterCooldown.y);
        }

        void ClearAim()
        {
            foreach (var d in aimDots) if (d != null) Destroy(d);
            aimDots.Clear();
        }

        protected override void OnLeave() => ClearAim();
        void OnDestroy() => ClearAim();
    }

    /// <summary>
    /// BRUTE (purple square). Walks toward the player; when adjacent it telegraphs a slam
    /// on all 8 surrounding tiles. Answer: get out (dodge is 2 tiles), or parry facing it.
    /// On the rail: follows you along the rail and slams the grid tiles within reach (default 2).
    /// </summary>
    public class BruteEnemy : Enemy
    {
        enum State { Approach, Windup, Recover }
        State state;
        float timer;
        readonly List<TileWarning> warnings = new List<TileWarning>();

        protected override float PoseScale => state == State.Windup ? 1f + .3f * Charge : state == State.Recover ? .9f : 1f;
        protected override float Charge => state == State.Windup ? 1f - Mathf.Clamp01(timer / G.enemy.bruteWindup) : 0f;

        protected override void Think(float dt)
        {
            timer -= dt;
            if (timer > 0f) return;

            switch (state)
            {
                case State.Approach:
                    if (Board.Chebyshev(Cell, OldPlayer.Cell) <= Reach) StartWindup();
                    else if (OnRail)
                    {
                        var t = NearestRailCellTo(OldPlayer.Cell);
                        if (t != Cell) TryStep(Board.RingStep(Cell, t));
                        timer = G.enemy.bruteRailStepInterval;
                    }
                    else { StepToward(); timer = G.enemy.bruteStepInterval; }
                    break;
                case State.Windup:
                    Slam();
                    break;
                case State.Recover:
                    state = State.Approach;
                    break;
            }
        }

        /// <summary>Inside the grid it slams the 8 tiles around it; from the rail it reaches further in.</summary>
        int Reach => OnRail ? Mathf.Max(1, G.enemy.bruteReachFromRail) : 1;

        Vector2Int NearestRailCellTo(Vector2Int p)
        {
            Vector2Int[] candidates =
            {
                new Vector2Int(-1, p.y), new Vector2Int(Board.Width, p.y),
                new Vector2Int(p.x, -1), new Vector2Int(p.x, Board.Height)
            };
            Vector2Int best = Cell;
            float bestD = float.MaxValue;
            foreach (var c in candidates)
            {
                float d = (c - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = c; }
            }
            return best;
        }

        void StepToward()
        {
            Vector2Int d = OldPlayer.Cell - Cell;
            var h = new Vector2Int(System.Math.Sign(d.x), 0);
            var v = new Vector2Int(0, System.Math.Sign(d.y));
            bool horizontalFirst = Mathf.Abs(d.x) > Mathf.Abs(d.y) || (Mathf.Abs(d.x) == Mathf.Abs(d.y) && Random.value < .5f);
            var first = horizontalFirst ? h : v;
            var second = horizontalFirst ? v : h;
            if (first != Vector2Int.zero && TryStep(Cell + first)) return;
            if (second != Vector2Int.zero) TryStep(Cell + second);
        }

        void StartWindup()
        {
            state = State.Windup;
            timer = G.enemy.bruteWindup;
            int r = Reach;
            for (int dx = -r; dx <= r; dx++)
            for (int dy = -r; dy <= r; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                var c = Cell + new Vector2Int(dx, dy);
                if (Board.InBounds(c)) warnings.Add(TileWarning.Create(this, c, G.enemy.bruteWindup));
            }
        }

        void Slam()
        {
            bool hit = false;
            foreach (var w in warnings) if (w != null && w.Cell == OldPlayer.Cell) hit = true;
            ClearWarnings();

            FadeFx.Spawn(Prims.Square, Palette.Brute.WithAlpha(.5f), World, 0f, Vector3.one, Vector3.one * (2 * Reach + 1), .2f, 3);
            G.Shake(.06f, .08f);
            if (hit) G.ResolveDirectHit(this);

            state = State.Recover;
            timer = G.enemy.bruteRecovery;
        }

        void ClearWarnings()
        {
            foreach (var w in warnings) if (w != null) Destroy(w.gameObject);
            warnings.Clear();
        }

        protected override void OnLeave() => ClearWarnings();
        void OnDestroy() => ClearWarnings();
    }

    /// <summary>
    /// CASTER (green diamond). Blinks into the player's row or column and telegraphs a beam
    /// along the whole line toward them. Long warning, long reach.
    /// On the rail: blinks to the rail at the end of your row or column.
    /// </summary>
    public class CasterEnemy : Enemy
    {
        enum State { Idle, Windup }
        State state;
        float timer;
        readonly List<TileWarning> warnings = new List<TileWarning>();
        Vector2Int beamDir;

        protected override float PoseScale => state == State.Windup ? 1f + .2f * Charge : 1f;
        protected override float Charge => state == State.Windup ? 1f - Mathf.Clamp01(timer / G.enemy.casterWindup) : 0f;

        protected override void OnInit() => timer = Random.Range(.5f, 1.5f);

        protected override void Think(float dt)
        {
            timer -= dt;
            if (timer > 0f) return;

            if (state == State.Idle)
            {
                bool aligned = Cell.x == OldPlayer.Cell.x || Cell.y == OldPlayer.Cell.y;
                if (!aligned)
                {
                    timer = BlinkToLane() ? .35f : .5f;
                    return;
                }
                StartWindup();
            }
            else Fire();
        }

        bool BlinkToLane()
        {
            var options = new List<Vector2Int>();
            var p = OldPlayer.Cell;
            if (OnRail)
            {
                // The four rail cells at the ends of the player's row and column.
                Vector2Int[] ends =
                {
                    new Vector2Int(-1, p.y), new Vector2Int(Board.Width, p.y),
                    new Vector2Int(p.x, -1), new Vector2Int(p.x, Board.Height)
                };
                foreach (var c in ends) if (Board.IsFree(c, true)) options.Add(c);
                return options.Count > 0 && Blink(options[Random.Range(0, options.Count)]);
            }
            for (int x = 0; x < Board.Width; x++)
            {
                var c = new Vector2Int(x, p.y);
                if (Mathf.Abs(x - p.x) >= 3 && Board.IsFree(c, false)) options.Add(c);
            }
            for (int y = 0; y < Board.Height; y++)
            {
                var c = new Vector2Int(p.x, y);
                if (Mathf.Abs(y - p.y) >= 3 && Board.IsFree(c, false)) options.Add(c);
            }
            return options.Count > 0 && Blink(options[Random.Range(0, options.Count)]);
        }

        void StartWindup()
        {
            state = State.Windup;
            timer = G.enemy.casterWindup;
            Vector2Int d = OldPlayer.Cell - Cell;
            beamDir = new Vector2Int(System.Math.Sign(d.x), System.Math.Sign(d.y));
            for (var c = Cell + beamDir; Board.InBounds(c); c += beamDir)
                warnings.Add(TileWarning.Create(this, c, G.enemy.casterWindup));
        }

        void Fire()
        {
            bool hit = false;
            Vector3 last = World;
            foreach (var w in warnings)
            {
                if (w == null) continue;
                if (w.Cell == OldPlayer.Cell) hit = true;
                last = Board.ToWorld(w.Cell);
            }
            ClearWarnings();

            Vector3 mid = (World + last) * .5f;
            float len = Vector3.Distance(World, last) + 1f;
            float angle = Mathf.Atan2(beamDir.y, beamDir.x) * Mathf.Rad2Deg;
            FadeFx.Spawn(Prims.Square, Palette.Caster.WithAlpha(.9f), mid, angle,
                new Vector3(len, .55f, 1f), new Vector3(len, .05f, 1f), .22f);
            if (hit) G.ResolveDirectHit(this);

            state = State.Idle;
            timer = Random.Range(G.enemy.casterCooldown.x, G.enemy.casterCooldown.y);
        }

        void ClearWarnings()
        {
            foreach (var w in warnings) if (w != null) Destroy(w.gameObject);
            warnings.Clear();
        }

        protected override void OnLeave() => ClearWarnings();
        void OnDestroy() => ClearWarnings();
    }
}
