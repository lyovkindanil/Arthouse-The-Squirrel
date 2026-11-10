using UnityEngine;

namespace Squirrel.Edge
{
    /// <summary>
    /// Картинка Края для локации с EdgeField: пустота закрашивается по клеткам, поэтому её граница
    /// идёт по проходам, а не кругом. Общее кольцо светится сиреневым, очаги — оранжевым,
    /// активный (давящий сейчас) очаг — ярче. Перед сдвигом всё пульсирует красным.
    /// Текстура 2 пикселя на клетку, перерисовывается не чаще refreshRate раз в секунду и только если Край сдвинулся.
    /// </summary>
    [RequireComponent(typeof(EdgeZone))]
    public class EdgeFieldVisual : MonoBehaviour
    {
        [SerializeField] Material material;
        [SerializeField] int pixelsPerCell = 2;
        [SerializeField] float refreshRate = 15f;
        [SerializeField] float lineWidth = 0.35f;
        [SerializeField] float glowWidth = 1.6f;
        [SerializeField] Color voidColor = new Color(0.05f, 0.03f, 0.08f, 0.86f);
        [SerializeField] Color ringColor = new Color(0.75f, 0.55f, 1f, 1f);
        [SerializeField] Color frontColor = new Color(1f, 0.55f, 0.25f, 1f);
        [SerializeField] Color warningColor = new Color(1f, 0.3f, 0.35f, 1f);
        [SerializeField] int sortingOrder = 100;

        EdgeZone zone;
        EdgeField field;
        Texture2D tex;
        Color32[] pixels;
        SpriteRenderer sr;
        float nextRefresh;
        float lastHash = float.NaN;
        float fade = 1f;

        void Awake()
        {
            zone = GetComponent<EdgeZone>();
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                material = new Material(shader);
            }
        }

        void Start()
        {
            field = zone.Field;
            if (field == null || !field.Ready) { enabled = false; return; }
            int w = field.Width * pixelsPerCell, h = field.Height * pixelsPerCell;
            tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "EdgeField" };
            pixels = new Color32[w * h];
            var go = new GameObject("Edge Field Void");
            go.transform.SetParent(transform, false);
            go.transform.position = Vector3.zero; // клетка (0,0) карты — мировые (0,0)
            sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, w, h), Vector2.zero, pixelsPerCell);
            sr.sharedMaterial = material;
            sr.sortingOrder = sortingOrder;
            Redraw();
        }

        float Hash()
        {
            float hsh = zone.Radius * 1.37f + (int)zone.Stage * 1000f;
            for (int i = 0; i < zone.FrontCount; i++)
                hsh += zone.FrontRadius(i) * (i + 2.11f) + (zone.IsFrontPressing(i) ? 500f * (i + 1) : 0f);
            return hsh;
        }

        void LateUpdate()
        {
            if (sr == null) return;
            fade = Mathf.MoveTowards(fade, zone.Stage == EdgeStage.Stabilized ? 0f : 1f, Time.deltaTime * 0.7f);
            sr.enabled = fade > 0.001f;

            // Пульс предупреждения — тоном всего спрайта, без перерисовки.
            Color tint = Color.white;
            if (zone.IsWarning)
            {
                float urgency = 1f - Mathf.Clamp01(zone.StageTimeLeft / zone.Settings.warningTime);
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Lerp(6f, 22f, urgency));
                tint = Color.Lerp(Color.white, warningColor * 1.3f, pulse * 0.6f);
            }
            else if (zone.Stage == EdgeStage.Collapse)
                tint = Color.Lerp(Color.white, warningColor * 1.3f, 0.5f + 0.5f * Mathf.Sin(Time.time * 18f));
            tint.a = fade;
            sr.color = tint;

            if (Time.unscaledTime < nextRefresh) return;
            float hsh = Hash();
            if (Mathf.Abs(hsh - lastHash) < 0.01f) return;
            nextRefresh = Time.unscaledTime + 1f / refreshRate;
            Redraw();
        }

        public void Redraw()
        {
            lastHash = Hash();
            int w = tex.width, h = tex.height;
            float step = 1f / pixelsPerCell;
            for (int py = 0; py < h; py++)
            {
                int cy = py / pixelsPerCell;
                for (int px = 0; px < w; px++)
                {
                    int cx = px / pixelsPerCell;
                    var p = new Vector2((px + 0.5f) * step, (py + 0.5f) * step);
                    // Глубокий камень (до него не дойти) не закрашиваем: камень и так камень.
                    if (field.IsSolidCell(cx, cy) && field.HeartDistance(p) >= float.MaxValue)
                    {
                        pixels[py * w + px] = default;
                        continue;
                    }
                    float inside = zone.Evaluate(p, out int source);
                    pixels[py * w + px] = Shade(inside, source);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false);
        }

        Color32 Shade(float inside, int source)
        {
            bool front = source >= 0;
            bool pressing = front && zone.IsFrontPressing(source);
            Color line = front ? (pressing ? Color.Lerp(frontColor, Color.white, 0.25f) : frontColor) : ringColor;
            if (inside < 0f)
            {
                // Пустота густеет за пару клеток от границы; у очагов она тёплая.
                float k = Mathf.Clamp01(-inside / 2.5f);
                Color v = front ? Color.Lerp(voidColor, new Color(0.25f, 0.08f, 0.03f, voidColor.a), 0.35f) : voidColor;
                if (inside > -lineWidth) return Color.Lerp(line, v, -inside / lineWidth);
                v.a *= Mathf.Lerp(0.75f, 1f, k);
                return v;
            }
            if (inside < glowWidth)
            {
                // Мягкое свечение с внутренней стороны — видно, откуда давит.
                float a = (1f - inside / glowWidth);
                a = a * a * (pressing ? 0.55f : 0.35f);
                return new Color(line.r, line.g, line.b, a);
            }
            return default;
        }

        void OnDestroy()
        {
            if (tex != null) Destroy(tex);
        }
    }
}
