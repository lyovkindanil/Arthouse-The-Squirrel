using UnityEngine;
using Squirrel.Combat;

namespace Squirrel
{
    /// <summary>
    /// Покадровая анимация белки: выбирает клип под то, что белка делает в игре, и листает его кадры.
    ///  • стоит — idle_test, бежит — run (темп шага зависит от скорости);
    ///  • в воздухе — кадры прыжка jump подбираются по вертикальной скорости (взлёт, вершина, падение),
    ///    при приземлении доигрывается его конец;
    ///  • на стене — climb: вверх листается вперёд, при сползании замирает;
    ///  • удар — paw_swipe (взмах лапой), проигрывается один раз поверх остального;
    ///  • лечение — sit_idle (сидит столбиком).
    /// Разворот по сторонам и сжатие при приземлении делает SquirrelController, масштабируя родителя.
    /// </summary>
    public class SquirrelClipAnimator : MonoBehaviour
    {
        [SerializeField] SquirrelController controller;
        [SerializeField] TailAttack attack;
        [SerializeField] SquirrelHealth health;
        [SerializeField] SquirrelClips clips;
        [SerializeField] SpriteRenderer body;
        [SerializeField] SpriteRenderer glow;

        [Header("Клипы")]
        [SerializeField] string idleClip = "idle_test";
        [SerializeField] string runClip = "run";
        [SerializeField] string jumpClip = "jump";
        [SerializeField] string climbClip = "climb";
        [SerializeField] string attackClip = "paw_swipe";
        [SerializeField] string healClip = "sit_idle";

        [Header("Бег")]
        [SerializeField] float runSpeedThreshold = 0.6f;
        [Tooltip("Скорость белки, при которой бег идёт в родном темпе клипа")]
        [SerializeField] float runReferenceSpeed = 9f;
        [SerializeField] Vector2 runRateRange = new Vector2(0.6f, 1.3f);

        [Header("Прыжок: кадры клипа jump")]
        [Tooltip("Первый кадр взлёта (лапы уже оторвались)")]
        [SerializeField] int riseFrame = 10;
        [Tooltip("Кадр вершины прыжка")]
        [SerializeField] int apexFrame = 20;
        [Tooltip("Последний кадр падения перед касанием")]
        [SerializeField] int fallFrame = 29;
        [Tooltip("Вертикальная скорость, на которой показывается первый кадр взлёта")]
        [SerializeField] float riseSpeed = 16f;
        [Tooltip("Скорость падения, на которой показывается последний кадр падения")]
        [SerializeField] float fallSpeed = 12f;
        [Tooltip("Как быстро кадр прыжка догоняет нужный, кадров в секунду")]
        [SerializeField] float airFrameRate = 60f;
        [Tooltip("Падение медленнее этого приземляется без отдельной анимации")]
        [SerializeField] float landMinSpeed = 4f;

        [Header("Стена")]
        [Tooltip("Скорость подъёма, при которой лазанье идёт в родном темпе клипа")]
        [SerializeField] float climbReferenceSpeed = 4f;

        [Header("Удар лапой")]
        [Tooltip("С какого кадра клипа начинать: замах в игре короче, чтобы удар не запаздывал")]
        [SerializeField] int attackStartFrame = 4;
        [SerializeField] float attackFps = 48f;

        [Header("Свечение")]
        [SerializeField] float glowPulse = 0.18f;
        [SerializeField] float glowPulseSpeed = 1.6f;

        enum Mode { Idle, Run, Air, Land, Wall, Heal }

        SquirrelClips.Clip idle, run, jump, climb, swipe, heal;
        SquirrelClips.Clip current;
        Mode mode = Mode.Idle;
        float frame;            // дробный номер кадра в текущем клипе
        float attackFrame = -1; // >= 0 — идёт удар
        float lastFallSpeed;
        Color glowBase;
        float time;

        /// <summary>Имя клипа, который показывается сейчас (для тестов и отладки).</summary>
        public string CurrentClip => attackFrame >= 0f && swipe != null ? swipe.name : current != null ? current.name : null;
        public int CurrentFrame { get; private set; }

        void Awake()
        {
            if (body == null) body = GetComponent<SpriteRenderer>();
            if (glow != null) glowBase = glow.color;
            Bind();
        }

        void OnEnable() { if (attack != null) attack.Swung += OnSwung; }
        void OnDisable() { if (attack != null) attack.Swung -= OnSwung; }

        /// <summary>Сменить набор клипов (другой облик белки).</summary>
        public void SetClips(SquirrelClips set)
        {
            clips = set;
            Bind();
        }

        void Bind()
        {
            if (clips == null) return;
            idle = clips.Find(idleClip);
            run = clips.Find(runClip) ?? idle;
            jump = clips.Find(jumpClip) ?? idle;
            climb = clips.Find(climbClip) ?? idle;
            heal = clips.Find(healClip) ?? idle;
            swipe = clips.Find(attackClip);
            current = idle;
            frame = 0f;
            if (glow != null)
            {
                glowBase = clips.glowColor;
                glow.color = glowBase;
                glow.transform.localScale = new Vector3(clips.glowSize.x, clips.glowSize.y, 1f);
            }
            Show(current, 0);
        }

