using System;
using System.Collections.Generic;
using UnityEngine;

namespace Squirrel.World
{
    /// <summary>
    /// Комната локации: прямоугольник для камеры и название. Камера не выезжает за границы текущей комнаты.
    /// </summary>
    public class Room : MonoBehaviour
    {
        static readonly List<Room> all = new List<Room>();

        [SerializeField] string id;
        [SerializeField] string title;
        [Tooltip("Размер комнаты; позиция объекта — левый нижний угол")]
        [SerializeField] Vector2 size = new Vector2(32, 18);

        public string Id => id;
        public string Title => title;
        public Rect Bounds => new Rect(transform.position, size);
        public static IReadOnlyList<Room> All => all;

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        public static Room At(Vector2 point)
        {
            foreach (var r in all)
                if (r.Bounds.Contains(point)) return r;
            return null;
        }

        public void Setup(string roomId, string roomTitle, Vector2 roomSize)
        {
            id = roomId;
            title = roomTitle;
            size = roomSize;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.35f);
            var b = Bounds;
            Gizmos.DrawWireCube(b.center, b.size);
        }
    }
}
