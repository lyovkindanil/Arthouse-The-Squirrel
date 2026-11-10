using System;
using System.Collections.Generic;
using UnityEngine;

namespace Squirrel.Edge
{
    public enum EdgeStage { Hold, Shrink, Collapse, Stabilized }

    /// <summary>
    /// Край: безопасная зона вокруг Сердца, которая сжимается фазами.
    /// Фаза = пауза (Hold) + сдвиг (Shrink). Каждая следующая фаза короче (EdgeSettings.escalation).
    /// После последней фазы — схлопывание до нуля, если Сердце не восстановлено.
    ///
    /// Без EdgeField зона — круг. С EdgeField (локация) расстояние меряется по проходам, а не по прямой,
    /// и добавляются очаги Края: каждый расползается от своей точки по проходам,
    /// но только пока белка рядом с ним. Так Край давит с той стороны, где белка сейчас находится.
    /// </summary>
    public class EdgeZone : MonoBehaviour
    {
        [SerializeField] EdgeSettings settings;
        [Tooltip("Сердце локации — центр, к которому ползёт Край")]
        [SerializeField] Transform center;
        [Tooltip("Расстояния по проходам и очаги Края. Пусто — Край круглый")]
        [SerializeField] EdgeField field;
        [Tooltip("На кого давят очаги (по умолчанию — белка)")]
        [SerializeField] Transform pressureTarget;

        float[] fronts, frontCaps, frontRecede;
        bool[] frontPressing;

        readonly HashSet<object> pausers = new HashSet<object>();

        public EdgeSettings Settings => settings;
        public EdgeStage Stage { get; private set; } = EdgeStage.Hold;
        /// <summary>Номер текущей фазы с нуля. Равен phaseCount во время финала.</summary>
        public int Phase { get; private set; }
        /// <summary>Текущий радиус: радиус фазы плюс то, на что Край оттолкнули воспоминания.</summary>
        public float Radius => Mathf.Max(0f, baseRadius + bonus);
        /// <summary>Сколько всего Край оттолкнули (ед) — копится до конца локации.</summary>
        public float PushedBack => bonusTarget;
        /// <summary>Сколько секунд осталось до конца текущей стадии.</summary>
        public float StageTimeLeft { get; private set; }
        public float StageDuration { get; private set; }
        public bool IsPaused => pausers.Count > 0;
        public bool IsWarning => (Stage == EdgeStage.Hold || (Stage == EdgeStage.Collapse && shrinkFrom == shrinkTo))
                                 && StageTimeLeft <= settings.warningTime && !IsPaused;
        public Vector2 Center => center != null ? (Vector2)center.position : (Vector2)transform.position;

        public event Action<int> PhaseStarted;   // пауза новой фазы началась
        public event Action<int> ShrinkStarted;  // Край пополз
        public event Action Stabilized;          // Сердце восстановлено
        public event Action<float> PushedBackBy; // воспоминание оттолкнуло Край

        float shrinkFrom, shrinkTo;
        float baseRadius;
        float bonus, bonusTarget;
        [Tooltip("Как быстро Край отступает после воспоминания, ед/с")]
        [SerializeField] float pushBackSpeed = 3f;

        public EdgeField Field => field;
        public bool HasFronts => EnsureFronts() && fronts.Length > 0;
        public int FrontCount => EnsureFronts() ? fronts.Length : 0;
        public float FrontRadius(int i) => fronts[i];
        public bool IsFrontPressing(int i) => frontPressing[i];
        public float FrontCap(int i) => frontCaps[i];

        void Awake()
        {
            if (settings == null)
            {
                Debug.LogWarning("EdgeZone: не назначены EdgeSettings, беру значения по умолчанию.");
                settings = ScriptableObject.CreateInstance<EdgeSettings>();
            }
            if (pressureTarget == null)
            {
                var sq = FindAnyObjectByType<SquirrelController>();
                if (sq != null) pressureTarget = sq.transform;
            }
            ResetZone();
        }

