using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ParadiseTycoon.Buildings;
using ParadiseTycoon.Guests;
using ParadiseTycoon.Hud;
using ParadiseTycoon.WaveResults;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ParadiseTycoon.GameState.Editor
{
    public sealed class ResortWaveIntegrationTests
    {
        [UnityTest]
        public IEnumerator AcceptedGuestPaysOnceAndRefreshesTheMainHud()
        {
            yield return new EnterPlayMode();
            string directory = Path.Combine(Path.GetTempPath(), "ParadiseTycoon-" + Guid.NewGuid().ToString("N"));
            const string main = "Assets/Scenes/Main.unity";
            Scene scene = default;
            ResortBuilding[] buildings = null;
            ResortGameState state = null;
            BuildingUpgradeController upgrades = null;
            GuestServiceController guests = null;
            ResortHud hud = null;
            void BindBeforeStart(Scene loadedScene, LoadSceneMode mode)
            {
                if (loadedScene.path != main) return;
                scene = loadedScene;
                var roots = scene.GetRootGameObjects();
                T Find<T>() where T : Component =>
                    roots.SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();
                buildings = roots.SelectMany(root => root.GetComponentsInChildren<ResortBuilding>(true)).ToArray();
                state = new ResortGameState(directory, buildings, 10000, new ResortEconomySettings());
                upgrades = Find<BuildingUpgradeController>();
                upgrades.BindState(state);
                guests = Find<GuestServiceController>();
                hud = Find<ResortHud>();
            }
            SceneManager.sceneLoaded += BindBeforeStart;
            try
            {
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(main, new LoadSceneParameters(LoadSceneMode.Additive));
                yield return null;

                var passengers = new[] { new AcceptedGuest { guestId = "guest", serviceIds = new[] { "hotel" } } };
                int completions = 0;
                guests.WaveCompleted += (id, results) => completions++;
                // Accepting the ship locks upgrades before its passengers reach the dock.
                upgrades.SetWaveActive(true);
                Assert.That(state.TryPurchaseUpgrade("hotel", 1, 450), Is.False);
                Assert.That(guests.TryStartWave("boat-test", passengers), Is.True);
                Assert.That(state.ActiveWaveId, Is.EqualTo("boat-test"));
                Assert.That(state.TryBeginWave("other-boat"), Is.False);
                Assert.That(state.IsWaveActive, Is.True);
                Assert.That(state.TryPurchaseUpgrade("hotel", 1, 450), Is.False);
                // Drive the same simulation as Update without waiting for the full visit in real time.
                for (int step = 0; step < 300 && guests.IsRunning; step++)
                    guests.SendMessage("Advance", 1f);
                Assert.That(completions, Is.EqualTo(1));
                var rating = Resources.Load<WaveResultsSettings>("WaveResultsSettings").rating;
                int expectedCoins = 10000 + WaveRatingCalculator.Evaluate(
                    GuestWaveSummaryAdapter.Create("boat-test", guests.CaptureRequests()), rating).Coins;
                Assert.That(state.Coins, Is.EqualTo(expectedCoins));
                Assert.That(guests.TryStartWave("boat-test", passengers), Is.False);
                Assert.That(state.TryPurchaseUpgrade("hotel", 1, 450), Is.True);
                Assert.That(buildings.Single(b => b.BuildingId == "hotel").Level, Is.EqualTo(2));
                hud.gameObject.SetActive(false);
                hud.gameObject.SetActive(true);
                Assert.That(hud.CoinsText, Is.EqualTo((expectedCoins - 450).ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("sk-SK"))));
                var loaded = new ResortGameState(directory, buildings, 10000, new ResortEconomySettings());
                Assert.That(loaded.Coins, Is.EqualTo(expectedCoins - 450));
                Assert.That(loaded.CompletedWaves, Is.EqualTo(1));
            }
            finally
            {
                SceneManager.sceneLoaded -= BindBeforeStart;
                if (scene.IsValid()) SceneManager.UnloadSceneAsync(scene);
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
            yield return new ExitPlayMode();
        }
    }
}
