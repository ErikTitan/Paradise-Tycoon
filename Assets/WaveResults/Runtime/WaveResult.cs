using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ParadiseTycoon.WaveResults
{
    public sealed class ServiceWaveStats
    {
        public string ServiceId { get; }
        public string DisplayName { get; }
        public int RequestedCount { get; }
        public int CompletedCount { get; }
        public double CompletedRequestWaitSeconds { get; }

        public ServiceWaveStats(string serviceId, string displayName, int requestedCount,
            int completedCount, double completedRequestWaitSeconds)
        {
            if (string.IsNullOrWhiteSpace(serviceId)) throw new ArgumentException("Service ID is required.");
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Service name is required.");
            if (requestedCount < 0 || completedCount < 0 || completedCount > requestedCount)
                throw new ArgumentOutOfRangeException(nameof(completedCount));
            if (double.IsNaN(completedRequestWaitSeconds) || double.IsInfinity(completedRequestWaitSeconds)
                || completedRequestWaitSeconds < 0 || (completedCount == 0 && completedRequestWaitSeconds != 0))
                throw new ArgumentOutOfRangeException(nameof(completedRequestWaitSeconds));
            ServiceId = serviceId;
            DisplayName = displayName;
            RequestedCount = requestedCount;
            CompletedCount = completedCount;
            CompletedRequestWaitSeconds = completedRequestWaitSeconds;
        }
    }

    public sealed class WaveSummary
    {
        public string WaveId { get; }
        public ReadOnlyCollection<ServiceWaveStats> Services { get; }

        public WaveSummary(string waveId, IEnumerable<ServiceWaveStats> services)
        {
            if (string.IsNullOrWhiteSpace(waveId)) throw new ArgumentException("Wave ID is required.");
            if (services == null) throw new ArgumentNullException(nameof(services));
            var items = new List<ServiceWaveStats>(services);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            long requested = 0, completed = 0;
            foreach (var item in items)
            {
                if (item == null || !ids.Add(item.ServiceId)) throw new ArgumentException("Services must have unique IDs.");
                requested += item.RequestedCount;
                completed += item.CompletedCount;
            }
            if (requested == 0 || requested > int.MaxValue || completed > int.MaxValue)
                throw new ArgumentException("Wave must contain a supported number of requests.");
            items.Sort((a, b) => StringComparer.Ordinal.Compare(a.ServiceId, b.ServiceId));
            WaveId = waveId;
            Services = items.AsReadOnly();
        }
    }

    public sealed class ServiceRating
    {
        public ServiceWaveStats Stats { get; }
        public double Satisfaction { get; }
        public double LostSatisfactionPoints { get; }
        public double AverageWaitSeconds => Stats.CompletedCount == 0 ? 0 :
            Stats.CompletedRequestWaitSeconds / Stats.CompletedCount;

        internal ServiceRating(ServiceWaveStats stats, double satisfaction)
        {
            Stats = stats;
            Satisfaction = satisfaction;
            LostSatisfactionPoints = stats.RequestedCount * (1 - satisfaction);
        }
    }

    public sealed class WaveResult
    {
        public string WaveId { get; }
        public int RequestedCount { get; }
        public int CompletedCount { get; }
        public double Satisfaction { get; }
        public int Stars { get; }
        public int Coins { get; }
        public ReadOnlyCollection<ServiceRating> ServicesByImpact { get; }

        internal WaveResult(string waveId, int requested, int completed, double satisfaction,
            int stars, int coins, List<ServiceRating> services)
        {
            WaveId = waveId;
            RequestedCount = requested;
            CompletedCount = completed;
            Satisfaction = satisfaction;
            Stars = stars;
            Coins = coins;
            ServicesByImpact = services.AsReadOnly();
        }
    }
}
