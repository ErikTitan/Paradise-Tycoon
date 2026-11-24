using System;
using System.Collections.Generic;
using ParadiseTycoon.WaveResults;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
#endif

namespace ParadiseTycoon.WaveResults.Editor
{
    public static class WaveResultsChecks
    {
#if UNITY_EDITOR
        [MenuItem("Tools/Paradise Tycoon/Wave results/Run calculation checks")]
        static void RunFromMenu() => Debug.Log("Wave results: " + Run() + " checks passed.");
#endif
        public static int Run()
        {
            int passed = 0;
            Action<bool, string> check = (value, name) => {
                if (!value) throw new Exception("Wave results check failed: " + name);
                passed++;
            };
            var rules = new WaveRatingRules();
            var quick = Summary("first", 0);
            var perfect = WaveRatingCalculator.Evaluate(quick, rules);
            check(perfect.Stars == 5 && perfect.Satisfaction == 1 && perfect.Coins == 60, "fast wave");
            var slow = WaveRatingCalculator.Evaluate(Summary("slow", 240), rules);
            check(slow.Stars == 3 && slow.Coins == 40, "overloaded wave rating and reward");
            check(slow.ServicesByImpact[0].Stats.ServiceId == "restaurant", "worst service identified");
            double previousScore = 1;
            int previousStars = 5, previousCoins = int.MaxValue;
            for (int wait = 0; wait <= 400; wait++)
            {
                var result = WaveRatingCalculator.Evaluate(Summary("monotone", wait), rules);
                check(result.Satisfaction <= previousScore && result.Stars <= previousStars
                    && result.Coins <= previousCoins, "longer waiting cannot improve results");
                previousScore = result.Satisfaction;
                previousStars = result.Stars;
                previousCoins = result.Coins;
            }
            var repeat = WaveRatingCalculator.Evaluate(quick, rules);
            check(repeat.Coins == perfect.Coins && repeat.Stars == perfect.Stars
                && repeat.Satisfaction == perfect.Satisfaction, "deterministic calculation");
            var reversed = new WaveSummary("first", new[] { quick.Services[1], quick.Services[0] });
            var reordered = WaveRatingCalculator.Evaluate(reversed, rules);
            check(reordered.Satisfaction == perfect.Satisfaction && reordered.Coins == perfect.Coins,
                "input ordering does not change results");
            check(reordered.ServicesByImpact[0].Stats.ServiceId == perfect.ServicesByImpact[0].Stats.ServiceId,
                "equal impacts have stable ordering");
            var partial = WaveRatingCalculator.Evaluate(new WaveSummary("partial", new[] {
                new ServiceWaveStats("hotel", "Hotel", 8, 4, 0)
            }), rules);
            check(partial.Satisfaction == .5 && partial.Coins == 20, "unserved requests reduce satisfaction and reward");
            var unserved = WaveRatingCalculator.Evaluate(new WaveSummary("unserved", new[] {
                new ServiceWaveStats("hotel", "Hotel", 8, 0, 0)
            }), rules);
            check(unserved.Coins == 0 && unserved.Satisfaction == 0, "no free reward for zero service");
            check(Throws<ArgumentException>(() => new WaveSummary("empty", new ServiceWaveStats[0])), "empty wave rejected");
            check(Throws<ArgumentException>(() => new WaveSummary("duplicate", new[] { quick.Services[0], quick.Services[0] })),
                "duplicate service rejected");
            check(Throws<ArgumentException>(() => new WaveSummary(" ", quick.Services)), "missing wave ID rejected");
            check(Throws<ArgumentOutOfRangeException>(() => new ServiceWaveStats("a", "A", 1, 2, 0)), "invalid request counts rejected");
            check(Throws<ArgumentOutOfRangeException>(() => new ServiceWaveStats("a", "A", 1, 1, double.NaN)), "invalid wait rejected");
            check(Throws<ArgumentOutOfRangeException>(() => new ServiceWaveStats("a", "A", 1, 1, -1)), "negative wait rejected");
            var badRules = new WaveRatingRules { StarThresholds = new[] { .2, .4, .3, .8 } };
            check(Throws<ArgumentException>(() => WaveRatingCalculator.Evaluate(quick, badRules)), "invalid star rules rejected");
            var overflow = new WaveRatingRules { CoinsPerCompletedRequest = int.MaxValue };
            check(Throws<OverflowException>(() => WaveRatingCalculator.Evaluate(quick, overflow)), "coin overflow rejected");

            var store = new Rewards();
            var view = new View();
            int returns = 0;
            var flow = new WaveResultsFlow(rules, store, view, () => returns++);
            var first = flow.CompleteWave(quick);
            flow.CompleteWave(quick);
            check(store.Coins == 60 && store.Writes == 1 && view.Shows == 2, "duplicate result does not add coins");
            check(Throws<InvalidOperationException>(() => flow.CompleteWave(Summary("other", 0))), "another wave cannot replace an open result");
            flow.ReturnToIsland();
            flow.ReturnToIsland();
            check(returns == 1 && flow.Current == null && view.Hides == 1, "return action runs once");
            flow.CompleteWave(Summary("second", 0));
            check(store.Coins == 120 && store.Writes == 2, "two consecutive waves credited independently");
            flow.ReturnToIsland();
            var recreated = new WaveResultsFlow(new WaveRatingRules { CoinsPerCompletedRequest = 100 }, store, new View(), () => { });
            var stored = recreated.CompleteWave(quick);
            check(store.Coins == 120 && store.Writes == 2 && ReferenceEquals(first, stored),
                "recreated flow uses the stored result rather than crediting again");

            var crashStore = new Rewards { FailAfterFirstWrite = true };
            var retryFlow = new WaveResultsFlow(rules, crashStore, new View(), () => { });
            check(Throws<InvalidOperationException>(() => retryFlow.CompleteWave(quick)), "simulated interruption after credit");
            retryFlow.CompleteWave(quick);
            check(crashStore.Coins == 60 && crashStore.Writes == 1, "retry after persisted credit does not duplicate reward");
            var brokenView = new View { FailNextShow = true };
            var viewFlow = new WaveResultsFlow(rules, store, brokenView, () => { });
            check(Throws<InvalidOperationException>(() => viewFlow.CompleteWave(Summary("third", 0))), "simulated view failure");
            viewFlow.CompleteWave(Summary("third", 0));
            check(store.Coins == 180 && store.Writes == 3, "view failure cannot grant coins twice");
            bool failReturn = true;
            var returnFlow = new WaveResultsFlow(rules, store, new View(), () => {
                if (failReturn) throw new InvalidOperationException("Return temporarily unavailable.");
            });
            returnFlow.CompleteWave(quick);
            check(Throws<InvalidOperationException>(() => returnFlow.ReturnToIsland()), "failed return is reported");
            check(returnFlow.Current != null, "failed return preserves the result for retry");
            failReturn = false;
            returnFlow.ReturnToIsland();
            check(returnFlow.Current == null, "return can be retried without another reward");
#if UNITY_EDITOR
            passed += GuestWaveSummaryChecks.Run();
#endif
            return passed;
        }

