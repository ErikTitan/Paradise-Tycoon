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
        const string BackupPrefix = "ParadiseTycoon.Audio.TestBackup.";
        static readonly string[] Keys = {
            PauseMenuController.VolumeKey,
            PauseMenuController.MusicVolumeKey,
            PauseMenuController.EffectsVolumeKey
        };
        bool read;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            bool write = args.Contains("-audioSettingsSmokeWrite");
            bool restart = args.Contains("-audioSettingsSmokeRead");
            if (!write && !restart) return;
            if (write)
            {
                foreach (string key in Keys)
                {
                    if (PlayerPrefs.HasKey(BackupPrefix + key + ".exists")) continue;
                    PlayerPrefs.SetInt(BackupPrefix + key + ".exists", PlayerPrefs.HasKey(key) ? 1 : 0);
                    PlayerPrefs.SetFloat(BackupPrefix + key, PlayerPrefs.GetFloat(key, .7f));
                }
                PlayerPrefs.SetFloat(PauseMenuController.VolumeKey, .42f);
                PlayerPrefs.DeleteKey(PauseMenuController.MusicVolumeKey);
                PlayerPrefs.DeleteKey(PauseMenuController.EffectsVolumeKey);
                PlayerPrefs.Save();
            }
            new GameObject("Audio settings verification").AddComponent<PauseMenuSmoke>().read = restart;
        }
        static void Check(bool ok, string message)
        {
            if (!ok) throw new Exception(message);
            Debug.Log("AUDIO_SETTINGS_PASS " + message);
        }
        IEnumerator Start()
        {
            yield return null;
            try
            {
                var c = PauseMenuController.Instance;
                Check(c != null && c.EffectsVolumeSlider != null, "both controls initialized");
                if (read)
                {
                    Check(c.MusicVolume == 0 && c.VolumeSlider.value == 0 && c.MusicSource.volume == 0,
                        "music mute restored after Editor restart");
                    Check(Mathf.Approximately(c.EffectsVolume, .65f)
                        && Mathf.Approximately(c.EffectsVolumeSlider.value, .65f)
                        && Mathf.Approximately(c.UiSource.volume, .65f),
                        "independent effects volume restored after Editor restart");
                    Restore();
                    Debug.Log("AUDIO_SETTINGS_RESTART_OK");
                    EditorApplication.Exit(0);
                    yield break;
                }
                Check(Mathf.Approximately(c.MusicVolume, .42f) && Mathf.Approximately(c.EffectsVolume, .42f),
                    "previous shared volume migrated to both channels");
                c.VolumeSlider.value = .37f;
                Check(Mathf.Approximately(c.MusicSource.volume, .37f) && Mathf.Approximately(c.UiSource.volume, .42f),
                    "music slider does not change effects");
                c.EffectsVolumeSlider.value = .65f;
                Check(Mathf.Approximately(c.UiSource.volume, .65f) && Mathf.Approximately(c.MusicSource.volume, .37f),
                    "effects slider does not change music");
                c.ReloadVolume();
                Check(Mathf.Approximately(c.MusicVolume, .37f) && Mathf.Approximately(c.EffectsVolume, .65f),
                    "both saved values reload independently");
                c.EffectsVolumeSlider.value = 0;
                Check(c.UiSource.volume == 0 && Mathf.Approximately(c.MusicSource.volume, .37f),
                    "effects mute leaves music playing");
                c.SetMusicVolume(2f);
                c.SetEffectsVolume(-1f);
                Check(c.MusicVolume == 1 && c.EffectsVolume == 0, "values are clamped to slider range");
                c.SetMusicVolume(float.NaN);
                c.SetEffectsVolume(float.PositiveInfinity);
                Check(Mathf.Approximately(c.MusicVolume, .7f) && Mathf.Approximately(c.EffectsVolume, .7f),
                    "invalid values use a safe default");
                c.SetMusicVolume(.37f);
                c.SetEffectsVolume(.65f);
                Time.timeScale = .75f;
                c.PauseButton.onClick.Invoke();
                Check(c.IsPaused && Time.timeScale == 0 && c.ResumeButton.gameObject.activeInHierarchy,
                    "pause still stops game time and opens menu");
                c.VolumeSlider.value = .25f;
                c.EffectsVolumeSlider.value = .5f;
                Check(Mathf.Approximately(c.MusicSource.volume, .25f) && Mathf.Approximately(c.UiSource.volume, .5f),
                    "both controls respond during pause");
                c.ResumeButton.onClick.Invoke();
                Check(!c.IsPaused && Mathf.Approximately(Time.timeScale, .75f), "resume restores previous time scale");
                c.OpenPauseMenu();
                c.enabled = false;
                Check(Mathf.Approximately(Time.timeScale, .75f), "disabling paused menu restores time");
                c.enabled = true;
                c.SetMusicVolume(0);
                c.SetEffectsVolume(.65f);
                Check(c.MusicSource.volume == 0 && Mathf.Approximately(c.UiSource.volume, .65f),
                    "music mute leaves effects enabled");
                Check(PlayerPrefs.GetFloat(PauseMenuController.MusicVolumeKey, -1) == 0
                    && Mathf.Approximately(PlayerPrefs.GetFloat(PauseMenuController.EffectsVolumeKey, -1), .65f),
                    "both settings persist under separate keys");
                Debug.Log("AUDIO_SETTINGS_SMOKE_OK");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Restore();
                EditorApplication.Exit(1);
            }
        }
        static void Restore()
        {
            foreach (string key in Keys)
            {
                string exists = BackupPrefix + key + ".exists";
                if (!PlayerPrefs.HasKey(exists)) continue;
                if (PlayerPrefs.GetInt(exists) == 1) PlayerPrefs.SetFloat(key, PlayerPrefs.GetFloat(BackupPrefix + key));
                else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.DeleteKey(exists);
                PlayerPrefs.DeleteKey(BackupPrefix + key);
            }
            PlayerPrefs.Save();
        }
    }
}
#endif
