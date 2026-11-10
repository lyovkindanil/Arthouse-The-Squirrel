using System.Collections.Generic;
using UnityEngine;
using Squirrel.Combat;

namespace Squirrel.Enemies
{
    public enum WardenState { Dormant, Waking, Idle, Telegraph, Attacking, Exposed, Enraging, Defeated }
    public enum WardenAttack { RootSpikes, Sweep, SporeRain }

    /// <summary>
    /// Корневой сторож — босс Мшистой колыбели, охраняет Сердце.
    /// Сидит в стене арены. Ранить его можно, только пока он раскрыт (после каждой атаки светится сердцевина).
    /// Фаза 1: корни-шипы из пола под белкой (по одному) и низкий удар корнем по полу (перепрыгнуть).
    /// Фаза 2 (с половины здоровья): всё быстрее, шипов больше, удар корнем дважды, сверху падают споры.
    /// Белка вошла на арену — ворота закрываются. Победа — ворота открываются, к Сердцу можно подойти.
    /// Белка погибла — бой начинается заново.
    /// </summary>
    public class RootWarden : Enemy
    {
        public const string DefeatedFlag = "boss.rootwarden.defeated";

        [Header("Арена")]
        [Tooltip("Прямоугольник арены в мире: белка внутри — бой начинается")]
        [SerializeField] Rect arena = new Rect(65, 56, 27, 14);
        [Tooltip("Высота пола арены (ноги белки)")]
        [SerializeField] float floorY = 56f;
        [SerializeField] GameObject gate;

        [Header("Ритм")]
        [SerializeField] float wakeTime = 1.6f;
        [SerializeField] float idleTime = 1.1f;
        [SerializeField] float exposedTime = 2.2f;
        [SerializeField] float enrageTime = 1.8f;
        [Tooltip("Во второй фазе всё быстрее во столько раз")]
        [SerializeField] float phaseTwoSpeed = 1.4f;

        [Header("Корни-шипы")]
        [SerializeField] float spikeTelegraph = 0.85f;
        [SerializeField] float spikeLife = 0.5f;
        [SerializeField] float spikeInterval = 0.55f;
        [SerializeField] Vector2 spikeSize = new Vector2(1.3f, 3.2f);
        [SerializeField] int spikesPhaseOne = 3;
        [SerializeField] int spikesPhaseTwo = 5;

        [Header("Удар корнем")]
        [SerializeField] float sweepTelegraph = 0.75f;
        [SerializeField] float sweepSpeed = 16f;
        [SerializeField] float sweepHeight = 1.1f;

        [Header("Споры (фаза 2)")]
        [SerializeField] int sporeCount = 6;
        [SerializeField] float sporeSpeed = 6f;

        [Header("Вид")]
        [SerializeField] SpriteRenderer core;
        [SerializeField] Color coreClosed = new Color(0.3f, 0.2f, 0.15f);
        [SerializeField] Color coreOpen = new Color(1f, 0.75f, 0.3f);
        [SerializeField] Sprite sprite;
        [SerializeField] Material material;

        public WardenState State { get; private set; }
        public int Phase => Hp > maxHp / 2 ? 1 : 2;
        public bool IsExposed => State == WardenState.Exposed;
        public bool IsFighting => State != WardenState.Dormant && State != WardenState.Defeated;
        public WardenAttack LastAttack { get; private set; }
        public static event System.Action<RootWarden> Defeated;

        float timer;
        int attackIndex;
        bool enraged;
        float speed = 1f;

        // Атаки в полёте.
        class Spike { public float x; public float t; public SpriteRenderer mark, body; }
        readonly List<Spike> spikes = new List<Spike>();
        int spikesLeft;
        float spikeTimer;
        int sweepsLeft;
        float sweepX;
        float sweepDir;
        SpriteRenderer sweepBody, sweepMark;

        public override bool GivesSap => IsExposed;
        public override bool CanBeHit => isActiveAndEnabled && IsFighting;

