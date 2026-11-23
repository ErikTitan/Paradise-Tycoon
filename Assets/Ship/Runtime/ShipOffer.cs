using System;
using System.Collections.Generic;
using System.Linq;
using ParadiseTycoon.Guests;

namespace ParadiseTycoon.Ship
{
    /// <summary>
    /// One ship offer: a fixed group of guests and their ordered service requests.
    /// The data is immutable, so the group accepted by the player is exactly the group that was shown.
    /// </summary>
    public sealed class ShipOffer
    {
        readonly string[] guestIds;
        readonly string[][] requests;

        public string WaveId { get; }
        public int GuestCount => guestIds.Length;
        public int RequestCount { get; }
        /// <summary>Number of requests per service id, in first-seen order.</summary>
        public IReadOnlyList<KeyValuePair<string, int>> RequestsPerService { get; }

        public ShipOffer(string waveId, IReadOnlyList<string> guestIds, IReadOnlyList<IReadOnlyList<string>> serviceIdsPerGuest)
        {
            if (string.IsNullOrWhiteSpace(waveId)) throw new ArgumentException("Wave id is required.", nameof(waveId));
            if (guestIds == null || serviceIdsPerGuest == null || guestIds.Count != serviceIdsPerGuest.Count || guestIds.Count == 0)
                throw new ArgumentException("Every guest needs a request list.");
            WaveId = waveId;
            this.guestIds = guestIds.ToArray();
            requests = serviceIdsPerGuest.Select(r => r.ToArray()).ToArray();
            RequestCount = requests.Sum(r => r.Length);
            var counts = new List<KeyValuePair<string, int>>();
            foreach (string id in requests.SelectMany(r => r))
            {
                int index = counts.FindIndex(c => c.Key == id);
                if (index < 0) counts.Add(new KeyValuePair<string, int>(id, 1));
                else counts[index] = new KeyValuePair<string, int>(id, counts[index].Value + 1);
            }
            RequestsPerService = counts;
        }

        public IReadOnlyList<string> GetRequests(int guestIndex) => requests[guestIndex];

        /// <summary>Fresh copies for the guest service, so nothing outside can change the stored offer.</summary>
        public List<AcceptedGuest> CreatePassengers()
        {
            var passengers = new List<AcceptedGuest>(guestIds.Length);
            for (int i = 0; i < guestIds.Length; i++)
                passengers.Add(new AcceptedGuest { guestId = guestIds[i], serviceIds = (string[])requests[i].Clone() });
            return passengers;
        }
    }

    /// <summary>Creates ship offers that only ask for services the resort can currently provide.</summary>
    public sealed class ShipOfferGenerator
    {
        readonly Random random;
        readonly int minGuests, maxGuests, maxRequestsPerGuest;

        public ShipOfferGenerator(int seed, int minGuests, int maxGuests, int maxRequestsPerGuest)
        {
            random = new Random(seed);
            this.minGuests = Math.Max(1, minGuests);
            this.maxGuests = Math.Max(this.minGuests, maxGuests);
            this.maxRequestsPerGuest = Math.Max(1, maxRequestsPerGuest);
        }

        /// <summary>
        /// Returns null when no service is available yet. The wave number is only consumed when a wave
        /// really starts, so rejected offers do not advance the player's progress.
        /// </summary>
        public ShipOffer Next(IReadOnlyCollection<string> availableServiceIds, int waveNumber)
        {
            var services = availableServiceIds?.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();
            if (services == null || services.Length == 0) return null;

            string waveId = $"wave-{Math.Max(1, waveNumber):000}";
            int guestCount = random.Next(minGuests, maxGuests + 1);
            var guestIds = new List<string>(guestCount);
            var guestRequests = new List<IReadOnlyList<string>>(guestCount);
            for (int i = 0; i < guestCount; i++)
            {
                int count = random.Next(1, Math.Min(maxRequestsPerGuest, services.Length) + 1);
                guestIds.Add($"{waveId}-guest-{i + 1}");
                // Distinct services per guest, in a random visiting order.
                guestRequests.Add(services.OrderBy(_ => random.Next()).Take(count).ToArray());
            }
            return new ShipOffer(waveId, guestIds, guestRequests);
        }
    }
}
