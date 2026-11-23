#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using UnityEditor;
using ParadiseTycoon.Guests;
using UnityEngine;

namespace ParadiseTycoon.Ship
{
    /// <summary>
    /// Play-mode check of the SCRUM-131 acceptance criteria. Editor only.
    /// Enter Play mode in Main, then run Tools/Paradise Tycoon/Ship/Run smoke test.
    /// </summary>
    public sealed class ShipWaveSmoke : MonoBehaviour
    {
        [MenuItem("Tools/Paradise Tycoon/Ship/Run smoke test (Play mode)")]
        static void Run()
        {
            if (!Application.isPlaying) { Debug.LogError("SHIP_WAVE_FAIL Enter Play mode in Main first."); return; }
            new GameObject("Ship wave smoke test").AddComponent<ShipWaveSmoke>();
        }

        static void Check(bool ok, string message)
        {
            if (!ok) throw new Exception(message);
            Debug.Log("SHIP_WAVE_PASS " + message);
        }

        IEnumerator Start()
        {
            var controller = FindAnyObjectByType<ShipWaveController>();
            float timeScale = Time.timeScale;
            var steps = Steps(controller);
            while (true)
            {
                object current;
                try
                {
                    if (!steps.MoveNext()) break;
                    current = steps.Current;
                }
                catch (Exception error)
                {
                    Debug.LogError("SHIP_WAVE_FAIL " + error.Message);
                    break;
                }
                yield return current;
            }
            Time.timeScale = timeScale;
            Destroy(gameObject);
        }

        static bool WaveLocked(ShipWaveController c)
        {
            if (c.Upgrades == null) return false;
            var field = typeof(ParadiseTycoon.Buildings.BuildingUpgradeController).GetField("state",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            return field?.GetValue(c.Upgrades) is ParadiseTycoon.Buildings.IBuildingUpgradeState state && state.IsWaveActive;
        }

        static IEnumerator Steps(ShipWaveController c)
        {
            Check(c != null && c.Panel != null && c.Ship != null && c.GuestService != null && c.Upgrades != null &&
                  c.Buildings.Count == 6, "controller is in the scene and fully wired");
            yield return null;
            Check(c.State == ShipWaveState.Offered && c.CurrentOffer != null, "an offer waits for the player");
            Check(c.Panel.gameObject.activeSelf && c.Panel.SummaryText.Contains(c.CurrentOffer.GuestCount.ToString()),
                "panel shows the guest count");
            var built = c.AvailableServiceIds;
            Check(c.CurrentOffer.RequestsPerService.All(s => built.Contains(s.Key)), "offer only asks for built services");

            // Reject: new offer, nothing else changes.
            var rejected = c.CurrentOffer;
            int waveNumber = c.WaveNumber;
            string coins = c.Hud != null ? c.Hud.CoinsText : "";
            var levels = c.Buildings.Select(b => b.Level).ToArray();
            c.Panel.RejectButton.onClick.Invoke();
            Check(c.State == ShipWaveState.Offered && c.CurrentOffer != null && c.CurrentOffer != rejected &&
                  c.Panel.gameObject.activeSelf, "reject replaces the offer with a new one");
            Check((c.Hud == null || c.Hud.CoinsText == coins) && c.Buildings.Select(b => b.Level).SequenceEqual(levels) &&
                  c.WaveNumber == waveNumber && !WaveLocked(c),
                "reject keeps coins, building levels and wave progress");

            // Two consecutive waves, as required for the joint test (SCRUM-143).
            int results = 0;
            c.WaveFinished += (offer, list) => results++;
            for (int wave = 1; wave <= 2; wave++)
            {
                string tag = $"wave {wave}: ";
                var accepted = c.CurrentOffer;
                int number = c.WaveNumber;
                var shown = accepted.CreatePassengers();
                c.Panel.AcceptButton.onClick.Invoke();
                c.Panel.AcceptButton.onClick.Invoke();
                Check(c.State == ShipWaveState.Arriving && c.IsWaveActive && !c.Panel.gameObject.activeSelf,
                    tag + "accept sends the ship and hides the panel");
                Check(!c.Accept() && !c.Reject(), tag + "repeated accept or a late reject changes nothing");
                Check(WaveLocked(c), tag + "upgrades are locked from the moment the ship is accepted");
                c.Ship.Complete();
                yield return null;
                yield return null;
                Check(c.State == ShipWaveState.Running && c.GuestService.IsRunning &&
                      c.GuestService.WaveId == accepted.WaveId && c.WaveNumber == number + 1,
                    tag + "docked ship starts exactly one guest service");
                var served = c.GuestService.CaptureRequests();
                Check(served.Count == accepted.RequestCount &&
                      served.Select(r => r.GuestId).Distinct().Count() == accepted.GuestCount &&
                      shown.All(g => served.Where(r => r.GuestId == g.guestId).OrderBy(r => r.RequestIndex)
                          .Select(r => r.ServiceId).SequenceEqual(g.serviceIds)),
                    tag + "same guests with the same requests as in the offer");
                Check(!c.Accept(), tag + "no second wave while one is running");

                int before = results;
                Time.timeScale = 20;
                float timeout = Time.realtimeSinceStartup + 90;
                bool stable = true;
                while (c.State == ShipWaveState.Running && Time.realtimeSinceStartup < timeout)
                {
                    stable &= c.GuestService.CaptureRequests().Count == accepted.RequestCount;
                    yield return null;
                }
                Time.timeScale = 1;
                Check(stable, tag + "no guests or requests are added during the wave");
                Check(c.State == ShipWaveState.Departing && !c.IsWaveActive &&
                      c.GuestService.CaptureRequests().All(r => r.State == GuestRequestState.Completed),
                    tag + "wave ends only after every request is handled");
                yield return null;
                Check(results == before + 1, tag + "result is produced exactly once");
                Check(!WaveLocked(c), tag + "upgrades unlock when the wave is over");
                c.Ship.Complete();
                yield return null;
                Check(c.State == ShipWaveState.Offered && c.CurrentOffer != accepted && c.Panel.gameObject.activeSelf &&
                      !c.GuestService.IsRunning, tag + "next offer waits for the player's decision");
                yield return null;
                yield return null;
                Check(c.State == ShipWaveState.Offered && !c.GuestService.IsRunning, tag + "next wave does not start on its own");
            }
            Debug.Log("SHIP_WAVE_SMOKE_OK");
        }
    }
}
#endif
