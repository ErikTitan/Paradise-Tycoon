using System;
using System.Collections.Generic;
using System.Linq;
using ParadiseTycoon.Buildings;
using ParadiseTycoon.GameState;
using ParadiseTycoon.Guests;
using ParadiseTycoon.Ship;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParadiseTycoon.WaveResults
{
    public sealed class WaveResultsIntegration : MonoBehaviour
    {
        GuestServiceController guests;
        ResortGameState state;
        WaveResultsController results;
        WaveRatingRules rules;
        Func<string, IReadOnlyList<GuestRequestResult>, int> rewardCalculator;
        Dictionary<string, string> names;
        ShipWaveController ship;
        WaveResultsSettings settings;
        Func<string, IReadOnlyList<GuestRequestResult>, AudioClip> completionSound;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            InstallInScene(SceneManager.GetActiveScene());
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => InstallInScene(scene);

        static void InstallInScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            var roots = scene.GetRootGameObjects();
            var sources = roots.SelectMany(root => root.GetComponentsInChildren<GuestServiceController>(true)).ToArray();
            var upgrades = roots.SelectMany(root => root.GetComponentsInChildren<BuildingUpgradeController>(true)).ToArray();
            if (sources.Length == 0) return;
            if (sources.Length != 1 || upgrades.Length != 1)
            {
                Debug.LogError("Wave results require one guest service and one shared resort state in the scene.");
                return;
            }
            if (sources[0].GetComponent<WaveResultsIntegration>() != null) return;
            sources[0].gameObject.AddComponent<WaveResultsIntegration>();
        }

        void Start()
        {
            var roots = gameObject.scene.GetRootGameObjects();
            var upgrades = roots.SelectMany(root => root.GetComponentsInChildren<BuildingUpgradeController>(true)).Single();
            Initialize(GetComponent<GuestServiceController>(), upgrades, roots);
        }

        void Initialize(GuestServiceController source, BuildingUpgradeController upgrades, GameObject[] roots)
        {
            guests = source;
            state = upgrades.GameState;
            if (state == null || guests.CalculateWaveReward != null)
            {
                Debug.LogError("Wave results could not connect to the shared state or reward calculator.", this);
                enabled = false;
                return;
            }
            names = roots.SelectMany(root => root.GetComponentsInChildren<ResortBuilding>(true))
                .Where(building => building != null && !string.IsNullOrEmpty(building.BuildingId))
                .GroupBy(building => building.BuildingId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First().DisplayName, StringComparer.Ordinal);
            settings = Resources.Load<WaveResultsSettings>("WaveResultsSettings");
            rules = settings != null ? settings.rating : new WaveRatingRules();
            rules.Validate();
            ship = roots.SelectMany(root => root.GetComponentsInChildren<ShipWaveController>(true)).FirstOrDefault();
            results = gameObject.AddComponent<WaveResultsController>();
            results.Bind(guests, new SettledRewardStore(state), ReturnToIsland, DisplayName, rules);
            rewardCalculator = CalculateReward;
            guests.CalculateWaveReward = rewardCalculator;

            if (ship != null && ship.CompletionSound == null)
            {
                completionSound = SelectCompletionSound;
                ship.CompletionSound = completionSound;
            }
        }

        string DisplayName(string id) => names.TryGetValue(id, out var name) ? name : id;

        int CalculateReward(string waveId, IReadOnlyList<GuestRequestResult> requests)
        {
            var result = WaveRatingCalculator.Evaluate(GuestWaveSummaryAdapter.Create(waveId, requests, DisplayName), rules);
            if (ship != null) ship.SetResultPending(true);
            return result.Coins;
        }

        void ReturnToIsland()
        {
            if (ship != null) ship.SetResultPending(false);
        }

        AudioClip SelectCompletionSound(string waveId, IReadOnlyList<GuestRequestResult> requests)
        {
            var result = WaveRatingCalculator.Evaluate(
                GuestWaveSummaryAdapter.Create(waveId, requests, DisplayName), rules);
            return result.Stars >= 4 && settings != null ? settings.successfulWave : null;
        }

        void OnDestroy()
        {
            if (guests != null && guests.CalculateWaveReward == rewardCalculator) guests.CalculateWaveReward = null;
            if (ship != null && ship.CompletionSound == completionSound) ship.CompletionSound = null;
        }

        sealed class SettledRewardStore : IWaveRewardStore
        {
            readonly ResortGameState state;
            public SettledRewardStore(ResortGameState state) => this.state = state;
            public WaveResult ApplyRewardOnce(WaveResult result)
            {
                if (!state.HasCompletedWave(result.WaveId))
                    throw new InvalidOperationException("Wave reward has not been saved by the shared state.");
                return result;
            }
        }
    }
}
