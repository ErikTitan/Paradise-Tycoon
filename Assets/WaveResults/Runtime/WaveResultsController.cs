using System;
using System.Collections.Generic;
using ParadiseTycoon.Guests;
using UnityEngine;

namespace ParadiseTycoon.WaveResults
{
    public sealed class WaveResultsController : MonoBehaviour
    {
        [SerializeField] WaveRatingRules rules = new WaveRatingRules();
        GuestServiceController guestService;
        WaveResultsPanel panel;
        WaveResultsFlow flow;
        WaveSummary pending;
        Func<string, string> serviceName;
        bool handling;
        public WaveResult Current => flow?.Current;
        public event Action<Exception> ResultFailed;

        public void Bind(GuestServiceController source, IWaveRewardStore rewards,
            Action returnToIsland, Func<string, string> displayName = null, WaveRatingRules ratingRules = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (rewards == null) throw new ArgumentNullException(nameof(rewards));
            if (returnToIsland == null) throw new ArgumentNullException(nameof(returnToIsland));
            if (pending != null || Current != null)
                throw new InvalidOperationException("Finish the pending result before rebinding.");
            if (guestService != null) guestService.WaveCompleted -= OnWaveCompleted;
            guestService = source;
            serviceName = displayName;
            if (ratingRules != null) rules = ratingRules;
            rules.Validate();
            if (panel == null)
            {
                panel = WaveResultsPanel.Create();
                panel.ReturnRequested += ReturnToIsland;
            }
            flow = new WaveResultsFlow(rules, rewards, panel, returnToIsland);
            guestService.WaveCompleted += OnWaveCompleted;
        }

        void OnWaveCompleted(string waveId, IReadOnlyList<GuestRequestResult> requests)
        {
            try
            {
                if (pending != null && !StringComparer.Ordinal.Equals(pending.WaveId, waveId))
                    throw new InvalidOperationException("A previous wave result is still pending.");
                pending = GuestWaveSummaryAdapter.Create(waveId, requests, serviceName);
                if (isActiveAndEnabled) RetryPendingResult();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                ResultFailed?.Invoke(exception);
            }
        }

        public void RetryPendingResult()
        {
            if (!isActiveAndEnabled || pending == null || flow == null || handling) return;
            handling = true;
            try { flow.CompleteWave(pending); }
            finally { handling = false; }
        }

        public void ReturnToIsland()
        {
            if (flow?.Current == null) return;
            flow.ReturnToIsland();
            pending = null;
        }

        void OnEnable()
        {
            if (flow?.Current != null) panel.Show(flow.Current);
            else if (pending != null) RetryPendingResult();
        }

        void OnDisable()
        {
            if (panel != null) panel.Hide();
        }

        void OnDestroy()
        {
            if (guestService != null) guestService.WaveCompleted -= OnWaveCompleted;
            if (panel != null)
            {
                panel.ReturnRequested -= ReturnToIsland;
                Destroy(panel.gameObject);
            }
        }
    }
}
