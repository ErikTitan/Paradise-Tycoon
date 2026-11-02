using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using ParadiseTycoon.Scrum104;

public static class Scrum104Builder
{
    public static void Smoke()
    {
        EditorSceneManager.OpenScene("Assets/Scrum104/Generated/PauseMenuDemo.unity");
        EditorApplication.isPlaying = true;
    }
    const string Root = "Assets/Scrum104/";
    static Font Font => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    static readonly Color Ink = new Color(.06f,.17f,.22f);
    static readonly Color Sea = new Color(.04f,.47f,.49f);
    static GameObject Obj(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        if (parent != null) go.transform.SetParent(parent, false);
        return go;
    }
    static void Box(GameObject go, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var r = (RectTransform)go.transform;
        r.anchorMin = r.anchorMax = anchor;
        r.anchoredPosition = pos; r.sizeDelta = size;
    }
    static void Stretch(GameObject go)
    {
        var r = (RectTransform)go.transform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }
    static Text Label(string name, Transform parent, string text, Vector2 pos, Vector2 size, int fontSize, Color color)
    {
        var go = Obj(name,parent); Box(go,Vector2.one*.5f,pos,size);
        var t = go.AddComponent<Text>(); t.font = Font; t.text = text;
        t.fontSize = fontSize; t.color = color; t.alignment = TextAnchor.MiddleCenter;
        t.raycastTarget = false;
        return t;
    }
    static Button Button(string name, Transform parent, string title, Vector2 pos, Vector2 size)
    {
        var go = Obj(name,parent); Box(go,Vector2.one*.5f,pos,size);
        var img = go.AddComponent<Image>(); img.color = Sea;
        var b = go.AddComponent<Button>(); b.targetGraphic = img;
        var colors = b.colors; colors.highlightedColor = new Color(.8f,1,1); colors.pressedColor = new Color(.6f,.8f,.8f); b.colors = colors;
        Label("Label",go.transform,title,Vector2.zero,size,27,Color.white);
        return b;
    }
    static Canvas Canvas(string name)
    {
        var go = Obj(name,null); var c = go.AddComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280,720); scaler.matchWidthOrHeight = .5f;
        go.AddComponent<GraphicRaycaster>(); return c;
    }
    [MenuItem("Tools/SCRUM-104/Create demo and prefab")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        AssetDatabase.Refresh();
        System.IO.Directory.CreateDirectory(Root+"Generated");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var camera = new GameObject("Main Camera",typeof(Camera),typeof(AudioListener));
        camera.tag = "MainCamera";
        camera.GetComponent<Camera>().backgroundColor = new Color(.04f,.15f,.2f);
        camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
        var events = new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
        var demo = Canvas("DemoCanvas");
        var clock = demo.gameObject.AddComponent<DemoClock>();
        clock.label = Label("Clock",demo.transform,"",new Vector2(0,80),new Vector2(800,200),28,Color.white);
        var marker = Obj("Moving marker",demo.transform); Box(marker,Vector2.one*.5f,new Vector2(0,-110),new Vector2(36,36));
        marker.AddComponent<Image>().color = new Color(.95f,.75f,.33f); clock.marker = (RectTransform)marker.transform;
        Label("Title",demo.transform,"PARADISE TYCOON",new Vector2(0,230),new Vector2(800,80),40,Color.white);
        var canvas = Canvas("PauseMenu"); canvas.sortingOrder = 100;
        var controller = canvas.gameObject.AddComponent<PauseMenuController>();
        var music = canvas.gameObject.AddComponent<AudioSource>(); music.playOnAwake = false;
        music.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/hudba_hry.wav");
        var ui = canvas.gameObject.AddComponent<AudioSource>(); ui.playOnAwake = false;
        var pause = Button("PauseButton",canvas.transform,"Pauza",Vector2.zero,new Vector2(150,64));
        Box(pause.gameObject,new Vector2(1,1),new Vector2(-100,-55),new Vector2(150,64));
        var overlay = Obj("PausePanel",canvas.transform); Stretch(overlay); overlay.AddComponent<Image>().color = new Color(0,0,0,.7f);
        var card = Obj("Card",overlay.transform); Box(card,Vector2.one*.5f,Vector2.zero,new Vector2(560,460));
        card.AddComponent<Image>().color = new Color(.94f,.96f,.91f);
        Label("Title",card.transform,"Hra je pozastavená",new Vector2(0,153),new Vector2(520,65),36,Ink);
        var volumeLabel = Label("VolumeLabel",card.transform,"Hlasitosť",new Vector2(0,52),new Vector2(480,55),26,Ink);
        var sliderGo = Obj("VolumeSlider",card.transform); Box(sliderGo,Vector2.one*.5f,new Vector2(0,-7),new Vector2(430,48));
        sliderGo.AddComponent<Image>().color = new Color(.8f,.85f,.83f);
        var slider = sliderGo.AddComponent<Slider>(); slider.minValue = 0; slider.maxValue = 1;
        var fillArea = Obj("FillArea",sliderGo.transform); Stretch(fillArea);
        var fill = Obj("Fill",fillArea.transform); Stretch(fill); fill.AddComponent<Image>().color = Sea;
        var handleArea = Obj("HandleArea",sliderGo.transform); Stretch(handleArea);
        var hr = (RectTransform)handleArea.transform; hr.offsetMin = new Vector2(14,0); hr.offsetMax = new Vector2(-14,0);
        var handle = Obj("Handle",handleArea.transform); Box(handle,Vector2.one*.5f,Vector2.zero,new Vector2(28,48));
        handle.AddComponent<Image>().color = new Color(.96f,.75f,.33f);
        slider.fillRect = (RectTransform)fill.transform; slider.handleRect = (RectTransform)handle.transform; slider.targetGraphic = handle.GetComponent<Image>();
        Label("Hint",card.transform,"0 % = stlmené • nastavenie sa uloží automaticky",new Vector2(0,-66),new Vector2(500,45),19,Ink);
        var resume = Button("ResumeButton",card.transform,"Pokračovať",new Vector2(0,-147),new Vector2(430,68));
        var so = new SerializedObject(controller);
        void Assign(string key, Object value) { so.FindProperty(key).objectReferenceValue = value; }
        Assign("panel",overlay); Assign("pauseButton",pause); Assign("resumeButton",resume); Assign("volumeSlider",slider);
        Assign("volumeLabel",volumeLabel); Assign("musicSource",music); Assign("uiSource",ui);
        Assign("clickClip",AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/kliknutie.wav"));
        so.ApplyModifiedPropertiesWithoutUndo();
        if (music.clip == null || so.FindProperty("clickClip").objectReferenceValue == null) throw new System.Exception("Missing audio assets");
        overlay.SetActive(false);
        var prefab = PrefabUtility.SaveAsPrefabAsset(canvas.gameObject,Root+"Generated/PauseMenu.prefab");
        Object.DestroyImmediate(canvas.gameObject);
        PrefabUtility.InstantiatePrefab(prefab);
        EditorSceneManager.SaveScene(scene,Root+"Generated/PauseMenuDemo.unity");
        AssetDatabase.SaveAssets();
        Debug.Log("SCRUM104_BUILD_OK");
    }
}
