#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using UnityEditor;
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

        static IEnumerator Steps(ShipWaveController c)
        {
            Check(c != null, "controller is in the scene");
            yield return null;
            Check(c.State == ShipWaveState.Offered && c.CurrentOffer != null, "an offer waits for the player");
            Check(c.Panel.gameObject.activeSelf && c.Panel.SummaryText.Contains(c.CurrentOffer.GuestCount.ToString()),
                "panel shows the guest count");
            var built = c.AvailableServiceIds;
            Check(c.CurrentOffer.RequestsPerService.All(s => built.Contains(s.Key)), "offer only asks for built services");

            // Reject: new offer, nothing else changes.
            var rejected = c.CurrentOffer;
            string coins = c.Hud != null ? c.Hud.CoinsText : "";
            var levels = c.Buildings.Select(b => b.Level).ToArray();
            c.Panel.RejectButton.onClick.Invoke();
            Check(c.State == ShipWaveState.Offered && c.CurrentOffer != null && c.CurrentOffer.WaveId != rejected.WaveId,
                "reject replaces the offer");
            Check((c.Hud == null || c.Hud.CoinsText == coins) && c.Buildings.Select(b => b.Level).SequenceEqual(levels),
                "reject keeps coins and building progress");

            // Accept: the exact group is served.
            var accepted = c.CurrentOffer;
            c.Panel.AcceptButton.onClick.Invoke();
            Check(c.State == ShipWaveState.Arriving && !c.Panel.gameObject.activeSelf, "accept sends the ship and hides the panel");
            Check(!c.Reject(), "offer cannot be rejected after accepting");
            c.Ship.Complete();
            yield return null;
            yield return null;
            Check(c.State == ShipWaveState.Running && c.GuestService.IsRunning && c.GuestService.WaveId == accepted.WaveId,
                "docked ship starts guest service");
            var served = c.GuestService.CaptureRequests();
            var expected = accepted.CreatePassengers();
            Check(served.Count == accepted.RequestCount &&
                  expected.All(g => served.Where(r => r.GuestId == g.guestId).OrderBy(r => r.RequestIndex)
                      .Select(r => r.ServiceId).SequenceEqual(g.serviceIds)),
                "same guests with the same requests are served");
            Check(!c.Accept(), "no second wave while one is running");

            // Finish the wave quickly.
            bool finished = false;
            c.WaveFinished += (offer, results) => finished = offer == accepted;
            Time.timeScale = 20;
            float timeout = Time.realtimeSinceStartup + 60;
            while (!finished && Time.realtimeSinceStartup < timeout) yield return null;
            Check(finished, "wave finishes after all requests are handled");
            Time.timeScale = 1;
            c.Ship.Complete();
            yield return null;
            Check(c.State == ShipWaveState.Offered && c.CurrentOffer != null && c.CurrentOffer.WaveId != accepted.WaveId &&
                  c.Panel.gameObject.activeSelf, "next offer waits for the player's decision");
            Debug.Log("SHIP_WAVE_SMOKE_OK");
        }
    }
}
#endif
