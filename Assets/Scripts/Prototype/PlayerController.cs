using UnityEngine;

namespace McBilly.Proto
{
    /// <summary>
    /// McBilly. Design rules for feel:
    ///  - Logic is instant, visuals catch up. A press changes Cell on that frame (as long as the
    ///    move-speed cap allows); the slide between tiles is cosmetic and never blocks input.
    ///    A press that arrives too early is held and fires the moment the cap allows it.
    ///  - Turning follows Rotation Speed. Parry checks the direction McBilly is actually facing,
    ///    so what you see is what counts.
    ///  - Every button press is buffered briefly, so presses during hitstop / dash aren't lost.
    ///  - A short "late parry" grace converts a hit into a parry if you were a few ms late.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        public Vector2Int Cell { get; private set; }
        public int Facing { get; private set; }
        public int HP { get; private set; }
        public int MaxHP { get; private set; }
        /// <summary>Attacks pass through: dodge i-frames, or mid-fall into a gap.</summary>
        public bool IsDodging => iframeT > 0f || falling;
        public bool IsHurtInvulnerable => hurtT > 0f;
        public bool ParryActive => parryActiveT > 0f;
        public Vector3 CellWorld => G.Board.ToWorld(Cell);

        McBillyGame G => McBillyGame.I;
        PlayerTuning T => McBillyGame.I.player;

        readonly InputReader input = new InputReader();

        Transform visual;
        SpriteRenderer body, facingTile, shield, dodgeBarBg, dodgeBarFill;

        // visual slide
        Vector3 visFrom, visTo;
        float visT = 1f, visDur = .05f;
        Vector2 bump;
        float facePop;
        float aimAngle = 90f; // smooth, what you see

        // timers
        float nextStepT, dashLockT;
        float parryActiveT, parryCooldownT, shieldFlashT;
        float iframeT, hurtT, dodgeCooldownT, dodgeReadyFlashT;
        float parryBufferT, dodgeBufferT, vaultBufferT, shootBufferT, shotCooldownT;
        float vaultT = 1f, vaultDur = 1f; // hop animation
        bool falling;
        float fallT;
        Vector2Int fallReturnCell;
        bool movePressBuffered;

        // late-parry grace
        bool pendingHit;
        float pendingT;
        Enemy pendingSource;
        Vector2 pendingFrom;

        public void Init(Vector2Int cell, int hp)
        {
            Cell = cell;
            HP = MaxHP = hp;
            Facing = 2; // up
            transform.position = visFrom = visTo = CellWorld;

            visual = new GameObject("Visual").transform;
            visual.SetParent(transform, false);
            facingTile = Prims.NewSprite("FacingTile", Prims.Square, Palette.FacingTile, transform, 1, .94f);
            body = Prims.NewSprite("Body", Prims.Triangle, Palette.Player, visual, 15, .9f);
            shield = Prims.NewSprite("ParryShield", Prims.Square, Palette.Shield, visual, 16);
            shield.enabled = false;

            // Dodge recharge bar under McBilly (only visible while recharging).
            dodgeBarBg = Prims.NewSprite("DodgeBarBg", Prims.Square, Palette.BarBg, transform, 20);
            dodgeBarBg.transform.localPosition = new Vector3(0f, -.52f, 0f);
            dodgeBarBg.transform.localScale = new Vector3(.7f, .09f, 1f);
            dodgeBarFill = Prims.NewSprite("DodgeBarFill", Prims.Square, Palette.Player, transform, 21);
            dodgeBarBg.enabled = dodgeBarFill.enabled = false;
        }

        void Update()
        {
            float dt = Time.deltaTime; // 0 during hitstop: timers freeze, input still gets read & buffered
            input.allowDiagonalMove = T.allowDiagonalMovement;
            input.Tick(CellWorld, G.Cam);

            if (G.State == McBillyGame.GameState.Playing)
            {
                TickTimers(dt);
                HandleLook(dt);
                if (!falling)
                {
                    if (G.Board.Kind(Cell) == TileKind.Gap) FallInto(Cell);   // the tile broke under us
                    else G.Board.Step(Cell);                                  // start cracking a breakable tile
                }
                if (falling) UpdateFall(dt);
                else
                {
                    HandleActions(dt);
                    HandleMovement(dt);
                }
            }
            UpdateVisuals(dt);
        }

