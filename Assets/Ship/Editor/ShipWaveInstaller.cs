using System;
using System.Linq;
using ParadiseTycoon.Buildings;
using ParadiseTycoon.Guests;
using ParadiseTycoon.Hud;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ParadiseTycoon.Ship.Editor
{
    /// <summary>Adds the ship offer panel, the boat and the wave controller to the open scene (SCRUM-131).</summary>
    public static class ShipWaveInstaller
    {
        static readonly Color Ink = new Color(.055f, .12f, .23f);
        static readonly Color Paper = new Color(.14f, .29f, .44f, .98f);
        static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Hud/Fonts/ResortHud SDF.asset");

        // Dock artwork sits at (9.6, 1.65) in IslandEnvironment; the boat moors on its sea side.
        static readonly Vector3 DockPosition = new Vector3(10.9f, .35f, 0);
        static readonly Vector3 SeaPosition = new Vector3(17.5f, -2.2f, 0);
        const float BoatWidth = 2.6f;

        [MenuItem("Tools/Paradise Tycoon/Ship/Install ship wave into active scene")]
        public static void Install()
        {
            var guestService = UnityEngine.Object.FindAnyObjectByType<GuestServiceController>(FindObjectsInactive.Include);
            if (guestService == null) throw new InvalidOperationException("Open Main: the scene needs a GuestServiceController.");
            var hud = UnityEngine.Object.FindAnyObjectByType<ResortHud>(FindObjectsInactive.Include);

            foreach (string name in new[] { "ShipWave", "GuestShip", "ShipRoute", "ShipOfferCanvas" })
            {
                var old = GameObject.Find(name);
                if (old != null) Undo.DestroyObjectImmediate(old);
            }

            var serviceObject = new SerializedObject(guestService);
            var stations = serviceObject.FindProperty("stations");
            var buildings = Enumerable.Range(0, stations.arraySize)
                .Select(i => stations.GetArrayElementAtIndex(i).FindPropertyRelative("building").objectReferenceValue as ResortBuilding)
                .Where(b => b != null).ToArray();

            // World: route markers and the boat.
            var world = GameObject.Find("World");
            var route = new GameObject("ShipRoute").transform;
            route.SetParent(world != null ? world.transform : null, false);
            var dock = Marker("DockPoint", route, DockPosition);
            var sea = Marker("SeaPoint", route, SeaPosition);

            var shipRoot = new GameObject("GuestShip").transform;
            shipRoot.SetParent(world != null ? world.transform : null, false);
            shipRoot.position = SeaPosition;
            var boatObject = new GameObject("Boat");
            boatObject.transform.SetParent(shipRoot, false);
            var boat = boatObject.AddComponent<SpriteRenderer>();
            boat.sprite = AssetDatabase.LoadAllAssetsAtPath("Assets/Textures/Harbor/Boats/guest_boat.png").OfType<Sprite>()
                .OrderByDescending(s => s.rect.width * s.rect.height).FirstOrDefault();
            boat.sortingLayerName = "Terrain";
            boat.sortingOrder = 13;
            if (boat.sprite != null)
            {
                float scale = BoatWidth / boat.sprite.bounds.size.x;
                boatObject.transform.localScale = new Vector3(scale, scale, 1);
                boatObject.transform.localPosition = -boat.sprite.bounds.center * scale;
            }
            var ship = shipRoot.gameObject.AddComponent<ShipVisual>();
            Assign(ship, ("seaPoint", sea), ("dockPoint", dock), ("boat", boat));

            // UI: own overlay canvas between the HUD (20) and modal panels (50+).
            var ui = GameObject.Find("UI");
            var canvasRect = Rect("ShipOfferCanvas", ui != null ? ui.transform : null);
            var canvas = canvasRect.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            var scaler = canvasRect.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 1;
            canvasRect.gameObject.AddComponent<GraphicRaycaster>();

            var card = Rect("ShipOfferPanel", canvasRect);
            Box(card, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-28, 10), new Vector2(380, 330));
            Surface(card, Paper, new Color(.08f, .17f, .30f));
            var title = Label(card, "Title", "Prichádza loď", new Vector2(22, -16), new Vector2(336, 36), 28, new Color(1f, .85f, .43f));
            var summary = Label(card, "Summary", "", new Vector2(22, -56), new Vector2(336, 28), 19, Color.white);
            summary.textWrappingMode = TextWrappingModes.Normal;
            var services = Label(card, "Services", "", new Vector2(22, -92), new Vector2(336, 136), 19, new Color(.80f, .92f, 1f));
            services.alignment = TextAlignmentOptions.TopLeft;
            var accept = Action(card, "Accept", "PRIJAŤ", new Vector2(22, -244), new Vector2(160, 64), new Color(.40f, .80f, .20f));
            var reject = Action(card, "Reject", "ODMIETNUŤ", new Vector2(198, -244), new Vector2(160, 64), new Color(.86f, .33f, .27f));
            var panel = card.gameObject.AddComponent<ShipOfferPanel>();
            Assign(panel, ("title", title), ("summary", summary), ("services", services), ("accept", accept), ("reject", reject));

            // Systems: the wave controller.
            var systems = GameObject.Find("Systems");
            var controllerObject = new GameObject("ShipWave");
            controllerObject.transform.SetParent(systems != null ? systems.transform : guestService.transform.parent, false);
            var controller = controllerObject.AddComponent<ShipWaveController>();
            var so = new SerializedObject(controller);
            so.FindProperty("guestService").objectReferenceValue = guestService;
            so.FindProperty("panel").objectReferenceValue = panel;
            so.FindProperty("ship").objectReferenceValue = ship;
            so.FindProperty("hud").objectReferenceValue = hud;
            so.FindProperty("arrivalClip").objectReferenceValue = Clip("prichod_lode");
            so.FindProperty("rejectClip").objectReferenceValue = Clip("odmietnutie_lode");
            so.FindProperty("completedClip").objectReferenceValue = Clip("dokoncenie_vlny");
            var list = so.FindProperty("buildings");
            list.arraySize = buildings.Length;
            for (int i = 0; i < buildings.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = buildings[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            foreach (var created in new[] { route.gameObject, shipRoot.gameObject, canvasRect.gameObject, controllerObject })
                Undo.RegisterCreatedObjectUndo(created, "Install ship wave");
            EditorSceneManager.MarkSceneDirty(controllerObject.scene);
            Selection.activeGameObject = controllerObject;
            Debug.Log($"SCRUM131_INSTALL_OK buildings={buildings.Length}");
        }

        static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Audio/SFX/{name}.wav");

        static Transform Marker(string name, Transform parent, Vector3 position)
        {
            var marker = new GameObject(name).transform;
            marker.SetParent(parent, false);
            marker.position = position;
            return marker;
        }

        static void Assign(UnityEngine.Object target, params (string field, UnityEngine.Object value)[] values)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in values) so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Same visual language as ResortHudBuilder.
        static RectTransform Rect(string name, Transform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            return rect;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        static void Box(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot; rect.anchoredPosition = pos; rect.sizeDelta = size;
        }

        static HudPanel Panel(RectTransform rect, Color top, Color bottom, float radius)
        {
            if (rect.GetComponent<CanvasRenderer>() == null) rect.gameObject.AddComponent<CanvasRenderer>();
            var panel = rect.gameObject.AddComponent<HudPanel>();
            panel.Style(top, bottom, radius);
            panel.raycastTarget = false;
            return panel;
        }

        static HudPanel Surface(RectTransform rect, Color top, Color bottom)
        {
            var shadow = Rect("Shadow", rect); Stretch(shadow); shadow.anchoredPosition = new Vector2(0, -6);
            Panel(shadow, new Color(.02f, .07f, .12f, .55f), new Color(.02f, .07f, .12f, .55f), 20);
            Panel(rect, Ink, Ink, 20);
            var inner = Rect("Face", rect); Stretch(inner); inner.offsetMin = Vector2.one * 4; inner.offsetMax = -Vector2.one * 4;
            return Panel(inner, top, bottom, 16);
        }

        static TMP_Text Label(Transform parent, string name, string content, Vector2 pos, Vector2 size, int fontSize, Color color)
        {
            var rect = Rect(name, parent); Box(rect, new Vector2(0, 1), new Vector2(0, 1), pos, size);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = Font; text.text = content; text.fontSize = fontSize; text.color = color;
            text.alignment = TextAlignmentOptions.MidlineLeft; text.fontStyle = FontStyles.Bold;
            text.enableAutoSizing = false; text.textWrappingMode = TextWrappingModes.NoWrap; text.raycastTarget = false;
            return text;
        }

        static Button Action(Transform parent, string name, string title, Vector2 pos, Vector2 size, Color color)
        {
            var rect = Rect(name, parent); Box(rect, new Vector2(0, 1), new Vector2(0, 1), pos, size);
            var face = Surface(rect, color, new Color(color.r * .65f, color.g * .70f, color.b * .72f));
            face.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            var navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
            var text = Label(rect, "Label", title, Vector2.zero, size, 22, Color.white);
            text.alignment = TextAlignmentOptions.Center;
            return button;
        }
    }
}
