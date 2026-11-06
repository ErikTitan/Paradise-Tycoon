using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using ParadiseTycoon.SceneFoundation;

namespace ParadiseTycoon.SceneFoundation.Editor
{
    public static class MainSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";
        public const string PausePrefabPath = "Assets/Scrum104/Generated/PauseMenu.prefab";

        [MenuItem("Tools/Paradise Tycoon/Create main scene foundation")]
        public static void Create()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("Main.unity already exists. Open it and integrate prefabs there; this tool never overwrites it.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var pausePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PausePrefabPath);
            if (pausePrefab == null) throw new InvalidOperationException("The SCRUM-104 pause menu prefab is required.");
            ConfigureRendering();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var systems = Child("Systems", null);
            var cameraObject = Child("Main Camera", systems);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.gameObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.04f, .55f, .66f);
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 100f;
            cameraObject.gameObject.AddComponent<AudioListener>();
            cameraObject.gameObject.AddComponent<IslandCameraFraming>().Refresh();
            var events = Child("EventSystem", systems);
            events.gameObject.AddComponent<EventSystem>();
            events.gameObject.AddComponent<InputSystemUIInputModule>();

            var world = Child("World", null);
            var environment = Child("Environment", world);
            var environmentPreview = Child("IntegrationPreview_ReplaceWithEnvironmentPrefab", environment);
            var sea = FlatSprite("Ocean", environmentPreview, "Environment/Water/ocean_background.png", 40f, "Background");
            sea.gameObject.AddComponent<OceanBackdrop>().SetCamera(camera);
            FlatSprite("Island", environmentPreview, "Environment/Terrain/island_base.png", 18f, "Terrain");
            var buildings = Child("Buildings", world);
            var buildingPreview = Child("IntegrationPreview_ReplaceWithBuildingsPrefab", buildings);
            WorldObject("Hotel_L1_ScaleExample", buildingPreview, "Buildings/Hotel/hotel_L1.png", new Vector2(-.5f, .3f), 3.7f);
            WorldObject("Restaurant_L1_ScaleExample", buildingPreview, "Buildings/Restaurant/restaurant_L1.png", new Vector2(-4.2f, -.7f), 3.1f);
            WorldObject("BeachBar_L1_ScaleExample", buildingPreview, "Buildings/BeachBar/beach_bar_L1.png", new Vector2(3.6f, -1.3f), 2.6f);
            var decorations = Child("Decorations", world);
            var sortingPreview = Child("IntegrationPreview_SortingExamples", decorations);
            WorldObject("Palm_BehindHotel", sortingPreview, "Environment/Vegetation/palm_a.png", new Vector2(-.8f, 1.7f), 1.6f);
            WorldObject("Palm_InFrontOfHotel", sortingPreview, "Environment/Vegetation/palm_b.png", new Vector2(.5f, -.2f), 1.7f);
            Child("Characters", world);

            var ui = Child("UI", null);
            var canvasObject = new GameObject("MainCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasObject.transform.SetParent(ui, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = .5f;
            var safe = new GameObject("SafeArea", typeof(RectTransform), typeof(SafeAreaRoot));
            safe.transform.SetParent(canvasObject.transform, false);
            Stretch((RectTransform)safe.transform);
            var hudSlot = new GameObject("HUD_IntegrationSlot", typeof(RectTransform));
            hudSlot.transform.SetParent(safe.transform, false);
            Stretch((RectTransform)hudSlot.transform);
            var pause = (GameObject)PrefabUtility.InstantiatePrefab(pausePrefab, safe.transform);
            pause.name = "PauseMenu";
            Stretch((RectTransform)pause.transform);
            pause.GetComponent<Canvas>().overrideSorting = true;
            pause.GetComponent<Canvas>().sortingOrder = 100;
            pause.GetComponent<UnityEngine.UI.CanvasScaler>().enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(pause.GetComponent<RectTransform>());
            PrefabUtility.RecordPrefabInstancePropertyModifications(pause.GetComponent<Canvas>());
            PrefabUtility.RecordPrefabInstancePropertyModifications(pause.GetComponent<UnityEngine.UI.CanvasScaler>());

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            var oldScenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            oldScenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = oldScenes.ToArray();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("MAIN_SCENE_CREATED: " + ScenePath);
        }

        static Transform Child(string name, Transform parent)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        static Sprite SourceSprite(string relativePath)
        {
            // Existing imports contain multiple slices; select the main image, not tiny alpha fragments.
            var sprite = AssetDatabase.LoadAllAssetsAtPath("Assets/Textures/" + relativePath).OfType<Sprite>()
                .OrderByDescending(s => s.rect.width * s.rect.height).FirstOrDefault();
            if (sprite == null) throw new InvalidOperationException("Missing sprite: " + relativePath);
            return sprite;
        }

        static SpriteRenderer FlatSprite(string name, Transform parent, string path, float width, string layer)
        {
            var renderer = Child(name, parent).gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = SourceSprite(path);
            renderer.sortingLayerName = layer;
            renderer.transform.localScale = Vector3.one * (width / renderer.sprite.bounds.size.x);
            renderer.transform.localPosition = -Vector3.Scale(renderer.sprite.bounds.center, renderer.transform.localScale);
            return renderer;
        }

        static void WorldObject(string name, Transform parent, string path, Vector2 footprint, float width)
        {
            var root = Child(name, parent);
            root.localPosition = footprint;
            var group = root.gameObject.AddComponent<SortingGroup>();
            group.sortingLayerName = "WorldObjects";
            group.sortingOrder = 0;
            var renderer = FlatSprite("Visual", root, path, width, "WorldObjects");
            renderer.spriteSortPoint = SpriteSortPoint.Pivot;
            var bounds = renderer.sprite.bounds;
            renderer.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, 0f) * renderer.transform.localScale.x;
        }

        static void EnsureSortingLayers()
        {
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = settings.FindProperty("m_SortingLayers");
            string[] names = { "Background", "Terrain", "WorldObjects", "WorldOverlay", "UI" };
            int[] ids = { 1080101, 1080102, 1080103, 1080104, 1080105 };
            for (int n = 0; n < names.Length; n++)
            {
                if (SortingLayer.layers.Any(layer => layer.name == names[n])) continue;
                int index = layers.arraySize;
                layers.InsertArrayElementAtIndex(index);
                var element = layers.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("name").stringValue = names[n];
                element.FindPropertyRelative("uniqueID").intValue = ids[n];
                element.FindPropertyRelative("locked").boolValue = false;
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("Tools/Paradise Tycoon/Apply main scene rendering policy")]
        public static void ConfigureRendering()
        {
            EnsureSortingLayers();
            // URP's 2D renderer owns sprite distance sorting; the camera setting alone is insufficient.
            var rendererData = AssetDatabase.LoadMainAssetAtPath("Assets/Settings/Renderer2D.asset");
            if (rendererData == null) throw new InvalidOperationException("The project's 2D renderer is missing.");
            var settings = new SerializedObject(rendererData);
            settings.FindProperty("m_TransparencySortMode").intValue = (int)TransparencySortMode.CustomAxis;
            settings.FindProperty("m_TransparencySortAxis").vector3Value = Vector3.up;
            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }
    }
}
