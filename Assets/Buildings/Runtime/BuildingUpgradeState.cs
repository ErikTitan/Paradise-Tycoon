using System;

namespace ParadiseTycoon.Buildings
{
    public interface IBuildingUpgradeState
    {
        event Action Changed;
        int Coins { get; }
        bool IsWaveActive { get; }
        int GetBuildingLevel(string buildingId, int initialLevel);

        // Validate wave state, balance and expected level, then persist coins and level together.
        bool TryPurchaseUpgrade(string buildingId, int expectedLevel, int cost);
    }
}
