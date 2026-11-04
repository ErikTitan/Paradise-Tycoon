using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using ParadiseTycoon.Scrum104;

namespace ParadiseTycoon.SceneFoundation.Editor
{
    public static class MainSceneValidation
    {
        // Batch verification also works without any automation package in the project.
        public static void ValidateFromDisk()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use Validate from the menu in an interactive Editor.");
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(MainSceneBuilder.ScenePath);
            Validate();
        }

        [MenuItem("Tools/Paradise Tycoon/Validate main scene foundation")]
        public static void Validate()
        {
            Require(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path == MainSceneBuilder.ScenePath,
                "Open Assets/Scenes/Main.unity first.");
            Require(UnityEngine.Object.FindObjectsByType<Camera>().Length == 1, "Exactly one camera is required.");
            Require(UnityEngine.Object.FindObjectsByType<AudioListener>().Length == 1, "Exactly one AudioListener is required.");
            Require(UnityEngine.Object.FindObjectsByType<EventSystem>().Length == 1, "Exactly one EventSystem is required.");
            foreach (string path in new[] { "Systems", "World/Environment", "World/Buildings", "World/Decorations",
                "World/Characters", "UI/MainCanvas/SafeArea/HUD_IntegrationSlot", "UI/MainCanvas/SafeArea/PauseMenu" })
                Require(GameObject.Find(path) != null, "Missing integration slot: " + path);
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) == 0,
                    "Missing script on " + transform.name + ": " + string.Join(", ",
                        transform.GetComponents<Component>().Select(c => c == null ? "<missing>" : c.GetType().FullName)));
                foreach (var component in transform.GetComponents<Component>().Where(c => c != null))
                {
                    var property = new SerializedObject(component).GetIterator();
                    while (property.NextVisible(true))
                        if (property.propertyType == SerializedPropertyType.ObjectReference)
                            Require(property.objectReferenceValue != null || property.objectReferenceEntityIdValue == EntityId.None,
                                "Missing reference: " + transform.name + "/" + property.propertyPath);
                }
            }
            var pause = UnityEngine.Object.FindFirstObjectByType<PauseMenuController>();
            Require(pause != null && pause.PauseButton != null && pause.ResumeButton != null && pause.VolumeSlider != null,
                "Pause menu UI references must be assigned.");
            Require(pause.MusicSource != null && pause.MusicSource.clip != null && pause.UiSource != null, "Pause menu audio references must be assigned.");
            Require(PrefabUtility.IsPartOfPrefabInstance(pause), "Keep the pause menu connected to its source prefab.");
            var camera = Camera.main;
            var framing = camera.GetComponent<IslandCameraFraming>();
            Require(framing != null, "Camera framing is missing.");
            try
            {
                VerifyFraming(camera, framing, 1280, 720, new Rect(0, 0, 1280, 720));
                VerifyFraming(camera, framing, 1600, 720, new Rect(0, 0, 1600, 720));
                VerifyFraming(camera, framing, 2400, 1080, new Rect(100, 35, 2200, 1045));
                VerifyFraming(camera, framing, 720, 1280, new Rect(0, 0, 720, 1280));
                VerifySorting();
            }
            finally
            {
                camera.ResetAspect();
                framing.Refresh();
                UnityEngine.Object.FindFirstObjectByType<OceanBackdrop>().Refresh();
            }
            Require(!PlayerSettings.allowedAutorotateToPortrait && !PlayerSettings.allowedAutorotateToPortraitUpsideDown,
                "Mobile orientation must remain landscape.");
            Require(EditorBuildSettings.scenes.First(s => s.enabled).path == MainSceneBuilder.ScenePath,
                "Main must be the first enabled build scene.");
            Debug.Log("MAIN_SCENE_VALIDATION_OK: references, slots, 16:9, 20:9, notched safe area, portrait fallback, rendered Y sorting, landscape, build entry.");
        }

        static void VerifyFraming(Camera camera, IslandCameraFraming framing, int width, int height, Rect safeArea)
        {
            camera.aspect = (float)width / height;
            framing.Frame(width, height, safeArea);
            Rect usable = framing.UsableViewport(width, height, safeArea);
            Bounds bounds = framing.WorldBounds;
            foreach (float x in new[] { bounds.min.x, bounds.max.x })
            foreach (float y in new[] { bounds.min.y, bounds.max.y })
            {
                Vector3 point = camera.WorldToViewportPoint(new Vector3(x, y, 0f));
                Require(point.z > 0f && point.x >= usable.xMin - .0001f && point.x <= usable.xMax + .0001f
                    && point.y >= usable.yMin - .0001f && point.y <= usable.yMax + .0001f,
                    "Island clipped at " + width + "x" + height);
            }
        }

        static void VerifySorting()
        {
            var cameraObject = new GameObject("SortingValidationCamera");
            var camera = cameraObject.AddComponent<Camera>();
            var target = new RenderTexture(64, 64, 16);
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white); texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1f);
            var readback = new Texture2D(64, 64, TextureFormat.RGB24, false);
            var back = SortingSquare("Back", sprite, Color.red, .2f);
            var front = SortingSquare("Front", sprite, Color.blue, -.2f);
            var oldTarget = RenderTexture.active;
            try
            {
                camera.enabled = false;
                camera.cullingMask = 1 << 31;
                camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographic = true; camera.orthographicSize = 2f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.transparencySortMode = TransparencySortMode.CustomAxis;
                camera.transparencySortAxis = Vector3.up;
                camera.targetTexture = target;
                Color CenterPixel()
                {
                    // Both renders happen in one Editor frame, before Unity's normal group update.
                    SortingGroup.UpdateAllSortingGroups();
                    camera.Render(); RenderTexture.active = target;
                    readback.ReadPixels(new Rect(0, 0, 64, 64), 0, 0); readback.Apply();
                    return readback.GetPixel(32, 32);
                }
                Color first = CenterPixel();
                Require(first.b > .8f && first.r < .2f, "Lower-Y group should render in front.");
                back.transform.position = new Vector3(0, -.2f, 0);
                front.transform.position = new Vector3(0, .2f, 0);
                Color swapped = CenterPixel();
                Require(swapped.r > .8f && swapped.b < .2f, "Y sorting must follow moved group footprints.");
            }
            finally
            {
                RenderTexture.active = oldTarget;
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(back);
                UnityEngine.Object.DestroyImmediate(front);
                UnityEngine.Object.DestroyImmediate(sprite);
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(readback);
                target.Release(); UnityEngine.Object.DestroyImmediate(target);
            }
        }

        static GameObject SortingSquare(string name, Sprite sprite, Color color, float y)
        {
            var root = new GameObject(name); root.layer = 31;
            root.transform.position = new Vector3(0, y, 0);
            var group = root.AddComponent<SortingGroup>(); group.sortingLayerName = "WorldObjects";
            var visual = new GameObject("Visual"); visual.layer = 31; visual.transform.SetParent(root.transform, false);
            var renderer = visual.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.color = color;
            renderer.sortingLayerName = "WorldObjects";
            return root;
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