        protected override void Awake()
        {
            base.Awake();
            if (gate != null) gate.SetActive(false);
            State = WardenState.Dormant;
        }

        void Start()
        {
            if (global::Squirrel.World.WorldState.HasFlag(DefeatedFlag)) DieSilently();
        }

        // Сидит в стене: не отлетает.
        protected override Vector2 KnockbackFor(in HitInfo hit) => Vector2.zero;

        public override bool TakeHit(in HitInfo hit)
        {
            if (!IsFighting) return false;
            if (!IsExposed)
            {
                // Закрыт: удар отскакивает (отдача у белки есть, урона нет).
                CameraShake.Add(0.05f, 0.1f);
                return true;
            }
            int before = Hp;
            bool ok = base.TakeHit(hit);
            if (ok && before > maxHp / 2 && Hp <= maxHp / 2 && Hp > 0) StartEnrage();
            return ok;
        }

        public override void ResetEnemy()
        {
            if (State == WardenState.Defeated) return; // побеждённый не возвращается
            base.ResetEnemy();
        }

        protected override void OnReset()
        {
            ClearAttacks();
            State = WardenState.Dormant;
            enraged = false;
            speed = 1f;
            attackIndex = 0;
            if (gate != null) gate.SetActive(false);
            SetCore(false);
        }

        protected override void OnDied()
        {
            ClearAttacks();
            State = WardenState.Defeated;
            if (gate != null) gate.SetActive(false);
            HideHint();
            global::Squirrel.World.WorldState.SetFlag(DefeatedFlag);
            global::Squirrel.World.Toast.Show("Корневой сторож уснул. Путь к Сердцу открыт.", 4f);
            HitStop.Freeze(0.15f);
            CameraShake.Add(0.5f, 0.6f);
            Defeated?.Invoke(this);
        }

        void DieSilently()
        {
            Hp = 0;
            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = false;
            ClearAttacks();
            State = WardenState.Defeated;
            if (gate != null) gate.SetActive(false);
            HideHint();
        }

        void HideHint()
        {
            var hint = GetComponent<global::Squirrel.World.WorldHint>();
            if (hint != null) hint.enabled = false;
        }

        void StartEnrage()
        {
            enraged = true;
            ClearAttacks();
            State = WardenState.Enraging;
            timer = enrageTime;
            SetCore(false);
            CameraShake.Add(0.4f, enrageTime * 0.6f);
            global::Squirrel.World.Toast.Show("Корневой сторож в ярости!", 2.5f);
        }

        protected override void Tick(float dt)
        {
            if (State == WardenState.Defeated) return;
            if (State == WardenState.Dormant)
            {
                if (HasTarget && SquirrelInArena()) Wake();
                return;
            }
            if (!HasTarget) return;

            UpdateSpikes(dt);
            UpdateSweep(dt);
            timer -= dt * speed;

            switch (State)
            {
                case WardenState.Waking:
                case WardenState.Enraging:
                    if (timer <= 0f)
                    {
                        if (enraged) speed = phaseTwoSpeed;
                        GoIdle();
                    }
                    break;
                case WardenState.Idle:
                    if (timer <= 0f) StartAttack(NextAttack());
                    break;
                case WardenState.Telegraph:
                    if (core != null) core.color = Color.Lerp(coreClosed, Color.red, 0.5f + 0.5f * Mathf.Sin(Time.time * 25f));
                    if (timer <= 0f) LaunchSweep();
                    break;
                case WardenState.Attacking:
                    if (spikesLeft <= 0 && spikes.Count == 0 && sweepsLeft <= 0 && sweepBody == null && timer <= 0f)
                        Expose();
                    break;
                case WardenState.Exposed:
                    if (core != null) core.color = Color.Lerp(coreOpen, Color.white, 0.3f + 0.3f * Mathf.Sin(Time.time * 10f));
                    if (timer <= 0f) { SetCore(false); GoIdle(); }
                    break;
            }
        }

