using System;
using System.IO;
using ParadiseTycoon.SceneFoundation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ParadiseTycoon.Hud.Editor
{
    public static class ResortHudBuilder
    {
        public const string PrefabPath = "Assets/Hud/Prefabs/ResortHud.prefab";
        public const string DemoPath = "Assets/Hud/Scenes/HudDemo.unity";
        static readonly Color Ink = new Color(.055f, .12f, .23f);
        static readonly Color Paper = new Color(.14f, .29f, .44f, .98f);
        static readonly Color Teal = new Color(.18f, .60f, .88f);
        static TMPro.TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Hud/Fonts/ResortHud SDF.asset");

        [MenuItem("Tools/Paradise Tycoon/HUD/Create prefab and integration demo")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Assets/Hud/Prefabs");
            Directory.CreateDirectory("Assets/Hud/Scenes");
            var root = new GameObject("ResortHud", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            try
            {
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 20;
                var scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280, 720);
                scaler.matchWidthOrHeight = 1;
                var hud = root.AddComponent<ResortHud>();
                var safe = Rect("SafeArea", root.transform);
                Stretch(safe); safe.gameObject.AddComponent<SafeAreaRoot>();
                var stats = Rect("Resources", safe);
                Box(stats, new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -24), new Vector2(820, 88));
                var coins = Stat(stats, "Coins", "MINCE", "1 250", 0, new Color(1f, .74f, .18f), HudIcon.Shape.Coin);
                var energy = Stat(stats, "Energy", "ENERGIA", "80 / 100", 270, new Color(.34f, .88f, 1f), HudIcon.Shape.Energy);
                var value = Stat(stats, "ResortValue", "HODNOTA REZORTU", "24 500", 540, new Color(.90f, .65f, 1f), HudIcon.Shape.Resort);
                var pause = Action(safe, "Pause", "II  PAUZA", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-28, -24), new Vector2(156, 88), Teal);
                var build = Action(safe, "Build", "+  STAVAŤ", Vector2.zero, Vector2.zero, new Vector2(28, 28), new Vector2(232, 76), new Color(.40f,.80f,.20f));
                var upgrade = Action(safe, "Upgrade", "VYLEPŠIŤ", Vector2.zero, Vector2.zero, new Vector2(276, 28), new Vector2(232, 76), new Color(1f,.68f,.14f));
                var status = Rect("StatusCard", safe);
                Box(status, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-28, 28), new Vector2(390, 76));
                Surface(status, Paper, new Color(.08f,.17f,.30f));
                Label(status, "DemoBadge", "PARADISE TYCOON", new Vector2(18, -8), new Vector2(354, 22), 13, new Color(1f,.85f,.43f));
                var statusLabel = Label(status, "Message", "Vitaj vo svojom rezorte!", new Vector2(18, -30), new Vector2(354, 32), 21, Color.white);
                var serialized = new SerializedObject(hud);
                Assign(serialized, "coinsLabel", coins); Assign(serialized, "energyLabel", energy);
                Assign(serialized, "resortLabel", value); Assign(serialized, "statusLabel", statusLabel);
                Assign(serialized, "pauseButton", pause); Assign(serialized, "buildButton", build); Assign(serialized, "upgradeButton", upgrade);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            AssetDatabase.Refresh();
            // A copy of Main proves integration without rewriting the shared scene.
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            Integrate();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), DemoPath, false);
            AssetDatabase.SaveAssets();
            Debug.Log("SCRUM102_BUILD_OK " + PrefabPath + " and " + DemoPath);
        }

        [MenuItem("Tools/Paradise Tycoon/HUD/Integrate prefab into active scene")]
        public static void Integrate()
        {
            if (UnityEngine.Object.FindAnyObjectByType<ResortHud>(FindObjectsInactive.Include) == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (prefab == null) throw new InvalidOperationException("Build the HUD prefab first.");
                var ui = GameObject.Find("UI");
                Undo.RegisterCreatedObjectUndo(PrefabUtility.InstantiatePrefab(prefab, ui != null ? ui.transform : null), "Integrate resort HUD");
            }
            // Hide the menu's old shortcut in the authored scene too, so Edit-mode previews
            // show the same single pause control as Play mode. Keep the menu prefab untouched.
            var menu = UnityEngine.Object.FindAnyObjectByType<ParadiseTycoon.Scrum104.PauseMenuController>();
            if (menu != null && menu.PauseButton != null)
            {
                Undo.RecordObject(menu.PauseButton.gameObject,"Use HUD pause shortcut");
                var visibility = menu.PauseButton.GetComponent<HudLegacyPauseShortcut>();
                if (visibility == null) visibility = Undo.AddComponent<HudLegacyPauseShortcut>(menu.PauseButton.gameObject);
                visibility.Bind(UnityEngine.Object.FindAnyObjectByType<ResortHud>(FindObjectsInactive.Include));
                EditorUtility.SetDirty(visibility);
                menu.PauseButton.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(menu.PauseButton.gameObject);
            }
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }

        static void Assign(SerializedObject target, string field, UnityEngine.Object value) => target.FindProperty(field).objectReferenceValue = value;
        static RectTransform Rect(string name, Transform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false); return rect;
        }
        static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        static void Box(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        { rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot; rect.anchoredPosition = pos; rect.sizeDelta = size; }
        static HudPanel Panel(RectTransform rect, Color top, Color bottom, float radius)
        {
            if (rect.GetComponent<CanvasRenderer>() == null) rect.gameObject.AddComponent<CanvasRenderer>();
            var panel = rect.gameObject.AddComponent<HudPanel>(); panel.Style(top,bottom,radius); panel.raycastTarget=false; return panel;
        }
        static HudPanel Surface(RectTransform rect, Color top, Color bottom)
        {
            var shadow = Rect("Shadow",rect); Stretch(shadow); shadow.anchoredPosition = new Vector2(0,-6);
            Panel(shadow,new Color(.02f,.07f,.12f,.55f),new Color(.02f,.07f,.12f,.55f),20);
            Panel(rect,Ink,Ink,20);
            var inner = Rect("Face",rect); Stretch(inner); inner.offsetMin=Vector2.one*4; inner.offsetMax=-Vector2.one*4;
            return Panel(inner,top,bottom,16);
        }
        static TMPro.TMP_Text Label(Transform parent, string name, string content, Vector2 pos, Vector2 size, int fontSize, Color color)
        {
            var rect = Rect(name, parent); Box(rect, new Vector2(0, 1), new Vector2(0, 1), pos, size);
            var text = rect.gameObject.AddComponent<TMPro.TextMeshProUGUI>(); text.font = Font; text.text = content;
            text.fontSize = fontSize; text.color = color; text.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
            text.fontStyle = TMPro.FontStyles.Bold;
            text.enableAutoSizing = false; text.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            text.raycastTarget = false; return text;
        }
        static TMPro.TMP_Text Stat(Transform parent, string name, string title, string value, float x, Color accent, HudIcon.Shape shape)
        {
            var card = Rect(name, parent); Box(card, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, 0), new Vector2(254, 88));
            Surface(card, Paper, new Color(.08f,.17f,.30f));
            var disk = Rect("IconDisk",card); Box(disk,new Vector2(0,1),new Vector2(0,1),new Vector2(12,-13),new Vector2(62,62));
            Panel(disk,Ink,Ink,31);
            var icon = Rect("Icon",disk); Stretch(icon); icon.offsetMin=Vector2.one*7; icon.offsetMax=-Vector2.one*7;
            icon.gameObject.AddComponent<CanvasRenderer>();
            var graphic = icon.gameObject.AddComponent<HudIcon>(); graphic.SetShape(shape); graphic.color=accent; graphic.raycastTarget=false;
            Label(card, "Title", title, new Vector2(84, -10), new Vector2(162, 24), 13, accent);
            return Label(card, "Value", value, new Vector2(84, -34), new Vector2(162, 42), 30, Color.white);
        }
        static Button Action(Transform parent, string name, string title, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color color)
        {
            var rect = Rect(name, parent); Box(rect, anchor, pivot, pos, size);
            var face = Surface(rect, color, new Color(color.r*.65f,color.g*.70f,color.b*.72f)); face.raycastTarget=true;
            var shine = Rect("Shine",rect); Box(shine,new Vector2(0,1),new Vector2(0,1),new Vector2(14,-8),new Vector2(size.x-28,4));
            Panel(shine,new Color(1,1,1,.35f),new Color(1,1,1,.35f),2);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = face;
            var navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
            var text = Label(rect, "Label", title, Vector2.zero, size, 26, Color.white); text.alignment = TMPro.TextAlignmentOptions.Center;
            return button;
        }
    }
}

