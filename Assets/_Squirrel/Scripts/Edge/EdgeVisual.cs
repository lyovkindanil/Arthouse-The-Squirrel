using UnityEngine;

namespace Squirrel.Edge
{
    /// <summary>
    /// Серобоксовая картинка Края: тёмная «пустота» снаружи круга и светящаяся граница.
    /// Перед сдвигом граница пульсирует, после восстановления Сердца — гаснет.
    /// Настоящий шейдер обесцвечивания сделаем позже, в вертикальном срезе.
    /// </summary>
    [RequireComponent(typeof(EdgeZone))]
    public class EdgeVisual : MonoBehaviour
    {
        [SerializeField] Material material;
        [SerializeField] int segments = 160;
        [SerializeField] float voidThickness = 300f;
        [SerializeField] float lineWidth = 0.25f;
        [SerializeField] Color voidColor = new Color(0.05f, 0.03f, 0.08f, 0.82f);
        [SerializeField] Color lineColor = new Color(0.75f, 0.55f, 1f, 1f);
        [SerializeField] Color warningColor = new Color(1f, 0.3f, 0.35f, 1f);
        [SerializeField] Color shrinkColor = new Color(1f, 0.6f, 0.3f, 1f);
        [SerializeField] int sortingOrder = 100;

        EdgeZone zone;
        Mesh mesh;
        Vector3[] verts;
        LineRenderer line;
        MeshRenderer voidRenderer;
        float lastRadius = -1f;
        Vector2 lastCenter;
        float fade = 1f;
        float lastFade = 1f;

        void Awake()
        {
            zone = GetComponent<EdgeZone>();
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                material = new Material(shader);
            }

            var voidGo = new GameObject("Edge Void");
            voidGo.transform.SetParent(transform, false);
            voidGo.AddComponent<MeshFilter>().sharedMesh = mesh = BuildMesh();
            voidRenderer = voidGo.AddComponent<MeshRenderer>();
            voidRenderer.sharedMaterial = material;
            voidRenderer.sortingOrder = sortingOrder;

            var lineGo = new GameObject("Edge Line");
            lineGo.transform.SetParent(transform, false);
            line = lineGo.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.loop = true;
            line.useWorldSpace = true;
            line.positionCount = segments;
            line.widthMultiplier = lineWidth;
            line.sortingOrder = sortingOrder + 1;
            line.numCapVertices = 0;
        }

        Mesh BuildMesh()
        {
            var m = new Mesh { name = "EdgeVoid" };
            verts = new Vector3[segments * 2];
            var tris = new int[segments * 6];
            var colors = new Color[segments * 2];
            for (int i = 0; i < segments; i++)
            {
                int n = (i + 1) % segments;
                int t = i * 6;
                tris[t] = i * 2; tris[t + 1] = i * 2 + 1; tris[t + 2] = n * 2;
                tris[t + 3] = n * 2; tris[t + 4] = i * 2 + 1; tris[t + 5] = n * 2 + 1;
                // Внутренний край чуть прозрачнее — мягкий переход в пустоту.
                colors[i * 2] = new Color(voidColor.r, voidColor.g, voidColor.b, voidColor.a * 0.6f);
                colors[i * 2 + 1] = voidColor;
            }
            m.vertices = verts;
            m.triangles = tris;
            m.colors = colors;
            m.MarkDynamic();
            return m;
        }

        void LateUpdate()
        {
            float r = zone.Radius;
            Vector2 c = zone.Center;
            if (!Mathf.Approximately(r, lastRadius) || c != lastCenter)
            {
                for (int i = 0; i < segments; i++)
                {
                    float a = i * Mathf.PI * 2f / segments;
                    Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    // Меш в локальных координатах дочернего объекта (он в нуле родителя).
                    Vector3 inner = transform.InverseTransformPoint(c + dir * r);
                    Vector3 outer = transform.InverseTransformPoint(c + dir * (r + voidThickness));
                    verts[i * 2] = inner;
                    verts[i * 2 + 1] = outer;
                    line.SetPosition(i, (Vector3)(c + dir * r));
                }
                mesh.vertices = verts;
                mesh.RecalculateBounds();
                lastRadius = r;
                lastCenter = c;
            }

            Color col = lineColor;
            float width = lineWidth;
            switch (zone.Stage)
            {
                case EdgeStage.Shrink:
                    col = shrinkColor;
                    width = lineWidth * 1.6f;
                    break;
                case EdgeStage.Collapse:
                    col = Color.Lerp(warningColor, Color.white, 0.5f + 0.5f * Mathf.Sin(Time.time * 18f));
                    width = lineWidth * 2f;
                    break;
            }
            if (zone.IsWarning)
            {
                // Чем ближе сдвиг, тем чаще пульс.
                float urgency = 1f - Mathf.Clamp01(zone.StageTimeLeft / zone.Settings.warningTime);
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Lerp(6f, 22f, urgency));
                col = Color.Lerp(lineColor, warningColor, pulse);
                width = lineWidth * (1f + pulse * (0.5f + urgency));
            }

            fade = Mathf.MoveTowards(fade, zone.Stage == EdgeStage.Stabilized ? 0f : 1f, Time.deltaTime * 0.7f);
            col.a *= fade;
            line.startColor = line.endColor = col;
            line.widthMultiplier = width;
            voidRenderer.enabled = fade > 0.001f;
            line.enabled = fade > 0.001f;
            if (voidRenderer.enabled && !Mathf.Approximately(fade, lastFade))
            {
                lastFade = fade;
                var colors = mesh.colors;
                for (int i = 0; i < segments; i++)
                {
                    colors[i * 2] = new Color(voidColor.r, voidColor.g, voidColor.b, voidColor.a * 0.6f * fade);
                    colors[i * 2 + 1] = new Color(voidColor.r, voidColor.g, voidColor.b, voidColor.a * fade);
                }
                mesh.colors = colors;
            }
        }
    }
}