        void OnSwung(Vector2 direction)
        {
            if (swipe == null || swipe.Length == 0) return;
            attackFrame = Mathf.Clamp(attackStartFrame, 0, swipe.Length - 1);
        }

        void LateUpdate()
        {
            if (controller == null || idle == null) return;
            float dt = Time.deltaTime;
            time += dt;
            Vector2 v = controller.Velocity;

            Mode want = Pick(v);
            if (want != mode) Enter(want, v);
            Advance(v, dt);
            if (controller.State == SquirrelState.Airborne) lastFallSpeed = Mathf.Max(0f, -v.y);

            // Удар идёт поверх: пока он не доигран, показываем его кадры, а остальное листается под ним.
            if (attackFrame >= 0f && swipe != null)
            {
                int af = Mathf.FloorToInt(attackFrame);
                attackFrame += attackFps * dt;
                if (af >= swipe.Length) attackFrame = -1f;
                else { Show(swipe, af); UpdateGlow(); return; }
            }

            Show(current, Mathf.FloorToInt(frame));
            UpdateGlow();
        }

        Mode Pick(Vector2 v)
        {
            switch (controller.State)
            {
                case SquirrelState.OnWall: return Mode.Wall;
                case SquirrelState.Airborne: return Mode.Air;
            }
            if (health != null && health.IsHealing) return Mode.Heal;
            bool moving = Mathf.Abs(v.x) > runSpeedThreshold;
            if (mode == Mode.Air && !moving && lastFallSpeed >= landMinSpeed) return Mode.Land;
            if (mode == Mode.Land && !moving && frame < jump.Length - 1) return Mode.Land; // доигрываем приземление
            return moving ? Mode.Run : Mode.Idle;
        }

        void Enter(Mode next, Vector2 v)
        {
            mode = next;
            switch (next)
            {
                case Mode.Idle: current = idle; frame = 0f; break;
                case Mode.Run: current = run; frame = 0f; break;
                case Mode.Heal: current = heal; frame = 0f; break;
                case Mode.Wall: current = climb; frame = 0f; break;
                case Mode.Air: current = jump; frame = AirFrame(v.y); break;
                case Mode.Land: current = jump; frame = Mathf.Min(fallFrame + 1, jump.Length - 1); break;
            }
        }

        void Advance(Vector2 v, float dt)
        {
            if (current == null || current.Length == 0) return;
            switch (mode)
            {
                case Mode.Run:
                    float rate = Mathf.Clamp(Mathf.Abs(v.x) / Mathf.Max(0.01f, runReferenceSpeed), runRateRange.x, runRateRange.y);
                    Step(current.fps * rate * dt);
                    break;
                case Mode.Wall:
                    // Вверх — перебирает лапами, сползает — замирает, вцепившись.
                    if (v.y > 0.05f) Step(current.fps * Mathf.Clamp(v.y / Mathf.Max(0.01f, climbReferenceSpeed), 0.3f, 1.5f) * dt);
                    break;
                case Mode.Air:
                    frame = Mathf.MoveTowards(frame, AirFrame(v.y), airFrameRate * dt);
                    break;
                case Mode.Land:
                    frame = Mathf.Min(frame + current.fps * dt, current.Length - 1);
                    break;
                default:
                    Step(current.fps * dt);
                    break;
            }
        }

        void Step(float frames)
        {
            frame += frames;
            int n = current.Length;
            if (current.loop) frame = Mathf.Repeat(frame, n);
            else frame = Mathf.Min(frame, n - 1);
        }

        /// <summary>Кадр прыжка под вертикальную скорость: быстро вверх — взлёт, около нуля — вершина, вниз — падение.</summary>
        float AirFrame(float vy)
        {
            int last = Mathf.Max(0, jump.Length - 1);
            float f = vy >= 0f
                ? Mathf.Lerp(apexFrame, riseFrame, Mathf.Clamp01(vy / Mathf.Max(0.01f, riseSpeed)))
                : Mathf.Lerp(apexFrame, fallFrame, Mathf.Clamp01(-vy / Mathf.Max(0.01f, fallSpeed)));
            return Mathf.Clamp(f, 0f, last);
        }

        void Show(SquirrelClips.Clip clip, int index)
        {
            if (body == null || clip == null || clip.Length == 0) return;
            index = Mathf.Clamp(index, 0, clip.Length - 1);
            CurrentFrame = index;
            body.sprite = clip.frames[index];
            body.transform.localPosition = new Vector3(clip.offset.x, clip.offset.y, 0f);
        }

        void UpdateGlow()
        {
            if (glow == null) return;
            Color c = glowBase;
            c.a *= 1f + glowPulse * Mathf.Sin(time * glowPulseSpeed);
            glow.color = c;
        }
    }
}
