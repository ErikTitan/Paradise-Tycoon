#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace ParadiseTycoon.WaveResults.Editor
{
    public sealed class WaveResultsPreview : MonoBehaviour
    {
        WaveResultsPanel panel;
        WaveResultsFlow flow;
        WaveSummary lastSummary;
        [SerializeField] WaveRatingRules rules = new WaveRatingRules();
        string status = "Ukážkové vstupy a pravidlá – skutočné mince sa nemenia.";

        void Start()
        {
            if (EventSystem.current == null)
                new GameObject("PreviewEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            panel = WaveResultsPanel.Create();
            flow = new WaveResultsFlow(rules, new PreviewRewards(), panel,
                () => status = "Návrat na ostrov. Ďalšia ukážková vlna čaká na výber.");
            panel.ReturnRequested += flow.ReturnToIsland;
            ShowWave(false);
        }

        void ShowWave(bool overloaded)
        {
            lastSummary = new WaveSummary(Guid.NewGuid().ToString("N"), new[] {
                new ServiceWaveStats("hotel", "Hotel", 8, 8, 16),
                new ServiceWaveStats("restaurant", "Reštaurácia", 8, 8, overloaded ? 400 : 24),
                new ServiceWaveStats("bar", "Plážový bar", 4, 4, overloaded ? 160 : 8)
            });
            flow.CompleteWave(lastSummary);
        }

        void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            GUI.Label(new Rect(16, 5, Screen.width - 32, 28), status, style);
            if (panel == null) return;
            if (panel.IsVisible)
            {
                if (GUI.Button(new Rect(16, Screen.height - 40, 280, 30), "Opakovať rovnaký výsledok"))
                    flow.CompleteWave(lastSummary);
                return;
            }
            if (GUI.Button(new Rect(16, 45, 230, 42), "Spustiť rýchlu ukážkovú vlnu")) ShowWave(false);
            if (GUI.Button(new Rect(260, 45, 260, 42), "Spustiť preťaženú ukážkovú vlnu")) ShowWave(true);
        }

        void OnDestroy()
        {
            if (panel == null) return;
            panel.ReturnRequested -= flow.ReturnToIsland;
            Destroy(panel.gameObject);
        }

        sealed class PreviewRewards : IWaveRewardStore
        {
            readonly Dictionary<string, WaveResult> results = new Dictionary<string, WaveResult>();
            public WaveResult ApplyRewardOnce(WaveResult result)
            {
                if (results.TryGetValue(result.WaveId, out var previous)) return previous;
                results.Add(result.WaveId, result);
                return result;
            }
        }
    }
}
#endif