        public void ResetZone()
        {
            pausers.Clear();
            bonus = bonusTarget = 0f;
            fronts = null;
            EnterHold(0);
        }

        /// <summary>Очаги создаются, когда поле готово (EdgeField строится в своём Awake).</summary>
        bool EnsureFronts()
        {
            if (fronts != null) return true;
            if (field == null || !field.Ready) return false;
            int n = field.FrontCount;
            fronts = new float[n];
            frontCaps = new float[n];
            frontRecede = new float[n];
            frontPressing = new bool[n];
            var nests = FindObjectsByType<global::Squirrel.World.Nest>();
            for (int i = 0; i < n; i++)
            {
                fronts[i] = settings.frontStartRadius;
                // Очаг не доходит до Сердца ближе frontHeartSafe: финал решает общее кольцо.
                float cap = field.FrontDistance(i, Center) - settings.frontHeartSafe;
                // И не заливает гнёзда: укрытие должно оставаться укрытием.
                foreach (var nest in nests)
                    cap = Mathf.Min(cap, field.FrontDistance(i, (Vector2)nest.transform.position + Vector2.up * 0.5f) - settings.frontNestSafe);
                frontCaps[i] = Mathf.Max(settings.frontStartRadius, cap);
            }
            return true;
        }

        void UpdateFronts(float dt)
        {
            if (!EnsureFronts()) return;
            Vector2 target = pressureTarget != null ? (Vector2)pressureTarget.position + Vector2.up * 0.35f : Center;
            float speed = settings.frontSpeed * Mathf.Pow(settings.frontEscalation, Mathf.Min(Phase, settings.phaseCount));
            for (int i = 0; i < fronts.Length; i++)
            {
                if (frontRecede[i] > 0f)
                {
                    float d = Mathf.Min(frontRecede[i], pushBackSpeed * 1.5f * dt);
                    frontRecede[i] -= d;
                    fronts[i] = Mathf.Max(settings.frontStartRadius, fronts[i] - d);
                    frontPressing[i] = false;
                    continue;
                }
                float margin = field.FrontDistance(i, target) - fronts[i];
                frontPressing[i] = margin < settings.frontAwareness && fronts[i] < frontCaps[i];
                if (frontPressing[i]) fronts[i] = Mathf.Min(frontCaps[i], fronts[i] + speed * dt);
            }
        }

        /// <summary>Очаг, который ближе всех к точке (по пути), и сколько до его границы.</summary>
        public int NearestFront(Vector2 point, out float margin)
        {
            margin = float.MaxValue;
            if (!EnsureFronts()) return -1;
            int best = -1;
            for (int i = 0; i < fronts.Length; i++)
            {
                float m = field.FrontDistance(i, point) - fronts[i];
                if (m < margin) { margin = m; best = i; }
            }
            return best;
        }

        /// <summary>Отладка и тесты.</summary>
        public void DebugSetFront(int i, float radius)
        {
            if (!EnsureFronts()) return;
            fronts[i] = Mathf.Clamp(radius, settings.frontStartRadius, frontCaps[i]);
        }

        void EnterHold(int phase)
        {
            Phase = phase;
            baseRadius = settings.RadiusAt(phase);
            if (phase >= settings.phaseCount)
            {
                // Финал: стоим, затем схлопываемся.
                Stage = settings.collapseAfterLastPhase ? EdgeStage.Collapse : EdgeStage.Hold;
                shrinkFrom = shrinkTo = baseRadius;
                SetTimer(settings.collapseAfterLastPhase ? settings.collapseHoldTime : float.PositiveInfinity);
            }
            else
            {
                Stage = EdgeStage.Hold;
                SetTimer(settings.HoldTime(phase));
            }
            PhaseStarted?.Invoke(phase);
        }

        void EnterShrink()
        {
            Stage = EdgeStage.Shrink;
            shrinkFrom = baseRadius;
            shrinkTo = settings.RadiusAt(Phase + 1);
            SetTimer(settings.ShrinkTime(Phase));
            ShrinkStarted?.Invoke(Phase);
        }

