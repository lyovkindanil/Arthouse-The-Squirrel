using UnityEngine;
using UnityEngine.InputSystem;

namespace Squirrel
{
    /// <summary>
    /// Ввод через новый Input System. Действия создаются в коде, чтобы прототип работал без .inputactions-файла.
    /// Клавиатура: A/D или стрелки — бег, W/S — вверх/вниз, Пробел/Z — прыжок,
    /// X/J — удар хвостом, C/K — жёлудь, V/L (держать) — лечение.
    /// Геймпад: левый стик, South — прыжок, West — удар, правый бампер — жёлудь, North (держать) — лечение.
    /// Читается раньше остальных скриптов, чтобы «нажато в этом кадре» видели все в том же кадре.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class SquirrelInput : MonoBehaviour
    {
        InputAction move;
        InputAction jump;
        InputAction attack;
        InputAction throwAcorn;
        InputAction heal;

        public Vector2 Move { get; private set; }
        public bool JumpHeld { get; private set; }
        public bool JumpPressedThisFrame { get; private set; }
        public bool AttackPressedThisFrame { get; private set; }
        public bool ThrowPressedThisFrame { get; private set; }
        public bool HealHeld { get; private set; }

        void Awake()
        {
            move = new InputAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/leftStick");
            move.AddBinding("<Gamepad>/dpad");

            jump = Button("Jump", "<Keyboard>/space", "<Keyboard>/z", "<Gamepad>/buttonSouth");
            attack = Button("Attack", "<Keyboard>/x", "<Keyboard>/j", "<Gamepad>/buttonWest");
            throwAcorn = Button("Throw", "<Keyboard>/c", "<Keyboard>/k", "<Gamepad>/rightShoulder");
            heal = Button("Heal", "<Keyboard>/v", "<Keyboard>/l", "<Gamepad>/buttonNorth");
        }

        static InputAction Button(string name, params string[] bindings)
        {
            var a = new InputAction(name, InputActionType.Button);
            foreach (var b in bindings) a.AddBinding(b);
            return a;
        }

        InputAction[] All => new[] { move, jump, attack, throwAcorn, heal };

        void OnEnable() { foreach (var a in All) a.Enable(); }
        void OnDisable() { foreach (var a in All) a.Disable(); }
        void OnDestroy() { foreach (var a in All) a.Dispose(); }

        // Подмена ввода для автотестов и будущих скриптовых сцен (катсцены, туториал).
        bool overridden;
        Vector2 overrideMove;
        bool overrideJump, overrideAttack, overrideThrow, overrideHeal;
        bool prevAttack, prevThrow;

        /// <summary>Управлять белкой из кода вместо клавиатуры/геймпада.</summary>
        public void SetOverride(Vector2 moveValue, bool jumpHeld)
            => SetOverride(moveValue, jumpHeld, false);

        /// <summary>То же, с кнопками боя. «Нажатие» срабатывает, когда кнопка переходит из false в true.</summary>
        public void SetOverride(Vector2 moveValue, bool jumpHeld, bool attackHeld, bool throwHeld = false, bool healHeld = false)
        {
            overridden = true;
            overrideMove = moveValue;
            overrideJump = jumpHeld;
            overrideAttack = attackHeld;
            overrideThrow = throwHeld;
            overrideHeal = healHeld;
        }

        public void ClearOverride() => overridden = false;

        void Update()
        {
            if (overridden)
            {
                Move = overrideMove;
                JumpPressedThisFrame = overrideJump && !JumpHeld;
                JumpHeld = overrideJump;
                AttackPressedThisFrame = overrideAttack && !prevAttack;
                prevAttack = overrideAttack;
                ThrowPressedThisFrame = overrideThrow && !prevThrow;
                prevThrow = overrideThrow;
                HealHeld = overrideHeal;
                return;
            }

            Vector2 m = move.ReadValue<Vector2>();
            // Мёртвая зона и «цифровой» X: для платформера стик выше 0.5 = полный бег.
            m.x = Mathf.Abs(m.x) < 0.2f ? 0f : Mathf.Sign(m.x) * Mathf.Clamp01(Mathf.Abs(m.x) * 2f);
            m.y = Mathf.Abs(m.y) < 0.5f ? 0f : Mathf.Sign(m.y);
            Move = m;
            JumpHeld = jump.IsPressed();
            JumpPressedThisFrame = jump.WasPressedThisFrame();
            AttackPressedThisFrame = attack.WasPressedThisFrame();
            ThrowPressedThisFrame = throwAcorn.WasPressedThisFrame();
            HealHeld = heal.IsPressed();
            prevAttack = attack.IsPressed();
            prevThrow = throwAcorn.IsPressed();
        }
    }
}
