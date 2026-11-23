using System;
using System.Collections.Generic;
using System.Linq;
using ParadiseTycoon.Buildings;
using ParadiseTycoon.Guests;
using ParadiseTycoon.Hud;
using ParadiseTycoon.Scrum104;
using UnityEngine;

namespace ParadiseTycoon.Ship
{
    public enum ShipWaveState { WaitingForServices, Offered, Arriving, Docked, Running, Departing }

    /// <summary>
    /// Owns the lifecycle of one wave: offer → accept/reject → ship arrives → guest service → ship leaves → next offer.
    /// Rejecting only replaces the offer; it never touches coins or building progress.
    /// </summary>
    public sealed class ShipWaveController : MonoBehaviour
    {
        [SerializeField] GuestServiceController guestService;
        [SerializeField] ResortBuilding[] buildings;
        [SerializeField] ShipOfferPanel panel;
        [SerializeField] ShipVisual ship;
        [SerializeField] ResortHud hud;
        [SerializeField] BuildingUpgradeController upgrades;
        [SerializeField] AudioClip arrivalClip, rejectClip, completedClip;
        [Tooltip("0 = different offers every session.")]
        [SerializeField] int seed;
        [SerializeField, Min(1)] int minGuests = 3;
        [SerializeField, Min(1)] int maxGuests = 6;
        [SerializeField, Min(1)] int maxRequestsPerGuest = 2;

        ShipOfferGenerator generator;

        ShipWaveState state;
        public ShipWaveState State
        {
            get => state;
            private set
            {
                if (state == value) return;
                state = value;
                StateChanged?.Invoke(value);
            }
        }
        /// <summary>True from accepting a ship until its guests are served (upgrades and UI can rely on this).</summary>
        public bool IsWaveActive => State is ShipWaveState.Arriving or ShipWaveState.Docked or ShipWaveState.Running;
        public event Action<ShipWaveState> StateChanged;
        public ShipOffer CurrentOffer { get; private set; }
        /// <summary>Number of the wave on offer; increases only when a wave actually starts.</summary>
        public int WaveNumber { get; private set; } = 1;
        /// <summary>Raised with the accepted offer and the per-request results (e.g. for rewards).</summary>
        public event Action<ShipOffer, IReadOnlyList<GuestRequestResult>> WaveFinished;
        public event Action<ShipOffer> OfferChanged;

        internal ShipOfferPanel Panel => panel;
        internal ShipVisual Ship => ship;
        internal GuestServiceController GuestService => guestService;
        internal ResortHud Hud => hud;
        internal BuildingUpgradeController Upgrades => upgrades;
        internal IReadOnlyList<ResortBuilding> Buildings => buildings;
        internal IReadOnlyCollection<string> AvailableServiceIds => AvailableServices();

        void Start()
        {
            generator = new ShipOfferGenerator(seed != 0 ? seed : Environment.TickCount, minGuests, maxGuests, maxRequestsPerGuest);
            guestService.WaveCompleted += OnWaveCompleted;
            panel.Bind(() => Accept(), () => Reject());
            NextOffer();
        }

        void OnDestroy()
        {
            if (guestService != null) guestService.WaveCompleted -= OnWaveCompleted;
        }

        void Update()
        {
            if (IsPaused()) return;
            // A building may be finished later; offer a ship as soon as something can serve guests.
            if (State == ShipWaveState.WaitingForServices && AvailableServices().Count > 0) NextOffer();
            if (State == ShipWaveState.Docked) StartService();
        }

        public bool Accept()
        {
            if (State != ShipWaveState.Offered || CurrentOffer == null || IsPaused() || guestService.IsRunning) return false;
            State = ShipWaveState.Arriving;
            // The wave counts as active from the moment the ship is accepted (upgrades wait until it is over).
            if (upgrades != null) upgrades.SetWaveActive(true);
            panel.Hide();
            Play(arrivalClip);
            Status("Loď sa blíži k prístavu.");
            ship.Arrive(() => State = ShipWaveState.Docked);
            return true;
        }

        public bool Reject()
        {
            if (State != ShipWaveState.Offered || IsPaused()) return false;
            Play(rejectClip);
            NextOffer();
            Status("Loď odmietnutá.");
            return true;
        }

        void StartService()
        {
            if (guestService.TryStartWave(CurrentOffer.WaveId, CurrentOffer.CreatePassengers()))
            {
                State = ShipWaveState.Running;
                WaveNumber++;
                Status("Hostia vystupujú na ostrov.");
                return;
            }
            // The offer can no longer be served (should not happen, buildings never lose levels).
            Debug.LogWarning($"Ship wave {CurrentOffer.WaveId} could not start; sending the ship away.");
            if (upgrades != null) upgrades.SetWaveActive(false);
            Leave();
        }

        void OnWaveCompleted(string waveId, IReadOnlyList<GuestRequestResult> results)
        {
            if (State != ShipWaveState.Running || CurrentOffer == null || waveId != CurrentOffer.WaveId) return;
            var finished = CurrentOffer;
            Play(completedClip);
            Status("Vlna dokončená!");
            Leave();
            WaveFinished?.Invoke(finished, results);
        }

        void Leave()
        {
            State = ShipWaveState.Departing;
            ship.Depart(NextOffer);
        }

        void NextOffer()
        {
            CurrentOffer = generator.Next(AvailableServices(), WaveNumber);
            if (CurrentOffer == null)
            {
                State = ShipWaveState.WaitingForServices;
                panel.ShowUnavailable("Postav aspoň jednu budovu, aby mohli prísť hostia.");
            }
            else
            {
                State = ShipWaveState.Offered;
                panel.Show(CurrentOffer, DisplayName);
            }
            OfferChanged?.Invoke(CurrentOffer);
        }

        List<string> AvailableServices() =>
            buildings.Where(b => b != null && b.CurrentService != null).Select(b => b.BuildingId).ToList();

        string DisplayName(string buildingId) =>
            buildings.FirstOrDefault(b => b != null && b.BuildingId == buildingId)?.DisplayName ?? buildingId;

        void Status(string message)
        {
            if (hud != null) hud.SetStatus(message);
        }

        static void Play(AudioClip clip)
        {
            var menu = PauseMenuController.Instance;
            if (clip != null && menu != null && menu.UiSource != null) menu.UiSource.PlayOneShot(clip);
        }

        static bool IsPaused() =>
            Time.timeScale <= 0 || (PauseMenuController.Instance != null && PauseMenuController.Instance.IsPaused);
    }
}
