using System;
using UnityEngine;

namespace Squirrel
{
    /// <summary>
    /// Покадровые анимации белки, отрендеренные из 3D-модели (Tools/SquirrelBlender).
    /// Ассет собирает сборщик сцен из Assets/_Squirrel/Art/Squirrel/&lt;облик&gt;/clips.json и листов кадров рядом с ним.
    /// </summary>
    public class SquirrelClips : ScriptableObject
    {
        [Serializable]
        public class Clip
        {
            public string name;
            public Sprite[] frames;
            public float fps = 30f;
            public bool loop = true;
            [Tooltip("Где пивот кадра относительно точки под лапами белки, в единицах мира")]
            public Vector2 offset;

            public int Length => frames != null ? frames.Length : 0;
        }

        public string id = "spirit3d";
        [Tooltip("Отпечаток clips.json, из которого собран ассет: если файл изменился, сборщик пересоберёт кадры")]
        public string source;
        public Clip[] clips = new Clip[0];

        [Header("Свечение")]
        public Color glowColor = new Color(0.45f, 0.7f, 1f, 0.22f);
        public Vector2 glowSize = new Vector2(3.2f, 2.8f);

        public Clip Find(string clipName)
        {
            for (int i = 0; i < clips.Length; i++)
                if (clips[i] != null && clips[i].name == clipName) return clips[i];
            return null;
        }
    }
}
