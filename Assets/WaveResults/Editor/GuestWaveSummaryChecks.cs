#if UNITY_EDITOR
using System;
using System.Reflection;
using ParadiseTycoon.Guests;

namespace ParadiseTycoon.WaveResults.Editor
{
    public static class GuestWaveSummaryChecks
    {
        public static int Run()
        {
            int passed = 0;
            Action<bool, string> check = (ok, name) => {
                if (!ok) throw new Exception("Guest summary check failed: " + name);
                passed++;
            };
            var requests = new[] {
                Request("a", "restaurant", 1, 12), Request("b", "hotel", 0, 3),
                Request("a", "hotel", 0, 1), Request("b", "restaurant", 1, 8)
            };
            var summary = GuestWaveSummaryAdapter.Create("real-wave", requests, id => "Name " + id);
            check(summary.WaveId == "real-wave" && summary.Services.Count == 2, "wave and service identities preserved");
            check(summary.Services[0].ServiceId == "hotel" && summary.Services[0].CompletedCount == 2,
                "completed requests grouped by building ID");
            check(summary.Services[0].CompletedRequestWaitSeconds == 4
                && summary.Services[1].CompletedRequestWaitSeconds == 20, "actual queue waiting summed");
            check(summary.Services[0].DisplayName == "Name hotel", "display name resolved separately from ID");
            var reversed = (GuestRequestResult[])requests.Clone();
            Array.Reverse(reversed);
            var other = GuestWaveSummaryAdapter.Create("real-wave", reversed);
            var rules = new WaveRatingRules();
            var a = WaveRatingCalculator.Evaluate(summary, rules);
            var b = WaveRatingCalculator.Evaluate(other, rules);
            check(a.Coins == b.Coins && a.Satisfaction == b.Satisfaction, "request order does not affect calculation");
            check(Rejects(() => GuestWaveSummaryAdapter.Create("duplicate", new[] { requests[0], requests[0] })),
                "duplicate request cannot inflate the reward");
            check(Rejects(() => GuestWaveSummaryAdapter.Create("collision", new[] {
                Request("a", "hotel", 0, 0), Request("a", "restaurant", 0, 0)
            })), "same guest/index under another service rejected");
            check(Rejects(() => GuestWaveSummaryAdapter.Create("incomplete", new[] {
                Request("a", "hotel", 0, 1, GuestRequestState.InService)
            })), "incomplete service cannot finalize a wave");
            check(Rejects(() => GuestWaveSummaryAdapter.Create("invalid", new[] { Request("a", "hotel", 0, float.NaN) })),
                "invalid waiting rejected");
            check(Rejects(() => GuestWaveSummaryAdapter.Create("invalid", new[] { Request("a", "hotel", 0, -1) })),
                "negative waiting rejected");
            check(Rejects(() => GuestWaveSummaryAdapter.Create("invalid", new[] { Request("", "hotel", 0, 0) })),
                "missing guest rejected");
            check(Rejects(() => GuestWaveSummaryAdapter.Create("empty", new GuestRequestResult[0])), "empty completion rejected");
            var repeatedService = GuestWaveSummaryAdapter.Create("repeated-service", new[] {
                Request("a", "hotel", 0, 1), Request("a", "hotel", 1, 2)
            });
            check(repeatedService.Services[0].CompletedCount == 2, "different request indices remain separate");
            return passed;
        }

        static GuestRequestResult Request(string guest, string service, int index, float wait,
            GuestRequestState state = GuestRequestState.Completed)
        {
            var constructor = typeof(GuestRequestResult).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                new[] { typeof(string), typeof(string), typeof(int), typeof(GuestRequestState), typeof(float) }, null);
            if (constructor == null) throw new InvalidOperationException("Guest request constructor is unavailable.");
            return (GuestRequestResult)constructor.Invoke(new object[] { guest, service, index, state, wait });
        }

        static bool Rejects(Action action)
        {
            try { action(); }
            catch (ArgumentException) { return true; }
            return false;
        }
    }
}
#endif
