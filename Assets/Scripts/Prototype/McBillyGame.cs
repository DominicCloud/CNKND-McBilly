using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace McBilly.Proto
{
    [System.Serializable]
    public class PlayerTuning
    {
        [Header("Movement (4-way, grid)")]
        [Tooltip("Player movement speed in tiles per second. Caps both holding and tapping.")]
        [Min(.5f)] public float moveSpeed = 6f;
        [Tooltip("Part of each step spent sliding between tiles (cosmetic). 1 = continuous glide, lower = snappier hop.")]
        [Range(.1f, 1f)] public float slideFraction = .6f;
        [Tooltip("Extra pause after the first step before holding starts repeating, so a tap moves exactly one tile.")]
        [Min(0f)] public float holdRepeatDelay = .05f;
        [Tooltip("Special tiles / abilities in the design doc. Off by default.")]
        public bool allowDiagonalMovement = false;

        [Header("Turning")]
        [Tooltip("Player rotation speed in degrees per second (720 = a 180-degree turn in 0.25s). 0 = instant snap. Parry uses the direction McBilly is actually facing mid-turn.")]
        [Min(0f)] public float rotationSpeed = 720f;

        [Header("Parry")]
        [Tooltip("How long the parry is active after pressing.")]
        public float parryWindow = .16f;
        [Tooltip("Extra lockout after a parry that didn't catch anything (anti-mash). Refunded on success.")]
        public float parryWhiffRecovery = .18f;
        [Tooltip("0 = must face the attack exactly. 1 = the neighbouring 45-degree directions also count.")]
        [Range(0, 2)] public int parryFacingTolerance = 1;
        [Tooltip("If you get hit, a parry (facing correctly) or dodge pressed within this many seconds still saves you.")]
        public float lateParryGrace = .07f;

        [Header("Dodge")]
        public int dodgeDistance = 2;
        [Tooltip("Cosmetic dash time. Movement input is locked for this long.")]
        public float dodgeVisualTime = .08f;
        public float dodgeInvulnerability = .2f;
        [Tooltip("Seconds before you can dodge again. Presses during the cooldown are ignored, not queued. A bar under McBilly shows the recharge.")]
        [Min(0f)] public float dodgeCooldownTime = 1f;
        [Tooltip("Dodging out of a tile that's about to be hit within this many seconds counts as a perfect dodge.")]
        public float perfectDodgeWindow = .25f;
        [Tooltip("A projectile closer than this to your start tile (and heading at it) counts as a perfect dodge.")]
        public float perfectDodgeProjectileRange = 1.6f;

        [Header("Shooting")]
        [Tooltip("Seconds between shots (holding the button fires at this rate).")]
        [Min(.02f)] public float shotCooldown = .3f;
        [Tooltip("Shot speed in tiles per second.")]
        [Min(.5f)] public float shotSpeed = 12f;
        [Tooltip("Patience each shot takes off an enemy (a parry takes 12).")]
        [Min(0f)] public float shotPatienceDamage = 4f;
        [Tooltip("Hold the shoot button to keep firing at the cooldown rate.")]
        public bool holdToAutoFire = true;

        [Header("Vault & gaps")]
        [Tooltip("Seconds McBilly is in the air when vaulting a crate (movement is locked meanwhile).")]
        [Min(.02f)] public float vaultTime = .22f;
        [Tooltip("If on, McBilly can't be hit while in the air during a vault.")]
        public bool vaultInvulnerable = false;
        [Tooltip("Seconds of the falling animation after walking into a gap.")]
        [Min(.05f)] public float fallTime = .45f;
        [Tooltip("Health lost when falling into a gap.")]
        [Min(0)] public int fallDamage = 1;

        [Header("General")]
        [Tooltip("Presses are remembered this long, so inputs during hitstop / dash aren't dropped.")]
        public float inputBuffer = .12f;
        public int maxHealth = 5;
        public float hurtInvulnerability = .8f;
    }

    [System.Serializable]
    public class EnemyTuning
    {
        [Header("Patience")]
        public float patience = 100f;
        [Tooltip("Patience drained per second automatically (100 / 4 = leaves after 25s if ignored).")]
        public float patienceDrainPerSecond = 4f;
        public float parryPatienceDamage = 12f;
        public float perfectDodgePatienceDamage = 22f;

        [Header("Spawning")]
        public int startEnemies = 2;
        public int maxEnemies = 5;
        [Tooltip("One more enemy allowed on the board every N seconds.")]
        public float secondsPerExtraEnemy = 25f;
        public float spawnInterval = 1.2f;
        [Range(0, 1)] public float shooterWeight = .45f;
        [Range(0, 1)] public float bruteWeight = .35f;
        [Range(0, 1)] public float casterWeight = .2f;

        [Header("Shooter")]
        public float shooterWindup = .55f;
        public Vector2 shooterCooldown = new Vector2(1.6f, 2.8f);
        public float projectileSpeed = 8f;

        [Header("Brute")]
        public float bruteStepInterval = .75f;
        public float bruteWindup = .7f;
        public float bruteRecovery = .8f;

        [Header("Caster")]
        public float casterWindup = .85f;
        public Vector2 casterCooldown = new Vector2(2.2f, 3.4f);

        [Header("Enemies outside the grid (rail)")]
        [Tooltip("Seconds per rail tile when a shooter repositions.")]
        public float railStepInterval = .2f;
        [Tooltip("Seconds per rail tile when a brute follows you.")]
        public float bruteRailStepInterval = .32f;
        [Tooltip("How many tiles into the grid a brute's slam reaches from the rail.")]
        public int bruteReachFromRail = 2;

        [Header("Mixed placement: where each type may spawn")]
        [Tooltip("Orange.")] public SpawnZone shooterZone = SpawnZone.Either;
        [Tooltip("Purple.")] public SpawnZone bruteZone = SpawnZone.Either;
        [Tooltip("Green.")] public SpawnZone casterZone = SpawnZone.OffGrid;
        [Tooltip("For 'Either' types: chance to spawn on the rail rather than on the grid.")]
        [Range(0, 1)] public float offGridChanceForEither = .5f;
    }

    public enum SpawnZone { OnGrid, OffGrid, Either }

    /// <summary>
    /// Drop this on an empty GameObject in any scene and press Play. Builds the camera, board,
    /// player and enemy spawner at runtime. All tuning is live-editable in the Inspector during play.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class McBillyGame : MonoBehaviour
    {
        public static McBillyGame I { get; private set; }

        public enum GameState { Playing, GameOver }

        public enum EnemyPlacement { OnGrid, OffGrid, Mixed }

        [Header("Board")]
        [Tooltip("OnGrid: enemies stand on the grid.  OffGrid: enemies stand on a rail one tile outside the grid.  Mixed: each enemy type follows its Spawn Zone (Enemy > Mixed placement).")]
        public EnemyPlacement enemyPlacement = EnemyPlacement.OnGrid;
        [Tooltip("Grid size for On Grid placement.")]
        public int boardWidth = 11;
        public int boardHeight = 9;
        [Tooltip("Grid size for Off Grid placement (smaller: the rail adds a ring around it).")]
        public int outsideBoardWidth = 7;
        public int outsideBoardHeight = 7;
        [Tooltip("Grid size for Mixed placement.")]
        public int mixedBoardWidth = 9;
        public int mixedBoardHeight = 9;

        [Header("Special tiles (new random layout every restart)")]
        [Tooltip("PILLARS: hard obstacles. Nothing walks, dodges or vaults through. Capped in code at 10% of the grid's tiles.")]
        [FormerlySerializedAs("max_missing_tiles")]
        [Min(0)] public int max_pillar_tiles = 0;
        [Tooltip("VAULTS: low crates. Block walking and dodging, but Space (or A) while facing one vaults McBilly over it. Capped in code at 10% of the grid's tiles.")]
        [Min(0)] public int max_vault_tiles = 0;
        [Tooltip("GAPS: holes. Dodge over them; walk into one and McBilly falls (loses health, returns to the tile he stepped from). Capped in code at 10% of the grid's tiles.")]
        [Min(0)] public int max_gap_tiles = 0;
        [Tooltip("BROKEN: floor that cracks when McBilly lands on it, blinks, breaks into a gap, then repairs itself. Capped in code at 10% of the grid's tiles.")]
        [Min(0)] public int max_broken_tiles = 0;
        [Tooltip("Seconds a broken tile blinks after being stepped on before it breaks.")]
        [Min(.05f)] public float brokenCrackTime = 1.5f;
        [Tooltip("Seconds a broken tile stays a gap before it comes back.")]
        [Min(.05f)] public float brokenDownTime = 3f;

        // Older scenes stored a simple on/off switch; it's converted to Enemy Placement on load.
        [SerializeField, HideInInspector] bool enemiesOutsideGrid;

        int Width => enemyPlacement == EnemyPlacement.OffGrid ? outsideBoardWidth
                   : enemyPlacement == EnemyPlacement.Mixed ? mixedBoardWidth : boardWidth;
        int Height => enemyPlacement == EnemyPlacement.OffGrid ? outsideBoardHeight
                    : enemyPlacement == EnemyPlacement.Mixed ? mixedBoardHeight : boardHeight;
        bool UsesRail => enemyPlacement != EnemyPlacement.OnGrid;

        void MigrateLegacyPlacement()
        {
            if (!enemiesOutsideGrid) return;
            if (enemyPlacement == EnemyPlacement.OnGrid) enemyPlacement = EnemyPlacement.OffGrid;
            enemiesOutsideGrid = false;
        }

        void OnValidate() => MigrateLegacyPlacement();

        public PlayerTuning player = new PlayerTuning();
        public EnemyTuning enemy = new EnemyTuning();
        public CoinTuning coins = new CoinTuning();

        [Header("Juice")]
        public float parryHitstop = .06f;
        public float hurtHitstop = .09f;
        public bool screenShake = true;
        public bool showControls = true;

        public GameState State { get; private set; }
        public Board Board { get; private set; }
        public PlayerController Player { get; private set; }
        public Camera Cam { get; private set; }
        public Transform Root { get; private set; }
        public readonly List<Projectile> Projectiles = new List<Projectile>();
        public readonly List<TileWarning> Warnings = new List<TileWarning>();
        public readonly List<Enemy> Enemies = new List<Enemy>();
        public readonly List<Coin> Coins = new List<Coin>();

        public int Dismissed { get; private set; }
        public float Elapsed { get; private set; }
        int parries, perfectDodges;
        int coinsCollected, parriesTowardCoin, dodgesTowardCoin;
        int lastPerfectDodgeFrame = -1;

        float spawnTimer;
        float hitstopUntil;
        float shakeAmount, shakeTime;
        Vector3 camBase;
        float gameOverTime;

        struct PopupText { public string text; public Vector3 pos; public Color color; public float born; }
        readonly List<PopupText> popups = new List<PopupText>();
        GUIStyle popupStyle, hudStyle, bigStyle;

        void Awake()
        {
            I = this;
            MigrateLegacyPlacement();
            SetupCamera();
            StartRound();
        }

        void OnDestroy()
        {
            if (I == this) I = null;
            Time.timeScale = 1f;
        }

        void SetupCamera()
        {
            Cam = Camera.main;
            if (Cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                Cam = go.AddComponent<Camera>();
            }
            Cam.orthographic = true;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = Palette.Background;
            Cam.transform.SetPositionAndRotation(new Vector3(0f, -.3f, -10f), Quaternion.identity);
            camBase = Cam.transform.position;
            FitCamera();
        }

        void FitCamera()
        {
            float rail = UsesRail ? 1f : 0f; // room for the enemy rail
            float byHeight = Height * .5f + 1.3f + rail;
            float byWidth = (Width * .5f + .8f + rail) / Mathf.Max(.1f, Cam.aspect);
            Cam.orthographicSize = Mathf.Max(byHeight, byWidth);
        }

        public void StartRound()
        {
            if (Root != null)
            {
                Root.gameObject.SetActive(false); // stop everything this frame
                Destroy(Root.gameObject);
            }
            Projectiles.Clear();
            Warnings.Clear();
            Enemies.Clear();
            popups.Clear();
            Coins.Clear();
            Time.timeScale = 1f;
            hitstopUntil = 0f;

            Root = new GameObject("McBilly Runtime").transform;

            Board = new GameObject("Board").AddComponent<Board>();
            Board.transform.SetParent(Root, false);
            var start = new Vector2Int(Width / 2, Height / 2);
            int cap = Board.MaxSpecialFor(Width, Height);
            if (max_pillar_tiles > cap || max_vault_tiles > cap || max_gap_tiles > cap || max_broken_tiles > cap)
                Debug.Log($"[McBilly] Special tiles are capped to {cap} of each kind (10% of {Width * Height} tiles).");
            Board.Build(Width, Height, UsesRail, max_pillar_tiles, max_vault_tiles, max_gap_tiles, max_broken_tiles, start);

            Player = new GameObject("McBilly").AddComponent<PlayerController>();
            Player.transform.SetParent(Root, false);
            Player.Init(start, player.maxHealth);

            State = GameState.Playing;
            Dismissed = parries = perfectDodges = 0;
            coinsCollected = parriesTowardCoin = dodgesTowardCoin = 0;
            Elapsed = 0f;
            spawnTimer = .4f;
        }

        void Update()
        {
            // Hitstop runs on real time.
            if (Time.timeScale == 0f && Time.unscaledTime >= hitstopUntil) Time.timeScale = 1f;

            var kb = Keyboard.current;
            var gp = Gamepad.current;
            if (kb != null && kb.f1Key.wasPressedThisFrame) showControls = !showControls;
            bool restart = (kb != null && kb.rKey.wasPressedThisFrame) || (gp != null && gp.startButton.wasPressedThisFrame);
            if (State == GameState.GameOver && Time.unscaledTime - gameOverTime > .4f &&
                ((gp != null && gp.buttonSouth.wasPressedThisFrame) || (kb != null && kb.enterKey.wasPressedThisFrame)))
                restart = true;
            if (restart) { StartRound(); return; }

            if (State != GameState.Playing) return;

            Elapsed += Time.deltaTime;
            HandleSpawning(Time.deltaTime);
            TryCollectCoin(Player.Cell);
        }

        void LateUpdate()
        {
            if (Cam == null) return;
            FitCamera();
            Vector3 offset = Vector3.zero;
            if (screenShake && shakeTime > 0f)
            {
                shakeTime -= Time.unscaledDeltaTime;
                offset = (Vector3)(Random.insideUnitCircle * shakeAmount);
            }
            Cam.transform.position = camBase + offset;
        }

        // ---------------- Spawning ----------------

        void HandleSpawning(float dt)
        {
            int active = 0;
            foreach (var e in Enemies) if (e != null && !e.Leaving) active++;
            int target = Mathf.Min(enemy.maxEnemies, enemy.startEnemies + Mathf.FloorToInt(Elapsed / Mathf.Max(1f, enemy.secondsPerExtraEnemy)));
            if (active >= target) return;

            spawnTimer -= dt;
            if (spawnTimer > 0f) return;
            spawnTimer = enemy.spawnInterval;
            SpawnEnemy();
        }

        enum EnemyKind { Shooter, Brute, Caster }

        void SpawnEnemy()
        {
            // 1) What kind?
            float total = enemy.shooterWeight + enemy.bruteWeight + enemy.casterWeight;
            float r = Random.value * Mathf.Max(.0001f, total);
            EnemyKind kind = r < enemy.shooterWeight ? EnemyKind.Shooter
                           : r < enemy.shooterWeight + enemy.bruteWeight ? EnemyKind.Brute
                           : EnemyKind.Caster;

            // 2) On the grid or on the rail?
            bool onRail;
            switch (enemyPlacement)
            {
                case EnemyPlacement.OffGrid: onRail = true; break;
                case EnemyPlacement.Mixed:
                    var zone = kind == EnemyKind.Shooter ? enemy.shooterZone
                             : kind == EnemyKind.Brute ? enemy.bruteZone
                             : enemy.casterZone;
                    onRail = zone == SpawnZone.OffGrid || (zone == SpawnZone.Either && Random.value < enemy.offGridChanceForEither);
                    break;
                default: onRail = false; break;
            }

            // 3) Where?
            Vector2Int cell = default;
            bool found = false;
            for (int i = 0; i < 60 && !found; i++)
            {
                cell = onRail
                    ? Board.Ring[Random.Range(0, Board.Ring.Count)]
                    : new Vector2Int(Random.Range(0, Width), Random.Range(0, Height));
                found = Board.IsFree(cell, onRail) && Board.Chebyshev(cell, Player.Cell) >= 3;
            }
            if (!found) return;

            var go = new GameObject(kind.ToString());
            go.transform.SetParent(Root, false);
            Enemy e;
            switch (kind)
            {
                case EnemyKind.Shooter:
                    e = go.AddComponent<ShooterEnemy>();
                    e.Init(cell, onRail, Prims.Circle, Palette.Shooter, .72f);
                    break;
                case EnemyKind.Brute:
                    e = go.AddComponent<BruteEnemy>();
                    e.Init(cell, onRail, Prims.Square, Palette.Brute, .72f);
                    break;
                default:
                    e = go.AddComponent<CasterEnemy>();
                    e.Init(cell, onRail, Prims.Diamond, Palette.Caster, .8f);
                    break;
            }
            Enemies.Add(e);
        }

        public void OnEnemyLeft(Enemy e)
        {
            Enemies.Remove(e);
            if (State == GameState.Playing) Dismissed++;
            Popup("HAD ENOUGH", e.transform.position + Vector3.up * .6f, Palette.Patience);
        }

        // ---------------- Combat resolution ----------------

        /// <summary>Returns true if the projectile is consumed.</summary>
        public bool ResolveProjectileHit(Projectile p)
        {
            if (Player.IsDodging) return false;                       // i-frames: it flies through
            Vector2 toward = -p.Velocity;                              // from McBilly back toward the shooter
            if (Player.TryParry(toward)) { OnParry(p.Source); return true; }
            if (Player.IsHurtInvulnerable) return false;
            Player.QueueHit(p.Source, toward);
            return true;
        }

        /// <summary>Melee / beam / anything that lands on McBilly's tile instantly.</summary>
        public void ResolveDirectHit(Enemy source)
        {
            if (Player.IsDodging) return;
            Vector2 toward = (Vector2)(source.transform.position - Player.CellWorld);
            if (Player.TryParry(toward)) { OnParry(source); return; }
            if (Player.IsHurtInvulnerable) return;
            Player.QueueHit(source, toward);
        }

        public void OnParry(Enemy source)
        {
            parries++;
            if (coins.parriesPerCoin > 0 && ++parriesTowardCoin >= coins.parriesPerCoin)
            {
                parriesTowardCoin = 0;
                SpawnCoins();
            }
            Vector3 front = Player.CellWorld + (Vector3)(Dir8.Unit(Player.Facing) * .6f);
            FadeFx.Spawn(Prims.Ring, Color.white, front, 0f, Vector3.one * .3f, Vector3.one * 1.3f, .18f, 45, true);
            Popup("PARRY", Player.CellWorld + Vector3.up * .8f, Color.white);
            if (source != null && !source.Leaving)
                source.LosePatience(enemy.parryPatienceDamage, source.transform.position - Player.CellWorld);
            Hitstop(parryHitstop);
            Shake(.1f, .08f);
        }

        /// <summary>Called by the player right before a dodge moves them off <paramref name="origin"/>.</summary>
        public void CheckPerfectDodge(Vector2Int origin)
        {
            var credited = new HashSet<Enemy>();
            Vector2 o = Board.ToWorld(origin);

            foreach (var p in Projectiles)
            {
                if (p == null || p.Dodged || p.Source == null) continue;
                Vector2 to = o - (Vector2)p.transform.position;
                if (to.magnitude > player.perfectDodgeProjectileRange) continue;
                if (Vector2.Dot(p.Velocity.normalized, to.normalized) < .9f) continue; // must be heading at us
                p.Dodged = true;
                credited.Add(p.Source);
            }
            foreach (var w in Warnings)
            {
                if (w == null || w.Owner == null || w.Cell != origin) continue;
                if (w.Remaining <= player.perfectDodgeWindow) credited.Add(w.Owner);
            }
            foreach (var e in credited) CreditPerfectDodge(e);
        }

        public void CreditPerfectDodge(Enemy source)
        {
            perfectDodges++;
            lastPerfectDodgeFrame = Time.frameCount;
            Popup("PERFECT DODGE", Player.CellWorld + Vector3.up * .8f, Palette.Player);
            if (source != null && !source.Leaving)
                source.LosePatience(enemy.perfectDodgePatienceDamage, source.transform.position - Player.CellWorld);
            Hitstop(parryHitstop * .7f);
        }

        public void OnPlayerHurt(string popup = "OUCH")
        {
            Popup(popup, Player.CellWorld + Vector3.up * .8f, Palette.Warning);
            Hitstop(hurtHitstop);
            Shake(.25f, .18f);
            if (Player.HP <= 0)
            {
                State = GameState.GameOver;
                gameOverTime = Time.unscaledTime;
            }
        }

        // ---------------- Coins ----------------

        /// <summary>Called by the player every time a dodge happens.</summary>
        public void OnPlayerDodged()
        {
            bool perfect = lastPerfectDodgeFrame == Time.frameCount;
            if (coins.dodgesPerCoin <= 0 || (coins.onlyPerfectDodgesCount && !perfect)) return;
            if (++dodgesTowardCoin >= coins.dodgesPerCoin)
            {
                dodgesTowardCoin = 0;
                SpawnCoins();
            }
        }

        void SpawnCoins()
        {
            for (int n = 0; n < coins.coinsPerSpawn && Coins.Count < coins.maxCoinsInArena; n++)
            {
                for (int i = 0; i < 60; i++)
                {
                    var c = new Vector2Int(Random.Range(0, Board.Width), Random.Range(0, Board.Height));
                    if (!Board.IsWalkable(c) || Board.EnemyAt(c) != null || CoinAt(c) != null) continue;
                    if (Board.Chebyshev(c, Player.Cell) < coins.minDistanceFromPlayer) continue;
                    Coin.Spawn(c, coins.coinLifetime);
                    break;
                }
            }
        }

        Coin CoinAt(Vector2Int c)
        {
            foreach (var coin in Coins) if (coin != null && coin.Cell == c) return coin;
            return null;
        }

        /// <summary>Picks up a coin on this tile, if there is one.</summary>
        public void TryCollectCoin(Vector2Int c)
        {
            if (State != GameState.Playing) return;
            var coin = CoinAt(c);
            if (coin == null) return;
            Coins.Remove(coin);
            coinsCollected++;
            Popup("+1", Board.ToWorld(c) + Vector3.up * .6f, Palette.Coin);
            coin.Collect();
        }

        // ---------------- Juice ----------------

        public void Hitstop(float duration)
        {
            if (duration <= 0f) return;
            hitstopUntil = Mathf.Max(hitstopUntil, Time.unscaledTime + duration);
            Time.timeScale = 0f;
        }

        public void Shake(float amount, float duration)
        {
            shakeAmount = Mathf.Max(amount, shakeTime > 0f ? shakeAmount : 0f);
            shakeTime = Mathf.Max(shakeTime, duration);
        }

        public void Popup(string text, Vector3 worldPos, Color color)
        {
            popups.Add(new PopupText { text = text, pos = worldPos, color = color, born = Time.unscaledTime });
        }

        // ---------------- HUD (IMGUI: zero setup) ----------------

        void OnGUI()
        {
            if (Cam == null || Player == null) return;
            float ui = Screen.height / 1080f;
            if (popupStyle == null)
            {
                popupStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                hudStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
                bigStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            }
            popupStyle.fontSize = Mathf.RoundToInt(26 * ui);
            hudStyle.fontSize = Mathf.RoundToInt(24 * ui);
            bigStyle.fontSize = Mathf.RoundToInt(64 * ui);
            hudStyle.normal.textColor = Color.white;

            // Health pips
            float pip = 26 * ui, pad = 24 * ui;
            for (int i = 0; i < Player.MaxHP; i++)
            {
                var r = new Rect(pad + i * (pip + 8 * ui), pad, pip, pip);
                DrawRect(r, i < Player.HP ? Palette.Player : new Color(1, 1, 1, .12f));
            }
            int secs = Mathf.FloorToInt(Elapsed);
            GUI.Label(new Rect(pad, pad + pip + 10 * ui, 900 * ui, 40 * ui),
                $"Dismissed {Dismissed}    Time {secs / 60}:{secs % 60:00}    Parries {parries}    Perfect dodges {perfectDodges}", hudStyle);

            // Coins: total, plus progress toward the next one.
            var coinStyle = new GUIStyle(hudStyle);
            coinStyle.normal.textColor = Palette.Coin;
            string next = "";
            if (coins.parriesPerCoin > 0) next += $"parries {parriesTowardCoin}/{coins.parriesPerCoin}";
            if (coins.dodgesPerCoin > 0) next += (next.Length > 0 ? "  ·  " : "") + $"{(coins.onlyPerfectDodgesCount ? "perfect dodges" : "dodges")} {dodgesTowardCoin}/{coins.dodgesPerCoin}";
            GUI.Label(new Rect(pad, pad + pip + 44 * ui, 900 * ui, 40 * ui),
                $"Coins {coinsCollected}" + (next.Length > 0 ? $"    next coin: {next}" : ""), coinStyle);

            if (showControls)
            {
                var hint = new GUIStyle(hudStyle) { fontSize = Mathf.RoundToInt(18 * ui), fontStyle = FontStyle.Normal, alignment = TextAnchor.LowerLeft };
                hint.normal.textColor = new Color(1, 1, 1, .55f);
                GUI.Label(new Rect(pad, Screen.height - 160 * ui, Screen.width, 140 * ui),
                    "MOVE  WASD / D-pad / L-stick      LOOK  Mouse / R-stick / Arrows\n" +
                    "PARRY  RB / LMB / J / Space       DODGE  LB / RMB / K / Shift\n" +
                    "SHOOT  RT / F / Middle mouse      VAULT  Space / (A) while facing a crate\n" +
                    "R / Start: restart      F1: hide controls", hint);
            }

            // "Space" prompt over a crate McBilly can vault right now.
            if (State == GameState.Playing && Player.CanVault(out var crate))
            {
                Vector3 sp = Cam.WorldToScreenPoint(Board.ToWorld(crate) + Vector3.up * .55f);
                var prompt = new GUIStyle(popupStyle) { fontSize = Mathf.RoundToInt(16 * ui) };
                prompt.normal.textColor = Palette.VaultEdge;
                GUI.Label(new Rect(sp.x - 100 * ui, Screen.height - sp.y - 14 * ui, 200 * ui, 28 * ui), "SPACE: VAULT", prompt);
            }

            // Floating popups (real time, so they keep moving during hitstop)
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                var p = popups[i];
                float age = Time.unscaledTime - p.born;
                if (age > .7f) { popups.RemoveAt(i); continue; }
                Vector3 sp = Cam.WorldToScreenPoint(p.pos + Vector3.up * age * .8f);
                var c = p.color; c.a = 1f - Mathf.Clamp01((age - .4f) / .3f);
                popupStyle.normal.textColor = c;
                float s = 1f + .25f * Mathf.Clamp01(1f - age / .1f);
                popupStyle.fontSize = Mathf.RoundToInt(26 * ui * s);
                GUI.Label(new Rect(sp.x - 200 * ui, Screen.height - sp.y - 20 * ui, 400 * ui, 40 * ui), p.text, popupStyle);
            }

            if (State == GameState.GameOver)
            {
                DrawRect(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, .55f));
                bigStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(0, Screen.height * .5f - 80 * ui, Screen.width, 90 * ui), "KNOCKED OUT", bigStyle);
                var sub = new GUIStyle(hudStyle) { alignment = TextAnchor.MiddleCenter };
                GUI.Label(new Rect(0, Screen.height * .5f + 10 * ui, Screen.width, 40 * ui),
                    $"Dismissed {Dismissed} in {secs / 60}:{secs % 60:00}   —   R / Enter / (A) to retry", sub);
            }
        }

        static void DrawRect(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
