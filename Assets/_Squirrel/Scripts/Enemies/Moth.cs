using UnityEngine;
using Squirrel.Combat;

namespace Squirrel.Enemies
{
    /// <summary>
    /// Споровая моль — парит у своего места, заметив белку, замирает (предупреждение) и пикирует
    /// в точку, где белка была. Потом возвращается. Учит уворачиваться и бить вверх.
    /// </summary>
    public class Moth : Enemy
    {
        public enum MothState { Hover, Windup, Swoop, Return }

        [SerializeField] float aggroRadius = 7f;
        [SerializeField] float windupTime = 0.45f;
        [SerializeField] float swoopSpeed = 11f;
        [SerializeField] float swoopMaxTime = 0.9f;
        [SerializeField] float returnSpeed = 3.5f;
        [SerializeField] float cooldown = 1.2f;
        [SerializeField] float hoverAmplitude = 0.4f;
        [SerializeField] Transform visual;

        Vector2 home;
        Vector2 swoopDir;
        float timer, cooldownTimer, phase;

        public MothState State { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            home = transform.position;
            phase = Random.value * 10f;
        }

        protected override void OnReset()
        {
            home = transform.position;
            State = MothState.Hover;
            timer = cooldownTimer = 0f;
            if (visual != null) visual.localPosition = Vector3.zero;
        }

        protected override void OnHurt(in HitInfo hit)
        {
            // Удар сбивает пике.
            if (State == MothState.Swoop || State == MothState.Windup) { State = MothState.Return; cooldownTimer = cooldown; }
        }

        protected override void Tick(float dt)
        {
            cooldownTimer -= dt;
            Vector2 pos = transform.position;
            Vector2 delta = Vector2.zero;

            if (knock.sqrMagnitude > 0.01f)
            {
                delta += knock * dt;
                knock = Vector2.MoveTowards(knock, Vector2.zero, 30f * dt);
            }

            switch (State)
            {
                case MothState.Hover:
                    phase += dt;
                    Vector2 hoverPoint = home + new Vector2(Mathf.Sin(phase * 0.9f) * hoverAmplitude * 2f, Mathf.Sin(phase * 2.1f) * hoverAmplitude);
                    delta += Vector2.ClampMagnitude(hoverPoint - pos, returnSpeed * dt);
                    if (cooldownTimer <= 0f && SeesTarget())
                    {
                        State = MothState.Windup;
                        timer = windupTime;
                    }
                    break;

                case MothState.Windup:
                    timer -= dt;
                    if (visual != null) visual.localPosition = Random.insideUnitCircle * 0.08f; // дрожит перед броском
                    if (timer <= 0f)
                    {
                        if (visual != null) visual.localPosition = Vector3.zero;
                        Vector2 to = HasTarget ? TargetCenter - HitCenter : Vector2.down;
                        swoopDir = to.sqrMagnitude > 0.001f ? to.normalized : Vector2.down;
                        State = MothState.Swoop;
                        timer = swoopMaxTime;
                    }
                    break;

                case MothState.Swoop:
                    timer -= dt;
                    delta += swoopDir * swoopSpeed * dt;
                    break;

                case MothState.Return:
                    delta += Vector2.ClampMagnitude(home - pos, returnSpeed * dt);
                    if (Vector2.Distance(pos, home) < 0.2f) { State = MothState.Hover; cooldownTimer = cooldown; }
                    break;
            }

            MoveBody(delta, out bool hitX, out bool hitY);
            if (State == MothState.Swoop && (hitX || hitY || timer <= 0f))
                State = MothState.Return;
        }

        bool SeesTarget()
        {
            if (!HasTarget) return false;
            Vector2 t = TargetCenter;
            return Vector2.Distance(HitCenter, t) <= aggroRadius && KinematicBox.LineOfSight(HitCenter, t);
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            Gizmos.color = new Color(0.6f, 0.9f, 1f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, aggroRadius);
        }
    }
}
