using UnityEngine;
using UnityEngine.SceneManagement;
using Squirrel.Combat;
using Squirrel.Edge;

namespace Squirrel.World
{
    /// <summary>
    /// Связывает сцену локации с сохранением. При старте загружает слот (если он от этой сцены),
    /// при каждом отдыхе в гнезде и при восстановлении Сердца — сохраняет.
    /// Работает раньше остальных Start(), поэтому объекты уже видят загруженные флаги.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(WorldState))]
    public class GameSession : MonoBehaviour
    {
        [SerializeField] string slot = "slot0";
        [SerializeField] bool loadOnStart = true;
        [SerializeField] EdgeZone zone;

        WorldState world;
        SquirrelController squirrel;
        SquirrelHealth health;
        SquirrelAbilities abilities;
        Respawner respawner;

        public string Slot => slot;
        public bool LoadedFromSave { get; private set; }
        public int SaveCount { get; private set; }

        void Awake()
        {
            world = GetComponent<WorldState>();
            if (zone == null) zone = FindAnyObjectByType<EdgeZone>();
            squirrel = FindAnyObjectByType<SquirrelController>();
            if (squirrel != null)
            {
                health = squirrel.GetComponent<SquirrelHealth>();
                abilities = squirrel.GetComponent<SquirrelAbilities>();
                respawner = squirrel.GetComponent<Respawner>();
            }
        }

        void OnEnable()
        {
            Nest.Rested += OnRested;
            Heart.AnyRestored += OnHeartRestored;
            if (abilities != null) abilities.Unlocked += OnAbility;
        }

        void OnDisable()
        {
            Nest.Rested -= OnRested;
            Heart.AnyRestored -= OnHeartRestored;
            if (abilities != null) abilities.Unlocked -= OnAbility;
        }

        void Start()
        {
            if (loadOnStart && SaveSystem.TryRead(slot, out var data) && data.scene == SceneManager.GetActiveScene().name)
                Apply(data);
        }

        void OnAbility(Ability a) => world.Set(SquirrelAbilities.FlagFor(a));
        void OnRested(Nest n) => Save();
        void OnHeartRestored(Heart h) => Save();

        public void Save()
        {
            var d = new SaveData
            {
                scene = SceneManager.GetActiveScene().name,
                nest = Nest.LastVisited != null ? Nest.LastVisited.name : null,
                dream = world.Dream,
                compassion = world.Compassion,
            };
            d.flags.AddRange(world.Flags);
            if (health != null) { d.bonusSeeds = health.BonusSeeds; d.sap = health.Sap; }
            if (respawner != null) { d.sleepDepth = respawner.SleepDepth; d.deaths = respawner.Deaths; }
            if (zone != null) { d.hasEdge = true; d.edge = zone.Capture(); }
            SaveSystem.Write(slot, d);
            SaveCount++;
        }

        void Apply(SaveData d)
        {
            LoadedFromSave = true;
            world.Load(d.flags, d.dream, d.compassion);
            if (abilities != null)
                foreach (Ability a in System.Enum.GetValues(typeof(Ability)))
                    if (world.Has(SquirrelAbilities.FlagFor(a))) abilities.Set(a, true);
            if (health != null) { health.SetBonusSeeds(d.bonusSeeds); health.Restore(); health.AddSap(d.sap); }
            if (respawner != null) respawner.RestoreCounters(d.sleepDepth, d.deaths);
            if (zone != null && d.hasEdge) zone.Restore(d.edge);

            if (!string.IsNullOrEmpty(d.nest) && squirrel != null)
            {
                foreach (var n in FindObjectsByType<Nest>())
                {
                    if (n.name != d.nest) continue;
                    Nest.MarkVisited(n);
                    squirrel.Teleport(n.SpawnPoint);
                    break;
                }
            }
            Debug.Log($"Загружено сохранение {slot}: гнездо {d.nest}, флагов {d.flags.Count}.");
        }

        /// <summary>Стереть сохранение и начать локацию заново.</summary>
        public void WipeAndRestart()
        {
            SaveSystem.Delete(slot);
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
