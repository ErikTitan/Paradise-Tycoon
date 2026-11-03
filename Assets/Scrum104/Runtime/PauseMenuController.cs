using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace ParadiseTycoon.Scrum104
{
    // Place one prefab in a scene. HUD buttons can call OpenPauseMenu().
    public sealed class PauseMenuController : MonoBehaviour
    {
        public const string VolumeKey = "ParadiseTycoon.Audio.MasterVolume";
        public static PauseMenuController Instance { get; private set; }
        [SerializeField] GameObject panel;
        [SerializeField] Button pauseButton;
        [SerializeField] Button resumeButton;
        [SerializeField] Slider volumeSlider;
        [SerializeField] Text volumeLabel;
        [SerializeField] AudioSource musicSource;
        [SerializeField] AudioSource uiSource;
        [SerializeField] AudioClip clickClip;
        float previousTimeScale = 1f;
        GameObject previousSelection;
        public bool IsPaused { get; private set; }
        public float Volume { get; private set; }
        public AudioSource MusicSource => musicSource;
        public AudioSource UiSource => uiSource;
        public Slider VolumeSlider => volumeSlider;
        public Button PauseButton => pauseButton;
        public Button ResumeButton => resumeButton;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }
            Instance = this;
            panel.SetActive(false);
            musicSource.ignoreListenerPause = true;
            uiSource.ignoreListenerPause = true;
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            uiSource.playOnAwake = false;
            pauseButton.onClick.AddListener(OpenPauseMenu);
            resumeButton.onClick.AddListener(Resume);
            volumeSlider.onValueChanged.AddListener(SetVolume);
            ReloadVolume();
            if (musicSource.clip != null && !musicSource.isPlaying) musicSource.Play();
        }

        public void OpenPauseMenu()
        {
            if (IsPaused || Instance != this) return;
            previousTimeScale = Time.timeScale;
            previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            IsPaused = true;
            Time.timeScale = 0f;
            panel.SetActive(true);
            pauseButton.gameObject.SetActive(false);
            PlayClick();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
        }

        public void Resume()
        {
            if (!IsPaused) return;
            PlayClick();
            RestoreTime();
            panel.SetActive(false);
            pauseButton.gameObject.SetActive(true);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(previousSelection != null ? previousSelection : pauseButton.gameObject);
        }

        public void SetVolume(float value)
        {
            ApplyVolume(value);
            PlayerPrefs.SetFloat(VolumeKey, Volume);
            PlayerPrefs.Save();
        }

        public void ReloadVolume() => ApplyVolume(PlayerPrefs.GetFloat(VolumeKey, 0.7f));

        void ApplyVolume(float value)
        {
            Volume = float.IsNaN(value) || float.IsInfinity(value) ? 0.7f : Mathf.Clamp01(value);
            musicSource.volume = Volume;
            uiSource.volume = Volume;
            volumeSlider.SetValueWithoutNotify(Volume);
            volumeLabel.text = Volume == 0 ? "Hlasitosť: stlmené" : "Hlasitosť: " + Mathf.RoundToInt(Volume * 100) + " %";
        }

        // Other UI buttons can use this event; sources survive closing the panel.
        public void PlayClick()
        {
            if (clickClip != null) uiSource.PlayOneShot(clickClip);
        }

        void RestoreTime()
        {
            if (!IsPaused) return;
            Time.timeScale = previousTimeScale;
            IsPaused = false;
        }
        void OnDisable()
        {
            if (Instance != this) return;
            RestoreTime();
            if (panel != null) panel.SetActive(false);
            if (pauseButton != null) pauseButton.gameObject.SetActive(true);
        }
        void OnDestroy() { if (Instance == this) { RestoreTime(); Instance = null; } }
    }
}
