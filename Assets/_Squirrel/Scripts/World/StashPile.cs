using System;
using UnityEngine;
using Squirrel.Combat;

namespace Squirrel.World
{
    /// <summary>Чужой запас (листья, ягоды). Удар хвостом — забрать.</summary>
    public class StashPile : Hittable
    {
        [SerializeField] GameObject visual;

        public bool Taken { get; private set; }
        public event Action TakenEvent;

        public override bool GivesSap => false;
        public override bool CanBeHit => base.CanBeHit && !Taken;

        public override bool TakeHit(in HitInfo hit)
        {
            if (Taken) return false;
            Hide();
            TakenEvent?.Invoke();
            return true;
        }

        public void Hide()
        {
            Taken = true;
            if (visual != null) visual.SetActive(false);
        }
    }
}
