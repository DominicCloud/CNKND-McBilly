using UnityEngine;

namespace McBilly.Proto
{
    /// <summary>
    /// McBilly's own projectile. Flies straight in one of the 8 directions and chips Patience off the
    /// first enemy it touches (grid or rail). Like enemy shots, it passes over tiles.
    /// </summary>
    public class PlayerShot : MonoBehaviour
    {
        Vector2 velocity;
        float damage;

        public static PlayerShot Spawn(Vector3 pos, int dir, float speed, float damage)
        {
            var g = McBillyGame.I;
            var sr = Prims.NewSprite("PlayerShot", Prims.Diamond, Palette.PlayerShot, g.Root, 31);
            sr.transform.position = pos;
            sr.transform.rotation = Quaternion.Euler(0f, 0f, Dir8.Angle(dir));
            sr.transform.localScale = new Vector3(.42f, .2f, 1f); // a stretched dart pointing along its flight
            var shot = sr.gameObject.AddComponent<PlayerShot>();
            shot.velocity = Dir8.Unit(dir) * speed;
            shot.damage = damage;
            return shot;
        }

        void Update()
        {
            var g = McBillyGame.I;
            if (g == null || g.State != McBillyGame.GameState.Playing) return;

            transform.position += (Vector3)(velocity * Time.deltaTime);

            // Find the first enemy we're touching (don't damage inside the loop: an enemy that runs
            // out of Patience removes itself from the list).
            Enemy target = null;
            foreach (var e in g.Enemies)
            {
                if (e == null || e.Leaving) continue;
                if (((Vector2)(e.transform.position - transform.position)).sqrMagnitude < .45f * .45f) { target = e; break; }
            }
            if (target != null)
            {
                FadeFx.Spawn(Prims.Ring, Palette.PlayerShot, transform.position, 0f, Vector3.one * .2f, Vector3.one * .7f, .15f, 45);
                target.LosePatience(damage, velocity);
                Destroy(gameObject);
                return;
            }

            if (!g.Board.InWorldBounds(transform.position, 1.6f)) Destroy(gameObject);
        }
    }
}
