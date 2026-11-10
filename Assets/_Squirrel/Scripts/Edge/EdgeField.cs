using System.Collections.Generic;
using UnityEngine;

namespace Squirrel.Edge
{
    /// <summary>
    /// Карта расстояний «по пути» для Края. Строится из той же текстовой карты, что и уровень:
    /// расстояние считается по проходам (через пустые клетки), а не по прямой сквозь камень.
    /// Одно поле — от Сердца (общее кольцо Края), и по полю от каждого очага Края (символ * на карте).
    /// </summary>
    public class EdgeField : MonoBehaviour
    {
        [Tooltip("Текстовая карта локации (та же, из которой собран уровень)")]
        [SerializeField] TextAsset map;
        [SerializeField] Transform heart;

        int width, height;
        bool[] solid;
        float[] heartDist;
        readonly List<float[]> frontDist = new List<float[]>();
        readonly List<Vector2> sources = new List<Vector2>();

        public int Width => width;
        public int Height => height;
        public bool Ready => heartDist != null;
        public int FrontCount => frontDist.Count;
        public IReadOnlyList<Vector2> Sources => sources;
        /// <summary>Самое дальнее от Сердца место, куда можно дойти.</summary>
        public float MaxHeartDistance { get; private set; }

        void Awake() => Build();

        public void Setup(TextAsset mapAsset, Transform heartTransform)
        {
            map = mapAsset;
            heart = heartTransform;
        }

        public void Build()
        {
            if (map == null) return;
            var rows = new List<string>();
            bool inMap = false;
            foreach (var raw in map.text.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (inMap) { if (line.Length > 0) rows.Add(line); }
                else if (line.Trim() == "map") inMap = true;
            }
            height = rows.Count;
            width = 0;
            foreach (var r in rows) width = Mathf.Max(width, r.Length);
            solid = new bool[width * height];
            sources.Clear();
            for (int row = 0; row < height; row++)
            {
                int y = height - 1 - row;
                for (int x = 0; x < width; x++)
                {
                    char c = x < rows[row].Length ? rows[row][x] : '#';
                    solid[y * width + x] = c == '#' || c == 'B' || c == 'R';
                    if (c == '*') sources.Add(new Vector2(x + 0.5f, y + 0.5f));
                }
            }

            Vector2 h = heart != null ? (Vector2)heart.position : new Vector2(width * 0.5f, height * 0.5f);
            heartDist = Flood(h);
            MaxHeartDistance = 0f;
            for (int i = 0; i < heartDist.Length; i++)
                if (!solid[i] && heartDist[i] < float.MaxValue) MaxHeartDistance = Mathf.Max(MaxHeartDistance, heartDist[i]);
            frontDist.Clear();
            foreach (var s in sources) frontDist.Add(Flood(s));
        }

        /// <summary>Расстояние по пути от точки source до каждой клетки (Дейкстра, 8 соседей, без срезания углов).</summary>
        float[] Flood(Vector2 source)
        {
            var dist = new float[width * height];
            for (int i = 0; i < dist.Length; i++) dist[i] = float.MaxValue;
            int sx = Mathf.Clamp(Mathf.FloorToInt(source.x), 0, width - 1);
            int sy = Mathf.Clamp(Mathf.FloorToInt(source.y), 0, height - 1);
            var heap = new MinHeap();
            int s = sy * width + sx;
            dist[s] = 0f;
            heap.Push(s, 0f);
            while (heap.Count > 0)
            {
                heap.Pop(out int cur, out float d);
                if (d > dist[cur]) continue;
                int cx = cur % width, cy = cur / width;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = cx + dx, ny = cy + dy;
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                    int n = ny * width + nx;
                    if (solid[n]) continue;
                    if (dx != 0 && dy != 0 && (solid[cy * width + nx] || solid[ny * width + cx])) continue;
                    float nd = d + (dx != 0 && dy != 0 ? 1.4142f : 1f);
                    if (nd < dist[n]) { dist[n] = nd; heap.Push(n, nd); }
                }
            }
            // Камень у прохода получает значение соседней пустоты (+1), чтобы у стен не было «дыр».
            for (int pass = 0; pass < 2; pass++)
            {
                var copy = (float[])dist.Clone();
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;
                    if (!solid[i] || dist[i] < float.MaxValue) continue;
                    float best = float.MaxValue;
                    if (x > 0) best = Mathf.Min(best, dist[i - 1]);
                    if (x < width - 1) best = Mathf.Min(best, dist[i + 1]);
                    if (y > 0) best = Mathf.Min(best, dist[i - width]);
                    if (y < height - 1) best = Mathf.Min(best, dist[i + width]);
                    if (best < float.MaxValue) copy[i] = best + 1f;
                }
                dist = copy;
            }
            return dist;
        }

        public float HeartDistance(Vector2 p) => Sample(heartDist, p);
        public float FrontDistance(int front, Vector2 p) => Sample(frontDist[front], p);

        /// <summary>Билинейная выборка по клеткам. float.MaxValue — недостижимо (глубокий камень, другая часть мира).</summary>
        public float Sample(float[] field, Vector2 p)
        {
            if (field == null) return float.MaxValue;
            float fx = p.x - 0.5f, fy = p.y - 0.5f;
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0, ty = fy - y0;
            float a = Cell(field, x0, y0), b = Cell(field, x0 + 1, y0);
            float c = Cell(field, x0, y0 + 1), d = Cell(field, x0 + 1, y0 + 1);
            // Если часть соседей недостижима — берём ближайшую достижимую клетку.
            if (a >= float.MaxValue || b >= float.MaxValue || c >= float.MaxValue || d >= float.MaxValue)
            {
                int cx = Mathf.Clamp(Mathf.FloorToInt(p.x), 0, width - 1), cy = Mathf.Clamp(Mathf.FloorToInt(p.y), 0, height - 1);
                float own = Cell(field, cx, cy);
                if (own < float.MaxValue) return own;
                return Mathf.Min(Mathf.Min(a, b), Mathf.Min(c, d));
            }
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        float Cell(float[] f, int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return float.MaxValue;
            return f[y * width + x];
        }

        public bool IsSolidCell(int x, int y) => x < 0 || y < 0 || x >= width || y >= height || solid[y * width + x];

        /// <summary>Простая двоичная куча для Дейкстры.</summary>
        class MinHeap
        {
            readonly List<int> items = new List<int>();
            readonly List<float> keys = new List<float>();
            public int Count => items.Count;

            public void Push(int item, float key)
            {
                items.Add(item); keys.Add(key);
                int i = items.Count - 1;
                while (i > 0)
                {
                    int p = (i - 1) / 2;
                    if (keys[p] <= keys[i]) break;
                    Swap(i, p); i = p;
                }
            }

            public void Pop(out int item, out float key)
            {
                item = items[0]; key = keys[0];
                int last = items.Count - 1;
                items[0] = items[last]; keys[0] = keys[last];
                items.RemoveAt(last); keys.RemoveAt(last);
                int i = 0;
                while (true)
                {
                    int l = 2 * i + 1, r = l + 1, m = i;
                    if (l < items.Count && keys[l] < keys[m]) m = l;
                    if (r < items.Count && keys[r] < keys[m]) m = r;
                    if (m == i) break;
                    Swap(i, m); i = m;
                }
            }

            void Swap(int a, int b)
            {
                (items[a], items[b]) = (items[b], items[a]);
                (keys[a], keys[b]) = (keys[b], keys[a]);
            }
        }
    }
}
