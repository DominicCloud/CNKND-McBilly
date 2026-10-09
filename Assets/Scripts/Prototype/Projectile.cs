using UnityEngine;

namespace McBilly.Proto
{
    /// <summary>
    /// A bolt that flies in a straight 8-way line. It checks against the player's LOGICAL tile
    /// (not the sliding visual), so a step or dodge gets you out of the lane on the frame you press.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        public Enemy Source { get; private set; }
        public Vector2 Velocity { get; private set; }
        /// <summary>Already credited to a perfect dodge (prevents double-counting).</summary>
        public bool Dodged { get; set; }

        bool spent; // passed through the player once (i-frames); never hits again

        public static Projectile Spawn(Enemy source, Vector3 pos, int dir, float speed)
        {
            var g = McBillyGame.I;
            var sr = Prims.NewSprite("Projectile", Prims.Diamond, Palette.Projectile, g.Root, 30, .38f);
            sr.transform.position = pos;
            var p = sr.gameObject.AddComponent<Projectile>();
            p.Source = source;
            p.Velocity = Dir8.Unit(dir) * speed;
            g.Projectiles.Add(p);
            return p;
        }

        void Update()
        {
            var g = McBillyGame.I;
            if (g.State != McBillyGame.GameState.Playing) return;

            float dt = Time.deltaTime;
            transform.position += (Vector3)(Velocity * dt);
            transform.Rotate(0f, 0f, 900f * dt);

            if (!spent)
            {
                Vector2 toPlayer = (Vector2)(g.OldPlayer.CellWorld - transform.position);
                if (toPlayer.sqrMagnitude < .42f * .42f)
                {
                    if (g.ResolveProjectileHit(this)) { Destroy(gameObject); return; }
                    spent = true;
                }
            }

            if (!g.Board.InWorldBounds(transform.position, 1.6f)) Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (McBillyGame.I != null) McBillyGame.I.Projectiles.Remove(this);
        }
    }
}
