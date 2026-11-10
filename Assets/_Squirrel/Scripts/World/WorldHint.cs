using System.Collections.Generic;
using UnityEngine;

namespace Squirrel.World
{
    /// <summary>
    /// Короткая подпись над объектом: появляется, когда белка рядом, и гаснет, когда отходит.
    /// Рисует её HUD. Объект выключен или уничтожен — подписи нет.
    /// Пока это серый бокс, подписи объясняют, что делает каждая штука; в срезе их заменит язык формы и цвета.
    /// </summary>
    public class WorldHint : MonoBehaviour
    {
        static readonly List<WorldHint> all = new List<WorldHint>();
        public static IReadOnlyList<WorldHint> All => all;

        [TextArea] [SerializeField] string text;
        [SerializeField] Vector2 offset = new Vector2(0f, 1.4f);
        [SerializeField] float showRadius = 4.5f;
        [SerializeField] Color color = new Color(1f, 0.95f, 0.8f, 1f);

        public string Text { get => text; set => text = value; }
        public Vector2 WorldPoint => (Vector2)transform.position + offset;
        public float ShowRadius => showRadius;
        public Color Color => color;

        public void Setup(string hintText, Vector2 hintOffset, float radius, Color hintColor)
        {
            text = hintText; offset = hintOffset; showRadius = radius; color = hintColor;
        }

        /// <summary>0..1: насколько видна подпись для белки в точке from.</summary>
        public float Visibility(Vector2 from)
        {
            float d = Vector2.Distance(from, (Vector2)transform.position);
            return Mathf.Clamp01((showRadius - d) / 1.5f);
        }

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);
    }
}
