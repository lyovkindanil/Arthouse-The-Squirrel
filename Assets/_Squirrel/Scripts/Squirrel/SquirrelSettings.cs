using UnityEngine;

namespace Squirrel
{
    /// <summary>
    /// Все числа, от которых зависит «ощущение» белки.
    /// Это ScriptableObject: правки, сделанные в Play Mode, сохраняются после выхода из него.
    /// </summary>
    [CreateAssetMenu(menuName = "Squirrel/Squirrel Settings", fileName = "SquirrelSettings")]
    public class SquirrelSettings : ScriptableObject
    {
        [Header("Бег")]
        [Tooltip("Максимальная скорость бега, ед/с")]
        public float maxRunSpeed = 9f;
        [Tooltip("Разгон на земле, ед/с²")]
        public float groundAcceleration = 90f;
        [Tooltip("Торможение на земле, когда стик/клавиши отпущены")]
        public float groundDeceleration = 120f;
        [Tooltip("Множитель разгона при смене направления (резкий разворот)")]
        public float turnMultiplier = 1.6f;
        [Tooltip("Разгон в воздухе")]
        public float airAcceleration = 65f;
        [Tooltip("Торможение в воздухе")]
        public float airDeceleration = 35f;

        [Header("Прыжок")]
        [Tooltip("Высота полного прыжка, ед")]
        public float jumpHeight = 3.2f;
        [Tooltip("Время до верхней точки полного прыжка, с")]
        public float timeToApex = 0.36f;
        [Tooltip("Множитель гравитации при падении (падает быстрее, чем взлетает)")]
        public float fallGravityMultiplier = 1.8f;
        [Tooltip("Множитель гравитации, если кнопку прыжка отпустили на взлёте (короткий прыжок)")]
        public float jumpCutGravityMultiplier = 3f;
        [Tooltip("Около вершины прыжка (|vy| меньше этого) гравитация слабее — «зависание»")]
        public float apexThreshold = 1.5f;
        public float apexGravityMultiplier = 0.55f;
        [Tooltip("Ограничение скорости падения")]
        public float maxFallSpeed = 18f;
        [Tooltip("Coyote time: можно прыгнуть, уже сойдя с края, с")]
        public float coyoteTime = 0.1f;
        [Tooltip("Буфер нажатия: прыжок, нажатый чуть раньше приземления, сработает, с")]
        public float jumpBufferTime = 0.12f;
        [Tooltip("Насколько далеко (ед) белку можно «соскользнуть» с угла потолка, чтобы не стукнуться головой")]
        public float cornerCorrection = 0.25f;

        [Header("Стены (кора)")]
        [Tooltip("Можно ли цепляться за стены. За кору — всегда, за камень — только с отталкиванием (SquirrelAbilities)")]
        public bool wallClingEnabled = true;
        [Tooltip("Медленное скольжение вниз по стене")]
        public float wallSlideSpeed = 2.5f;
        [Tooltip("Быстрое скольжение, если зажать «вниз»")]
        public float wallFastSlideSpeed = 8f;
        [Tooltip("Лазание вверх по коре при зажатом «вверх» (по камню лазать нельзя). 0 — выключено")]
        public float wallClimbSpeed = 4f;
        [Tooltip("Горизонтальная скорость отталкивания от стены")]
        public float wallJumpHorizontalSpeed = 9f;
        [Tooltip("Высота отталкивания от стены, ед")]
        public float wallJumpHeight = 2.8f;
        [Tooltip("Сколько после отталкивания управление по X заблокировано, с")]
        public float wallJumpInputLock = 0.14f;
        [Tooltip("Сколько надо держать направление «от стены», чтобы отцепиться, с")]
        public float wallStickTime = 0.12f;
        [Tooltip("Coyote time для отталкивания от стены, с")]
        public float wallCoyoteTime = 0.1f;

        [Header("Планирование на хвосте (способность 2-й локации, включается в SquirrelAbilities)")]
        [Tooltip("Скорость падения при планировании")]
        public float glideFallSpeed = 2.2f;

        [Header("Сочность")]
        [Tooltip("Сжатие/растяжение спрайта при прыжке и приземлении")]
        public float squashAmount = 0.18f;
        public float squashRecoverSpeed = 10f;

        public float Gravity => 2f * jumpHeight / (timeToApex * timeToApex);
        public float JumpVelocity => 2f * jumpHeight / timeToApex;
        public float WallJumpVelocity => Mathf.Sqrt(2f * Gravity * wallJumpHeight);
    }
}
