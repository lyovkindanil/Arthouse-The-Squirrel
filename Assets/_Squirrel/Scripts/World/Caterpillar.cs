using UnityEngine;
using Squirrel.Combat;

namespace Squirrel.World
{
    /// <summary>
    /// Крупный выбор Мшистой колыбели. Гусеница заперта камнем, её запас лежит снаружи.
    /// Разбить камень — освободить: она уходит к расщелине и плетёт мост (Сострадание +1).
    /// Ударить по запасу — забрать: +1 зерно навсегда и полный сок, а гусеница остаётся замурованной (Дикость, Сострадание −1).
    /// Выбор делается действием, без меню; после одного второй закрыт.
    /// </summary>
    public class Caterpillar : MonoBehaviour
    {
        public const string HelpedFlag = "caterpillar.helped";
        public const string RobbedFlag = "caterpillar.robbed";

        [SerializeField] BreakableRock rock;
        [SerializeField] StashPile stash;
        [SerializeField] SpriteRenderer body;
        [SerializeField] int bonusSeeds = 1;
        [SerializeField] float crawlSpeed = 1.5f;

        SquirrelHealth health;
        bool leaving;

        public bool Decided => WorldState.HasFlag(HelpedFlag) || WorldState.HasFlag(RobbedFlag) || helpedLocal || robbedLocal;
        public bool Helped => WorldState.HasFlag(HelpedFlag) || helpedLocal;
        public bool Robbed => WorldState.HasFlag(RobbedFlag) || robbedLocal;
        bool helpedLocal, robbedLocal; // если в сцене нет WorldState

        void Awake()
        {
            health = FindAnyObjectByType<SquirrelHealth>();
        }

        void Start()
        {
            if (rock != null) rock.BrokenEvent += OnFreed;
            if (stash != null) stash.TakenEvent += OnRobbed;

            // Состояние из сохранения.
            if (WorldState.HasFlag(HelpedFlag))
            {
                if (rock != null) rock.BreakSilently();
                if (stash != null) stash.Hide();
                gameObject.SetActive(false);
            }
            else if (WorldState.HasFlag(RobbedFlag))
            {
                if (stash != null) stash.Hide();
                if (rock != null) rock.Unbreakable = true;
                Wither();
            }
        }

        void OnDestroy()
        {
            if (rock != null) rock.BrokenEvent -= OnFreed;
            if (stash != null) stash.TakenEvent -= OnRobbed;
        }

        void OnFreed()
        {
            if (Decided) return;
            helpedLocal = true;
            WorldState.SetFlag(HelpedFlag);
            if (WorldState.Current != null) WorldState.Current.AddCompassion(1f);
            if (stash != null) stash.Hide(); // она забирает запас с собой
            leaving = true;
            Toast.Show("Гусеница свободна. Она уползла к расщелине.", 4f);
        }

        void OnRobbed()
        {
            if (Decided) return;
            robbedLocal = true;
            WorldState.SetFlag(RobbedFlag);
            if (WorldState.Current != null) WorldState.Current.AddCompassion(-1f);
            if (rock != null) rock.Unbreakable = true;
            if (health != null)
            {
                health.AddMaxSeeds(bonusSeeds);
                health.AddSap(health.Settings.maxSap);
            }
            Wither();
            Toast.Show("Ты забрала запас: +1 зерно. Гусеница осталась за камнем.", 4f);
        }

        void Wither()
        {
            if (body != null) body.color = new Color(0.45f, 0.45f, 0.42f);
        }

        void Update()
        {
            if (!leaving) return;
            transform.position += Vector3.right * crawlSpeed * Time.deltaTime;
            if (body != null)
            {
                var c = body.color;
                c.a = Mathf.MoveTowards(c.a, 0f, Time.deltaTime * 0.6f);
                body.color = c;
                if (c.a <= 0f) gameObject.SetActive(false);
            }
            else gameObject.SetActive(false);
        }
    }
}
