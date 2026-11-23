using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ParadiseTycoon.Buildings;
using ParadiseTycoon.GameState;
using ParadiseTycoon.Guests;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ParadiseTycoon.Ship.Editor
{
    public sealed class ShipWaveRestartTests
    {
        [UnityTest]
        public IEnumerator NextShipAfterRestartUsesAnUnusedWaveId()
        {
            yield return new EnterPlayMode();
            string directory = Path.Combine(Path.GetTempPath(), "ParadiseTycoon-" + Guid.NewGuid().ToString("N"));
            const string main = "Assets/Scenes/Main.unity";
            Scene scene = default;
            ResortGameState state = null;
            ShipWaveController ships = null;
            ShipVisual boat = null;
            GuestServiceController guests = null;
            void BindBeforeStart(Scene loadedScene, LoadSceneMode mode)
            {
                if (loadedScene.path != main) return;
                scene = loadedScene;
                var roots = scene.GetRootGameObjects();
                T Find<T>() where T : Component =>
                    roots.SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();
                var buildings = roots.SelectMany(root => root.GetComponentsInChildren<ResortBuilding>(true)).ToArray();
                // A save from an earlier session in which the first two waves were finished.
                var earlier = new ResortGameState(directory, buildings, 10000, new ResortEconomySettings());
                foreach (string id in new[] { "wave-001", "wave-002" })
                {
                    Assert.That(earlier.TryBeginWave(id), Is.True);
                    Assert.That(earlier.TryCompleteWave(id, 100), Is.True);
                }
                // "Restart": a fresh state object loads that save, as the game does on launch.
                state = new ResortGameState(directory, buildings, 10000, new ResortEconomySettings());
                Find<BuildingUpgradeController>().BindState(state);
                ships = Find<ShipWaveController>();
                boat = Find<ShipVisual>();
                guests = Find<GuestServiceController>();
            }
            SceneManager.sceneLoaded += BindBeforeStart;
            try
            {
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(main, new LoadSceneParameters(LoadSceneMode.Additive));
                yield return null;

                Assert.That(state.CompletedWaves, Is.EqualTo(2));
                Assert.That(ships.CurrentOffer.WaveId, Is.EqualTo("wave-003"));
                Assert.That(ships.WaveNumber, Is.EqualTo(3));

                Assert.That(ships.Accept(), Is.True);
                boat.Complete();
                yield return null;
                yield return null;
                // The ship must not turn back: the shared state accepted the new wave and guests disembark.
                Assert.That(ships.State, Is.EqualTo(ShipWaveState.Running));
                Assert.That(guests.IsRunning, Is.True);
                Assert.That(state.ActiveWaveId, Is.EqualTo("wave-003"));
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
