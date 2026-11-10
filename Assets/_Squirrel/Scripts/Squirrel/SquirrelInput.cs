using UnityEngine;
using UnityEngine.InputSystem;

namespace Squirrel
{
    /// <summary>
    /// Ввод через новый Input System. Действия создаются в коде, чтобы прототип работал без .inputactions-файла.
    /// Клавиатура: A/D или стрелки — бег, W/S — лазание, Пробел — прыжок. Геймпад: левый стик и кнопка South (A / Крест).
    /// </summary>
    public class SquirrelInput : MonoBehaviour
    {
        InputAction move;
        InputAction jump;

        public Vector2 Move { get; private set; }
        public bool JumpHeld { get; private set; }
        public bool JumpPressedThisFrame { get; private set; }

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

            jump = new InputAction("Jump", InputActionType.Button);
            jump.AddBinding("<Keyboard>/space");
            jump.AddBinding("<Keyboard>/z");
            jump.AddBinding("<Gamepad>/buttonSouth");
        }

        void OnEnable() { move.Enable(); jump.Enable(); }
        void OnDisable() { move.Disable(); jump.Disable(); }
        void OnDestroy() { move.Dispose(); jump.Dispose(); }

        // Подмена ввода для автотестов и будущих скриптовых сцен (катсцены, туториал).
        bool overridden;
        Vector2 overrideMove;
        bool overrideJump;

        /// <summary>Управлять белкой из кода вместо клавиатуры/геймпада.</summary>
        public void SetOverride(Vector2 moveValue, bool jumpHeld)
        {
            overridden = true;
            overrideMove = moveValue;
            overrideJump = jumpHeld;
        }

        public void ClearOverride() => overridden = false;

        void Update()
        {
            if (overridden)
            {
                Move = overrideMove;
                JumpPressedThisFrame = overrideJump && !JumpHeld;
                JumpHeld = overrideJump;
                return;
            }

            Vector2 m = move.ReadValue<Vector2>();
            // Мёртвая зона и «цифровой» X: для платформера стик выше 0.5 = полный бег.
            m.x = Mathf.Abs(m.x) < 0.2f ? 0f : Mathf.Sign(m.x) * Mathf.Clamp01(Mathf.Abs(m.x) * 2f);
            m.y = Mathf.Abs(m.y) < 0.5f ? 0f : Mathf.Sign(m.y);
            Move = m;
            JumpHeld = jump.IsPressed();
            JumpPressedThisFrame = jump.WasPressedThisFrame();
        }
    }
}
