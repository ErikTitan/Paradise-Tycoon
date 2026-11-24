using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ParadiseTycoon.Buildings;
using ParadiseTycoon.Guests;
using ParadiseTycoon.Hud;
using ParadiseTycoon.Ship;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ParadiseTycoon.GameState.Editor
{
    public sealed class BuildingUpgradeWaveTests
    {
        [UnityTest]
        public IEnumerator MainSceneAppliesAnUpgradeBetweenTwoShipsAndRestoresIt()
        {
            yield return new EnterPlayMode();
            const string main = "Assets/Scenes/Main.unity";
            const int startingCoins = 10000;
            string directory = Path.Combine(Path.GetTempPath(), "ParadiseTycoon-" + Guid.NewGuid().ToString("N"));
            var load = new LoadSceneParameters(LoadSceneMode.Additive);
            Scene scene = default;
            BuildingUpgradeController upgrades = null;
            ResortBuilding[] buildings = null;
            ResortBuilding hotel = null;
            ResortGameState state = null;
            ResortHud hud = null;
            BuildingUpgradePanel panel = null;
            ShipWaveController ships = null;
            ShipVisual boat = null;
            GuestServiceController guests = null;

            void BindBeforeStart(Scene loaded, LoadSceneMode mode)
            {
                if (loaded.path != main) return;
                scene = loaded;
                var roots = scene.GetRootGameObjects();
                T Find<T>() where T : Component => roots.SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();
                buildings = roots.SelectMany(root => root.GetComponentsInChildren<ResortBuilding>(true)).ToArray();
                hotel = buildings.Single(building => building.BuildingId == "hotel");
                state = new ResortGameState(directory, buildings, startingCoins, new ResortEconomySettings());
                upgrades = Find<BuildingUpgradeController>();
                upgrades.BindState(state);
                hud = Find<ResortHud>();
                panel = Find<BuildingUpgradePanel>();
                ships = Find<ShipWaveController>();
                boat = Find<ShipVisual>();
                guests = Find<GuestServiceController>();

                var offerSettings = new SerializedObject(ships);
                offerSettings.FindProperty("seed").intValue = 17;
                offerSettings.FindProperty("minGuests").intValue = 6;
                offerSettings.FindProperty("maxGuests").intValue = 6;
                offerSettings.FindProperty("maxRequestsPerGuest").intValue = 3;
                offerSettings.ApplyModifiedPropertiesWithoutUndo();
            }

            SceneManager.sceneLoaded += BindBeforeStart;
            try
            {
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(main, load);
                yield return null;

                var visual = hotel.GetComponentInChildren<SpriteRenderer>();
                var originalSprite = visual.sprite;
                var groundPosition = hotel.transform.position;
                var originalBounds = visual.bounds;
                var sortingLayer = hotel.SortingGroup.sortingLayerID;
                int sortingOrder = hotel.SortingGroup.sortingOrder;
                int price = hotel.GetLevel(2).Cost;
                var buy = panel.GetComponentsInChildren<Button>(true).Single(button => button.name == "Buy");
                var feedback = panel.GetComponentsInChildren<TMP_Text>(true).Single(label => label.name == "Feedback");
                var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
                var culture = CultureInfo.GetCultureInfo("sk-SK");

                var limitedFunds = new ResortGameState(Path.Combine(directory, "limited-funds"), buildings,
                    price - 1, new ResortEconomySettings());
                upgrades.BindState(limitedFunds);
                upgrades.SendMessage("Open", hotel);
                Assert.That(buy.interactable, Is.False);
                Assert.That(feedback.text, Does.Contain("Chýba 1 mincí."));
                buy.OnPointerClick(pointer);
                Assert.That(limitedFunds.Coins, Is.EqualTo(price - 1));
                Assert.That(hotel.Level, Is.EqualTo(1));
                upgrades.BindState(state);

                var serviceTimes = new float[2];
                for (int wave = 0; wave < serviceTimes.Length; wave++)
                {
                    Assert.That(ships.CurrentOffer.RequestsPerService.Any(entry => entry.Key == "hotel"), Is.True);
                    int before = state.Coins;
                    if (wave == 1)
                    {
                        upgrades.SendMessage("Open", hotel);
                        Assert.That(buy.interactable, Is.True);
                        buy.OnPointerClick(pointer);
                        Assert.That(state.Coins, Is.EqualTo(before), "Opening confirmation must not purchase.");
                    }
                    Assert.That(ships.Accept(), Is.True);
                    Assert.That(buy.interactable, Is.False, "Lock upgrades during ship arrival.");
                    boat.Complete();
                    yield return null;
                    yield return null;
                    Assert.That(guests.IsRunning, Is.True);
                    Assert.That(buy.interactable, Is.False, "Keep the lock at docking and during service.");
                    Assert.That(feedback.text, Does.Contain("medzi vlnami"));
                    buy.OnPointerClick(pointer);
                    Assert.That(state.Coins, Is.EqualTo(before));
                    Assert.That(hotel.Level, Is.EqualTo(wave + 1));

                    serviceTimes[wave] = FinishAndMeasureHotelService(guests);
                    boat.Complete();
                    Assert.That(state.CompletedWaves, Is.EqualTo(wave + 1));
                    Assert.That(state.IsWaveActive, Is.False);
                    Assert.That(state.Coins, Is.GreaterThan(before));
                    Assert.That(hud.CoinsText, Is.EqualTo(state.Coins.ToString("N0", culture)));

                    if (wave != 0) continue;
                    int earned = state.Coins;
                    Assert.That(buy.interactable, Is.True);
                    buy.OnPointerClick(pointer);
                    Assert.That(state.Coins, Is.EqualTo(earned));
                    buy.OnPointerClick(pointer);
                    buy.OnPointerClick(pointer);
                    Assert.That(state.Coins, Is.EqualTo(earned - price), "The confirmed purchase is charged once.");
                    Assert.That(hotel.Level, Is.EqualTo(2));
                    Assert.That(hud.CoinsText, Is.EqualTo(state.Coins.ToString("N0", culture)));
                    Assert.That(visual.sprite, Is.EqualTo(hotel.GetLevel(2).Sprite).And.Not.EqualTo(originalSprite));
                    Assert.That(visual.bounds.min.y, Is.EqualTo(originalBounds.min.y).Within(.001f));
                    Assert.That(hotel.transform.position, Is.EqualTo(groundPosition));
                    Assert.That(hotel.SortingGroup.sortingLayerID, Is.EqualTo(sortingLayer));
                    Assert.That(hotel.SortingGroup.sortingOrder, Is.EqualTo(sortingOrder));
                }

                Assert.That(serviceTimes[1], Is.LessThan(serviceTimes[0]), "The next wave uses the purchased service speed.");
                int savedCoins = state.Coins;
                yield return SceneManager.UnloadSceneAsync(scene);
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(main, load);
                yield return null;
                Assert.That(state.CompletedWaves, Is.EqualTo(2));
                Assert.That(state.Coins, Is.EqualTo(savedCoins));
                Assert.That(hotel.Level, Is.EqualTo(2));
                Assert.That(hotel.GetComponentInChildren<SpriteRenderer>().sprite, Is.EqualTo(hotel.GetLevel(2).Sprite));
                Assert.That(ships.CurrentOffer.WaveId, Is.EqualTo(ShipOfferGenerator.WaveId(3)));
            }
            finally
            {
                SceneManager.sceneLoaded -= BindBeforeStart;
                if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
            yield return new ExitPlayMode();
        }

        static float FinishAndMeasureHotelService(GuestServiceController guests)
        {
            const float interval = .25f;
            string guestId = null;
            float elapsed = 0;
            float duration = 0;
            for (int step = 0; step < 12000 && guests.IsRunning; step++)
            {
                guests.SendMessage("Advance", interval);
                if (duration != 0) continue;
                var hotelRequests = guests.CaptureRequests().Where(request => request.ServiceId == "hotel");
                if (guestId == null)
                {
                    guestId = hotelRequests.FirstOrDefault(request => request.State == GuestRequestState.InService).GuestId;
                }
                else
                {
                    elapsed += interval;
                    if (hotelRequests.Any(request => request.GuestId == guestId && request.State == GuestRequestState.Completed))
                        duration = elapsed;
                }
            }
            Assert.That(guests.IsRunning, Is.False, "The ship must finish all guest service and settle the wave.");
            Assert.That(duration, Is.GreaterThan(0), "Measure the actual processing time of a hotel request.");
            return duration;
        }
    }
}
