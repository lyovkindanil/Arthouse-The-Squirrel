using UnityEngine;

namespace Squirrel
{
    public enum SquirrelState { Grounded, Airborne, OnWall }

    /// <summary>
    /// Конечный автомат белки: на земле, в воздухе, на стене.
    /// Все числа — в SquirrelSettings.
    /// </summary>
    [RequireComponent(typeof(SquirrelMotor), typeof(SquirrelInput), typeof(SquirrelAbilities))]
    public class SquirrelController : MonoBehaviour
    {
        [SerializeField] SquirrelSettings settings;
        [Tooltip("Дочерний объект со спрайтами: его отражаем по X и сжимаем/растягиваем")]
        [SerializeField] Transform visual;

        SquirrelMotor motor;
        SquirrelInput input;
        SquirrelAbilities abilities;
        bool wallIsBark;

        Vector2 velocity;
        float coyoteTimer, jumpBufferTimer, wallCoyoteTimer, inputLockTimer, wallUnstickTimer;
        int wallDir, lastWallDir;
        bool isJumping; // взлёт от прыжка (для короткого прыжка при отпускании кнопки)
        Vector3 visualBaseScale = Vector3.one;
        Vector2 squash = Vector2.one;

        public SquirrelState State { get; private set; } = SquirrelState.Airborne;
        public Vector2 Velocity => velocity;
        public int Facing { get; private set; } = 1;
        public bool IsGliding { get; private set; }
        public SquirrelSettings Settings => settings;
        /// <summary>Белка стоит на месте (лечение): бег отключён, прыжок работает и прерывает лечение.</summary>
        public bool Rooted { get; set; }

        void Awake()
        {
            motor = GetComponent<SquirrelMotor>();
            input = GetComponent<SquirrelInput>();
            abilities = GetComponent<SquirrelAbilities>();
            if (visual != null) visualBaseScale = visual.localScale;
            if (settings == null)
            {
                Debug.LogWarning("SquirrelController: не назначены SquirrelSettings, беру значения по умолчанию.");
                settings = ScriptableObject.CreateInstance<SquirrelSettings>();
            }
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            if (dt <= 0f) return;
            var s = settings;
            Vector2 move = input.Move;

            // --- Таймеры ввода ---
            if (input.JumpPressedThisFrame) jumpBufferTimer = s.jumpBufferTime;
            else jumpBufferTimer -= dt;
            coyoteTimer -= dt;
            wallCoyoteTimer -= dt;
            inputLockTimer -= dt;

            // --- Сенсоры ---
            bool grounded = velocity.y <= 0.01f && motor.Probe(Vector2.down);
            wallDir = 0;
            wallIsBark = false;
            if (!grounded)
            {
                if (motor.Probe(Vector2.right, out var wr)) { wallDir = 1; wallIsBark = BarkSurface.Is(wr); }
                else if (motor.Probe(Vector2.left, out var wl)) { wallDir = -1; wallIsBark = BarkSurface.Is(wl); }
                // За кору можно держаться всегда, за камень — только освоив отталкивание.
                if (wallDir != 0 && !wallIsBark && !abilities.WallJump) wallDir = 0;
            }

            // --- Переходы состояний ---
            var prev = State;
            if (grounded)
            {
                State = SquirrelState.Grounded;
            }
            else if (s.wallClingEnabled && wallDir != 0 && CanHoldWall(move, prev, dt))
            {
                State = SquirrelState.OnWall;
            }
            else
            {
                State = SquirrelState.Airborne;
            }

            if (State != prev) OnStateChanged(prev, State);
            if (State == SquirrelState.Grounded) coyoteTimer = s.coyoteTime;
            if (State == SquirrelState.OnWall) { wallCoyoteTimer = s.wallCoyoteTime; lastWallDir = wallDir; }

            // --- Горизонталь ---
            if (State == SquirrelState.OnWall)
            {
                velocity.x = 0f;
            }
            else if (inputLockTimer <= 0f)
            {
                float target = Rooted ? 0f : move.x * s.maxRunSpeed;
                bool onGround = State == SquirrelState.Grounded;
                float accel;
                if (Mathf.Approximately(move.x, 0f))
                    accel = onGround ? s.groundDeceleration : s.airDeceleration;
                else
                {
                    accel = onGround ? s.groundAcceleration : s.airAcceleration;
                    if (Mathf.Sign(move.x) != Mathf.Sign(velocity.x) && Mathf.Abs(velocity.x) > 0.01f)
                        accel *= s.turnMultiplier;
                }
                velocity.x = Mathf.MoveTowards(velocity.x, target, accel * dt);
            }

            // --- Вертикаль ---
            IsGliding = false;
            switch (State)
            {
                case SquirrelState.Grounded:
                    if (velocity.y < 0f) velocity.y = 0f;
                    isJumping = false;
                    break;

                case SquirrelState.OnWall:
                    if (move.y > 0f && s.wallClimbSpeed > 0f && wallIsBark)
                        velocity.y = s.wallClimbSpeed;
                    else if (move.y < 0f)
                        velocity.y = -s.wallFastSlideSpeed;
                    else
                        velocity.y = Mathf.MoveTowards(velocity.y, -s.wallSlideSpeed, s.Gravity * dt);
                    isJumping = false;
                    break;

                case SquirrelState.Airborne:
                    float g = s.Gravity;
                    if (velocity.y > 0f && isJumping && !input.JumpHeld) g *= s.jumpCutGravityMultiplier;
                    else if (Mathf.Abs(velocity.y) < s.apexThreshold && input.JumpHeld) g *= s.apexGravityMultiplier;
                    else if (velocity.y < 0f) g *= s.fallGravityMultiplier;
                    velocity.y -= g * dt;

                    if (abilities.Glide && velocity.y < 0f && input.JumpHeld && !isJumping)
                    {
                        velocity.y = Mathf.Max(velocity.y, -s.glideFallSpeed);
                        IsGliding = true;
                    }
                    velocity.y = Mathf.Max(velocity.y, -s.maxFallSpeed);
                    if (velocity.y <= 0f) isJumping = false;
                    break;
            }

            // --- Прыжки ---
            if (jumpBufferTimer > 0f)
            {
                if (State == SquirrelState.Grounded || coyoteTimer > 0f)
                {
                    DoJump(s.JumpVelocity);
                }
                else if (abilities.WallJump && (State == SquirrelState.OnWall || wallCoyoteTimer > 0f))
                {
                    int dir = State == SquirrelState.OnWall ? wallDir : lastWallDir;
                    velocity.x = -dir * s.wallJumpHorizontalSpeed;
                    DoJump(s.WallJumpVelocity);
                    inputLockTimer = s.wallJumpInputLock;
                    Facing = -dir;
                }
            }

            // --- Перемещение ---
            motor.Move(velocity * dt, velocity.y > 0f ? s.cornerCorrection : 0f);
            var c = motor.Last;
            if (c.above && velocity.y > 0f) { velocity.y = 0f; isJumping = false; }
            if (c.below && velocity.y < 0f) velocity.y = 0f;
            if ((c.left && velocity.x < 0f) || (c.right && velocity.x > 0f)) velocity.x = 0f;

            if (State != SquirrelState.OnWall && Mathf.Abs(move.x) > 0f && inputLockTimer <= 0f && !Rooted)
                Facing = move.x > 0 ? 1 : -1;
            else if (State == SquirrelState.OnWall)
                Facing = wallDir;

            UpdateVisual(dt);
        }

