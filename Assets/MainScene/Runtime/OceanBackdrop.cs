using UnityEngine;

namespace ParadiseTycoon.SceneFoundation
{
    /// <summary>Keeps the sea behind the full camera view on wider screens.</summary>
    [ExecuteAlways, DefaultExecutionOrder(100), RequireComponent(typeof(SpriteRenderer))]
    public sealed class OceanBackdrop : MonoBehaviour
    {
        [SerializeField] Camera sceneCamera;
        SpriteRenderer spriteRenderer;

        public void SetCamera(Camera value) { sceneCamera = value; Refresh(); }
        void OnEnable() => Refresh();
        void LateUpdate() => Refresh();

        public void Refresh()
        {
            if (sceneCamera == null) return;
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer.sprite == null) return;
            Vector2 size = spriteRenderer.sprite.bounds.size;
            float height = 2f * sceneCamera.orthographicSize;
            float width = height * sceneCamera.aspect;
            transform.position = new Vector3(sceneCamera.transform.position.x, sceneCamera.transform.position.y, 0f);
            transform.localScale = new Vector3((width + .1f) / size.x, (height + .1f) / size.y, 1f);
        }
    }
}
