using UnityEngine;

namespace McBilly.Proto
{
    [System.Serializable]
    public class CoinTuning
    {
        [Tooltip("X: a coin spawns after every X successful parries. 0 = parries never spawn coins.")]
        [Min(0)] public int parriesPerCoin = 3;
        [Tooltip("Y: a coin spawns after every Y dodges. 0 = dodges never spawn coins.")]
        [Min(0)] public int dodgesPerCoin = 5;
        [Tooltip("If on, only perfect dodges count toward Y (stops farming coins by dodging at nothing).")]
        public bool onlyPerfectDodgesCount = false;
        [Tooltip("How many coins appear each time X or Y is reached.")]
        [Min(1)] public int coinsPerSpawn = 1;
        [Tooltip("Most coins allowed in the arena at once. Extra spawns are skipped.")]
        [Min(1)] public int maxCoinsInArena = 6;
        [Tooltip("Seconds before an uncollected coin disappears (it blinks for the last 2 seconds). 0 = never.")]
        [Min(0f)] public float coinLifetime = 12f;
        [Tooltip("Coins never spawn closer than this many tiles to McBilly.")]
        [Min(0)] public int minDistanceFromPlayer = 2;
    }

    /// <summary>
    /// A minted coin: spins, bobs and throws off little sparkles. Collected by stepping
    /// or dashing onto its tile (checked against McBilly's logical tile, so it's instant).
    /// </summary>
    public class Coin : MonoBehaviour
    {
        public Vector2Int Cell { get; private set; }

        Transform visual;
        SpriteRenderer rim, face, shine;
        float age, lifetime, sparkleT;

        public static Coin Spawn(Vector2Int cell, float lifetime)
        {
            var g = McBillyGame.I;
            var go = new GameObject("Coin");
            go.transform.SetParent(g.Root, false);
            go.transform.position = g.Board.ToWorld(cell);

            var coin = go.AddComponent<Coin>();
            coin.Cell = cell;
            coin.lifetime = lifetime;
            coin.visual = new GameObject("Visual").transform;
            coin.visual.SetParent(go.transform, false);
            coin.visual.localScale = Vector3.zero;
            coin.rim = Prims.NewSprite("Rim", Prims.Circle, Palette.CoinRim, coin.visual, 7, .46f);
            coin.face = Prims.NewSprite("Face", Prims.Circle, Palette.Coin, coin.visual, 8, .36f);
            coin.shine = Prims.NewSprite("Shine", Prims.Square, new Color(1f, 1f, 1f, .85f), coin.visual, 9);
            coin.shine.transform.localScale = new Vector3(.05f, .2f, 1f);
            coin.shine.transform.localPosition = new Vector3(-.06f, .03f, 0f);
            coin.shine.transform.localRotation = Quaternion.Euler(0, 0, -20f);

            g.Coins.Add(coin);
            FadeFx.Spawn(Prims.Ring, Palette.Coin, go.transform.position, 0f, Vector3.one * .2f, Vector3.one * 1f, .3f, 30);
            return coin;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;

            // Pop in, spin (squash on X like a flipping coin), bob.
            float pop = Ease.OutBack(age / .3f);
            float spin = Mathf.Max(.18f, Mathf.Abs(Mathf.Cos(age * 3.2f)));
            visual.localScale = new Vector3(spin * pop, pop, 1f);
            visual.localPosition = new Vector3(0f, Mathf.Sin(age * 3f) * .05f, 0f);

            // Sparkles: little twinkling crosses around the coin.
            sparkleT -= dt;
            if (sparkleT <= 0f)
            {
                sparkleT = Random.Range(.12f, .3f);
                Sparkle(transform.position + (Vector3)(Random.insideUnitCircle * .38f), .22f);
            }

            // Expiry: blink for the last two seconds, then vanish.
            if (lifetime > 0f)
            {
                float left = lifetime - age;
                if (left <= 0f)
                {
                    FadeFx.Spawn(Prims.Circle, Palette.Coin.WithAlpha(.6f), transform.position, 0f, Vector3.one * .4f, Vector3.zero, .2f, 8);
                    Destroy(gameObject);
                    return;
                }
                bool visible = left > 2f || Mathf.Repeat(left, .2f) > .08f;
                rim.enabled = face.enabled = shine.enabled = visible;
            }
        }

        public void Collect()
        {
            Vector3 p = transform.position;
            FadeFx.Spawn(Prims.Ring, Palette.Coin, p, 0f, Vector3.one * .3f, Vector3.one * 1.3f, .25f, 45, true);
            for (int i = 0; i < 6; i++)
                Sparkle(p + (Vector3)(Random.insideUnitCircle * .55f), .3f, true);
            Destroy(gameObject);
        }

        static void Sparkle(Vector3 pos, float size, bool unscaled = false)
        {
            float d = Random.Range(.25f, .4f);
            FadeFx.Spawn(Prims.Square, Color.white, pos, 0f, new Vector3(.03f, size, 1f), new Vector3(.01f, 0f, 1f), d, 26, unscaled);
            FadeFx.Spawn(Prims.Square, Color.white, pos, 0f, new Vector3(size, .03f, 1f), new Vector3(0f, .01f, 1f), d, 26, unscaled);
        }

        void OnDestroy()
        {
            if (McBillyGame.I != null) McBillyGame.I.Coins.Remove(this);
        }
    }
}
