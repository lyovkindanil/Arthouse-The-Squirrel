using System;
using System.Collections.Generic;
using UnityEngine;

namespace Squirrel.World
{
    /// <summary>
    /// Состояние мира: флаги («гусеница спасена», «способность открыта», «воспоминание собрано»)
    /// и две скрытые шкалы выбора. Один на сцену локации; объекты сами читают флаги и подписываются на изменения.
    /// </summary>
    public class WorldState : MonoBehaviour
    {
        readonly HashSet<string> flags = new HashSet<string>();

        public static WorldState Current { get; private set; }

        /// <summary>Шкала «Явь — Сон»: меньше нуля — белка цепляется за свою реальность, больше — растворяется в сне.</summary>
        public float Dream { get; private set; }
        /// <summary>Шкала «Дикость — Сострадание»: меньше нуля — выживает за счёт других, больше — вместе с ними.</summary>
        public float Compassion { get; private set; }

        public IEnumerable<string> Flags => flags;
        public event Action<string> FlagSet;
        public event Action Changed;

        void Awake() => Current = this;
        void OnDestroy() { if (Current == this) Current = null; }

        public bool Has(string flag) => !string.IsNullOrEmpty(flag) && flags.Contains(flag);

        public void Set(string flag)
        {
            if (string.IsNullOrEmpty(flag) || !flags.Add(flag)) return;
            FlagSet?.Invoke(flag);
            Changed?.Invoke();
        }

        public void AddDream(float delta) { Dream += delta; Changed?.Invoke(); }
        public void AddCompassion(float delta) { Compassion += delta; Changed?.Invoke(); }

        /// <summary>Загрузка из сохранения.</summary>
        public void Load(IEnumerable<string> savedFlags, float dream, float compassion)
        {
            Dream = dream;
            Compassion = compassion;
            foreach (var f in savedFlags) Set(f);
            Changed?.Invoke();
        }

        /// <summary>Удобно для объектов, которым состояние мира не обязательно (серые комнаты без WorldState).</summary>
        public static bool HasFlag(string flag) => Current != null && Current.Has(flag);
        public static void SetFlag(string flag) { if (Current != null) Current.Set(flag); }
    }
}
