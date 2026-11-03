#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEditor;
namespace ParadiseTycoon.Scrum104
{
    // Only runs with an explicit command-line flag; excluded from player builds.
    public sealed class PauseMenuSmoke : MonoBehaviour
    {
        const string BackupKey = "ParadiseTycoon.Scrum104.TestBackup";
        const string HadKey = "ParadiseTycoon.Scrum104.TestHadKey";
        bool read;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            if (args.Contains("-scrum104SmokeWrite") || args.Contains("-scrum104SmokeRead"))
                new GameObject("Smoke verification").AddComponent<PauseMenuSmoke>().read = args.Contains("-scrum104SmokeRead");
        }
        static void Check(bool ok, string message) { if (!ok) throw new Exception(message); Debug.Log("SCRUM104_PASS " + message); }
        IEnumerator Start()
        {
            yield return null;
            try
            {
                var c = PauseMenuController.Instance;
                Check(c != null, "controller initialized");
                if (read)
                {
                    Check(c.Volume == 0 && c.VolumeSlider.value == 0 && c.MusicSource.volume == 0 && c.UiSource.volume == 0,
                        "mute restored after Editor process restart");
                    Restore();
                    Debug.Log("SCRUM104_RESTART_OK"); EditorApplication.Exit(0); yield break;
                }
                if (!PlayerPrefs.HasKey(HadKey)) {
                PlayerPrefs.SetInt(HadKey,PlayerPrefs.HasKey(PauseMenuController.VolumeKey)?1:0);
                PlayerPrefs.SetFloat(BackupKey,PlayerPrefs.GetFloat(PauseMenuController.VolumeKey,.7f)); PlayerPrefs.Save(); }
                c.VolumeSlider.value = .37f;
                Check(Mathf.Approximately(c.Volume,.37f) && Mathf.Approximately(c.MusicSource.volume,.37f) && Mathf.Approximately(c.UiSource.volume,.37f), "slider changes both audio channels");
                c.ReloadVolume(); Check(Mathf.Approximately(c.Volume,.37f), "saved volume reloaded");
                Check(c.MusicSource.clip != null && c.UiSource != null, "audio references present");
                Time.timeScale = .75f;
                c.PauseButton.onClick.Invoke();
                Check(c.IsPaused && Time.timeScale == 0 && c.ResumeButton.gameObject.activeInHierarchy, "HUD button pauses game and opens menu");
                c.VolumeSlider.value = .25f;
                Check(Mathf.Approximately(c.Volume,.25f), "UI volume responds during pause");
            }
            catch(Exception e) { Fail(e); yield break; }

            try
            {
                var c = PauseMenuController.Instance;
                Check(Time.timeScale == 0, "scaled game time is paused");
                var music = c.MusicSource;
                c.OpenPauseMenu(); Check(c.MusicSource == music, "reopening uses same music source");
                c.ResumeButton.onClick.Invoke();
                Check(!c.IsPaused && Mathf.Approximately(Time.timeScale,.75f), "resume restores previous time scale");
                var duplicate = Instantiate(c.gameObject); Check(!duplicate.activeSelf, "duplicate menu disabled immediately");
                c.OpenPauseMenu(); c.enabled = false;
                Check(Mathf.Approximately(Time.timeScale,.75f), "disabling controller restores time");
                c.enabled = true; c.Resume();
                c.VolumeSlider.value = 0;
                Check(c.MusicSource.volume == 0 && c.UiSource.volume == 0 && PlayerPrefs.GetFloat(PauseMenuController.VolumeKey,-1) == 0, "mute applies and persists");
                Debug.Log("SCRUM104_SMOKE_OK"); EditorApplication.Exit(0);
            }
            catch(Exception e) { Fail(e); }
        }
        static void Restore()
        {
            if (PlayerPrefs.GetInt(HadKey)==1) PlayerPrefs.SetFloat(PauseMenuController.VolumeKey,PlayerPrefs.GetFloat(BackupKey));
            else PlayerPrefs.DeleteKey(PauseMenuController.VolumeKey);
            PlayerPrefs.DeleteKey(HadKey); PlayerPrefs.DeleteKey(BackupKey); PlayerPrefs.Save();
        }
        static void Fail(Exception e) { Debug.LogException(e); Restore(); EditorApplication.Exit(1); }
    }
}
#endif
