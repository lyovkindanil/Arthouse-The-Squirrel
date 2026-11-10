using System;
using UnityEngine;

namespace Squirrel
{
    public enum Ability { WallJump, Glide }

    /// <summary>
    /// Открытые способности белки. Стартовые значения задаются на сцене
    /// (в серой комнате всё открыто для проверки, в локации — ничего), дальше их выдают святилища.
    /// </summary>
    public class SquirrelAbilities : MonoBehaviour
    {
        [Tooltip("Отталкивание от стен и цепляние за камень")]
        [SerializeField] bool startWithWallJump;
        [Tooltip("Планирование на хвосте")]
        [SerializeField] bool startWithGlide;

        public bool WallJump { get; private set; }
        public bool Glide { get; private set; }

        public event Action<Ability> Unlocked;

        void Awake()
        {
            WallJump = startWithWallJump;
            Glide = startWithGlide;
        }

        public bool Has(Ability a) => a == Ability.WallJump ? WallJump : Glide;

        public void Set(Ability a, bool value)
        {
            bool was = Has(a);
            if (a == Ability.WallJump) WallJump = value; else Glide = value;
            if (value && !was) Unlocked?.Invoke(a);
        }

        public static string FlagFor(Ability a) => "ability." + a.ToString().ToLowerInvariant();
    }
}