        static WaveSummary Summary(string id, double restaurantWait) => new WaveSummary(id, new[] {
            new ServiceWaveStats("hotel", "Hotel", 4, 4, 0),
            new ServiceWaveStats("restaurant", "Reštaurácia", 4, 4, restaurantWait)
        });

        static bool Throws<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T) { return true; }
            return false;
        }

        sealed class Rewards : IWaveRewardStore
        {
            readonly Dictionary<string, WaveResult> saved = new Dictionary<string, WaveResult>();
            public int Coins, Writes;
            public bool FailAfterFirstWrite;
            public WaveResult ApplyRewardOnce(WaveResult result)
            {
                if (saved.TryGetValue(result.WaveId, out var stored)) return stored;
                Coins = checked(Coins + result.Coins);
                saved.Add(result.WaveId, result);
                Writes++;
                if (FailAfterFirstWrite) { FailAfterFirstWrite = false; throw new InvalidOperationException("Interrupted after write."); }
                return result;
            }
        }

        sealed class View : IWaveResultsView
        {
            public int Shows, Hides;
            public bool FailNextShow;
            public void Show(WaveResult result)
            {
                if (FailNextShow) { FailNextShow = false; throw new InvalidOperationException("View unavailable."); }
                Shows++;
            }
            public void Hide() => Hides++;
        }
    }
}
