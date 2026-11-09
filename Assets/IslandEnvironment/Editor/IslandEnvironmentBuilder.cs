using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using ParadiseTycoon.SceneFoundation;

namespace ParadiseTycoon.Island.Editor
{
    public static class IslandEnvironmentBuilder
    {
        public const string Folder = "Assets/IslandEnvironment";
        public const string PrefabPath = Folder + "/Prefabs/IslandEnvironment.prefab";
        public const string DemoPath = Folder + "/Scenes/EnvironmentReview.unity";
        static Material spriteMaterial;
        static Transform paths, markers;
        static int pathIndex;
        static readonly Vector2[] Entrances = {
            new Vector2(-7.5f,2.5f), new Vector2(-1.4f,2.6f), new Vector2(4,3),
            new Vector2(-6.5f,-2.3f), new Vector2(-1.6f,-1), new Vector2(5.8f,-2.5f), new Vector2(8.2f,1.2f) };
        static readonly string[] Names = { "Bungalow", "Hotel", "Spa", "Restaurant", "Pool", "BeachBar", "Harbor" };

        [MenuItem("Tools/Paradise Tycoon/Island/Build environment and review scene")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            foreach (string dir in new[] { "Prefabs", "Scenes", "Generated" }) Directory.CreateDirectory(Folder + "/" + dir);
            AssetDatabase.Refresh();
            // Work on a copy only. Main belongs to the scene integrator.
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            EditorSceneManager.SaveScene(scene, DemoPath);
            foreach(var stray in scene.GetRootGameObjects().Where(g=>g.GetComponent<SpriteRenderer>()!=null && Mathf.Abs(g.transform.position.x)>20).ToArray()) UnityEngine.Object.DestroyImmediate(stray);
            spriteMaterial = GameObject.Find("World/Environment/IslandEnvironment/Island").GetComponent<SpriteRenderer>().sharedMaterial;
            var old = GameObject.Find("World/Environment");
            foreach (Transform t in old.transform.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(t.gameObject);
            var decorations = GameObject.Find("World/Decorations");
            foreach (Transform t in decorations.transform.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(t.gameObject);
            var root = Child("IslandEnvironment", null);
            var ground = Child("TerrainAndCoast", root);
            var sea = GameObject.CreatePrimitive(PrimitiveType.Quad); sea.name="Ocean"; sea.transform.SetParent(ground,false);
            UnityEngine.Object.DestroyImmediate(sea.GetComponent<Collider>());
            sea.transform.localPosition=new Vector3(0,0,5); sea.transform.localScale=new Vector3(55,35,1);
            var seaMaterial=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Generated/Ocean.mat");
            if(seaMaterial==null) { seaMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(seaMaterial,Folder+"/Generated/Ocean.mat"); }
            seaMaterial.SetTexture("_BaseMap",Source("Environment/Water/ocean_background.png").texture);
            seaMaterial.SetColor("_BaseColor",Color.white); seaMaterial.renderQueue=2000;
            sea.GetComponent<MeshRenderer>().sharedMaterial=seaMaterial;
            sea.GetComponent<MeshRenderer>().sortingLayerName="Background";
            // This artwork already includes continuous sand, rocks, shallow water and foam.
            Flat("Island_ContinuousCoast", ground, "Environment/Terrain/island_base_large_v2.png", Vector2.zero, 28, "Terrain", 0);
            CreatePaths(root);
            var harbor = Child("HarborApproach", root);
            Flat("DockLanding", harbor, "Harbor/Docks/wooden_dock.png", new Vector2(9.6f,1.65f), 3.7f, "Terrain", 12);
            var plants = Child("Vegetation", root);
            Prop("Palm_West", plants, "Environment/Vegetation/palm_a.png", -11,-2,1.9f);
            Prop("Palm_WestNorth", plants, "Environment/Vegetation/palm_b.png", -10,2.8f,1.8f);
            Prop("Palm_North", plants, "Environment/Vegetation/palm_cluster.png", -4.7f,5.5f,2.2f);
            Prop("Palm_NorthEast", plants, "Environment/Vegetation/palm_a.png", 7.2f,4.5f,1.9f);
            Prop("Palm_East", plants, "Environment/Vegetation/palm_b.png", 11,-2.8f,1.8f);
            Prop("Palm_BeachWest", plants, "Environment/Vegetation/palm_a.png", -8,-5.1f,1.8f);
            Prop("Palm_BeachEast", plants, "Environment/Vegetation/palm_b.png", 8.7f,-5.5f,1.8f);
            Prop("Palm_WestPair", plants, "Environment/Vegetation/palm_cluster.png", -11,.1f,2);
            foreach (var p in new[] { new Vector2(-10,-3.7f), new Vector2(-4.5f,4.9f), new Vector2(1.8f,5.7f), new Vector2(6.7f,4), new Vector2(11,-1), new Vector2(-10.6f,1.7f) })
                Prop("TropicalBush", plants, "Environment/Vegetation/tropical_bushes.png",p.x,p.y,1.05f);
            var props = Child("RocksAndBeachDecor", root);
            Prop("Rocks_West", props, "Environment/Rocks/coastal_rocks.png", -12,-.6f,1.6f);
            Prop("Rocks_East", props, "Environment/Rocks/coastal_rocks.png", 12,-3.8f,1.5f);
            Prop("BeachUmbrella_West", props, "Props/Beach/beach_umbrella.png", -5.3f,-5.4f,1.35f);
            Prop("BeachLounger_West", props, "Props/Beach/beach_lounger.png", -4,-5.7f,1.05f);
            Prop("BeachUmbrella_East", props, "Props/Beach/beach_umbrella.png", 4.5f,-5.5f,1.35f);
            Prop("BeachLounger_East", props, "Props/Beach/beach_lounger.png", 5.9f,-5.8f,1.05f);
            Prop("Bench", props, "Props/Furniture/bench.png", 3,-4,1.1f);
            foreach (float x in new[] { -8.5f,-3.5f,2.5f,7.5f }) Prop("PromenadeLamp", props, "Props/Lighting/path_lamp.png",x,-4.15f,.55f);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root.gameObject);
            PrefabUtility.InstantiatePrefab(prefab, old.transform);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            Validate(); Capture();
            Debug.Log("ISLAND_ENVIRONMENT_BUILD_OK");
        }

        static Transform Child(string name, Transform parent) { var t = new GameObject(name).transform; t.SetParent(parent,false); return t; }
        static Sprite Source(string path) => AssetDatabase.LoadAllAssetsAtPath("Assets/Textures/"+path).OfType<Sprite>().OrderByDescending(s=>s.rect.width*s.rect.height).First();
        static SpriteRenderer Flat(string name, Transform parent, string path, Vector2 at, float width, string layer, int order)
        {
            var r = Child(name,parent).gameObject.AddComponent<SpriteRenderer>(); r.sprite = Source(path);
            r.sharedMaterial = spriteMaterial;
            r.sortingLayerName=layer; r.sortingOrder=order;
            float scale=width/r.sprite.bounds.size.x; r.transform.localScale=new Vector3(scale,scale,1);
            r.transform.localPosition=(Vector3)at-r.sprite.bounds.center*scale; return r;
        }
        static void Prop(string name, Transform parent, string path, float x,float y,float width)
        {
            var t=Child(name,parent); t.localPosition=new Vector3(x,y,0);
            var group=t.gameObject.AddComponent<SortingGroup>(); group.sortingLayerName="WorldObjects";
            var r=Flat("Visual",t,path,Vector2.zero,width,"WorldObjects",0);
            var b=r.sprite.bounds; r.transform.localPosition=new Vector3(-b.center.x,-b.min.y,0)*r.transform.localScale.x;
            r.spriteSortPoint=SpriteSortPoint.Pivot;
        }
        [MenuItem("Tools/Paradise Tycoon/Island/Rebuild custom path sprites")]
        public static void RebuildPaths()
        {
            var root=PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                spriteMaterial=root.transform.Find("TerrainAndCoast/Island_ContinuousCoast").GetComponent<SpriteRenderer>().sharedMaterial;
                CreatePaths(root.transform);
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            // Remove only the superseded generated meshes authored by this builder.
            foreach(string path in Directory.GetFiles(Folder+"/Generated","Path_*.asset"))
                if(System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(path),@"^Path_\d+\.asset$")) AssetDatabase.DeleteAsset(path.Replace('\\','/'));
            if(AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Generated/Path.mat")!=null) AssetDatabase.DeleteAsset(Folder+"/Generated/Path.mat");
            AssetDatabase.SaveAssets();
            ReviewFromDisk();
            Debug.Log("CUSTOM_PATHS_REBUILD_OK");
        }
        static void CreatePaths(Transform root)
        {
            var old=root.Find("HandPlacedPaths"); if(old!=null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            old=root.Find("PathsVisual"); if(old!=null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            paths = Child("HandPlacedPaths", root); markers = root.Find("BuildingEntrances") ?? Child("BuildingEntrances", root); pathIndex = 0;
            IslandPathArtwork.Begin();
            for (int i = 0; i < Names.Length; i++) (markers.Find(Names[i] + "_Entrance") ?? Child(Names[i] + "_Entrance", markers)).localPosition = Entrances[i];
            Route("Promenade", new Vector2(-8,-3.35f), new Vector2(-3.5f,-3.35f), new Vector2(1.3f,-3.35f), new Vector2(5.8f,-3.35f), new Vector2(8,-3.35f));
            Route("Bungalow", new Vector2(-8,-3.35f), new Vector2(-9.3f,-.7f), new Vector2(-9.3f,1.6f), Entrances[0]);
            Route("Hotel", new Vector2(1.3f,-3.35f), new Vector2(1.3f,1.25f), Entrances[1]);
            Route("Spa", new Vector2(1.3f,1.25f), new Vector2(3.2f,1.25f), Entrances[2]);
            // Continue beneath the visible thresholds, past transparent sprite padding.
            Route("Restaurant", new Vector2(-6.5f,-3.35f), Entrances[3], Entrances[3]+Vector2.up*.65f);
            Route("Pool", new Vector2(1.3f,-1.8f), new Vector2(-.3f,-1.8f), Entrances[4], Entrances[4]+Vector2.up*.65f);
            Route("BeachBar", new Vector2(5.8f,-3.35f), Entrances[5], Entrances[5]+Vector2.up*.6f);
            Route("Harbor", new Vector2(8,-3.35f), new Vector2(7.8f,-.4f), Entrances[6]);
            var visual=Child("PathsVisual",root).gameObject.AddComponent<SpriteRenderer>();
            visual.sprite=IslandPathArtwork.Bake(Entrances); visual.sharedMaterial=spriteMaterial;
            visual.transform.localPosition=IslandPathArtwork.SpriteCenter;
            visual.sortingLayerName="Terrain"; visual.sortingOrder=10;
        }
        static void Route(string name, params Vector2[] points)
        {
            IslandPathArtwork.Add(points);
            for(int i=1;i<points.Length;i++)
            {
                var t=Child(name+"_"+(++pathIndex),paths); t.localPosition=(points[i-1]+points[i])*.5f;
                Child("Start",t).position=paths.TransformPoint(points[i-1]);
                Child("End",t).position=paths.TransformPoint(points[i]);
            }
        }
        [MenuItem("Tools/Paradise Tycoon/Island/Validate environment")]
        public static void Validate()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Require(prefab!=null,"Missing environment prefab"); Require(prefab.transform.localScale==Vector3.one,"Root scale");
            Require(prefab.GetComponentsInChildren<Camera>(true).Length==0,"Prefab must not own camera");
            Require(prefab.GetComponentsInChildren<Canvas>(true).Length==0,"Prefab must not own UI");
            foreach(var t in prefab.GetComponentsInChildren<Transform>(true)) Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0,"Missing script "+t.name);
            foreach(var r in prefab.GetComponentsInChildren<SpriteRenderer>(true)) Require(r.sprite!=null && r.sprite.vertices.Length>=3 && r.sharedMaterial!=null,"Missing sprite/material "+r.name);
            foreach(var r in prefab.GetComponentsInChildren<MeshRenderer>(true)) Require(r.sharedMaterial!=null,"Missing mesh material");
            foreach(var r in prefab.GetComponentsInChildren<MeshFilter>(true)) Require(r.sharedMesh!=null,"Missing path mesh");
            var pathVisual=prefab.transform.Find("PathsVisual").GetComponent<SpriteRenderer>();
            Require(AssetDatabase.GetAssetPath(pathVisual.sprite)==IslandPathArtwork.AssetPath,"Expected custom continuous path sprite");
            Require(prefab.transform.Find("HandPlacedPaths").GetComponentsInChildren<Renderer>().Length==0,"Segment renderers must not overlap the continuous sprite");
            foreach(var g in prefab.GetComponentsInChildren<SortingGroup>(true)) { Require(g.sortingLayerName=="WorldObjects"&&g.sortingOrder==0,"Incorrect sorting"); foreach(var p in Entrances) Require(Vector2.Distance(g.transform.localPosition,p)>.75f,"Entrance blocked: "+g.name); }
            for(int i=0;i<Names.Length;i++) Require(Vector2.Distance(prefab.transform.Find("BuildingEntrances/"+Names[i]+"_Entrance").localPosition,Entrances[i])<.01f,"Entrance drift");
            var segments=prefab.transform.Find("HandPlacedPaths").Cast<Transform>().ToArray();
            Vector2 A(Transform t) => t.Find("Start").position;
            Vector2 B(Transform t) => t.Find("End").position;
            float Distance(Vector2 p, Vector2 a, Vector2 b) => Vector2.Distance(p,a+Vector2.ClampMagnitude(b-a,Mathf.Clamp(Vector2.Dot(p-a,(b-a).normalized),0,Vector2.Distance(a,b))));
            bool Touch(Transform a,Transform b) => Distance(A(a),A(b),B(b))<.01f || Distance(B(a),A(b),B(b))<.01f || Distance(A(b),A(a),B(a))<.01f || Distance(B(b),A(a),B(a))<.01f;
            var reached=new System.Collections.Generic.HashSet<Transform> {segments[0]};
            bool changed; do { changed=false; foreach(var t in segments) if(!reached.Contains(t)&&reached.Any(r=>Touch(r,t))) {reached.Add(t);changed=true;} } while(changed);
            Require(reached.Count==segments.Length,"Disconnected walking route");
            foreach(var entrance in Entrances) Require(segments.Any(t=>Distance(entrance,A(t),B(t))<.01f),"Entrance not reached by paths");
            Debug.Log("ISLAND_VALIDATION_OK: self-contained prefab, sprites/materials/meshes, 7 entrance anchors, clearance, ground sorting, connected walking network.");
        }
        static void Require(bool condition,string message) { if(!condition) throw new InvalidOperationException(message); }
        public static void ReviewFromDisk() { EditorSceneManager.OpenScene(DemoPath); Validate(); Capture(); }
        public static void Capture()
        {
            var camera=Camera.main; var framing=camera.GetComponent<IslandCameraFraming>();
            string output=Path.Combine(Directory.GetCurrentDirectory(),"Logs/IslandReview"); Directory.CreateDirectory(output);
            foreach(var size in new[] { new Vector2Int(1280,720),new Vector2Int(1600,720),new Vector2Int(2560,1440) })
            {
                var target=new RenderTexture(size.x,size.y,24); var texture=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);
                var previous=RenderTexture.active;
                try { camera.aspect=(float)size.x/size.y; framing.Frame(size.x,size.y,new Rect(0,0,size.x,size.y)); camera.targetTexture=target; SortingGroup.UpdateAllSortingGroups(); camera.Render(); camera.Render(); camera.Render(); RenderTexture.active=target;
                    texture.ReadPixels(new Rect(0,0,size.x,size.y),0,0); texture.Apply(); File.WriteAllBytes(output+"/Island_"+size.x+"x"+size.y+".png",texture.EncodeToPNG()); }
                finally { camera.targetTexture=null; RenderTexture.active=previous; target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(texture); }
            }
            camera.ResetAspect(); framing.Refresh();
        }
    }
}
