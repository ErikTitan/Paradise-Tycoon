using System.Collections.Generic;
using ParadiseTycoon.GameState;
using ParadiseTycoon.Hud;
using ParadiseTycoon.Scrum104;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace ParadiseTycoon.Buildings
{
    public sealed class BuildingUpgradeController : MonoBehaviour
    {
        [SerializeField] ResortBuilding[] buildings;
        [SerializeField] Camera worldCamera;
        [SerializeField] BuildingUpgradePanel panel;
        [SerializeField] ResortHud hud;
        [SerializeField, Min(0)] int startingCoins = 10000;
        [SerializeField] ResortEconomySettings economy = new();

        readonly List<RaycastResult> uiHits = new();
        IBuildingUpgradeState state;
        ResortBuilding selected;
        ButtonControl worldPress;
        int quotedLevel;
        bool confirming;
        bool started;
        string feedback = "";

        // The same instance is used by purchases, the HUD, and the guest/ship systems.
        public ResortGameState GameState
        {
            get
            {
                EnsureState();
                return state as ResortGameState;
            }
        }

        void EnsureState()
        {
            if (state == null)
                BindState(new ResortGameState(Application.persistentDataPath, buildings, startingCoins, economy));
        }

        void Start()
        {
            panel.Bind(ConfirmOrPurchase, Close);
            panel.gameObject.SetActive(false);
            hud.UpgradeRequested += ShowFromHud;
            EnsureState();
            started = true;
            Refresh();
            if (state is ResortGameState resort && resort.SaveError != null)
                hud.SetStatus("Postup sa nepodarilo načítať alebo uložiť. Skontroluj úložisko.");
        }

        public void BindState(IBuildingUpgradeState sharedState)
        {
            if (sharedState == null) throw new System.ArgumentNullException(nameof(sharedState));
            if (state != null) state.Changed -= Refresh;
            state = sharedState;
            state.Changed += Refresh;
            hud.BindState(sharedState);
            confirming = false;
            Refresh();
        }

        public void SetWaveActive(bool active) => GameState?.SetExternalWaveActive(active);

        void OnDestroy()
        {
            if (hud != null) hud.UpgradeRequested -= ShowFromHud;
            if (state != null) state.Changed -= Refresh;
        }

        void Update()
        {
            if (panel.gameObject.activeSelf || IsPaused())
            {
                worldPress = null;
                return;
            }

            var touch = Touchscreen.current?.primaryTouch;
            var mouse = Mouse.current;
            if (touch != null && (touch.press.isPressed || touch.press.wasReleasedThisFrame))
                SelectOnRelease(touch.press, touch.position.ReadValue());
            else if (mouse != null)
                SelectOnRelease(mouse.leftButton, mouse.position.ReadValue());
        }

        void SelectOnRelease(ButtonControl press, Vector2 screenPoint)
        {
            if (press.wasPressedThisFrame)
                worldPress = IsOverUi(screenPoint) ? null : press;

            if (!press.wasReleasedThisFrame || worldPress != press) return;
            worldPress = null;
            if (IsOverUi(screenPoint)) return;

            Ray ray = worldCamera.ScreenPointToRay(screenPoint);
            var islandPlane = new Plane(Vector3.forward, Vector3.zero);
            if (!islandPlane.Raycast(ray, out float distance)) return;

            Vector3 worldPoint = ray.GetPoint(distance);
            ResortBuilding hit = null;
            foreach (var building in buildings)
            {
                if (building.Contains(worldPoint) && (hit == null || DrawsInFront(building, hit)))
                    hit = building;
            }

            if (hit != null)
            {
                PauseMenuController.Instance?.PlayClick();
                Open(hit);
            }
        }

        bool IsOverUi(Vector2 screenPoint)
        {
            if (EventSystem.current == null) return false;

            var pointer = new PointerEventData(EventSystem.current) { position = screenPoint };
            EventSystem.current.RaycastAll(pointer, uiHits);
            foreach (var hit in uiHits)
            {
                if (hit.module is GraphicRaycaster) return true;
            }
            return false;
        }

        static bool DrawsInFront(ResortBuilding candidate, ResortBuilding current)
        {
            var a = candidate.SortingGroup;
            var b = current.SortingGroup;
            int layer = SortingLayer.GetLayerValueFromID(a.sortingLayerID)
                .CompareTo(SortingLayer.GetLayerValueFromID(b.sortingLayerID));
            if (layer != 0) return layer > 0;
            if (a.sortingOrder != b.sortingOrder) return a.sortingOrder > b.sortingOrder;
            return candidate.transform.position.y < current.transform.position.y;
        }

        static bool IsPaused() => PauseMenuController.Instance != null && PauseMenuController.Instance.IsPaused;

        void ShowFromHud()
        {
            if (selected != null) Open(selected);
            else hud.SetStatus("Vyber budovu na ostrove.");
        }

        void Open(ResortBuilding building)
        {
            worldPress = null;
            selected = building;
            confirming = false;
            feedback = "";
            panel.Show(selected, state.Coins, state.IsWaveActive, false, feedback);
        }

        void Close()
        {
            confirming = false;
            feedback = "";
            panel.gameObject.SetActive(false);
            PauseMenuController.Instance?.PlayClick();
        }

        void Refresh()
        {
            if (!started) return;
            foreach (var building in buildings)
            {
                int level = state.GetBuildingLevel(building.BuildingId, building.InitialLevel);
                building.ApplyLevel(Mathf.Clamp(level, 0, building.MaxLevel));
            }

            if (selected == null || !panel.gameObject.activeSelf) return;
            if (selected.Level != quotedLevel || state.IsWaveActive) confirming = false;
            panel.Show(selected, state.Coins, state.IsWaveActive, confirming, feedback);
        }

        void ConfirmOrPurchase()
        {
            if (selected == null || IsPaused()) return;
            var next = selected.GetLevel(selected.Level + 1);
            if (selected.Level == 0 || next == null || state.IsWaveActive || state.Coins < next.Cost)
            {
                confirming = false;
                Refresh();
                return;
            }

            PauseMenuController.Instance?.PlayClick();
            if (!confirming)
            {
                quotedLevel = selected.Level;
                confirming = true;
                feedback = "";
                Refresh();
                return;
            }

            confirming = false;
            feedback = selected.Level == quotedLevel && state.TryPurchaseUpgrade(selected.BuildingId, quotedLevel, next.Cost)
                ? $"Vylepšenie dokončené. Úroveň {quotedLevel + 1}."
                : "Vylepšenie sa nepodarilo. Skús znova.";
            Refresh();
        }
    }
}
