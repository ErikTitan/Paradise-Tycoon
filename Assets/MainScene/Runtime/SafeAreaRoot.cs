using UnityEngine;

namespace ParadiseTycoon.SceneFoundation
{
    /// <summary>Use under a full-screen overlay Canvas, then place HUD content inside.</summary>
    [ExecuteAlways, RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaRoot : MonoBehaviour
    {
        void OnEnable() => Refresh();
        void LateUpdate() => Refresh();

        public void Refresh()
        {
            if (Screen.width <= 0 || Screen.height <= 0) return;
            var rect = (RectTransform)transform;
            Rect safe = Screen.safeArea;
            rect.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            rect.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
