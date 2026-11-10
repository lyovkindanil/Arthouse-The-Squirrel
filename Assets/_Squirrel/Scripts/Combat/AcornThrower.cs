using System;
using UnityEngine;

namespace Squirrel.Combat
{
    /// <summary>
    /// Бросок жёлудя — дальний удар. Жёлудей мало (3), они восстанавливаются со временем и в гнезде.
    /// Сока жёлуди не дают: лечиться можно только ближним боем.
    /// </summary>
    [RequireComponent(typeof(SquirrelController), typeof(SquirrelInput), typeof(SquirrelHealth))]
    public class AcornThrower : MonoBehaviour
    {
        [SerializeField] Sprite sprite;
        [SerializeField] Material material;

        SquirrelController controller;
        SquirrelInput input;
        SquirrelHealth health;
        SquirrelMotor motor;
        float cooldown, rechargeTimer;

        public int Ammo { get; private set; }
        public int MaxAmmo => health.Settings.maxAcorns;
        /// <summary>0..1 — сколько осталось до следующего жёлудя.</summary>
        public float RechargeProgress => Ammo >= MaxAmmo ? 0f : rechargeTimer / Mathf.Max(0.01f, health.Settings.acornRecharge);

        public event Action<Acorn> Thrown;

        void Awake()
        {
            controller = GetComponent<SquirrelController>();
            input = GetComponent<SquirrelInput>();
            health = GetComponent<SquirrelHealth>();
            motor = GetComponent<SquirrelMotor>();
        }

        void Start() => Ammo = MaxAmmo;

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            var s = health.Settings;
            cooldown -= dt;

            if (Ammo < MaxAmmo)
            {
                rechargeTimer += dt;
                if (rechargeTimer >= s.acornRecharge) { Ammo++; rechargeTimer = 0f; }
            }
            else rechargeTimer = 0f;

            if (input.ThrowPressedThisFrame && cooldown <= 0f && Ammo > 0 && !health.IsDead) Throw();
        }

        void Throw()
        {
            var s = health.Settings;
            cooldown = s.acornCooldown;
            Ammo--;
            health.CancelHeal();

            Vector2 dir;
            if (input.Move.y > 0.5f) dir = Vector2.up;
            else if (controller.State == SquirrelState.OnWall) dir = new Vector2(-controller.Facing, 0f);
            else dir = new Vector2(controller.Facing, 0f);

            Vector2 from = motor.WorldCenter + dir * 0.3f;
            var acorn = Acorn.Spawn(from, dir * s.acornSpeed, s, sprite, material);
            Thrown?.Invoke(acorn);
        }

        public void Refill()
        {
            Ammo = MaxAmmo;
            rechargeTimer = 0f;
        }
    }
}