        void TickTimers(float dt)
        {
            dashLockT -= dt;
            parryActiveT -= dt;
            parryCooldownT -= dt;
            shieldFlashT -= dt;
            iframeT -= dt;
            hurtT -= dt;
            nextStepT -= dt;
            if (dodgeCooldownT > 0f)
            {
                dodgeCooldownT -= dt;
                if (dodgeCooldownT <= 0f) dodgeReadyFlashT = .15f;
            }
            dodgeReadyFlashT -= dt;

            // Space is contextual: vault if facing a vaultable crate, otherwise parry. (A) only vaults.
            bool parryPress = input.ParryPressed;
            if (input.SpacePressed || input.VaultPressed)
            {
                if (!falling && CanVault(out _)) vaultBufferT = T.inputBuffer;
                else if (input.SpacePressed) parryPress = true;
            }
            else vaultBufferT -= dt;

            if (parryPress) parryBufferT = T.inputBuffer; else parryBufferT -= dt;
            if (input.ShootPressed) shootBufferT = T.inputBuffer; else shootBufferT -= dt;
            shotCooldownT -= dt;
            // Dodge presses during the cooldown are ignored (not queued), unless they land in the
            // last few ms of it, so mashing can't bypass the cooldown but a well-timed press still counts.
            if (input.DodgePressed && dodgeCooldownT <= T.inputBuffer) dodgeBufferT = T.inputBuffer;
            else dodgeBufferT -= dt;
        }

        void HandleLook(float dt)
        {
            float target = Dir8.Angle(input.Look);
            aimAngle = T.rotationSpeed <= 0f
                ? target
                : Mathf.MoveTowardsAngle(aimAngle, target, T.rotationSpeed * dt);
            aimAngle = Mathf.Repeat(aimAngle, 360f);

            int facing = Dir8.FromVector(new Vector2(Mathf.Cos(aimAngle * Mathf.Deg2Rad), Mathf.Sin(aimAngle * Mathf.Deg2Rad)));
            if (facing == Facing) return;
            Facing = facing;
            facePop = 1f;
        }

        void HandleActions(float dt)
        {
            // A hit that landed a few ms before a parry/dodge press can still be saved.
            if (pendingHit)
            {
                if (parryBufferT > 0f && parryCooldownT <= 0f && FacingOk(pendingFrom))
                {
                    parryBufferT = 0f;
                    pendingHit = false;
                    StartParry();
                    TryParry(pendingFrom);
                    G.OnParry(pendingSource);
                }
                else if (dodgeBufferT > 0f && dodgeCooldownT <= 0f)
                {
                    dodgeBufferT = 0f;
                    pendingHit = false;
                    G.CreditPerfectDodge(pendingSource);
                    Dodge();
                }
                else
                {
                    pendingT -= dt;
                    if (pendingT <= 0f)
                    {
                        pendingHit = false;
                        TakeHit(pendingFrom);
                    }
                }
            }

            if (vaultBufferT > 0f && dashLockT <= 0f && CanVault(out var crate))
            {
                vaultBufferT = 0f;
                Vault(crate);
            }

            if (dodgeBufferT > 0f && dodgeCooldownT <= 0f)
            {
                dodgeBufferT = 0f;
                Dodge();
            }

            if (parryBufferT > 0f && parryCooldownT <= 0f)
            {
                parryBufferT = 0f;
                StartParry();
            }

            bool wantShot = shootBufferT > 0f || (T.holdToAutoFire && input.ShootHeld);
            if (wantShot && shotCooldownT <= 0f)
            {
                shootBufferT = 0f;
                Shoot();
            }
        }