        bool SquirrelInArena() => arena.Contains(TargetCenter);

        void Wake()
        {
            State = WardenState.Waking;
            timer = wakeTime;
            if (gate != null) gate.SetActive(true);
            CameraShake.Add(0.3f, 0.8f);
            global::Squirrel.World.Toast.Show("Корневой сторож", 2.5f);
        }

        void GoIdle()
        {
            State = WardenState.Idle;
            timer = idleTime;
        }

        WardenAttack NextAttack()
        {
            // Фиксированный порядок: игрок учится читать бой. Во второй фазе добавляются споры.
            WardenAttack[] order = Phase == 1
                ? new[] { WardenAttack.RootSpikes, WardenAttack.Sweep }
                : new[] { WardenAttack.RootSpikes, WardenAttack.SporeRain, WardenAttack.Sweep };
            return order[attackIndex++ % order.Length];
        }

        /// <summary>Начать атаку сразу (тесты, отладка).</summary>
        public void StartAttack(WardenAttack attack)
        {
            if (!IsFighting) return;
            LastAttack = attack;
            switch (attack)
            {
                case WardenAttack.RootSpikes:
                    State = WardenState.Attacking;
                    spikesLeft = Phase == 1 ? spikesPhaseOne : spikesPhaseTwo;
                    spikeTimer = 0f;
                    timer = 0.3f;
                    break;
                case WardenAttack.Sweep:
                    State = WardenState.Telegraph;
                    sweepsLeft = Phase == 1 ? 1 : 2;
                    timer = sweepTelegraph;
                    ShowSweepMark(true);
                    break;
                case WardenAttack.SporeRain:
                    State = WardenState.Attacking;
                    for (int i = 0; i < sporeCount; i++)
                    {
                        float x = Mathf.Lerp(arena.xMin + 1.5f, arena.xMax - 1.5f, (i + Random.Range(0.2f, 0.8f)) / sporeCount);
                        SporeShot.Spawn(new Vector2(x, arena.yMax - 0.5f - Random.Range(0f, 2.5f)), Vector2.down * sporeSpeed * speed, sprite, material);
                    }
                    timer = 1.6f;
                    break;
            }
        }

        /// <summary>Отладка и тесты: сразу раскрыться.</summary>
        public void DebugExpose()
        {
            if (!IsFighting) return;
            ClearAttacks();
            Expose();
        }

        /// <summary>Отладка и тесты: разбудить, не заходя на арену.</summary>
        public void DebugWake() { if (State == WardenState.Dormant) Wake(); }

        void Expose()
        {
            State = WardenState.Exposed;
            timer = exposedTime;
            SetCore(true);
        }

        void SetCore(bool open)
        {
            if (core != null) core.color = open ? coreOpen : coreClosed;
        }

        // ---------- Корни-шипы ----------

        void UpdateSpikes(float dt)
        {
            float sdt = dt * speed;
            if (spikesLeft > 0)
            {
                spikeTimer -= sdt;
                if (spikeTimer <= 0f)
                {
                    spikesLeft--;
                    spikeTimer = spikeInterval;
                    float x = Mathf.Clamp(TargetCenter.x, arena.xMin + 0.7f, arena.xMax - 0.7f);
                    spikes.Add(new Spike
                    {
                        x = x,
                        t = 0f,
                        mark = MakeRenderer("Spike Warning", new Vector2(x, floorY + 0.1f), new Vector2(spikeSize.x, 0.2f), new Color(1f, 0.4f, 0.2f, 0.8f)),
                    });
                }
            }
            for (int i = spikes.Count - 1; i >= 0; i--)
            {
                var s = spikes[i];
                s.t += sdt;
                if (s.body == null && s.t >= spikeTelegraph)
                {
                    Destroy(s.mark.gameObject);
                    s.mark = null;
                    s.body = MakeRenderer("Root Spike", new Vector2(s.x, floorY + spikeSize.y * 0.5f), spikeSize, new Color(0.45f, 0.3f, 0.2f));
                    CameraShake.Add(0.08f, 0.1f);
                }
                if (s.mark != null)
                    s.mark.color = new Color(1f, 0.4f, 0.2f, 0.4f + 0.5f * Mathf.Abs(Mathf.Sin(s.t * 14f)));
                if (s.body != null)
                {
                    var r = new Rect(s.x - spikeSize.x * 0.5f, floorY, spikeSize.x, spikeSize.y);
                    HurtIfTouching(r, new Vector2(s.x, floorY));
                    if (s.t >= spikeTelegraph + spikeLife)
                    {
                        Destroy(s.body.gameObject);
                        spikes.RemoveAt(i);
                    }
                }
            }
        }

