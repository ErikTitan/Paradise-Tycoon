using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ParadiseTycoon.Buildings;
using ParadiseTycoon.GameState;
using ParadiseTycoon.Guests;
using ParadiseTycoon.Hud;
using ParadiseTycoon.Ship;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ParadiseTycoon.WaveResults.Editor
{
    public sealed class WaveResultsIntegrationTests
    {
        [Test]
        public void CalculationAndDuplicateDeliveryChecks() => Assert.That(WaveResultsChecks.Run(), Is.GreaterThan(400));

        [UnityTest]
        public IEnumerator TwoShipsOverloadDuplicateResultAndReload()
        {
            yield return new EnterPlayMode();
            const string main = "Assets/Scenes/Main.unity";
            string directory = Path.Combine(Path.GetTempPath(), "ParadiseTycoon-WaveResults-" + Guid.NewGuid().ToString("N"));
            Scene scene = default;
            ResortGameState state = null;
            ResortBuilding[] buildings = null;
            GuestServiceController guests = null;
            ShipWaveController ships = null;
            ShipVisual boat = null;
            ResortHud hud = null;
            void BindBeforeStart(Scene loaded, LoadSceneMode mode)
            {
                if (loaded.path != main) return;
                scene = loaded;
                var roots = loaded.GetRootGameObjects();
                T Find<T>() where T : Component => roots.SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();
                buildings = roots.SelectMany(root => root.GetComponentsInChildren<ResortBuilding>(true)).ToArray();
                state = new ResortGameState(directory, buildings, 10000, new ResortEconomySettings());
                Find<BuildingUpgradeController>().BindState(state);
                guests = Find<GuestServiceController>();
                ships = Find<ShipWaveController>();
                boat = Find<ShipVisual>();
                hud = Find<ResortHud>();
            }
            SceneManager.sceneLoaded += BindBeforeStart;
            try
            {
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(main, new LoadSceneParameters(LoadSceneMode.Additive));
                yield return null;
                var results = guests.GetComponent<WaveResultsController>();
                Assert.That(results, Is.Not.Null);
                var settings = Resources.Load<WaveResultsSettings>("WaveResultsSettings");
                Assert.That(settings, Is.Not.Null);
                Assert.That(settings.successfulWave, Is.Not.Null);
                for (int wave = 1; wave <= 2; wave++)
                {
                    int before = state.Coins;
                    string id = ships.CurrentOffer.WaveId;
                    Assert.That(ships.Accept(), Is.True);
                    boat.Complete();
                    yield return null;
                    yield return null;
                    Assert.That(guests.IsRunning, Is.True);
                    FinishService(guests);
                    Assert.That(results.Current, Is.Not.Null);
                    Assert.That(results.Current.WaveId, Is.EqualTo(id));
                    var expected = WaveRatingCalculator.Evaluate(GuestWaveSummaryAdapter.Create(id, guests.CaptureRequests()), settings.rating);
                    Assert.That(results.Current.Stars, Is.EqualTo(expected.Stars));
                    Assert.That(state.Coins, Is.EqualTo(before + expected.Coins));
                    Assert.That(state.CompletedWaves, Is.EqualTo(wave));
                    Assert.That(hud.CoinsText, Is.EqualTo(state.Coins.ToString("N0", CultureInfo.GetCultureInfo("sk-SK"))));
                    results.RetryPendingResult();
                    results.enabled = false;
                    results.enabled = true;
                    Assert.That(state.Coins, Is.EqualTo(before + expected.Coins));
                    var loaded = new ResortGameState(directory, buildings, 10000, new ResortEconomySettings());
                    Assert.That(loaded.Coins, Is.EqualTo(state.Coins));
                    Assert.That(loaded.HasCompletedWave(id), Is.True);
                    Assert.That(loaded.TryBeginWave(id), Is.False);
                    boat.Complete();
                    Assert.That(ships.AwaitingResultAcknowledgement, Is.True);
                    Assert.That(ships.Accept(), Is.False);
                    results.ReturnToIsland();
                    Assert.That(results.Current, Is.Null);
                    Assert.That(ships.State, Is.EqualTo(ShipWaveState.Offered));
                    Assert.That(guests.IsRunning, Is.False);
                    Assert.That(ships.CurrentOffer.WaveId, Is.Not.EqualTo(id));
                }
                var passengers = Enumerable.Range(1, 80).Select(index => new AcceptedGuest {
                    guestId = "overload-" + index, serviceIds = new[] { "hotel" }
                }).ToArray();
                Assert.That(guests.TryStartWave("overload-test", passengers), Is.True);
                FinishService(guests);
                Assert.That(results.Current.Satisfaction, Is.LessThan(1));
                Assert.That(results.Current.ServicesByImpact[0].Stats.ServiceId, Is.EqualTo("hotel"));
                Assert.That(results.Current.ServicesByImpact[0].AverageWaitSeconds, Is.GreaterThan(settings.rating.FullSatisfactionWaitSeconds));
                results.ReturnToIsland();
                Assert.That(state.CompletedWaves, Is.EqualTo(3));
            }
            finally
            {
                SceneManager.sceneLoaded -= BindBeforeStart;
                if (scene.IsValid()) SceneManager.UnloadSceneAsync(scene);
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
            yield return null;
            yield return new ExitPlayMode();
        }

        static void FinishService(GuestServiceController guests)
        {
            for (int step = 0; step < 12000 && guests.IsRunning; step++) guests.SendMessage("Advance", .25f);
            Assert.That(guests.IsRunning, Is.False, "Guest service did not finish or the reward could not be saved.");
        }
    }
}
