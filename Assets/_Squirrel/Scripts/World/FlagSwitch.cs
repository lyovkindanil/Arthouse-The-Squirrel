using UnityEngine;

namespace Squirrel.World
{
    /// <summary>
    /// Включает или выключает объект по флагу мира. Так видны последствия выборов:
    /// шёлковый мост появляется, если гусеницу освободили, засохший кокон — если забрали её запас.
    /// </summary>
    public class FlagSwitch : MonoBehaviour
    {
        [SerializeField] string flag;
        [Tooltip("true — объект виден, когда флаг стоит; false — наоборот")]
        [SerializeField] bool activeWhenSet = true;
        [SerializeField] GameObject target;

        WorldState world;

        public string Flag => flag;
        public GameObject Target => target;

        public void Setup(string flagName, bool showWhenSet, GameObject targetObject)
        {
            flag = flagName;
            activeWhenSet = showWhenSet;
            target = targetObject;
        }

        void Start()
        {
            world = WorldState.Current;
            if (world != null) world.FlagSet += OnFlag;
            Apply();
        }

        void OnDestroy()
        {
            if (world != null) world.FlagSet -= OnFlag;
        }

        void OnFlag(string f) { if (f == flag) Apply(); }

        void Apply()
        {
            if (target == null) return;
            bool set = WorldState.HasFlag(flag);
            target.SetActive(set == activeWhenSet);
        }
    }
}
