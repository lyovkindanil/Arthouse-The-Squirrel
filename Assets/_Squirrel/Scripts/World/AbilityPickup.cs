using UnityEngine;

namespace Squirrel.World
{
    /// <summary>Святилище: белка касается — открывается способность.</summary>
    public class AbilityPickup : MonoBehaviour
    {
        [SerializeField] Ability ability = Ability.WallJump;
        [SerializeField] float radius = 1.2f;
        [SerializeField] Transform visual;
        [TextArea] [SerializeField] string message = "Отталкивание от стен: цепляйся за любую стену и прыгай от неё.";

        SquirrelAbilities abilities;
        Transform squirrel;

        public bool Taken { get; private set; }
        public Ability Ability => ability;

        void Awake()
        {
            var c = FindAnyObjectByType<SquirrelController>();
            if (c != null) { squirrel = c.transform; abilities = c.GetComponent<SquirrelAbilities>(); }
        }

        void Start()
        {
            if (WorldState.HasFlag(SquirrelAbilities.FlagFor(ability)) || (abilities != null && abilities.Has(ability)))
                Hide();
        }

        void Update()
        {
            if (Taken) return;
            if (visual != null) visual.Rotate(0f, 0f, 90f * Time.deltaTime);
            if (squirrel == null || abilities == null) return;
            Vector2 body = (Vector2)squirrel.position + Vector2.up * 0.35f;
            if (Vector2.Distance(body, transform.position) > radius) return;
            abilities.Set(ability, true);
            WorldState.SetFlag(SquirrelAbilities.FlagFor(ability));
            Toast.Show(message, 5f);
            Hide();
        }

        void Hide()
        {
            Taken = true;
            if (visual != null) visual.gameObject.SetActive(false);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.5f, 1f, 0.6f, 0.7f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