        void SetTimer(float t)
        {
            StageDuration = t;
            StageTimeLeft = t;
        }

        void Update()
        {
            if (Stage == EdgeStage.Stabilized) return;
            bonus = Mathf.MoveTowards(bonus, bonusTarget, pushBackSpeed * Time.deltaTime);
            if (IsPaused) return;
            UpdateFronts(Time.deltaTime);
            if (float.IsPositiveInfinity(StageTimeLeft)) return;

            StageTimeLeft -= Time.deltaTime;

            switch (Stage)
            {
                case EdgeStage.Hold:
                    if (StageTimeLeft <= 0f) EnterShrink();
                    break;

                case EdgeStage.Shrink:
                    baseRadius = Mathf.Lerp(shrinkFrom, shrinkTo, Ease(1f - StageTimeLeft / StageDuration));
                    if (StageTimeLeft <= 0f) EnterHold(Phase + 1);
                    break;

                case EdgeStage.Collapse:
                    if (shrinkFrom == shrinkTo)
                    {
                        // Пауза перед схлопыванием закончилась — ползём к нулю. Отталкивание больше не спасает.
                        if (StageTimeLeft <= 0f)
                        {
                            shrinkFrom = baseRadius = Radius;
                            bonus = bonusTarget = 0f;
                            shrinkTo = 0f;
                            SetTimer(settings.collapseTime);
                            ShrinkStarted?.Invoke(Phase);
                        }
                    }
                    else
                    {
                        baseRadius = Mathf.Lerp(shrinkFrom, shrinkTo, Ease(1f - StageTimeLeft / StageDuration));
                        if (StageTimeLeft <= 0f) { baseRadius = 0f; StageTimeLeft = 0f; }
                    }
                    break;
            }
        }

