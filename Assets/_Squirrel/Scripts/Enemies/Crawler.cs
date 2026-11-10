using UnityEngine;
using Squirrel.Combat;

namespace Squirrel.Enemies
{
    /// <summary>
    /// Моховик — ползёт по земле туда-сюда, разворачивается у стен и обрывов. Опасен касанием.
    /// Самый простой враг: учит бить вбок и отскакивать от врагов ударом вниз.
    /// </summary>
    public class Crawler : Enemy
    {
        [SerializeField] float speed = 2.2f;
        [SerializeField] float gravity = 35f;
        [Tooltip("1 — вправо, -1 — влево")]
        [SerializeField] int startDirection = -1;
        [Tooltip("Дочерний объект со спрайтами: отражаем по X")]
        [SerializeField] Transform visual;

        int dir;
        float vy;
        bool grounded;

        public int Direction => dir;

        protected override void Awake()
        {
            base.Awake();
            dir = startDirection >= 0 ? 1 : -1;
        }

        protected override void OnReset()
        {
            dir = startDirection >= 0 ? 1 : -1;
            vy = 0f;
        }

        // Удар сверху/снизу не сдвигает моховика вбок — только вбок.
        protected override Vector2 KnockbackFor(in HitInfo hit)
            => hit.direction.x == 0f ? Vector2.zero : new Vector2(Mathf.Sign(hit.direction.x), 0f);

        protected override void Tick(float dt)
        {
            vy = Mathf.Max(vy - gravity * dt, -20f);
            bool knocked = knock.sqrMagnitude > 0.01f;
            float vx = knocked ? knock.x : dir * speed;
            knock = Vector2.MoveTowards(knock, Vector2.zero, 40f * dt);

            MoveBody(new Vector2(vx * dt, vy * dt), out bool hitX, out bool hitY);
            if (hitY) { grounded = vy < 0f; vy = 0f; }
            else grounded = false;

            if (!knocked && grounded && (hitX || !GroundAhead())) dir = -dir;

            if (visual != null)
            {
                var s = visual.localScale;
                visual.localScale = new Vector3(Mathf.Abs(s.x) * dir, s.y, s.z);
            }
        }

        bool GroundAhead()
        {
            Vector2 c = HitCenter;
            Vector2 foot = new Vector2(c.x + dir * (hitSize.x * 0.5f + 0.1f), c.y - hitSize.y * 0.5f + 0.05f);
            return KinematicBox.Raycast(foot, Vector2.down, 0.4f, out _);
        }
    }
}