        bool CanHoldWall(Vector2 move, SquirrelState prev, float dt)
        {
            bool pressingToward = move.x != 0f && Mathf.Sign(move.x) == wallDir;
            bool pressingAway = move.x != 0f && Mathf.Sign(move.x) == -wallDir;

            if (prev != SquirrelState.OnWall)
            {
                wallUnstickTimer = settings.wallStickTime;
                // Цепляемся, только если жмём к стене и уже не взлетаем после отталкивания.
                return pressingToward && inputLockTimer <= 0f;
            }

            // Уже на стене: держимся, пока не будем жать «от стены» дольше wallStickTime.
            if (pressingAway) wallUnstickTimer -= dt;
            else wallUnstickTimer = settings.wallStickTime;
            return wallUnstickTimer > 0f;
        }

        void DoJump(float jumpVelocity)
        {
            velocity.y = jumpVelocity;
            isJumping = true;
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
            wallCoyoteTimer = 0f;
            State = SquirrelState.Airborne;
            Squash(1f - settings.squashAmount, 1f + settings.squashAmount);
        }

        void OnStateChanged(SquirrelState from, SquirrelState to)
        {
            if (to == SquirrelState.Grounded && from == SquirrelState.Airborne)
            {
                float impact = Mathf.InverseLerp(4f, settings.maxFallSpeed, -velocity.y);
                Squash(1f + settings.squashAmount * (0.5f + impact), 1f - settings.squashAmount * (0.5f + impact));
            }
        }

        void Squash(float x, float y) => squash = new Vector2(x, y);

        void UpdateVisual(float dt)
        {
            if (visual == null) return;
            squash = Vector2.Lerp(squash, Vector2.one, settings.squashRecoverSpeed * dt);
            visual.localScale = new Vector3(
                visualBaseScale.x * squash.x * Facing,
                visualBaseScale.y * squash.y,
                visualBaseScale.z);
        }

        /// <summary>Отбрасывание от удара: задаёт скорость и на время отбирает управление по X.</summary>
        public void Knockback(Vector2 newVelocity, float lockTime)
        {
            velocity = newVelocity;
            inputLockTimer = Mathf.Max(inputLockTimer, lockTime);
            isJumping = false;
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
            wallCoyoteTimer = 0f;
            State = SquirrelState.Airborne;
        }

        /// <summary>Отскок вверх после удара хвостом вниз по врагу. Высота не зависит от кнопки прыжка.</summary>
        public void Pogo(float upVelocity)
        {
            velocity.y = upVelocity;
            isJumping = false;
            coyoteTimer = 0f;
            State = SquirrelState.Airborne;
            Squash(1f - settings.squashAmount * 0.6f, 1f + settings.squashAmount * 0.6f);
        }

        /// <summary>Отдача по X после удара вбок.</summary>
        public void Recoil(float vx, float lockTime)
        {
            velocity.x = vx;
            inputLockTimer = Mathf.Max(inputLockTimer, lockTime);
        }

        /// <summary>Перенос белки (возврат в гнездо и т.п.).</summary>
        public void Teleport(Vector2 position)
        {
            motor.Teleport(position);
            velocity = Vector2.zero;
            State = SquirrelState.Airborne;
            isJumping = false;
            inputLockTimer = 0f;
            Rooted = false;
        }
    }
}
