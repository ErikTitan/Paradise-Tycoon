using UnityEngine;

namespace ParadiseTycoon.Hud
{
    /// <summary>Owns visibility of the menu's legacy shortcut while a HUD replaces it.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(CanvasGroup))]
    public sealed class HudLegacyPauseShortcut : MonoBehaviour
    {
        [SerializeField] ResortHud owner;
        [SerializeField] bool captured;
        [SerializeField] float originalAlpha = 1;
        [SerializeField] bool originalInteractable = true, originalBlocksRaycasts = true;
        CanvasGroup group;

        public void Bind(ResortHud hud)
        {
            group = GetComponent<CanvasGroup>();
            if (!captured)
            {
                originalAlpha = group.alpha;
                originalInteractable = group.interactable;
                originalBlocksRaycasts = group.blocksRaycasts;
                captured = true;
            }
            owner = hud;
            Refresh();
        }
        void OnEnable() => Refresh();
        void LateUpdate() => Refresh();
        void Refresh()
        {
            if (!captured) return;
            if (group == null) group = GetComponent<CanvasGroup>();
            bool hidden = owner != null && owner.isActiveAndEnabled;
            float alpha = hidden ? 0 : originalAlpha;
            bool interactable = !hidden && originalInteractable;
            bool blocks = !hidden && originalBlocksRaycasts;
            if (group.alpha != alpha) group.alpha = alpha;
            if (group.interactable != interactable) group.interactable = interactable;
            if (group.blocksRaycasts != blocks) group.blocksRaycasts = blocks;
        }
        public void Release(ResortHud hud)
        {
            if (owner != hud) return;
            owner = null;
            Refresh();
            gameObject.SetActive(true);
        }
    }
}
