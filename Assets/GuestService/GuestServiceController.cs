using System;
using System.Collections.Generic;
using System.Linq;
using ParadiseTycoon.Buildings;
using ParadiseTycoon.GameState;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace ParadiseTycoon.Guests
{
    public enum GuestRequestState { Pending, Walking, Waiting, InService, Completed }

    [Serializable]
    public sealed class AcceptedGuest
    {
        public string guestId;
        public string[] serviceIds;
    }

    public readonly struct GuestRequestResult
    {
        public readonly string GuestId, ServiceId;
        public readonly int RequestIndex;
        public readonly GuestRequestState State;
        public readonly float WaitSeconds;

        internal GuestRequestResult(string guestId, string serviceId, int index, GuestRequestState state, float wait)
        {
            GuestId = guestId;
            ServiceId = serviceId;
            RequestIndex = index;
            State = state;
            WaitSeconds = wait;
        }
    }

    public sealed class GuestServiceController : MonoBehaviour
    {
        [Serializable]
        sealed class Station
        {
            public ResortBuilding building;
            public Transform entrance;
            public TMP_Text label;
            [NonSerialized] public Queue<Guest> waiting = new();
            [NonSerialized] public List<Guest> serving = new();
            [NonSerialized] public int total, completed;
            [NonSerialized] public int shownCompleted = -1, shownTotal = -1;
            [NonSerialized] public bool shownWarning;
            [NonSerialized] public Vector2[] approach;
        }

        sealed class Request
        {
            public Station station;
            public GuestRequestState state;
            public float wait;
        }

        sealed class Guest
        {
            public string id;
            public Request[] requests;
            public int index, waypoint, direction;
            public float work, animation, departureDelay, queueDistance;
            public Vector2[] route;
            public SpriteRenderer sprite;
            public bool returned;
        }

        [SerializeField] Transform pathSegments, harbor, characters;
        [SerializeField] Station[] stations;
        [SerializeField] BuildingUpgradeController upgrades;
        [SerializeField] Sprite[] walkFrames;
        [SerializeField] GameObject progressPanel;
        [SerializeField, Min(.1f)] float walkSpeed = 3.2f;
        [SerializeField, Min(1)] int overloadThreshold = 2;
        [SerializeField, Min(0)] int coinsPerCompletedRequest = 100;
        readonly List<Guest> guests = new();
        readonly HashSet<string> acceptedWaveIds = new(StringComparer.Ordinal);
        IslandGuestPaths paths;
        ResortGameState waveState;
        IReadOnlyList<GuestRequestResult> finalRequests;
        int? pendingReward;
        double nextSettlementAttempt;

        public bool IsRunning { get; private set; }
        public string WaveId { get; private set; }
        // Optional multiplier supplied by shared game state (e.g. manager bonus).
        public Func<string, float> ServiceSpeedMultiplier { get; set; }
        // A rewards system can supply the rated payout before the wave is settled.
        public Func<string, IReadOnlyList<GuestRequestResult>, int> CalculateWaveReward { get; set; }
        public event Action<string, IReadOnlyList<GuestRequestResult>> WaveCompleted;

        /// <summary>Starts service for the exact passengers and ordered requests from an accepted offer.</summary>
        public bool TryStartWave(string waveId, IReadOnlyList<AcceptedGuest> passengers)
        {
            if (IsRunning || !isActiveAndEnabled || Time.timeScale <= 0 || string.IsNullOrWhiteSpace(waveId) ||
                passengers == null || passengers.Count == 0) return false;
            var lookup = stations.ToDictionary(s => s.building.BuildingId, StringComparer.Ordinal);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var incoming = new List<Guest>(passengers.Count);
            foreach (var passenger in passengers)
            {
                if (passenger == null || string.IsNullOrWhiteSpace(passenger.guestId) ||
                    !ids.Add(passenger.guestId) || passenger.serviceIds == null) return false;
                var requests = new List<Request>();
                foreach (string id in passenger.serviceIds)
                {
                    if (string.IsNullOrEmpty(id) || !lookup.TryGetValue(id, out var station) ||
                        station.building.CurrentService == null) return false;
                    requests.Add(new Request { station = station });
                }
                incoming.Add(new Guest { id = passenger.guestId, requests = requests.ToArray() });
            }

            paths ??= new IslandGuestPaths(pathSegments);
            foreach (var station in stations)
            {
                station.approach ??= paths.Route(station.entrance.position, harbor.position);
                station.total = station.completed = 0;
            }
            var sharedState = upgrades != null ? upgrades.GameState : null;
            if (sharedState != null ? !sharedState.TryBeginWave(waveId) : !acceptedWaveIds.Add(waveId))
                return false;
            waveState = sharedState;
            guests.Clear();
            guests.AddRange(incoming);
            int departureSlot = 0;
            foreach (var guest in guests)
            {
                foreach (var request in guest.requests) request.station.total++;
                if (guest.requests.Length == 0) { guest.returned = true; continue; }
                guest.sprite = CreateView(guest.id);
                guest.sprite.enabled = false;
                guest.departureDelay = departureSlot++ * .12f;
                Send(guest, harbor.position);
            }
            WaveId = waveId;
            IsRunning = true;
            progressPanel.SetActive(true);
            Refresh();
            return true;
        }

        public IReadOnlyList<GuestRequestResult> CaptureRequests() => guests.SelectMany(g =>
            g.requests.Select((r, i) => new GuestRequestResult(g.id, r.station.building.BuildingId, i, r.state, r.wait))).ToArray();

        void Update() => Advance(Time.deltaTime);

        internal void Advance(float seconds)
        {
            if (!IsRunning || seconds <= 0 || Time.timeScale <= 0) return;
            if (finalRequests != null)
            {
                if (Time.realtimeSinceStartupAsDouble >= nextSettlementAttempt) SettleWave();
                return;
            }
            foreach (var station in stations)
            {
                foreach (var guest in station.waiting) guest.requests[guest.index].wait += seconds;
                var level = station.building.CurrentService;
                if (level == null) continue;
                float speed = ServiceSpeedMultiplier?.Invoke(station.building.BuildingId) ?? 1;
                if (!float.IsFinite(speed) || speed <= 0) speed = 1;
                for (int i = station.serving.Count - 1; i >= 0; i--)
                {
                    var guest = station.serving[i];
                    guest.work += seconds * speed / level.SecondsPerRequest;
                    if (guest.work < 1) continue;
                    station.serving.RemoveAt(i);
                    guest.requests[guest.index++].state = GuestRequestState.Completed;
                    station.completed++;
                    guest.sprite.enabled = true;
                    Send(guest, station.entrance.position);
                }
                Admit(station, level.Capacity);
            }

            foreach (var guest in guests)
            {
                if (guest.returned || guest.route == null) continue;
                float walkSeconds = Mathf.Max(0, seconds - guest.departureDelay);
                guest.departureDelay = Mathf.Max(0, guest.departureDelay - seconds);
                if (walkSeconds <= 0) continue;
                guest.sprite.enabled = true;
                if (!Walk(guest, walkSeconds)) continue;
                guest.route = null;
                if (guest.index == guest.requests.Length)
                {
                    guest.returned = true;
                    continue;
                }
                var request = guest.requests[guest.index];
                request.state = GuestRequestState.Waiting;
                guest.queueDistance = 0;
                request.station.waiting.Enqueue(guest);
            }
            foreach (var station in stations)
            {
                if (station.building.CurrentService is BuildingLevel level) Admit(station, level.Capacity);
                int place = 0;
                foreach (var guest in station.waiting)
                    PlaceInQueue(guest, station, ++place, seconds);
            }

            Refresh();
            if (!guests.All(g => g.returned)) return;
            finalRequests = CaptureRequests();
            SettleWave();
        }

        void SettleWave()
        {
            if (waveState != null)
            {
                if (!pendingReward.HasValue)
                {
                    long reward = CalculateWaveReward != null ? CalculateWaveReward(WaveId, finalRequests)
                        : (long)finalRequests.Count(request => request.State == GuestRequestState.Completed) * coinsPerCompletedRequest;
                    if (reward >= 0 && reward <= int.MaxValue - waveState.Coins)
                        pendingReward = (int)reward;
                }
                if (!pendingReward.HasValue || !waveState.TryCompleteWave(WaveId, pendingReward.Value))
                {
                    // Keep the wave and its results available until the entire checkpoint can be written.
                    nextSettlementAttempt = Time.realtimeSinceStartupAsDouble + 2;
                    return;
                }
            }
            var results = finalRequests;
            finalRequests = null;
            pendingReward = null;
            waveState = null;
            foreach (var guest in guests)
                if (guest.sprite != null) Destroy(guest.sprite.transform.parent.gameObject);
            IsRunning = false;
            progressPanel.SetActive(false);
            WaveCompleted?.Invoke(WaveId, results);
        }

        void Admit(Station station, int capacity)
        {
            while (station.serving.Count < capacity && station.waiting.Count > 0)
            {
                var guest = station.waiting.Dequeue();
                guest.requests[guest.index].state = GuestRequestState.InService;
                guest.work = 0;
                guest.sprite.enabled = false;
                guest.sprite.transform.parent.position = station.entrance.position;
                station.serving.Add(guest);
            }
        }

        void PlaceInQueue(Guest guest, Station station, int place, float seconds)
        {
            // Space guests along the existing approach, including bends in longer queues.
            guest.queueDistance = Mathf.MoveTowards(guest.queueDistance, place * .29f, walkSpeed * seconds);
            float remaining = guest.queueDistance;
            var route = station.approach;
            for (int i = 1; i < route.Length; i++)
            {
                float length = Vector2.Distance(route[i-1], route[i]);
                if (remaining <= length || i == route.Length - 1)
                {
                    guest.sprite.transform.parent.position = Vector2.MoveTowards(route[i-1], route[i], remaining);
                    return;
                }
                remaining -= length;
            }
        }

        void Send(Guest guest, Vector2 from)
        {
            Vector2 destination = harbor.position;
            if (guest.index < guest.requests.Length)
            {
                var request = guest.requests[guest.index];
                request.state = GuestRequestState.Walking;
                destination = request.station.entrance.position;
            }
            guest.route = paths.Route(from, destination);
            guest.waypoint = 1;
        }

        bool Walk(Guest guest, float seconds)
        {
            float remaining = walkSpeed * seconds;
            while (guest.waypoint < guest.route.Length)
            {
                Vector2 position = guest.sprite.transform.parent.position;
                Vector2 target = guest.route[guest.waypoint];
                float distance = Vector2.Distance(position,target);
                if (distance > remaining) { MoveView(guest,target,remaining / walkSpeed); return false; }
                MoveView(guest,target,distance / walkSpeed);
                guest.sprite.transform.parent.position = target;
                remaining -= distance;
                guest.waypoint++;
            }
            return true;
        }

        SpriteRenderer CreateView(string id)
        {
            var root = new GameObject("Guest " + id);
            root.transform.SetParent(characters, false);
            root.transform.position = harbor.position;
            root.AddComponent<SortingGroup>().sortingLayerName = "WorldObjects";
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            var sprite = visual.AddComponent<SpriteRenderer>();
            sprite.sortingLayerName = "WorldObjects";
            SetFrame(sprite, walkFrames[0]);
            return sprite;
        }

        static void SetFrame(SpriteRenderer view, Sprite frame)
        {
            if (view.sprite == frame) return;
            view.sprite = frame;
            Bounds bounds = frame.bounds;
            float scale = .58f / bounds.size.y;
            view.transform.localScale = Vector3.one * scale;
            view.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, 0) * scale;
        }

        void MoveView(Guest guest, Vector2 target, float seconds)
        {
            var root = guest.sprite.transform.parent;
            Vector2 position = root.position;
            Vector2 delta = target - position;
            if (delta.sqrMagnitude < .000001f || seconds <= 0) return;
            root.position = Vector2.MoveTowards(position, target, walkSpeed * seconds);
            guest.direction = (Mathf.RoundToInt(Mathf.Atan2(delta.y,delta.x) * Mathf.Rad2Deg / 45) + 8) % 8;
            guest.animation += seconds * 12;
            SetFrame(guest.sprite, walkFrames[guest.direction * 8 + (int)guest.animation % 8]);
        }

        void Refresh()
        {
            int visible = 0;
            foreach (var station in stations)
            {
                var row = (RectTransform)station.label.transform.parent;
                bool hasRequests = station.total > 0;
                if (row.gameObject.activeSelf != hasRequests) row.gameObject.SetActive(hasRequests);
                if (!hasRequests) continue;
                row.anchoredPosition = new Vector2(0, -34 - 21 * visible++);
                bool warning = station.waiting.Count >= overloadThreshold;
                if (station.shownTotal == station.total && station.shownCompleted == station.completed &&
                    station.shownWarning == warning) continue;
                station.shownTotal = station.total;
                station.shownCompleted = station.completed;
                station.shownWarning = warning;
                station.label.text = $"{station.completed} / {station.total}" + (warning ? "  |  RAD" : "");
                station.label.color = warning ? new Color(1,.75f,.32f) : Color.white;
            }
            ((RectTransform)progressPanel.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 45 + 21 * visible);
        }
    }
}