        float StepInterval => 1f / Mathf.Max(.5f, T.moveSpeed);

        void HandleMovement(float dt)
        {
            if (input.MovePressed) movePressBuffered = true;
            if (dashLockT > 0f) return;

            Vector2Int dir = input.Move;
            if (dir == Vector2Int.zero) { movePressBuffered = false; return; }
            if (nextStepT > 0f) return; // move-speed cap; a fresh press stays buffered until it's allowed

            if (movePressBuffered)
            {
                movePressBuffered = false;
                TryStep(dir);
                nextStepT = StepInterval + T.holdRepeatDelay; // tap = one tile; hold a bit longer to run
            }
            else
            {
                TryStep(dir);
                // carry the frame's overshoot so holding moves at exactly moveSpeed tiles/sec
                nextStepT = Mathf.Max(nextStepT, -StepInterval) + StepInterval;
            }
        }

        bool TryStep(Vector2Int dir)
        {
            var target = Cell + dir;
            var kind = G.Board.Kind(target);
            if (!G.Board.InBounds(target) || G.Board.EnemyAt(target) != null
                || kind == TileKind.Pillar || kind == TileKind.Vault)
            {
                bump = (Vector2)dir * .16f; // blocked: a little nudge so the input still "registers"
                return false;
            }
            if (kind == TileKind.Gap)
            {
                FallInto(target);
                return true;
            }
            Cell = target;
            G.TryCollectCoin(Cell);
            SlideTo(StepInterval * T.slideFraction);
            return true;
        }

        void SlideTo(float duration)
        {
            visFrom = transform.position;
            visTo = CellWorld;
            visT = 0f;
            visDur = Mathf.Max(.001f, duration);
        }

        // ---------------- Parry ----------------

        void StartParry()
        {
            parryActiveT = T.parryWindow;
            parryCooldownT = T.parryWindow + T.parryWhiffRecovery;
        }

        bool FacingOk(Vector2 towardThreat) =>
            Dir8.Steps(Facing, Dir8.FromVector(towardThreat)) <= T.parryFacingTolerance;

        /// <summary>Called by the game when an attack reaches the player. towardThreat points from McBilly to the attack's origin.</summary>
        public bool TryParry(Vector2 towardThreat)
        {
            if (parryActiveT <= 0f || !FacingOk(towardThreat)) return false;
            parryCooldownT = 0f;   // success refunds the recovery: chained parries feel great
            shieldFlashT = .12f;
            return true;
        }

        // ---------------- Dodge ----------------

        void Dodge()
        {
            Vector2Int dir = input.Move != Vector2Int.zero ? input.Move : Dir8.Vec(Facing); // no move input = dodge where you're looking (diagonals included)
            var origin = Cell;
            Vector3 originWorld = CellWorld;

            G.CheckPerfectDodge(origin);

            // Walls, pillars, vault crates and enemies stop the dash. Gaps are flown over, but the dash
            // only ever lands on solid floor: if it would end in a gap it stops at the last floor tile.
            var dest = Cell;
            var probe = Cell;
            for (int i = 0; i < T.dodgeDistance; i++)
            {
                var next = probe + dir;
                if (!G.Board.InBounds(next) || G.Board.EnemyAt(next) != null) break;
                var kind = G.Board.Kind(next);
                if (kind == TileKind.Pillar || kind == TileKind.Vault) break;
                probe = next;
                if (kind == TileKind.Floor || kind == TileKind.Broken)
                {
                    dest = probe;
                    G.TryCollectCoin(dest); // dashing over a coin grabs it
                }
            }

            Cell = dest;
            SlideTo(T.dodgeVisualTime);
            dashLockT = T.dodgeVisualTime;
            iframeT = T.dodgeInvulnerability;
            G.OnPlayerDodged();
            dodgeCooldownT = T.dodgeCooldownTime;
            dodgeReadyFlashT = 0f;
            movePressBuffered = false;
            nextStepT = 0f;

            // afterimages along the path
            Vector3 destWorld = CellWorld;
            for (int i = 0; i < 3; i++)
            {
                Vector3 p = Vector3.Lerp(originWorld, destWorld, i / 3f);
                FadeFx.Spawn(Prims.Triangle, Palette.Player.WithAlpha(.35f - i * .08f), p, aimAngle,
                    Vector3.one * .9f, Vector3.one * .7f, .18f, 12);
            }
        }

