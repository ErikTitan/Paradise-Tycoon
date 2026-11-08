using System.Globalization;
using ParadiseTycoon.Scrum104;
using UnityEngine;
using UnityEngine.UI;

namespace ParadiseTycoon.Hud
{
    /// <summary>Demo HUD. Values are presentation data, not an economy implementation.</summary>
    public sealed class ResortHud : MonoBehaviour
    {
        [SerializeField] Text coinsLabel, energyLabel, resortLabel, statusLabel;
        [SerializeField] Button pauseButton, buildButton, upgradeButton;
        [SerializeField, Min(0)] int demoCoins = 1250;
        [SerializeField, Min(0)] int demoEnergy = 80;
        [SerializeField, Min(1)] int demoMaxEnergy = 100;
        [SerializeField, Min(0)] int demoResortValue = 24500;
        PauseMenuController menu;
        CanvasGroup legacyPause;
        float oldAlpha;
        bool oldInteractable, oldBlocks;
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
            RefreshDemoValues();
        }

        public void RefreshDemoValues()
        {
            var format = CultureInfo.GetCultureInfo("sk-SK");
            coinsLabel.text = Mathf.Max(0, demoCoins).ToString("N0", format);
            energyLabel.text = Mathf.Clamp(demoEnergy, 0, Mathf.Max(1, demoMaxEnergy)) + " / " + Mathf.Max(1, demoMaxEnergy);
            resortLabel.text = Mathf.Max(0, demoResortValue).ToString("N0", format);
            statusLabel.text = "Vitaj vo svojom rezorte!";
        }

        void LateUpdate()
        {
            if (menu != PauseMenuController.Instance)
            {
                RestoreLegacyButton();
                menu = PauseMenuController.Instance;
                if (menu != null)
                {
                    legacyPause = menu.PauseButton.GetComponent<CanvasGroup>();
                    if (legacyPause == null) legacyPause = menu.PauseButton.gameObject.AddComponent<CanvasGroup>();
                    oldAlpha = legacyPause.alpha;
                    oldInteractable = legacyPause.interactable;
                    oldBlocks = legacyPause.blocksRaycasts;
                    legacyPause.alpha = 0;
                    legacyPause.interactable = legacyPause.blocksRaycasts = false;
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
        void PreviewUpgrade() { statusLabel.text = "Vylepšenia • ukážka rozhrania"; PlayClick(); }
        void PlayClick() { if (PauseMenuController.Instance != null) PauseMenuController.Instance.PlayClick(); }

        void RestoreLegacyButton()
        {
            if (legacyPause != null)
            {
                legacyPause.alpha = oldAlpha;
                legacyPause.interactable = oldInteractable;
                legacyPause.blocksRaycasts = oldBlocks;
            }
            legacyPause = null;
        }
        void OnDisable()
        {
            pauseButton.onClick.RemoveListener(OpenPause);
            buildButton.onClick.RemoveListener(PreviewBuild);
            upgradeButton.onClick.RemoveListener(PreviewUpgrade);
            RestoreLegacyButton();
            menu = null;
        }
    }
}
