using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ParadiseTycoon.Hud.Editor
{
    public static class ResortHudValidation
    {
        [MenuItem("Tools/Paradise Tycoon/HUD/Validate responsive layout")]
        public static void Validate()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ResortHudBuilder.PrefabPath);
            if (prefab == null) throw new Exception("Missing HUD prefab");
            var instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                instance.GetComponent<CanvasScaler>().enabled = false;
                instance.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                instance.GetComponentInChildren<ParadiseTycoon.SceneFoundation.SafeAreaRoot>().enabled = false;
                var root = (RectTransform)instance.transform;
                var safe = (RectTransform)root.Find("SafeArea");
                int scenario = 0;
                foreach (var size in new[] {new Vector2(1280,720), new Vector2(1600,720), new Vector2(1600,720)})
                {
                    root.sizeDelta = size;
                    // Third case also reserves notch and bottom gesture regions.
                    safe.anchorMin = scenario == 2 ? new Vector2(.05f,.04f) : Vector2.zero;
                    safe.anchorMax = scenario == 2 ? new Vector2(.95f,.98f) : Vector2.one;
                    Canvas.ForceUpdateCanvases();
                    var groups = new[] {"Resources/Coins", "Resources/Energy", "Resources/ResortValue", "Pause", "Build", "Upgrade", "StatusCard"};
                    Rect[] bounds = groups.Select(path => Bounds((RectTransform)safe.Find(path), safe)).ToArray();
                    for (int i = 0; i < bounds.Length; i++)
                    {
                        Require(safe.rect.Contains(bounds[i].min) && safe.rect.Contains(bounds[i].max), groups[i] + " outside safe area");
                        for (int j = i+1; j < bounds.Length; j++) Require(!bounds[i].Overlaps(bounds[j]), groups[i] + " overlaps " + groups[j]);
                    }
                    foreach (var text in instance.GetComponentsInChildren<Text>())
                    {
                        Require(text.preferredWidth <= text.rectTransform.rect.width + 1, "Text clipped: " + text.name);
                        Require(text.preferredHeight <= text.rectTransform.rect.height + 1, "Text height clipped: " + text.name);
                    }
                    Debug.Log("SCRUM102_LAYOUT_PASS " + size);
                    scenario++;
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
            Debug.Log("SCRUM102_LAYOUT_OK");
        }
        static Rect Bounds(RectTransform rect, RectTransform relative)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var min = relative.InverseTransformPoint(corners[0]); var max = relative.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }
        static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }

        // Explicit automation entry point; never runs during normal project use.
        public static void Smoke()
        {
            Validate();
            EditorSceneManager.OpenScene(ResortHudBuilder.DemoPath);
            var args = Environment.GetCommandLineArgs();
            int width = args.Contains("-hud20x9") ? 1600 : 1280;
            var assembly = typeof(UnityEditor.Editor).Assembly;
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes = singleton.GetProperty("instance", BindingFlags.Static|BindingFlags.Public|BindingFlags.FlattenHierarchy).GetValue(null);
            var groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType");
            var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] {Enum.Parse(groupType,"Standalone")});
            var sizeType = assembly.GetType("UnityEditor.GameViewSize");
            var sizeKind = assembly.GetType("UnityEditor.GameViewSizeType");
            var fixedSize = Activator.CreateInstance(sizeType, new object[] {Enum.Parse(sizeKind,"FixedResolution"),width,720,"HUD verification"});
            group.GetType().GetMethod("AddCustomSize").Invoke(group,new[] {fixedSize});
            int index = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null)-1;
            var view = EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
            view.GetType().GetMethod("SizeSelectionCallback",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).Invoke(view,new object[]{index,null});
            view.Show(); view.Focus();
            // Startup executeMethod runs before the Editor has finished restoring its state.
            EditorApplication.delayCall += () => { view.Focus(); EditorApplication.isPlaying = true; };
        }
    }
}
