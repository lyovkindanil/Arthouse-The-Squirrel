using System;
using UnityEngine;

namespace Squirrel.World
{
    /// <summary>На белке: в какой она комнате. Комнаты-соседи стыкуются по стенам, проходы — дыры в стенах.</summary>
    public class RoomTracker : MonoBehaviour
    {
        SquirrelMotor motor;
        public Room Current { get; private set; }
        public event Action<Room> Entered;

        void Awake() => motor = GetComponent<SquirrelMotor>();

        void LateUpdate()
        {
            Vector2 p = motor != null ? motor.WorldCenter : (Vector2)transform.position;
            var r = Room.At(p);
            if (r == null || r == Current) return;
            Current = r;
            Entered?.Invoke(r);
        }
    }
}
