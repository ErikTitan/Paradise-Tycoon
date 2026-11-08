using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ParadiseTycoon.SceneFoundation;
using ParadiseTycoon.Scrum104;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace ParadiseTycoon.Hud.Editor
{
    /// <summary>Adds the existing HUD prefab without rebuilding the shared scene.</summary>
    public static class ResortHudIntegration
    {
        const string MainPath = "Assets/Scenes/Main.unity";

        [MenuItem("Tools/Paradise Tycoon/HUD/Integrate and validate Main scene")]
        public static void IntegrateMain()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var args = Environment.GetCommandLineArgs();
            int baselineArg = Array.IndexOf(args, "-hudSceneStateBaseline");
            string baseline = File.ReadAllText(baselineArg >= 0 ? args[baselineArg+1] : MainPath);
            var scene = EditorSceneManager.OpenScene(MainPath);
            var world = GameObject.Find("World");
            Require(world != null, "Main scene World root is required.");
            var before = WorldSnapshot(world);
            var menu = UnityEngine.Object.FindAnyObjectByType<PauseMenuController>();
            Require(menu != null && PrefabUtility.IsPartOfPrefabInstance(menu), "Keep Henrieta's pause menu prefab.");
            string pauseSource = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(menu);

            ResortHudBuilder.Integrate();
            Require(before == WorldSnapshot(world), "HUD integration must not change environment, buildings or decorations.");
            Require(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(menu) == pauseSource, "Pause prefab source changed.");
            ValidateScene();
            RestoreAutomaticFraming(baseline);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            // Reload the saved scene to verify the serialized result, not only in-memory state.
            EditorSceneManager.OpenScene(MainPath);
            ValidateScene();
            Debug.Log("SCRUM102_MAIN_INTEGRATION_OK: one HUD prefab, existing world preserved, pause references and saved scene verified.");
        }

        // ExecuteAlways scene-foundation scripts reframe the camera/ocean for the Editor's
        // current window during scene load. Do not serialize those incidental changes into a HUD commit.
        static void RestoreAutomaticFraming(string baseline)
        {
            foreach(var framing in UnityEngine.Object.FindObjectsByType<IslandCameraFraming>(FindObjectsSortMode.None))
            {
                RestoreTransform(framing.transform, baseline);
                var camera = framing.GetComponent<Camera>();
                var match = Regex.Match(Block(camera,baseline), @"(?m)^  orthographic size: ([^\r\n]+)");
                if(match.Success) camera.orthographicSize = float.Parse(match.Groups[1].Value,CultureInfo.InvariantCulture);
            }
            foreach(var ocean in UnityEngine.Object.FindObjectsByType<OceanBackdrop>(FindObjectsSortMode.None))
                RestoreTransform(ocean.transform,baseline);
            foreach(var safe in UnityEngine.Object.FindObjectsByType<SafeAreaRoot>(FindObjectsSortMode.None))
            {
                var rect = (RectTransform)safe.transform;
                string block = Block(rect,baseline);
                if(TryVector(block,"m_AnchorMin",out var min)) rect.anchorMin = min;
                if(TryVector(block,"m_AnchorMax",out var max)) rect.anchorMax = max;
            }
        }
        static void RestoreTransform(Transform transform, string baseline)
        {
            string block = Block(transform,baseline);
            if(TryVector(block,"m_LocalPosition",out var position)) transform.localPosition = position;
            if(TryVector(block,"m_LocalScale",out var scale)) transform.localScale = scale;
        }
        static string Block(UnityEngine.Object component, string baseline)
        {
            ulong id = GlobalObjectId.GetGlobalObjectIdSlow(component).targetObjectId;
            return Regex.Match(baseline,@"(?ms)^--- !u!\d+ &"+id+@"\r?\n(.*?)(?=^--- !u!|\z)").Groups[1].Value;
        }
        static bool TryVector(string block,string name,out Vector3 value)
        {
            var match=Regex.Match(block,@"(?m)^  "+name+@": \{x: ([^,]+), y: ([^,}]+)(?:, z: ([^}]+))?\}");
            value=Vector3.zero;
            if(!match.Success) return false;
            value.x=float.Parse(match.Groups[1].Value,CultureInfo.InvariantCulture);
            value.y=float.Parse(match.Groups[2].Value,CultureInfo.InvariantCulture);
            value.z=match.Groups[3].Success?float.Parse(match.Groups[3].Value,CultureInfo.InvariantCulture):0;
            return true;
        }

        public static void ValidateScene()
        {
            Require(SceneManager.GetActiveScene().path == MainPath, "Validate the actual Main scene.");
            var huds = UnityEngine.Object.FindObjectsByType<ResortHud>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Require(huds.Length == 1, "Main must contain exactly one HUD, including inactive objects.");
            Require(PrefabUtility.IsPartOfPrefabInstance(huds[0]), "HUD must remain linked to its standalone prefab.");
            Require(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1, "Exactly one EventSystem is required.");
            Require(UnityEngine.Object.FindObjectsByType<PauseMenuController>(FindObjectsSortMode.None).Length == 1, "Exactly one pause menu is required.");
            var canvas = huds[0].GetComponent<Canvas>();
            Require(canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay && canvas.sortingOrder == 20, "HUD Canvas configuration is invalid.");
            Require(huds[0].GetComponent<UnityEngine.UI.GraphicRaycaster>() != null, "HUD raycaster is required.");
            var scaler = huds[0].GetComponent<UnityEngine.UI.CanvasScaler>();
            Require(scaler != null && scaler.uiScaleMode == UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize, "Responsive CanvasScaler is required.");
            var serialized = new SerializedObject(huds[0]);
            foreach(string field in new[] {"coinsLabel", "energyLabel", "resortLabel", "statusLabel", "pauseButton", "buildButton", "upgradeButton"})
                Require(serialized.FindProperty(field).objectReferenceValue != null, "Missing HUD reference: " + field);
            foreach(var transform in huds[0].GetComponentsInChildren<Transform>(true))
                Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) == 0, "Missing HUD script on " + transform.name);
            Debug.Log("SCRUM102_MAIN_REFERENCES_OK");
        }

        static string WorldSnapshot(GameObject world)
        {
            var entries = new List<string>();
            foreach(var transform in world.GetComponentsInChildren<Transform>(true))
            {
                entries.Add(transform.gameObject.name + ":" + transform.gameObject.activeSelf);
                foreach(var component in transform.GetComponents<Component>())
                    entries.Add(component == null ? "<missing>" : component.GetType().FullName + ":" + EditorJsonUtility.ToJson(component));
            }
            return string.Join("\n",entries);
        }
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
