using System;
using System.Collections.Generic;

namespace ParadiseTycoon.WaveResults
{
    [Serializable]
    public sealed class WaveRatingRules
    {
        // Default tuning can be changed in the WaveResultsSettings asset.
        public double FullSatisfactionWaitSeconds = 5;
        public double ZeroSatisfactionWaitSeconds = 60;
        public double[] StarThresholds = { .2, .4, .6, .8 };
        public int CoinsPerCompletedRequest = 5;
        public double[] StarRewardMultipliers = { .5, .75, 1, 1.25, 1.5 };

        public void Validate()
        {
            if (!Finite(FullSatisfactionWaitSeconds) || FullSatisfactionWaitSeconds < 0
                || !Finite(ZeroSatisfactionWaitSeconds) || ZeroSatisfactionWaitSeconds <= FullSatisfactionWaitSeconds)
                throw new ArgumentException("Wait thresholds must be finite, nonnegative and increasing.");
            if (StarThresholds == null || StarThresholds.Length != 4)
                throw new ArgumentException("Four star thresholds are required.");
            double previous = 0;
            foreach (double value in StarThresholds)
            {
                if (!Finite(value) || value <= previous || value > 1)
                    throw new ArgumentException("Star thresholds must increase within (0, 1].");
                previous = value;
            }
            if (CoinsPerCompletedRequest < 0 || StarRewardMultipliers == null || StarRewardMultipliers.Length != 5)
                throw new ArgumentException("A nonnegative coin rate and five multipliers are required.");
            previous = 0;
            foreach (double value in StarRewardMultipliers)
            {
                if (!Finite(value) || value < previous)
                    throw new ArgumentException("Reward multipliers must be finite, nonnegative and nondecreasing.");
                previous = value;
            }
        }

        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    public static class WaveRatingCalculator
    {
        public static WaveResult Evaluate(WaveSummary summary, WaveRatingRules rules)
        {
            if (summary == null) throw new ArgumentNullException(nameof(summary));
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            rules.Validate();
            int requested = 0, completed = 0;
            double satisfactionPoints = 0;
            var services = new List<ServiceRating>();
            foreach (var stats in summary.Services)
            {
                requested = checked(requested + stats.RequestedCount);
                completed = checked(completed + stats.CompletedCount);
                double averageWait = stats.CompletedCount == 0 ? 0 :
                    stats.CompletedRequestWaitSeconds / stats.CompletedCount;
                double waitScore = Math.Max(0, Math.Min(1,
                    (rules.ZeroSatisfactionWaitSeconds - averageWait) /
                    (rules.ZeroSatisfactionWaitSeconds - rules.FullSatisfactionWaitSeconds)));
                double points = stats.CompletedCount * waitScore;
                satisfactionPoints += points;
                double serviceScore = stats.RequestedCount == 0 ? 0 : points / stats.RequestedCount;
                if (stats.RequestedCount > 0) services.Add(new ServiceRating(stats, serviceScore));
            }
            double satisfaction = Math.Max(0, Math.Min(1, satisfactionPoints / requested));
            int stars = 1;
            foreach (double threshold in rules.StarThresholds)
                if (satisfaction >= threshold) stars++;
            double reward = Math.Floor((double)completed * rules.CoinsPerCompletedRequest * rules.StarRewardMultipliers[stars - 1]);
            if (double.IsInfinity(reward) || reward > int.MaxValue)
                throw new OverflowException("Coin reward exceeds the supported range.");
            services.Sort((a, b) => {
                int byImpact = b.LostSatisfactionPoints.CompareTo(a.LostSatisfactionPoints);
                return byImpact != 0 ? byImpact : StringComparer.Ordinal.Compare(a.Stats.ServiceId, b.Stats.ServiceId);
            });
            return new WaveResult(summary.WaveId, requested, completed, satisfaction, stars, (int)reward, services);
        }
    }
}
