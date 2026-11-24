using System;
using System.Collections.Generic;
using ParadiseTycoon.Guests;

namespace ParadiseTycoon.WaveResults
{
    public static class GuestWaveSummaryAdapter
    {
        public static WaveSummary Create(string waveId, IReadOnlyList<GuestRequestResult> requests,
            Func<string, string> serviceName = null)
        {
            if (requests == null) throw new ArgumentNullException(nameof(requests));
            var groups = new SortedDictionary<string, List<GuestRequestResult>>(StringComparer.Ordinal);
            var identities = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
            foreach (var request in requests)
            {
                if (string.IsNullOrWhiteSpace(request.GuestId) || string.IsNullOrWhiteSpace(request.ServiceId)
                    || request.RequestIndex < 0)
                    throw new ArgumentException("Every request must identify its guest, service and index.");
                if (request.State != GuestRequestState.Completed)
                    throw new ArgumentException("Cannot evaluate a wave with incomplete requests.");
                if (float.IsNaN(request.WaitSeconds) || float.IsInfinity(request.WaitSeconds) || request.WaitSeconds < 0)
                    throw new ArgumentException("Waiting time must be finite and nonnegative.");
                if (!identities.TryGetValue(request.GuestId, out var indices))
                    identities.Add(request.GuestId, indices = new HashSet<int>());
                if (!indices.Add(request.RequestIndex))
                    throw new ArgumentException("The same guest request was delivered more than once.");
                if (!groups.TryGetValue(request.ServiceId, out var group))
                    groups.Add(request.ServiceId, group = new List<GuestRequestResult>());
                group.Add(request);
            }
            var services = new List<ServiceWaveStats>();
            foreach (var entry in groups)
            {
                entry.Value.Sort((a, b) => {
                    int byGuest = StringComparer.Ordinal.Compare(a.GuestId, b.GuestId);
                    return byGuest != 0 ? byGuest : a.RequestIndex.CompareTo(b.RequestIndex);
                });
                double wait = 0;
                foreach (var request in entry.Value) wait += request.WaitSeconds;
                string name = serviceName?.Invoke(entry.Key);
                services.Add(new ServiceWaveStats(entry.Key,
                    string.IsNullOrWhiteSpace(name) ? entry.Key : name,
                    entry.Value.Count, entry.Value.Count, wait));
            }
            return new WaveSummary(waveId, services);
        }
    }
}
