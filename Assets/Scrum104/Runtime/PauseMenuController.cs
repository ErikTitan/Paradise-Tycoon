using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace ParadiseTycoon.Scrum104
{
    // Place one prefab in a scene. HUD buttons can call OpenPauseMenu().
    public sealed class PauseMenuController : MonoBehaviour
    {
        public const string VolumeKey = "ParadiseTycoon.Audio.MasterVolume";
        public const string MusicVolumeKey = "ParadiseTycoon.Audio.MusicVolume";
        public const string EffectsVolumeKey = "ParadiseTycoon.Audio.EffectsVolume";
        public static PauseMenuController Instance { get; private set; }
        [SerializeField] GameObject panel;
        [SerializeField] Button pauseButton;
        [SerializeField] Button resumeButton;
        [SerializeField] Slider volumeSlider;
        [SerializeField] Text volumeLabel;
        [SerializeField] AudioSource musicSource;
        [SerializeField] AudioSource uiSource;
        [SerializeField] AudioClip clickClip;
        Slider effectsVolumeSlider;
        Text effectsVolumeLabel;
        float previousTimeScale = 1f;
        GameObject previousSelection;
        public bool IsPaused { get; private set; }
        public float Volume { get; private set; }
        public float MusicVolume => Volume;
        public float EffectsVolume { get; private set; }
        public AudioSource MusicSource => musicSource;
        public AudioSource UiSource => uiSource;
        public Slider VolumeSlider => volumeSlider;
        public Slider EffectsVolumeSlider => effectsVolumeSlider;
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
            CreateEffectsControl();
            volumeSlider.onValueChanged.AddListener(SetMusicVolume);
            effectsVolumeSlider.onValueChanged.AddListener(SetEffectsVolume);
            ReloadVolume();
            PlayerPrefs.SetFloat(MusicVolumeKey, MusicVolume);
            PlayerPrefs.SetFloat(EffectsVolumeKey, EffectsVolume);
            PlayerPrefs.Save();
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

        void CreateEffectsControl()
        {
            var parent = volumeSlider.transform.parent;
            effectsVolumeSlider = Instantiate(volumeSlider, parent, false);
            effectsVolumeSlider.name = "EffectsVolumeSlider";
            effectsVolumeSlider.onValueChanged = new Slider.SliderEvent();
            effectsVolumeLabel = Instantiate(volumeLabel, volumeLabel.transform.parent, false);
            effectsVolumeLabel.name = "EffectsVolumeLabel";

            var sliderRect = (RectTransform)volumeSlider.transform;
            var effectsRect = (RectTransform)effectsVolumeSlider.transform;
            var labelRect = volumeLabel.rectTransform;
            var effectsLabelRect = effectsVolumeLabel.rectTransform;
            sliderRect.anchoredPosition += Vector2.up * 60f;
            labelRect.anchoredPosition += Vector2.up * 72f;
            effectsRect.anchoredPosition = sliderRect.anchoredPosition + Vector2.down * 130f;
            effectsLabelRect.anchoredPosition = labelRect.anchoredPosition + Vector2.down * 130f;

            foreach (var slider in new[] { volumeSlider, effectsVolumeSlider })
            {
                var handle = slider.handleRect;
                handle.sizeDelta = new Vector2(handle.sizeDelta.x, 8f);
            }

            var card = parent as RectTransform;
            if (card != null) card.sizeDelta += Vector2.up * 120f;
            var title = parent.Find("Title") as RectTransform;
            if (title != null) title.anchoredPosition += Vector2.up * 60f;
            var hint = parent.Find("Hint") as RectTransform;
            if (hint != null) hint.anchoredPosition += Vector2.down * 75f;
            ((RectTransform)resumeButton.transform).anchoredPosition += Vector2.down * 60f;
        }

        public void SetVolume(float value)
        {
            ApplyMusicVolume(value);
            ApplyEffectsVolume(value);
            PlayerPrefs.SetFloat(VolumeKey, Volume);
            PlayerPrefs.SetFloat(MusicVolumeKey, MusicVolume);
            PlayerPrefs.SetFloat(EffectsVolumeKey, EffectsVolume);
            PlayerPrefs.Save();
        }

        public void SetMusicVolume(float value)
        {
            ApplyMusicVolume(value);
            PlayerPrefs.SetFloat(MusicVolumeKey, MusicVolume);
            PlayerPrefs.Save();
        }

        public void SetEffectsVolume(float value)
        {
            ApplyEffectsVolume(value);
            PlayerPrefs.SetFloat(EffectsVolumeKey, EffectsVolume);
            PlayerPrefs.Save();
        }

        public void ReloadVolume()
        {
            float previousVolume = PlayerPrefs.GetFloat(VolumeKey, 0.7f);
            ApplyMusicVolume(PlayerPrefs.GetFloat(MusicVolumeKey, previousVolume));
            ApplyEffectsVolume(PlayerPrefs.GetFloat(EffectsVolumeKey, previousVolume));
        }

        static float ClampVolume(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) ? 0.7f : Mathf.Clamp01(value);

        static string VolumeText(string channel, float value) =>
            channel + (value == 0 ? ": stlmené" : ": " + Mathf.RoundToInt(value * 100) + " %");

        void ApplyMusicVolume(float value)
        {
            Volume = ClampVolume(value);
            musicSource.volume = MusicVolume;
            volumeSlider.SetValueWithoutNotify(MusicVolume);
            volumeLabel.text = VolumeText("Hudba", MusicVolume);
        }

        void ApplyEffectsVolume(float value)
        {
            EffectsVolume = ClampVolume(value);
            uiSource.volume = EffectsVolume;
            effectsVolumeSlider.SetValueWithoutNotify(EffectsVolume);
            effectsVolumeLabel.text = VolumeText("Zvukové efekty", EffectsVolume);
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
