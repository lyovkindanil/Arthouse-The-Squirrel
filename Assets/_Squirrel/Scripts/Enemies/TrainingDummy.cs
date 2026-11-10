using UnityEngine;
using Squirrel.Combat;

namespace Squirrel.Enemies
{
    /// <summary>Пень-манекен для отработки ударов: не бьёт, не умирает, показывает число попаданий.</summary>
    public class TrainingDummy : Enemy
    {
        [SerializeField] Transform visual;
        float wobble;

        public int HitsTaken { get; private set; }

        protected override Vector2 KnockbackFor(in HitInfo hit) => Vector2.zero;

        public override bool TakeHit(in HitInfo hit)
        {
            base.TakeHit(hit);
            HitsTaken++;
            wobble = 1f;
            if (Hp <= 0) Hp = maxHp; // не умирает
            return true;
        }

        protected override void Die() { Hp = maxHp; }

        protected override void Tick(float dt)
        {
            wobble = Mathf.MoveTowards(wobble, 0f, dt * 4f);
            if (visual != null) visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 40f) * 8f * wobble);
        }
    }
}
