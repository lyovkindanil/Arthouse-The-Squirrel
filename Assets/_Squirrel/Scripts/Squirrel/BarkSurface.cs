using UnityEngine;

namespace Squirrel
{
    /// <summary>
    /// Метка «это кора». По коре белка лазает с самого начала.
    /// Всё без этой метки — камень: за него можно держаться и отталкиваться только после способности.
    /// </summary>
    public class BarkSurface : MonoBehaviour
    {
        public static bool Is(Collider2D c) => c != null && c.GetComponent<BarkSurface>() != null;
    }
}
