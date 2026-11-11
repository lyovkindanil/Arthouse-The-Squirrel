using UnityEngine;
using Squirrel.Combat;

namespace Squirrel
{
    /// <summary>
    /// Плавная анимация белки из нескольких нарисованных поз (стоит, бежит, прыжок, на стене).
    /// Кадров между позами нет, поэтому движение делается кодом:
    ///  • позы перетекают одна в другую;
    ///  • спрайт лежит на сетке, дальние от туловища точки (хвост, кончики ушей) колышутся
    ///    и отстают от движения — хвост «тянется» за белкой;
    ///  • при ударе хвост хлещет в сторону удара;
    ///  • тело дышит стоя, покачивается на бегу, наклоняется в падении;
    ///  • сзади мягко пульсирует свечение.
    /// Облик (позы, окраска, свечение) задаётся SquirrelForm и меняется через SetForm.
    /// </summary>
    public class SquirrelSpriteAnimator : MonoBehaviour
    {
        const int Idle = 0, Run = 1, Jump = 2, Cling = 3, PoseCount = 4;

        [SerializeField] SquirrelController controller;
        [SerializeField] TailAttack attack;
        [SerializeField] SquirrelForm form;
        [Tooltip("Два меша: текущая поза и уходящая")]
        [SerializeField] MeshFilter layerA;
        [SerializeField] MeshFilter layerB;
        [SerializeField] SpriteRenderer glow;
        [SerializeField] int sortingOrder = 110;
        [SerializeField] int grid = 18;

        [Header("Переходы")]
        [SerializeField] float crossfade = 0.1f;
        [SerializeField] float runSpeedThreshold = 0.6f;

        [Header("Хвост и уши")]
        [Tooltip("До этого расстояния от центра туловища всё жёсткое")]
        [SerializeField] float rigidRadius = 0.32f;
        [Tooltip("С этого расстояния точки колышутся в полную силу")]
        [SerializeField] float looseRadius = 1.15f;
        [SerializeField] float waveAmplitude = 0.045f;
        [SerializeField] float waveSpeed = 3.2f;
        [SerializeField] float waveLength = 4.5f;
        [Tooltip("Насколько хвост отстаёт от движения (ед. на ед./с скорости)")]
        [SerializeField] float lag = 0.018f;
        [SerializeField] float maxLag = 0.22f;
        [SerializeField] float lagSharpness = 9f;

        [Header("Удар хвостом")]
        [SerializeField] float whipTime = 0.24f;
        [SerializeField] float whipSideAngle = -125f;
        [SerializeField] float whipUpAngle = -70f;
        [SerializeField] float whipDownAngle = 100f;
        [Tooltip("Ширина «шарнира» у основания хвоста: на этой длине поворот набирает полную силу")]
        [SerializeField] float tailHinge = 0.22f;
        [Tooltip("Кончик хвоста доворачивается сильнее основания — хвост гнётся, а не машет палкой")]
        [SerializeField] float whipCurl = 0.25f;

        [Header("Тело")]
        [SerializeField] float breathe = 0.014f;
        [SerializeField] float strideLength = 1.6f;
        [SerializeField] float runBob = 0.06f;
        [SerializeField] float runTilt = 5f;
        [SerializeField] float fallTiltPerSpeed = 2.2f;
        [SerializeField] float maxFallTilt = 32f;

        [Header("Свечение")]
        [SerializeField] float glowPulse = 0.18f;
        [SerializeField] float glowPulseSpeed = 1.6f;

        class PoseData
        {
            public Texture texture;
            public Vector3[] rest;
            public Vector2[] uv;
            public int[] tris;
            public float[] loose, tail;
            public Vector2 anchor, tailBase;
        }

        class Layer
        {
            public MeshFilter filter;
            public MeshRenderer renderer;
            public Mesh mesh;
            public MaterialPropertyBlock block;
            public PoseData pose;
            public Vector3[] verts;
            public Color[] colors;
            public float alpha;
        }

        readonly PoseData[] poses = new PoseData[PoseCount];
        Layer current, previous;
        int poseIndex = -1;
        float fade = 1f, clock, stride, whipClock = -1f, whipAngle, tilt, bob;
        Vector2 lagVelocity;
        static readonly int MainTex = Shader.PropertyToID("_MainTex");

        public SquirrelForm Form => form;

        void Awake()
        {
            current = MakeLayer(layerA);
            previous = MakeLayer(layerB);
            if (form != null) SetForm(form);
        }

        void OnEnable() { if (attack != null) attack.Swung += OnSwung; }
        void OnDisable() { if (attack != null) attack.Swung -= OnSwung; }

