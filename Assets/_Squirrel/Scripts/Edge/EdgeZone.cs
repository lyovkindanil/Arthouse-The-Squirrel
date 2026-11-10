using System;
using System.Collections.Generic;
using UnityEngine;

namespace Squirrel.Edge
{
    public enum EdgeStage { Hold, Shrink, Collapse, Stabilized }

    /// <summary>
    /// Край: круглая безопасная зона вокруг Сердца, которая сжимается фазами.
    /// Фаза = пауза (Hold) + сдвиг (Shrink). Каждая следующая фаза короче (EdgeSettings.escalation).
    /// После последней фазы — схлопывание до нуля, если Сердце не восстановлено.
    /// </summary>
    public class EdgeZone : MonoBehaviour
    {
        [SerializeField] EdgeSettings settings;
        [Tooltip("Сердце локации — центр, к которому ползёт Край")]
        [SerializeField] Transform center;

        readonly HashSet<object> pausers = new HashSet<object>();

        public EdgeSettings Settings => settings;
        public EdgeStage Stage { get; private set; } = EdgeStage.Hold;
        /// <summary>Номер текущей фазы с нуля. Равен phaseCount во время финала.</summary>
        public int Phase { get; private set; }
        public float Radius { get; private set; }
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

        float shrinkFrom, shrinkTo;

        void Awake()
        {
            if (settings == null)
            {
                Debug.LogWarning("EdgeZone: не назначены EdgeSettings, беру значения по умолчанию.");
                settings = ScriptableObject.CreateInstance<EdgeSettings>();
            }
            ResetZone();
        }

        public void ResetZone()
        {
            pausers.Clear();
            EnterHold(0);
        }

        void EnterHold(int phase)
        {
            Phase = phase;
            Radius = settings.RadiusAt(phase);
            if (phase >= settings.phaseCount)
            {
                // Финал: стоим, затем схлопываемся.
                Stage = settings.collapseAfterLastPhase ? EdgeStage.Collapse : EdgeStage.Hold;
                shrinkFrom = shrinkTo = Radius;
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
            shrinkFrom = Radius;
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
            if (Stage == EdgeStage.Stabilized || IsPaused) return;
            if (float.IsPositiveInfinity(StageTimeLeft)) return;

            StageTimeLeft -= Time.deltaTime;

            switch (Stage)
            {
                case EdgeStage.Hold:
                    if (StageTimeLeft <= 0f) EnterShrink();
                    break;

                case EdgeStage.Shrink:
                    Radius = Mathf.Lerp(shrinkFrom, shrinkTo, Ease(1f - StageTimeLeft / StageDuration));
                    if (StageTimeLeft <= 0f) EnterHold(Phase + 1);
                    break;

                case EdgeStage.Collapse:
                    if (shrinkFrom == shrinkTo)
                    {
                        // Пауза перед схлопыванием закончилась — ползём к нулю.
                        if (StageTimeLeft <= 0f)
                        {
                            shrinkTo = 0f;
                            SetTimer(settings.collapseTime);
                            ShrinkStarted?.Invoke(Phase);
                        }
                    }
                    else
                    {
                        Radius = Mathf.Lerp(shrinkFrom, shrinkTo, Ease(1f - StageTimeLeft / StageDuration));
                        if (StageTimeLeft <= 0f) { Radius = 0f; StageTimeLeft = 0f; }
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
        public float SignedDistanceInside(Vector2 point) => Radius - Vector2.Distance(point, Center);
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
            StageTimeLeft = Mathf.Min(StageTimeLeft, 0.01f);
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