        // ---------------- Shooting ----------------

        void Shoot()
        {
            shotCooldownT = T.shotCooldown;
            Vector2 dir = Dir8.Unit(Facing);
            Vector3 muzzle = CellWorld + (Vector3)(dir * .45f);
            PlayerShot.Spawn(muzzle, Facing, T.shotSpeed, T.shotPatienceDamage);
            FadeFx.Spawn(Prims.Ring, Palette.PlayerShot.WithAlpha(.7f), muzzle, 0f, Vector3.one * .15f, Vector3.one * .45f, .1f, 32);
            bump -= dir * .07f; // tiny recoil
        }

        // ---------------- Vault ----------------

        /// <summary>
        /// Facing a vault crate with solid, empty floor right behind it?
        /// Works in all 8 directions (it's the tile McBilly is looking at).
        /// </summary>
        public bool CanVault(out Vector2Int crate)
        {
            var dir = Dir8.Vec(Facing);
            crate = Cell + dir;
            if (falling || G.Board.Kind(crate) != TileKind.Vault || !G.Board.InBounds(crate)) return false;
            var land = crate + dir;
            return G.Board.IsSolid(land) && G.Board.EnemyAt(land) == null;
        }

        void Vault(Vector2Int crate)
        {
            Cell = crate + Dir8.Vec(Facing);
            SlideTo(T.vaultTime);
            dashLockT = T.vaultTime;
            vaultT = 0f;
            vaultDur = T.vaultTime;
            if (T.vaultInvulnerable) iframeT = Mathf.Max(iframeT, T.vaultTime);
            nextStepT = 0f;
            movePressBuffered = false;
            G.TryCollectCoin(Cell);
            FadeFx.Spawn(Prims.Ring, Palette.VaultEdge.WithAlpha(.6f), G.Board.ToWorld(crate), 0f,
                Vector3.one * .4f, Vector3.one * 1f, .2f, 30);
        }

        // ---------------- Gaps ----------------

        void FallInto(Vector2Int gap)
        {
            fallReturnCell = Cell;
            Cell = gap;
            SlideTo(StepInterval * T.slideFraction);
            falling = true;
            fallT = 0f;
            pendingHit = false;
            parryActiveT = 0f;
        }

        void UpdateFall(float dt)
        {
            fallT += dt;
            if (fallT < T.fallTime) return;

            // Climb back out where we stepped from, hurt.
            falling = false;
            // Back where we came from, unless that's not solid floor any more (e.g. it was a breakable tile that broke).
            Cell = G.Board.Kind(fallReturnCell) == TileKind.Floor && G.Board.EnemyAt(fallReturnCell) == null
                ? fallReturnCell
                : G.Board.NearestSafeCell(fallReturnCell);
            transform.position = visFrom = visTo = CellWorld;
            visT = 1f;
            facePop = 1f;
            nextStepT = StepInterval;
            movePressBuffered = false;
            if (T.fallDamage > 0)
            {
                HP = Mathf.Max(0, HP - T.fallDamage);
                hurtT = T.hurtInvulnerability;
                G.OnPlayerHurt("FELL!");
            }
        }

        // ---------------- Damage ----------------

        public void QueueHit(Enemy source, Vector2 towardThreat)
        {
            if (T.lateParryGrace <= 0f) { TakeHit(towardThreat); return; }
            if (pendingHit) TakeHit(pendingFrom);
            pendingHit = true;
            pendingT = T.lateParryGrace;
            pendingSource = source;
            pendingFrom = towardThreat;
        }