        /// <summary>Сменить облик белки (другой уровень — другая расцветка или другие позы).</summary>
        public void SetForm(SquirrelForm newForm)
        {
            form = newForm;
            if (form == null || current == null) return;
            for (int i = 0; i < PoseCount; i++) poses[i] = BuildPose(form.Get(i));
            poseIndex = -1;
            previous.alpha = 0f;
            previous.pose = null;
            Show(PickPose(), instant: true);
            if (glow != null)
            {
                glow.color = form.glowColor;
                glow.transform.localScale = new Vector3(form.glowSize.x, form.glowSize.y, 1f);
            }
        }

        Layer MakeLayer(MeshFilter filter)
        {
            if (filter == null) return null;
            var l = new Layer
            {
                filter = filter,
                renderer = filter.GetComponent<MeshRenderer>(),
                mesh = new Mesh { name = "Squirrel Pose" },
                block = new MaterialPropertyBlock()
            };
            l.mesh.MarkDynamic();
            filter.sharedMesh = l.mesh;
            return l;
        }

        PoseData BuildPose(SquirrelForm.Pose pose)
        {
            var s = pose.sprite;
            if (s == null) return null;
            int n = Mathf.Max(2, grid);
            var p = new PoseData
            {
                texture = s.texture,
                anchor = pose.anchor,
                tailBase = pose.tailBase,
                rest = new Vector3[(n + 1) * (n + 1)],
                uv = new Vector2[(n + 1) * (n + 1)],
                loose = new float[(n + 1) * (n + 1)],
                tail = new float[(n + 1) * (n + 1)],
                tris = new int[n * n * 6]
            };
            Rect r = s.rect;
            float ppu = s.pixelsPerUnit;
            float tw = s.texture.width, th = s.texture.height;
            for (int y = 0, i = 0; y <= n; y++)
            {
                for (int x = 0; x <= n; x++, i++)
                {
                    float px = r.width * x / n, py = r.height * y / n;
                    var pos = new Vector2((px - s.pivot.x) / ppu, (py - s.pivot.y) / ppu);
                    p.rest[i] = pos;
                    p.uv[i] = new Vector2((r.x + px) / tw, (r.y + py) / th);
                    Vector2 d = pos - pose.anchor;
                    float loose = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(rigidRadius, looseRadius, d.magnitude));
                    p.loose[i] = loose;
                    // Хвост — всё, что дальше его основания по направлению «туловище → основание хвоста».
                    Vector2 axis = pose.tailBase - pose.anchor;
                    float beyond = axis.sqrMagnitude > 1e-6f ? Vector2.Dot(pos - pose.tailBase, axis.normalized) : 0f;
                    p.tail[i] = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.02f, tailHinge, beyond));
                }
            }
            for (int y = 0, t = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    int a = y * (n + 1) + x, b = a + 1, c = a + n + 1, e = c + 1;
                    p.tris[t++] = a; p.tris[t++] = c; p.tris[t++] = b;
                    p.tris[t++] = b; p.tris[t++] = c; p.tris[t++] = e;
                }
            }
            return p;
        }

        int PickPose()
        {
            if (controller == null) return Idle;
            switch (controller.State)
            {
                case SquirrelState.OnWall: return Cling;
                case SquirrelState.Airborne: return controller.Velocity.y > 0.5f ? Jump : Run; // вниз летит вытянувшись
                default: return Mathf.Abs(controller.Velocity.x) > runSpeedThreshold ? Run : Idle;
            }
        }

        void Show(int index, bool instant = false)
        {
            if (index == poseIndex || poses[index] == null) return;
            poseIndex = index;
            if (!instant)
            {
                var t = current; current = previous; previous = t; // уходящая поза доживает на втором слое
                fade = 0f;
            }
            else fade = 1f;

            var p = poses[index];
            current.pose = p;
            current.verts = (Vector3[])p.rest.Clone();
            current.colors = new Color[p.rest.Length];
            var m = current.mesh;
            m.Clear();
            m.vertices = current.verts;
            m.uv = p.uv;
            m.triangles = p.tris;
            m.bounds = new Bounds(Vector3.zero, new Vector3(8f, 8f, 1f)); // с запасом: вершины гуляют
            current.block.SetTexture(MainTex, p.texture);
            current.renderer.SetPropertyBlock(current.block);
            current.renderer.sortingOrder = sortingOrder + 1;
            if (previous != null && previous.renderer != null) previous.renderer.sortingOrder = sortingOrder;
        }

        void OnSwung(Vector2 dir)
        {
            int facing = controller != null ? controller.Facing : 1;
            float side = Mathf.Abs(dir.x), up = dir.y;
            whipAngle = up > 0.5f ? whipUpAngle : (up < -0.5f ? whipDownAngle : whipSideAngle);
            // Удар назад по отношению к взгляду — хвосту почти не надо лететь.
            if (side > 0.5f && Mathf.Sign(dir.x) != facing) whipAngle *= 0.25f;
            whipClock = 0f;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || form == null || current == null || current.pose == null) return;
            clock += dt;

            Show(PickPose());
            fade = Mathf.MoveTowards(fade, 1f, dt / Mathf.Max(crossfade, 0.001f));
            current.alpha = fade;
            if (previous != null) previous.alpha = previous.pose != null ? 1f - fade : 0f;

            Vector2 v = controller != null ? controller.Velocity : Vector2.zero;
            int facing = controller != null ? controller.Facing : 1;
            var localVelocity = new Vector2(v.x * facing, v.y); // спрайт зеркалится вместе с Visual
            lagVelocity = Vector2.Lerp(lagVelocity, localVelocity, 1f - Mathf.Exp(-lagSharpness * dt));

            // --- Тело: дыхание, покачивание на бегу, наклон в падении ---
            float targetTilt = 0f, targetBob = 0f, scaleY = 1f;
            bool grounded = controller == null || controller.State == SquirrelState.Grounded;
            if (poseIndex == Idle) scaleY = 1f + Mathf.Sin(clock * 2.2f) * breathe;
            else if (poseIndex == Run && grounded)
            {
                stride += Mathf.Abs(v.x) * dt / Mathf.Max(strideLength, 0.01f) * Mathf.PI * 2f;
                targetBob = Mathf.Abs(Mathf.Sin(stride * 0.5f)) * runBob;
                targetTilt = Mathf.Sin(stride) * runTilt;
            }
            else if (poseIndex == Run) targetTilt = Mathf.Clamp(v.y * fallTiltPerSpeed, -maxFallTilt, 8f);

            float whip = 0f;
            if (whipClock >= 0f)
            {
                whipClock += dt;
                float k = whipClock / Mathf.Max(whipTime, 0.01f);
                if (k >= 1f) whipClock = -1f;
                else
                {
                    // Быстрый замах и плавный возврат.
                    whip = Mathf.Sin(Mathf.Pow(k, 0.6f) * Mathf.PI) * whipAngle;
                    targetTilt += Mathf.Sin(k * Mathf.PI) * Mathf.Sign(-whipAngle) * 7f;
                }
            }

            float smooth = 1f - Mathf.Exp(-18f * dt);
            tilt = Mathf.Lerp(tilt, targetTilt, smooth);
            bob = Mathf.Lerp(bob, targetBob, smooth);
            transform.localPosition = new Vector3(0f, bob, 0f);
            transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
            transform.localScale = new Vector3(1f, scaleY, 1f);

            Deform(current, whip);
            if (previous != null && previous.pose != null) Deform(previous, whip);

            if (glow != null)
            {
                float pulse = 1f + Mathf.Sin(clock * glowPulseSpeed) * glowPulse + Mathf.Abs(whip) / 120f * 0.6f;
                var c = form.glowColor;
                c.a = Mathf.Clamp01(c.a * pulse);
                glow.color = c;
            }
        }

        void Deform(Layer l, float whipDegrees)
        {
            var p = l.pose;
            Vector2 anchor = p.anchor;
            Vector2 trail = Vector2.ClampMagnitude(lagVelocity * lag, maxLag);
            float time = clock * waveSpeed;
            var color = form.tint;
            color.a *= l.alpha;
            for (int i = 0; i < l.verts.Length; i++)
            {
                Vector2 rest = p.rest[i];
                float loose = p.loose[i];
                float tail = p.tail[i];
                if (tail > 0.0001f && whipDegrees != 0f)
                {
                    // Удар: хвост поворачивается вокруг своего основания, кончик — чуть сильнее.
                    Vector2 t = rest - p.tailBase;
                    float a = whipDegrees * tail * (1f + whipCurl * (loose - 0.5f)) * Mathf.Deg2Rad;
                    float cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                    rest = p.tailBase + new Vector2(t.x * cs - t.y * sn, t.x * sn + t.y * cs);
                }
                if (loose > 0.0001f)
                {
                    Vector2 d = rest - anchor;
                    float dist = d.magnitude;
                    Vector2 along = dist > 0.0001f ? d / dist : Vector2.right;
                    var across = new Vector2(-along.y, along.x);
                    float phase = time - dist * waveLength;
                    Vector2 wave = across * (Mathf.Sin(phase) * waveAmplitude) + along * (Mathf.Sin(phase * 0.7f + 1.3f) * waveAmplitude * 0.35f);
                    rest += (wave - trail) * loose;
                }
                l.verts[i] = rest;
                l.colors[i] = color;
            }
            l.mesh.vertices = l.verts;
            l.mesh.colors = l.colors;
        }
    }
}
