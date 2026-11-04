using UnityEngine;

namespace ParadiseTycoon.SceneFoundation
{
    /// <summary>Fits the island inside the safe area, leaving room for the HUD.</summary>
    [ExecuteAlways, RequireComponent(typeof(Camera))]
    public sealed class IslandCameraFraming : MonoBehaviour
    {
        [SerializeField] Bounds worldBounds = new Bounds(Vector3.zero, new Vector3(20f, 13f, 0f));
        [SerializeField, Range(0f, .4f)] float topInset = .14f;
        [SerializeField, Range(0f, .4f)] float bottomInset = .05f;
        [SerializeField, Range(0f, .4f)] float sideInset = .04f;
        Camera sceneCamera;

        public Bounds WorldBounds => worldBounds;

        void OnEnable() => Refresh();
        void LateUpdate() => Refresh();
        void OnValidate() => Refresh();

        public Rect UsableViewport(float width, float height, Rect safeArea)
        {
            width = Mathf.Max(1f, width);
            height = Mathf.Max(1f, height);
            float left = Mathf.Clamp01(safeArea.xMin / width);
            float right = Mathf.Clamp01(safeArea.xMax / width);
            float bottom = Mathf.Clamp01(safeArea.yMin / height);
            float top = Mathf.Clamp01(safeArea.yMax / height);
            if (right <= left || top <= bottom) { left = bottom = 0f; right = top = 1f; }
            float safeWidth = right - left;
            float safeHeight = top - bottom;
            return Rect.MinMaxRect(left + safeWidth * sideInset, bottom + safeHeight * bottomInset,
                right - safeWidth * sideInset, top - safeHeight * topInset);
        }

        // Also used by Editor validation at explicit device resolutions.
        public void Frame(float width, float height, Rect safeArea)
        {
            if (sceneCamera == null) sceneCamera = GetComponent<Camera>();
            float aspect = Mathf.Max(1f, width) / Mathf.Max(1f, height);
            Rect usable = UsableViewport(width, height, safeArea);
            float halfHeight = Mathf.Max(.01f, Mathf.Max(
                worldBounds.size.y / (2f * usable.height),
                worldBounds.size.x / (2f * aspect * usable.width)));
            sceneCamera.orthographic = true;
            sceneCamera.orthographicSize = halfHeight;
            sceneCamera.transparencySortMode = UnityEngine.TransparencySortMode.CustomAxis;
            sceneCamera.transparencySortAxis = Vector3.up;
            Vector2 offset = (usable.center - Vector2.one * .5f) * (2f * halfHeight);
            transform.position = new Vector3(worldBounds.center.x - offset.x * aspect,
                worldBounds.center.y - offset.y, -10f);
            transform.rotation = Quaternion.identity;
        }

        public void Refresh()
        {
            Frame(Screen.width, Screen.height, Screen.safeArea);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(worldBounds.center, worldBounds.size);
        }
    }
}
