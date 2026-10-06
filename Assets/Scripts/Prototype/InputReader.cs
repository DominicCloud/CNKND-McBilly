using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace McBilly.Proto
{
    /// <summary>
    /// Polls keyboard / mouse / gamepad directly every frame. No action assets, no callbacks:
    /// whatever the hardware says this frame is what the player does this frame.
    ///
    /// Move : WASD, D-pad, left stick           (most recently pressed direction wins)
    /// Look : mouse, right stick, arrow keys    (snapped to 8 directions)
    /// Parry: RB, left mouse, J, Space
    /// Vault: Space or (A) while facing a vault crate (Space parries otherwise)
    /// Dodge: LB, right mouse, K, Left Shift
    /// Shoot: RT, F, middle mouse (hold to auto-fire)
    /// </summary>
    public class InputReader
    {
        public Vector2Int Move { get; private set; }
        /// <summary>True on the frame a new move direction is pressed. The player steps on this frame.</summary>
        public bool MovePressed { get; private set; }
        public int Look { get; private set; }
        public bool ParryPressed { get; private set; }
        /// <summary>Space: vaults if McBilly is facing a crate, otherwise parries (the player decides).</summary>
        public bool SpacePressed { get; private set; }
        /// <summary>Gamepad (A): vault only.</summary>
        public bool VaultPressed { get; private set; }
        public bool DodgePressed { get; private set; }
        public bool ShootPressed { get; private set; }
        public bool ShootHeld { get; private set; }

        public bool allowDiagonalMove;
        public float moveStickDeadzone = .5f;
        public float moveStickRelease = .35f;
        public float lookStickDeadzone = .45f;
        public float mouseDeadzone = .3f;
        /// <summary>Releasing two arrow keys a few ms apart shouldn't drop a diagonal look.</summary>
        public float diagonalReleaseGrace = .07f;

        // 0 up, 1 down, 2 left, 3 right
        static readonly Vector2Int[] Cardinals = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        readonly bool[] held = new bool[4];
        readonly bool[] prevHeld = new bool[4];
        readonly List<int> pressOrder = new List<int>(4);
        int stickDir = -1;
        bool lookFromMouse = true;
        float lastDiagonalArrowTime = -1f;
        int lastDiagonalArrowLook;

        public void Tick(Vector2 playerWorld, Camera cam)
        {
            var kb = Keyboard.current;
            var gp = Gamepad.current;
            var mouse = Mouse.current;

            ReadMove(kb, gp);
            ReadLook(kb, gp, mouse, playerWorld, cam);

            ParryPressed =
                (gp != null && gp.rightShoulder.wasPressedThisFrame) ||
                (mouse != null && mouse.leftButton.wasPressedThisFrame) ||
                (kb != null && kb.jKey.wasPressedThisFrame);
            SpacePressed = kb != null && kb.spaceKey.wasPressedThisFrame;
            VaultPressed = gp != null && gp.buttonSouth.wasPressedThisFrame;

            ShootPressed =
                (gp != null && gp.rightTrigger.wasPressedThisFrame) ||
                (mouse != null && mouse.middleButton.wasPressedThisFrame) ||
                (kb != null && kb.fKey.wasPressedThisFrame);
            ShootHeld =
                (gp != null && gp.rightTrigger.isPressed) ||
                (mouse != null && mouse.middleButton.isPressed) ||
                (kb != null && kb.fKey.isPressed);

            DodgePressed =
                (gp != null && gp.leftShoulder.wasPressedThisFrame) ||
                (mouse != null && mouse.rightButton.wasPressedThisFrame) ||
                (kb != null && (kb.kKey.wasPressedThisFrame || kb.leftShiftKey.wasPressedThisFrame));
        }

        void ReadMove(Keyboard kb, Gamepad gp)
        {
            // Left stick -> a single cardinal, with hysteresis so it doesn't flicker near 45 degrees.
            int stickSecondary = -1;
            if (gp != null)
            {
                Vector2 s = gp.leftStick.ReadValue();
                int cand = -1;
                if (s.magnitude >= moveStickDeadzone)
                    cand = Mathf.Abs(s.x) > Mathf.Abs(s.y) ? (s.x > 0 ? 3 : 2) : (s.y > 0 ? 0 : 1);

                if (stickDir >= 0 && cand != stickDir)
                {
                    float along = Vector2.Dot(s, Cardinals[stickDir]);
                    float biggest = Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y));
                    if (along >= moveStickRelease && (cand == -1 || along > .8f * biggest)) cand = stickDir;
                }
                stickDir = cand;

                if (allowDiagonalMove && stickDir >= 0 && Mathf.Abs(s.x) > .38f && Mathf.Abs(s.y) > .38f)
                    stickSecondary = stickDir <= 1 ? (s.x > 0 ? 3 : 2) : (s.y > 0 ? 0 : 1);
            }
            else stickDir = -1;

            held[0] = (kb != null && kb.wKey.isPressed) || (gp != null && gp.dpad.up.isPressed);
            held[1] = (kb != null && kb.sKey.isPressed) || (gp != null && gp.dpad.down.isPressed);
            held[2] = (kb != null && kb.aKey.isPressed) || (gp != null && gp.dpad.left.isPressed);
            held[3] = (kb != null && kb.dKey.isPressed) || (gp != null && gp.dpad.right.isPressed);
            if (stickDir >= 0) held[stickDir] = true;
            if (stickSecondary >= 0) held[stickSecondary] = true;

            MovePressed = false;
            for (int i = 0; i < 4; i++)
            {
                if (held[i] && !prevHeld[i])
                {
                    pressOrder.Remove(i);
                    pressOrder.Add(i);
                    MovePressed = true;
                }
                else if (!held[i] && prevHeld[i])
                {
                    pressOrder.Remove(i);
                }
                prevHeld[i] = held[i];
            }

            if (pressOrder.Count == 0) { Move = Vector2Int.zero; return; }

            int primary = pressOrder[pressOrder.Count - 1];
            Vector2Int move = Cardinals[primary];
            if (allowDiagonalMove)
            {
                bool primaryVertical = primary <= 1;
                for (int k = pressOrder.Count - 2; k >= 0; k--)
                {
                    int other = pressOrder[k];
                    if ((other <= 1) != primaryVertical) { move += Cardinals[other]; break; }
                }
            }
            Move = move;
        }

        void ReadLook(Keyboard kb, Gamepad gp, Mouse mouse, Vector2 playerWorld, Camera cam)
        {
            // 1) Right stick wins when deflected.
            if (gp != null)
            {
                Vector2 r = gp.rightStick.ReadValue();
                if (r.magnitude >= lookStickDeadzone)
                {
                    Look = Dir8.FromVector(r);
                    lookFromMouse = false;
                    return;
                }
            }

            // 2) Arrow keys (diagonals by holding two).
            if (kb != null)
            {
                Vector2 a = Vector2.zero;
                if (kb.upArrowKey.isPressed) a.y += 1;
                if (kb.downArrowKey.isPressed) a.y -= 1;
                if (kb.leftArrowKey.isPressed) a.x -= 1;
                if (kb.rightArrowKey.isPressed) a.x += 1;
                if (a != Vector2.zero)
                {
                    int dir = Dir8.FromVector(a);
                    bool diagonal = a.x != 0 && a.y != 0;
                    if (diagonal) { lastDiagonalArrowTime = Time.unscaledTime; lastDiagonalArrowLook = dir; }
                    else if (Time.unscaledTime - lastDiagonalArrowTime < diagonalReleaseGrace
                             && Dir8.Steps(dir, lastDiagonalArrowLook) == 1)
                        dir = lastDiagonalArrowLook; // one key of a diagonal released a hair early
                    Look = dir;
                    lookFromMouse = false;
                    return;
                }
            }

            // 3) Mouse: takes over as soon as it physically moves, then tracks every frame
            //    (so the facing stays correct while the player walks under a still cursor).
            if (mouse == null || cam == null) return;
            if (mouse.delta.ReadValue().sqrMagnitude > 4f) lookFromMouse = true;
            if (!lookFromMouse) return;

            Vector2 mp = mouse.position.ReadValue();
            Vector3 w = cam.ScreenToWorldPoint(new Vector3(mp.x, mp.y, -cam.transform.position.z));
            Vector2 d = (Vector2)w - playerWorld;
            if (d.magnitude > mouseDeadzone) Look = Dir8.FromVector(d);
        }
    }
}
