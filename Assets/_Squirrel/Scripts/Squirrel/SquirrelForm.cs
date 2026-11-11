using System;
using UnityEngine;

namespace Squirrel
{
    /// <summary>
    /// Облик белки: набор поз, окраска и свечение. У каждого уровня может быть свой облик —
    /// меняется вызовом SquirrelSpriteAnimator.SetForm. Позы собирает сборщик сцен из
    /// Assets/_Squirrel/Art/Squirrel/&lt;облик&gt;/form.json.
    /// </summary>
    [CreateAssetMenu(menuName = "Squirrel/Squirrel Form (облик белки)")]
    public class SquirrelForm : ScriptableObject
    {
        [Serializable]
        public struct Pose
        {
            public Sprite sprite;
            [Tooltip("Центр туловища относительно пивота спрайта, в единицах мира. Вокруг него колышется хвост")]
            public Vector2 anchor;
            [Tooltip("Основание хвоста относительно пивота. Вокруг него хвост поворачивается при ударе")]
            public Vector2 tailBase;
        }

        public string id = "spirit";
        public Pose idle, run, jump, cling;

        [Header("Окраска")]
        [Tooltip("Множитель цвета спрайта: белый — как нарисовано")]
        public Color tint = Color.white;
        public Color glowColor = new Color(0.45f, 0.7f, 1f, 0.22f);
        public Vector2 glowSize = new Vector2(3.2f, 2.8f);

        public Pose Get(int index)
        {
            switch (index)
            {
                case 1: return run;
                case 2: return jump;
                case 3: return cling;
                default: return idle;
            }
        }
    }
}
