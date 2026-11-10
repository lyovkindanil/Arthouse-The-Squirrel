using System;
using System.Collections.Generic;
using UnityEngine;

namespace Squirrel.Combat
{
    /// <summary>
    /// Удар хвостом: вбок (по взгляду), вверх (зажат «вверх»), вниз в воздухе (зажат «вниз»).
    /// Удар вниз по врагу подбрасывает белку (отскок), удар вбок слегка отталкивает назад.
    /// Каждое попадание даёт сок.
    /// </summary>
    [RequireComponent(typeof(SquirrelController), typeof(SquirrelInput), typeof(SquirrelHealth))]
    public class TailAttack : MonoBehaviour
    {
        [Tooltip("Полупрозрачный прямоугольник-«взмах» (необязательно)")]
        [SerializeField] SpriteRenderer slash;

        SquirrelController controller;
        SquirrelInput input;
        SquirrelHealth health;
        SquirrelMotor motor;
        float cooldown, buffer, activeTimer;
        readonly HashSet<Hittable> hitThisSwing = new HashSet<Hittable>();
        readonly List<Hittable> found = new List<Hittable>();

        public bool IsSwinging => activeTimer > 0f;
        public Vector2 SwingDirection { get; private set; }
        /// <summary>Сколько целей задел последний взмах.</summary>
        public int LastSwingHits { get; private set; }

        public event Action<Vector2> Swung;
        public event Action<Hittable> Hit;

        CombatSettings S => health.Settings;

        void Awake()
        {
            controller = GetComponent<SquirrelController>();
            input = GetComponent<SquirrelInput>();
            health = GetComponent<SquirrelHealth>();
            motor = GetComponent<SquirrelMotor>();
            if (slash != null) slash.enabled = false;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            cooldown -= dt;
            if (input.AttackPressedThisFrame) buffer = S.tailBuffer;
            else buffer -= dt;

            if (buffer > 0f && cooldown <= 0f && !health.IsDead) StartSwing();
            else if (activeTimer > 0f) DoHits();

            if (activeTimer > 0f)
            {
                activeTimer -= dt;
                if (activeTimer <= 0f && slash != null) slash.enabled = false;
            }
        }

        void StartSwing()
        {
            buffer = 0f;
            cooldown = S.tailCooldown;
            activeTimer = S.tailActiveTime;
            hitThisSwing.Clear();
            LastSwingHits = 0;
            health.CancelHeal();

            Vector2 m = input.Move;
            Vector2 dir;
            if (m.y > 0.5f) dir = Vector2.up;
            else if (m.y < -0.5f && controller.State != SquirrelState.Grounded) dir = Vector2.down;
            else if (controller.State == SquirrelState.OnWall) dir = new Vector2(-controller.Facing, 0f); // со стены — от стены
            else dir = new Vector2(controller.Facing, 0f);
            SwingDirection = dir;

            if (slash != null)
            {
                Rect r = SwingRect();
                slash.enabled = true;
                slash.transform.position = new Vector3(r.center.x, r.center.y, slash.transform.position.z);
                slash.transform.localScale = new Vector3(r.width, r.height, 1f);
            }
            Swung?.Invoke(dir);
            DoHits();
        }

        Rect SwingRect()
        {
            Vector2 c = motor.WorldCenter;
            Vector2 dir = SwingDirection;
            Vector2 size, center;
            if (dir.y == 0f)
            {
                size = S.tailSideSize;
                center = c + new Vector2(dir.x * S.tailSideReach, 0f);
            }
            else
            {
                size = S.tailVerticalSize;
                center = c + new Vector2(0f, dir.y * S.tailVerticalReach);
            }
            return new Rect(center - size * 0.5f, size);
        }

        void DoHits()
        {
            Hittable.Query(SwingRect(), found);
            bool any = false;
            foreach (var h in found)
            {
                if (!hitThisSwing.Add(h)) continue;
                var info = new HitInfo { damage = S.tailDamage, direction = SwingDirection, source = HitSource.Tail };
                if (!h.TakeHit(info)) continue;
                any = true;
                LastSwingHits++;
                if (h.GivesSap) health.AddSap(S.sapPerHit);
                Hit?.Invoke(h);
            }
            if (!any) return;

            HitStop.Freeze(S.hitStopOnHit);
            CameraShake.Add(S.shakeOnHit, 0.12f);
            if (SwingDirection.y < 0f) controller.Pogo(S.pogoVelocity);
            else if (SwingDirection.y == 0f) controller.Recoil(-SwingDirection.x * S.sideRecoil, S.recoilLock);
        }

        void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying || motor == null) return;
            Rect r = SwingRect();
            Gizmos.color = IsSwinging ? Color.yellow : new Color(1f, 1f, 0f, 0.25f);
            Gizmos.DrawWireCube(r.center, r.size);
        }
    }
}
