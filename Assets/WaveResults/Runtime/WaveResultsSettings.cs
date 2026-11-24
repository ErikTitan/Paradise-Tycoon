using UnityEngine;

namespace ParadiseTycoon.WaveResults
{
    public sealed class WaveResultsSettings : ScriptableObject
    {
        public WaveRatingRules rating = new WaveRatingRules();
        public AudioClip successfulWave;
    }
}
