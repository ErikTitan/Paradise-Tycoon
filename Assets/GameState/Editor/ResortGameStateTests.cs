using System;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ParadiseTycoon.Buildings;
using ParadiseTycoon.Hud;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParadiseTycoon.GameState.Editor
{
    public sealed class ResortGameStateTests
    {
        Scene scene;
        string directory;
        ResortBuilding[] buildings;
        ResortHud hud;
        string SavePath => Path.Combine(directory, ResortGameState.SaveFileName);

        [SetUp]
        public void OpenIsolatedMainScene()
        {
            directory = Path.Combine(Path.GetTempPath(), "ParadiseTycoon-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Main.unity");
            var roots = scene.GetRootGameObjects();
            buildings = roots.SelectMany(root => root.GetComponentsInChildren<ResortBuilding>(true)).ToArray();
            hud = roots.SelectMany(root => root.GetComponentsInChildren<ResortHud>(true)).Single();
        }

        [TearDown]
        public void CloseIsolatedMainScene()
        {
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            Directory.Delete(directory, true);
        }

        ResortGameState Open(int coins = 10000) => new(directory, buildings, coins, new ResortEconomySettings());

        void CheckHud(int coins, int value)
        {
            var culture = CultureInfo.GetCultureInfo("sk-SK");
            Assert.That(hud.CoinsText, Is.EqualTo(coins.ToString("N0", culture)));
            Assert.That(hud.EnergyText, Is.EqualTo("80 / 100"));
            Assert.That(hud.ResortText, Is.EqualTo(value.ToString("N0", culture)));
        }

        [Test]
        public void MainLayoutPurchaseRewardReloadAndNewGame()
        {
            var state = Open();
            hud.BindState(state);
            Assert.That(buildings.Select(b => b.BuildingId), Is.EquivalentTo(
                new[] { "hotel", "restaurant", "beach_bar", "pool", "bungalow", "spa" }));
            foreach (string id in new[] { "hotel", "restaurant", "beach_bar" })
                Assert.That(state.GetBuildingLevel(id, -1), Is.EqualTo(1));
            foreach (string id in new[] { "pool", "bungalow", "spa" })
                Assert.That(state.GetBuildingLevel(id, -1), Is.Zero);
            Assert.That(state.Gems, Is.Zero);
            CheckHud(10000, 3000);

            Assert.That(state.TryPurchaseUpgrade("hotel", 1, 450), Is.True);
            CheckHud(9550, 3450);
            Assert.That(state.TryBeginWave("boat-1"), Is.True);
            Assert.That(state.TryPurchaseUpgrade("restaurant", 1, 350), Is.False);
            Assert.That(state.TrySaveBetweenWaves(), Is.False);
            Assert.That(state.TryCompleteWave("wrong-boat", 100), Is.False);
            Assert.That(state.TryCompleteWave("boat-1", -1), Is.False);
            Assert.That(state.TryCompleteWave("boat-1", 200), Is.True);
            Assert.That(state.TryCompleteWave("boat-1", 200), Is.False);
            CheckHud(9750, 3450);

            var restored = Open();
            hud.BindState(restored);
            CheckHud(9750, 3450);
            Assert.That(restored.GetBuildingLevel("hotel", -1), Is.EqualTo(2));
            Assert.That(restored.IsWaveActive, Is.False);
            Assert.That(restored.CompletedWaves, Is.EqualTo(1));
            Assert.That(restored.TryBeginWave("boat-1"), Is.False);
            Assert.That(restored.TryNewGame(), Is.True);
            CheckHud(10000, 3000);
            var fresh = Open();
            Assert.That(fresh.GetBuildingLevel("hotel", -1), Is.EqualTo(1));
            Assert.That(fresh.CompletedWaves, Is.Zero);
        }

        [Test]
        public void InvalidPurchasesAndFailedWritesPreserveTheCheckpoint()
        {
            var state = Open(350);
            string original = File.ReadAllText(SavePath);
            Assert.That(state.TryPurchaseUpgrade("hotel", 1, 450), Is.False);
            Assert.That(state.TryPurchaseUpgrade("hotel", 1, 1), Is.False);
            Assert.That(state.TryPurchaseUpgrade("pool", 0, 1), Is.False);
            Assert.That(state.TryPurchaseUpgrade("restaurant", 2, 700), Is.False);
            var blocked = Directory.CreateDirectory(SavePath + ".tmp");
            Assert.That(state.TryPurchaseUpgrade("restaurant", 1, 350), Is.False);
            Assert.That(state.Coins, Is.EqualTo(350));
            Assert.That(state.GetBuildingLevel("restaurant", -1), Is.EqualTo(1));
            Assert.That(state.TryBeginWave("retry"), Is.True);
            Assert.That(state.TryCompleteWave("retry", 100), Is.False);
            Assert.That(state.IsWaveActive, Is.True);
            Assert.That(state.CompletedWaves, Is.Zero);
            Assert.That(File.ReadAllText(SavePath), Is.EqualTo(original));
            blocked.Delete();
            Assert.That(state.TryCompleteWave("retry", 100), Is.True);
            Assert.That(Open().Coins, Is.EqualTo(450));
        }

        [Test]
        public void SavedCurrenciesTakePrecedenceOverNewGameDefaults()
        {
            var settings = new ResortEconomySettings { startingEnergy = 27, maxEnergy = 60, startingGems = 11 };
            var state = new ResortGameState(directory, buildings, 123, settings);
            Assert.That(state.TrySaveBetweenWaves(), Is.True);
            var restored = Open();
            Assert.That(restored.Coins, Is.EqualTo(123));
            Assert.That(restored.Energy, Is.EqualTo(27));
            Assert.That(restored.MaxEnergy, Is.EqualTo(60));
            Assert.That(restored.Gems, Is.EqualTo(11));
        }

        [Test]
        public void ActiveWaveRestartsAtTheLastBetweenWaveCheckpoint()
        {
            var state = Open();
            Assert.That(state.TryBeginWave("interrupted"), Is.True);
            var restored = Open();
            Assert.That(restored.IsWaveActive, Is.False);
            Assert.That(restored.TryBeginWave("interrupted"), Is.True);
            Assert.That(restored.TryCompleteWave("interrupted", 0), Is.True);
            Assert.That(restored.CompletedWaves, Is.EqualTo(1));
        }

        [Test]
        public void MigratesThePreviousUpgradeSave()
        {
            string entries = string.Join(",", buildings.Select(b =>
                $"{{\"id\":\"{b.BuildingId}\",\"level\":{(b.BuildingId == "hotel" ? 2 : b.InitialLevel)}}}"));
            string legacy = Path.Combine(directory, "building-upgrades-v1.json");
            File.WriteAllText(legacy, $"{{\"version\":1,\"coins\":731,\"buildings\":[{entries}]}}");
            var migrated = Open();
            Assert.That(migrated.Coins, Is.EqualTo(731));
            Assert.That(migrated.GetBuildingLevel("hotel", -1), Is.EqualTo(2));
            Assert.That(migrated.Energy, Is.EqualTo(80));
            Assert.That(File.Exists(SavePath), Is.True);
            Assert.That(Open().ResortValue, Is.EqualTo(3450));
        }

        [TestCase("{\"version\":99}")]
        [TestCase("{broken")]
        public void IncompatibleOrDamagedSaveIsPreserved(string content)
        {
            File.WriteAllText(SavePath, content);
            var state = Open();
            Assert.That(state.SaveError, Is.Not.Null);
            Assert.That(state.TrySaveBetweenWaves(), Is.False);
            Assert.That(state.TryPurchaseUpgrade("hotel", 1, 450), Is.False);
            Assert.That(File.ReadAllText(SavePath), Is.EqualTo(content));
        }
    }
}
