#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ParadiseTycoon.Scrum104;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ParadiseTycoon.Hud
{
    public sealed class ResortHudSmoke : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Environment.GetCommandLineArgs().Contains("-hudSmoke") || Environment.GetCommandLineArgs().Contains("--qa")) new GameObject("HUD verification").AddComponent<ResortHudSmoke>();
        }
        static void Require(bool ok, string message) { if (!ok) throw new Exception(message); Debug.Log("SCRUM102_PASS " + message); }
        static void Click(Button button)
        {
            var point = RectTransformUtility.WorldToScreenPoint(null, button.transform.TransformPoint(((RectTransform)button.transform).rect.center));
            var data = new PointerEventData(EventSystem.current) { position = point };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data,hits);
            Require(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button, "pointer reaches " + button.name);
            ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerClickHandler);
        }
        IEnumerator Start()
        {
            Application.runInBackground = true;
            Debug.Log("SCRUM102_PLAY_STARTED");
            // Let the player finish its splash screen and first UI geometry pass.
            yield return new WaitForSecondsRealtime(3);
            yield return null; yield return new WaitForEndOfFrame();
            var args = Environment.GetCommandLineArgs();
            var evidence = args.SkipWhile(a => a != "--capture-dir" && a != "-hudEvidence").Skip(1).First();
            Directory.CreateDirectory(evidence);
            string ratio = args.Contains("-hud20x9") || Screen.width == 1600 ? "20x9" : "16x9";
            try
            {
                if (args.Contains("--qa-main"))
                    Require(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path == "Assets/Scenes/Main.unity", "actual integrated Main scene loaded");
                Require(Screen.width == (ratio == "20x9" ? 1600 : 1280) && Screen.height == 720,"actual Game view " + Screen.width + "x" + Screen.height);
                var hud = FindAnyObjectByType<ResortHud>(); var pause = PauseMenuController.Instance;
                Require(hud != null && pause != null,"HUD and Henrieta pause menu initialized");
                Require(hud.CoinsText.Replace("\u00a0", " ").Replace("\u202f", " ") == "1 250" && hud.EnergyText == "80 / 100" && hud.ResortText.Replace("\u00a0", " ").Replace("\u202f", " ") == "24 500","consistent demo values");
                Click(hud.BuildButton); Require(hud.StatusText.Contains("Stavanie"),"build demo feedback");
                Click(hud.UpgradeButton); Require(hud.StatusText.Contains("Vylepšenia"),"upgrade demo feedback");
                hud.RefreshDemoValues();
                ScreenCapture.CaptureScreenshot(Path.Combine(evidence,"hud-"+ratio+".png"));
            }
            catch(Exception e) { Fail(e); yield break; }
            yield return new WaitForEndOfFrame(); yield return null;
            try
            {
                var hud = FindAnyObjectByType<ResortHud>(); var pause = PauseMenuController.Instance;
                Time.timeScale = .75f; Click(hud.PauseButton);
                Require(pause.IsPaused && Time.timeScale == 0 && pause.ResumeButton.gameObject.activeInHierarchy,"pause opens existing menu and stops time");
                Canvas.ForceUpdateCanvases();
                ScreenCapture.CaptureScreenshot(Path.Combine(evidence,"pause-"+ratio+".png"));
            }
            catch(Exception e) { Fail(e); yield break; }
            yield return new WaitForEndOfFrame(); yield return null;
            try
            {
                var hud = FindAnyObjectByType<ResortHud>(); var pause = PauseMenuController.Instance;
                var point = RectTransformUtility.WorldToScreenPoint(null,hud.BuildButton.transform.TransformPoint(((RectTransform)hud.BuildButton.transform).rect.center));
                var data = new PointerEventData(EventSystem.current) {position = point};
                var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data,hits);
                Debug.Log("SCRUM102_PAUSE_HITS " + string.Join(",",hits.Select(h => h.gameObject.name + ":" + h.sortingOrder)));
                Require(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<ResortHud>() == null,"pause overlay blocks HUD clicks");
                Click(pause.ResumeButton); Require(!pause.IsPaused && Mathf.Approximately(Time.timeScale,.75f),"resume restores previous time scale");
                Click(hud.PauseButton); Click(pause.ResumeButton); Require(!pause.IsPaused,"pause works repeatedly");
                hud.gameObject.SetActive(false);
                Require(pause.PauseButton.GetComponent<CanvasGroup>().alpha == 1,"removing HUD restores original pause button");
                hud.gameObject.SetActive(true);
            }
            catch(Exception e) { Fail(e); yield break; }
            yield return null;
            try
            {
                var hud = FindAnyObjectByType<ResortHud>(); var pause = PauseMenuController.Instance;
                Click(hud.BuildButton); Require(hud.StatusText.Contains("Stavanie"),"HUD responds after re-enable");
                pause.gameObject.SetActive(false);
            }
            catch(Exception e) { Fail(e); yield break; }
            yield return null; yield return null;
            try { Require(!FindAnyObjectByType<ResortHud>().PauseButton.interactable,"missing menu disables pause safely"); Debug.Log("SCRUM102_SMOKE_OK " + ratio); Finish(0,"All HUD checks passed at " + Screen.width + "x" + Screen.height); }
            catch(Exception e) { Fail(e); }
        }
        [Serializable] sealed class Report { public bool passed; public int width, height; public string message; }
        static void Finish(int code, string message)
        {
            var args = Environment.GetCommandLineArgs();
            var path = args.SkipWhile(a => a != "--qa-report").Skip(1).FirstOrDefault();
            if (!string.IsNullOrEmpty(path)) File.WriteAllText(path,JsonUtility.ToJson(new Report {passed=code==0,width=Screen.width,height=Screen.height,message=message},true));
#if UNITY_EDITOR
            EditorApplication.Exit(code);
#else
            Application.Quit(code);
#endif
        }
        static void Fail(Exception e) { Time.timeScale = 1; Debug.LogException(e); Finish(1,e.ToString()); }
    }
}
#endif
