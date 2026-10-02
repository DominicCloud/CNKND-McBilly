using UnityEngine;

namespace McBilly.Proto
{
    /// <summary>
    /// Shared enemy behaviour: grid occupancy, Patience (a health bar that drains on its own),
    /// hit feedback, and leaving the board when Patience runs out.
    /// </summary>
    public abstract class Enemy : MonoBehaviour
    {
        public Vector2Int Cell { get; private set; }
        public float Patience { get; private set; }
        public float MaxPatience { get; private set; }
        public bool Leaving { get; private set; }
        /// <summary>Stands on the rail outside the grid (attacks inward) rather than on the grid.</summary>
        public bool OnRail { get; private set; }

        protected McBillyGame G => McBillyGame.I;
        protected Board Board => McBillyGame.I.Board;
        protected PlayerController Player => McBillyGame.I.Player;

        /// <summary>Seconds after spawning before the enemy starts acting.</summary>
        protected float arriveDelay = .7f;

        Transform visual;
        SpriteRenderer body, barBg, barFill;
        Color baseColor;
        float bodyScale;
        Vector3 moveFrom, moveTo;
        float moveT = 1f, moveDur = .09f;
        float age, popAge, flashT, leaveT;
        Vector2 punch;

        public void Init(Vector2Int cell, bool onRail, Sprite shape, Color color, float scale)
        {
            Cell = cell;
            OnRail = onRail;
            Board.SetOccupant(cell, this);
            transform.position = Board.ToWorld(cell);
            moveFrom = moveTo = transform.position;

            visual = new GameObject("Visual").transform;
            visual.SetParent(transform, false);
            visual.localScale = Vector3.zero;
            body = Prims.NewSprite("Body", shape, color, visual, 10, scale);
            baseColor = color;
            bodyScale = scale;

            barBg = Prims.NewSprite("PatienceBg", Prims.Square, Palette.BarBg, transform, 20);
            barBg.transform.localPosition = new Vector3(0f, .52f, 0f);
            barBg.transform.localScale = new Vector3(.82f, .11f, 1f);
            barFill = Prims.NewSprite("PatienceFill", Prims.Square, Palette.Patience, transform, 21);

            MaxPatience = Patience = G.enemy.patience;
            UpdateBar();
            OnInit();
        }

        protected virtual void OnInit() { }
        protected abstract void Think(float dt);
        protected virtual void OnLeave() { }
        /// <summary>Extra scale for wind-up "inhale" animations.</summary>
        protected virtual float PoseScale => 1f;
        /// <summary>0..1, how close an attack is to landing; brightens the body.</summary>
        protected virtual float Charge => 0f;

        protected virtual void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            popAge += dt;

            if (moveT < 1f)
            {
                moveT = Mathf.Min(1f, moveT + dt / moveDur);
                transform.position = Vector3.LerpUnclamped(moveFrom, moveTo, Ease.OutCubic(moveT));
            }

            punch = Vector2.Lerp(punch, Vector2.zero, 1f - Mathf.Exp(-18f * dt));
            flashT = Mathf.Max(0f, flashT - dt);

            if (Leaving)
            {
                // Fed up: float away, spin, fade.
                leaveT += dt;
                float k = Mathf.Clamp01(leaveT / .45f);
                visual.localScale = Vector3.one * (1f - k * .6f);
                visual.localPosition = new Vector3(0f, k * .8f, 0f);
                visual.localRotation = Quaternion.Euler(0, 0, k * 200f);
                body.color = baseColor.WithAlpha(1f - k);
                if (k >= 1f) Destroy(gameObject);
                return;
            }

            float pop = Ease.OutBack(popAge / .25f);
            visual.localScale = Vector3.one * pop * PoseScale;
            visual.localPosition = punch;
            body.color = flashT > 0f ? Color.white : Color.Lerp(baseColor, Color.white, Charge * .55f);

            if (G.State != McBillyGame.GameState.Playing) return;

            Patience -= G.enemy.patienceDrainPerSecond * dt;
            UpdateBar();
            if (Patience <= 0f) { Leave(); return; }

            if (age >= arriveDelay) Think(dt);
        }

        /// <summary>Hitting an enemy (parry, perfect dodge...) burns its Patience faster.</summary>
        public void LosePatience(float amount, Vector2 knockDirection)
        {
            if (Leaving) return;
            Patience -= amount;
            flashT = .09f;
            punch = knockDirection.normalized * .22f;
            UpdateBar();
            G.Popup($"-{Mathf.RoundToInt(amount)}", transform.position + Vector3.up * .8f, Palette.Patience);
            if (Patience <= 0f) Leave();
        }

        void Leave()
        {
            if (Leaving) return;
            Leaving = true;
            Board.Vacate(Cell, this);
            barBg.enabled = barFill.enabled = false;
            OnLeave();
            G.OnEnemyLeft(this);
        }

        void UpdateBar()
        {
            float k = Mathf.Clamp01(Patience / MaxPatience);
            barFill.transform.localScale = new Vector3(.78f * k, .07f, 1f);
            barFill.transform.localPosition = new Vector3(-.39f * (1f - k), .52f, 0f);
        }

        protected bool TryStep(Vector2Int to)
        {
            if (!Board.IsFree(to, OnRail)) return false;
            Board.Vacate(Cell, this);
            Cell = to;
            Board.SetOccupant(to, this);
            moveFrom = transform.position;
            moveTo = Board.ToWorld(to);
            moveT = 0f;
            moveDur = .09f;
            return true;
        }

        protected bool Blink(Vector2Int to)
        {
            if (!Board.IsFree(to, OnRail)) return false;
            FadeFx.Spawn(body.sprite, baseColor.WithAlpha(.6f), transform.position, 0f,
                Vector3.one * bodyScale, Vector3.one * bodyScale * 1.6f, .2f, 5);
            Board.Vacate(Cell, this);
            Cell = to;
            Board.SetOccupant(to, this);
            transform.position = moveFrom = moveTo = Board.ToWorld(to);
            moveT = 1f;
            popAge = .08f; // replay the pop-in
            return true;
        }

        protected Vector3 World => Board.ToWorld(Cell);
    }
}