        static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t); // smoothstep: Край трогается и останавливается плавно
        }

        /// <summary>Сколько единиц до Края (положительно — внутри зоны, отрицательно — за Краем).</summary>
        public float SignedDistanceInside(Vector2 point) => Evaluate(point, out _);

        /// <summary>
        /// Сколько единиц от точки до Края (меньше нуля — точка за Краем).
        /// source — что ближе всего давит: −1 общее кольцо, иначе номер очага.
        /// </summary>
        public float Evaluate(Vector2 point, out int source)
        {
            source = -1;
            if (field == null || !field.Ready) return Radius - Vector2.Distance(point, Center);
            float h = field.HeartDistance(point);
            float inside = h >= float.MaxValue ? -1000f : Radius - h;
            if (EnsureFronts())
                for (int i = 0; i < fronts.Length; i++)
                {
                    float f = field.FrontDistance(i, point);
                    if (f < float.MaxValue && f - fronts[i] < inside) { inside = f - fronts[i]; source = i; }
                }
            return inside;
        }

        /// <summary>Расстояние до Сердца — по проходам, если есть поле, иначе по прямой.</summary>
        public float DistanceToCenter(Vector2 point)
            => field != null && field.Ready ? field.HeartDistance(point) : Vector2.Distance(point, Center);
        public bool IsInside(Vector2 point) => SignedDistanceInside(point) >= 0f;

        /// <summary>Гнездо, меню, диалог: Край стоит, пока хоть кто-то держит паузу.</summary>
        public void SetPaused(object who, bool paused)
        {
            if (paused) pausers.Add(who); else pausers.Remove(who);
        }

        /// <summary>Провал «глубже в сон»: Край откатывается на одну фазу.</summary>
        public void RollBackOnePhase()
        {
            if (Stage == EdgeStage.Stabilized) return;
            int target = Stage == EdgeStage.Shrink ? Phase : Phase - 1;
            EnterHold(Mathf.Max(0, target));
            if (EnsureFronts())
                for (int i = 0; i < fronts.Length; i++)
                    fronts[i] = Mathf.Max(settings.frontStartRadius, fronts[i] - settings.frontRollback);
        }

        /// <summary>
        /// Воспоминание отталкивает Край на amount единиц (плавно, со скоростью pushBackSpeed).
        /// Отталкивание копится до конца локации. Во время схлопывания вместо этого откатывает таймер на amount секунд.
        /// </summary>
        public void PushBack(float amount)
        {
            if (Stage == EdgeStage.Stabilized || amount <= 0f) return;
            if (Stage == EdgeStage.Collapse)
                StageTimeLeft = Mathf.Min(StageDuration, StageTimeLeft + amount);
            else
                bonusTarget += amount;
            // Ближайший к белке очаг тоже отступает.
            if (pressureTarget != null && HasFronts)
            {
                int i = NearestFront(pressureTarget.position, out _);
                if (i >= 0) frontRecede[i] += amount * settings.frontPushMultiplier;
            }
            PushedBackBy?.Invoke(amount);
        }

        /// <summary>Снимок для сохранения.</summary>
        public Squirrel.World.EdgeSnapshot Capture() => new Squirrel.World.EdgeSnapshot
        {
            phase = Phase,
            stage = (int)Stage,
            timeLeft = float.IsInfinity(StageTimeLeft) ? -1f : StageTimeLeft,
            baseRadius = baseRadius,
            pushedBack = bonusTarget,
            fronts = EnsureFronts() ? (float[])fronts.Clone() : new float[0],
        };

        /// <summary>Восстановить Край из сохранения. Во время схлопывания начинает финал сначала.</summary>
        public void Restore(Squirrel.World.EdgeSnapshot s)
        {
            if (s == null) return;
            bonus = bonusTarget = Mathf.Max(0f, s.pushedBack);
            if (EnsureFronts() && s.fronts != null)
                for (int i = 0; i < Mathf.Min(fronts.Length, s.fronts.Length); i++)
                    fronts[i] = Mathf.Clamp(s.fronts[i], settings.frontStartRadius, frontCaps[i]);
            int phase = Mathf.Clamp(s.phase, 0, settings.phaseCount);
            switch ((EdgeStage)s.stage)
            {
                case EdgeStage.Stabilized:
                    Phase = phase;
                    baseRadius = s.baseRadius;
                    Stage = EdgeStage.Stabilized;
                    break;
                case EdgeStage.Shrink when phase < settings.phaseCount:
                    Phase = phase;
                    Stage = EdgeStage.Shrink;
                    shrinkFrom = settings.RadiusAt(phase);
                    shrinkTo = settings.RadiusAt(phase + 1);
                    SetTimer(settings.ShrinkTime(phase));
                    StageTimeLeft = Mathf.Clamp(s.timeLeft, 0f, StageDuration);
                    baseRadius = Mathf.Lerp(shrinkFrom, shrinkTo, Ease(1f - StageTimeLeft / StageDuration));
                    break;
                case EdgeStage.Collapse:
                    EnterHold(settings.phaseCount);
                    break;
                default:
                    EnterHold(phase);
                    if (s.timeLeft >= 0f && !float.IsInfinity(StageDuration))
                        StageTimeLeft = Mathf.Clamp(s.timeLeft, 0f, StageDuration);
                    break;
            }
        }

        /// <summary>Сердце восстановлено: Край замирает там, где был.</summary>
        public void Stabilize()
        {
            if (Stage == EdgeStage.Stabilized) return;
            Stage = EdgeStage.Stabilized;
            Stabilized?.Invoke();
        }

        /// <summary>Отладка: перемотать к концу текущей стадии.</summary>
        public void DebugSkipStage()
        {
            if (Stage == EdgeStage.Stabilized) return;
            StageTimeLeft = Mathf.Min(StageTimeLeft, 0f); // ровно 0: сработает в следующем кадре при любом FPS
        }

        void OnDrawGizmos()
        {
            Vector2 c = Center;
            float r = Application.isPlaying ? Radius : (settings != null ? settings.startRadius : 10f);
            Gizmos.color = new Color(0.7f, 0.4f, 1f, 0.8f);
            const int seg = 96;
            Vector3 prev = c + new Vector2(r, 0f);
            for (int i = 1; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                Vector3 p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
        }
    }
}