        // ---------- Удар корнем по полу ----------

        void ShowSweepMark(bool show)
        {
            if (show && sweepMark == null)
                sweepMark = MakeRenderer("Sweep Warning", new Vector2(arena.center.x, floorY + 0.05f), new Vector2(arena.width, 0.1f), new Color(1f, 0.3f, 0.2f, 0.7f));
            if (!show && sweepMark != null) { Destroy(sweepMark.gameObject); sweepMark = null; }
        }

        void LaunchSweep()
        {
            ShowSweepMark(false);
            State = WardenState.Attacking;
            sweepDir = 1f;
            sweepX = arena.xMin;
            sweepBody = MakeRenderer("Root Sweep", new Vector2(sweepX, floorY + sweepHeight * 0.5f), new Vector2(0.1f, sweepHeight), new Color(0.4f, 0.28f, 0.18f));
            timer = 0.4f;
        }

        void UpdateSweep(float dt)
        {
            if (sweepBody == null) return;
            sweepX += sweepDir * sweepSpeed * speed * dt;
            float tip = Mathf.Clamp(sweepX, arena.xMin, arena.xMax);
            float len = Mathf.Max(0.1f, tip - arena.xMin);
            sweepBody.transform.position = new Vector3(arena.xMin + len * 0.5f, floorY + sweepHeight * 0.5f);
            sweepBody.transform.localScale = new Vector3(len, sweepHeight, 1f);
            if (sweepDir > 0f)
                HurtIfTouching(new Rect(arena.xMin, floorY, len, sweepHeight), new Vector2(arena.xMin, floorY));
            if (sweepDir > 0f && sweepX >= arena.xMax) sweepDir = -1f;
            else if (sweepDir < 0f && sweepX <= arena.xMin)
            {
                Destroy(sweepBody.gameObject);
                sweepBody = null;
                sweepsLeft--;
                if (sweepsLeft > 0) { State = WardenState.Telegraph; timer = sweepTelegraph * 0.6f; ShowSweepMark(true); }
                else timer = 0.3f;
            }
        }

        void HurtIfTouching(Rect r, Vector2 from)
        {
            if (HasTarget && r.Overlaps(targetMotor.WorldRect)) target.Damage(1, from);
        }

        SpriteRenderer MakeRenderer(string name, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            if (material != null) sr.sharedMaterial = material;
            sr.color = color;
            sr.sortingOrder = 70;
            return sr;
        }

        void ClearAttacks()
        {
            foreach (var s in spikes)
            {
                if (s.mark != null) Destroy(s.mark.gameObject);
                if (s.body != null) Destroy(s.body.gameObject);
            }
            spikes.Clear();
            spikesLeft = 0;
            sweepsLeft = 0;
            if (sweepBody != null) { Destroy(sweepBody.gameObject); sweepBody = null; }
            ShowSweepMark(false);
        }

        /// <summary>Тесты: выставить здоровье.</summary>
        public void DebugSetHp(int hp)
        {
            int before = Hp;
            Hp = Mathf.Clamp(hp, 1, maxHp);
            if (before > maxHp / 2 && Hp <= maxHp / 2 && IsFighting) StartEnrage();
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.5f);
            Gizmos.DrawWireCube(arena.center, arena.size);
        }
    }
}