        void TakeHit(Vector2 towardThreat)
        {
            if (iframeT > 0f || hurtT > 0f) return;
            HP--;
            hurtT = T.hurtInvulnerability;
            bump = -towardThreat.normalized * .25f;
            G.OnPlayerHurt();
        }

        // ---------------- Visuals ----------------

        void UpdateVisuals(float dt)
        {
            visT = Mathf.Min(1f, visT + dt / visDur);
            bump = Vector2.Lerp(bump, Vector2.zero, 1f - Mathf.Exp(-28f * dt));
            transform.position = Vector3.LerpUnclamped(visFrom, visTo, Ease.OutCubic(visT)) + (Vector3)bump;

            // Facing: turns at Rotation Speed, with a quick pop each time it crosses into a new direction.
            facePop = Mathf.Max(0f, facePop - dt / .08f);
            visual.localRotation = Quaternion.Euler(0f, 0f, aimAngle);
            float scale = 1f + .14f * facePop;

            // Vault hop: grow and lift at the top of the arc.
            if (vaultT < vaultDur)
            {
                vaultT += dt;
                float hop = Mathf.Sin(Mathf.Clamp01(vaultT / vaultDur) * Mathf.PI);
                scale *= 1f + .4f * hop;
                visual.localPosition = new Vector3(0f, .3f * hop, 0f);
            }
            else visual.localPosition = Vector3.zero;

            // Falling: shrink and spin down the hole.
            if (falling)
            {
                float k = Mathf.Clamp01(fallT / T.fallTime);
                scale *= 1f - k;
                visual.localRotation = Quaternion.Euler(0f, 0f, aimAngle + k * 540f);
            }
            visual.localScale = Vector3.one * scale;

            // Tile you're facing (where parries count from).
            facingTile.transform.position = CellWorld + (Vector3)(Vector2)Dir8.Vec(Facing);
            var front = Cell + Dir8.Vec(Facing);
            bool canVault = CanVault(out _);
            facingTile.enabled = !falling && (G.Board.IsSolid(front) || canVault);
            facingTile.color = canVault ? Palette.VaultPrompt : Palette.FacingTile;

            // Shield bar in front while the parry window is open.
            bool showShield = parryActiveT > 0f || shieldFlashT > 0f;
            shield.enabled = showShield;
            if (showShield)
            {
                bool flash = shieldFlashT > 0f;
                shield.transform.localPosition = new Vector3(.58f, 0f, 0f); // local +X = facing
                shield.transform.localScale = flash ? new Vector3(.2f, 1.15f, 1f) : new Vector3(.12f, .85f, 1f);
                shield.color = flash ? Color.white : Palette.Shield.WithAlpha(.85f);
            }

            // Dodge recharge bar
            bool recharging = dodgeCooldownT > 0f && T.dodgeCooldownTime > 0f;
            dodgeBarBg.enabled = dodgeBarFill.enabled = recharging || dodgeReadyFlashT > 0f;
            if (dodgeBarFill.enabled)
            {
                float k = recharging ? 1f - Mathf.Clamp01(dodgeCooldownT / T.dodgeCooldownTime) : 1f;
                dodgeBarFill.transform.localScale = new Vector3(.66f * k, .05f, 1f);
                dodgeBarFill.transform.localPosition = new Vector3(-.33f * (1f - k), -.52f, 0f);
                dodgeBarFill.color = recharging ? Palette.Player.WithAlpha(.6f) : Color.white;
            }

            Color c = Palette.Player;
            if (iframeT > 0f) c = Palette.Dodge;
            else if (parryActiveT > 0f) c = Color.Lerp(Palette.Player, Color.white, .6f);
            if (hurtT > 0f && Mathf.Repeat(Time.time, .1f) < .05f) c.a = .25f;
            if (G.State == McBillyGame.GameState.GameOver) c = Palette.Player.WithAlpha(.3f);
            body.color = c;
        }
    }
}
