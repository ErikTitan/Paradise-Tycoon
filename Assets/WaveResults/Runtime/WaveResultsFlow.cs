using System;
using System.Collections.Generic;

namespace ParadiseTycoon.WaveResults
{
    public interface IWaveRewardStore
    {
        // Ensure the wave reward is applied at most once and return its result.
        WaveResult ApplyRewardOnce(WaveResult result);
    }

    public interface IWaveResultsView
    {
        void Show(WaveResult result);
        void Hide();
    }

    public sealed class WaveResultsFlow
    {
        readonly WaveRatingRules rules;
        readonly IWaveRewardStore rewardStore;
        readonly IWaveResultsView view;
        readonly Action returnToIsland;
        readonly Dictionary<string, WaveResult> results = new Dictionary<string, WaveResult>(StringComparer.Ordinal);
        public WaveResult Current { get; private set; }

        public WaveResultsFlow(WaveRatingRules rules, IWaveRewardStore rewardStore,
            IWaveResultsView view, Action returnToIsland)
        {
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
            this.rewardStore = rewardStore ?? throw new ArgumentNullException(nameof(rewardStore));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.returnToIsland = returnToIsland ?? throw new ArgumentNullException(nameof(returnToIsland));
        }

        public WaveResult CompleteWave(WaveSummary summary)
        {
            if (summary == null) throw new ArgumentNullException(nameof(summary));
            if (Current != null && !StringComparer.Ordinal.Equals(Current.WaveId, summary.WaveId))
                throw new InvalidOperationException("Close the current result before completing another wave.");
            if (!results.TryGetValue(summary.WaveId, out var result))
            {
                result = rewardStore.ApplyRewardOnce(WaveRatingCalculator.Evaluate(summary, rules));
                if (result == null || !StringComparer.Ordinal.Equals(result.WaveId, summary.WaveId))
                    throw new InvalidOperationException("Reward store returned a result for a different wave.");
                results.Add(summary.WaveId, result);
            }
            Current = result;
            view.Show(result);
            return result;
        }

        public void ReturnToIsland()
        {
            if (Current == null) return;
            var previous = Current;
            view.Hide();
            Current = null;
            try { returnToIsland(); }
            catch
            {
                Current = previous;
                view.Show(previous);
                throw;
            }
        }
    }
}
