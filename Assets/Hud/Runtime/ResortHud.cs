using System.Globalization;
using ParadiseTycoon.Buildings;
using ParadiseTycoon.GameState;
using ParadiseTycoon.Scrum104;
using UnityEngine;
using UnityEngine.UI;

namespace ParadiseTycoon.Hud
{
    /// <summary>Resort HUD with presentation defaults for the standalone HUD scene.</summary>
    public sealed class ResortHud : MonoBehaviour
    {
        [SerializeField] TMPro.TMP_Text coinsLabel, energyLabel, resortLabel, statusLabel;
        [SerializeField] Button pauseButton, buildButton, upgradeButton;
        [SerializeField, Min(0)] int demoCoins = 1250;
        [SerializeField, Min(0)] int demoEnergy = 80;
        [SerializeField, Min(1)] int demoMaxEnergy = 100;
        [SerializeField, Min(0)] int demoResortValue = 24500;
        PauseMenuController menu;
        HudLegacyPauseShortcut legacyPause;
        IBuildingUpgradeState state;
        public event System.Action UpgradeRequested;
        public Button PauseButton => pauseButton;
        public Button BuildButton => buildButton;
        public Button UpgradeButton => upgradeButton;
        public string CoinsText => coinsLabel.text;
        public string EnergyText => energyLabel.text;
        public string ResortText => resortLabel.text;
        public string StatusText => statusLabel.text;

        void OnEnable()
        {
            pauseButton.onClick.AddListener(OpenPause);
            buildButton.onClick.AddListener(PreviewBuild);
            upgradeButton.onClick.AddListener(PreviewUpgrade);
            if (state != null) state.Changed += RefreshValues;
            RefreshValues();
            statusLabel.text = "Vitaj vo svojom rezorte!";
        }

        public void BindState(IBuildingUpgradeState sharedState)
        {
            if (state != null) state.Changed -= RefreshValues;
            state = sharedState;
            if (!isActiveAndEnabled) return;
            if (state != null) state.Changed += RefreshValues;
            RefreshValues();
        }

        public void RefreshDemoValues() => RefreshValues();

        void RefreshValues()
        {
            var resort = state as ResortGameState;
            SetCoins(state?.Coins ?? demoCoins);
            int maximum = resort?.MaxEnergy ?? Mathf.Max(1, demoMaxEnergy);
            int energy = resort?.Energy ?? Mathf.Clamp(demoEnergy, 0, maximum);
            energyLabel.text = energy + " / " + maximum;
            long value = resort?.ResortValue ?? Mathf.Max(0, demoResortValue);
            resortLabel.text = value.ToString("N0", CultureInfo.GetCultureInfo("sk-SK"));
        }

        void LateUpdate()
        {
            if (menu != PauseMenuController.Instance)
            {
                RestoreLegacyButton();
                menu = PauseMenuController.Instance;
                if (menu != null)
                {
                    legacyPause = menu.PauseButton.GetComponent<HudLegacyPauseShortcut>();
                    if (legacyPause == null) legacyPause = menu.PauseButton.gameObject.AddComponent<HudLegacyPauseShortcut>();
                    legacyPause.Bind(this);
                }
            }
            // A standalone HUD remains usable, but cannot pause without the menu prefab.
            pauseButton.interactable = menu != null && menu.isActiveAndEnabled;
        }

        public void OpenPause()
        {
            if (PauseMenuController.Instance != null && PauseMenuController.Instance.isActiveAndEnabled) PauseMenuController.Instance.OpenPauseMenu();
        }
        void PreviewBuild() { statusLabel.text = "Stavanie • ukážka rozhrania"; PlayClick(); }
        public void SetCoins(int coins) =>
            coinsLabel.text = Mathf.Max(0, coins).ToString("N0", CultureInfo.GetCultureInfo("sk-SK"));

        public void SetStatus(string message) => statusLabel.text = message;

        void PreviewUpgrade()
        {
            if (UpgradeRequested != null)
                UpgradeRequested.Invoke();
            else
                SetStatus("Vylepšenia • ukážka rozhrania");
            PlayClick();
        }
        void PlayClick() { if (PauseMenuController.Instance != null) PauseMenuController.Instance.PlayClick(); }

        void RestoreLegacyButton()
        {
            if (legacyPause != null)
            {
                legacyPause.Release(this);
            }
            legacyPause = null;
        }
        void OnDisable()
        {
            if (state != null) state.Changed -= RefreshValues;
            pauseButton.onClick.RemoveListener(OpenPause);
            buildButton.onClick.RemoveListener(PreviewBuild);
            upgradeButton.onClick.RemoveListener(PreviewUpgrade);
            RestoreLegacyButton();
            menu = null;
        }
    }
}

