using BWAPI.NET;

namespace Shared;

// library from https://www.nuget.org/packages/BWAPI.NET

public class MyStarcraftBot : DefaultBWListener
{
    private BWClient? _bwClient = null;
    public Game? Game => _bwClient?.Game;

    public bool IsRunning { get; private set; } = false;
    public bool InGame { get; private set; } = false;
    public int? GameSpeedToSet { get; set; } = null;

    public event Action? StatusChanged;

    public void Connect()
    {
        _bwClient = new BWClient(this);
        IsRunning = true;
        _bwClient.StartGame();
    }


    // Bot Callbacks below
    public override void OnStart()
    {
        InGame = true;
        Game?.EnableFlag(Flag.UserInput); // let human control too
        SendWorkersToMine();
    }

    public override void OnEnd(bool isWinner)
    {
        InGame = false;
    }

    public override void OnFrame()
    {
        if (Game == null)
            return;
        if (GameSpeedToSet != null)
        {
            Game.SetLocalSpeed(GameSpeedToSet.Value);
            GameSpeedToSet = null;
        }

        HandleTerranProduction();
        HandleTerranArmyMovement();
        Game.DrawTextScreen(100, 100, "Hello Bot!");
    }

    public override void OnUnitComplete(Unit unit)
    {
        if (unit.GetUnitType().IsWorker())
            SendWorkerToMine(unit);
    }

    public override void OnUnitDestroy(Unit unit) { }

    public override void OnUnitMorph(Unit unit) { }

    public override void OnSendText(string text) { }

    public override void OnReceiveText(Player player, string text) { }

    public override void OnPlayerLeft(Player player) { }

    public override void OnNukeDetect(Position target) { }

    public override void OnUnitEvade(Unit unit) { }

    public override void OnUnitShow(Unit unit) { }

    public override void OnUnitHide(Unit unit) { }

    public override void OnUnitCreate(Unit unit) { }

    public override void OnUnitRenegade(Unit unit) { }

    public override void OnSaveGame(string gameName) { }

    public override void OnUnitDiscover(Unit unit) { }

    private void SendWorkersToMine()
    {
        if (Game == null)
            return;

        foreach (var unit in Game.Self().GetUnits())
        {
            if (unit.GetUnitType().IsWorker())
                SendWorkerToMine(unit);
        }
    }

    private void SendWorkerToMine(Unit worker)
    {
        if (Game == null)
            return;

        Unit? closestMineral = null;
        var closestDistance = int.MaxValue;

        foreach (var mineral in Game.GetMinerals())
        {
            var distance = worker.GetDistance(mineral);
            if (distance < closestDistance)
            {
                closestMineral = mineral;
                closestDistance = distance;
            }
        }

        if (closestMineral != null)
            worker.Gather(closestMineral);
    }

    private void HandleTerranProduction()
    {
        if (Game == null)
            return;

        var self = Game.Self();
        if (self.GetRace() != Race.Terran)
            return;

        var supplyDepotCount = CountSelfUnits(UnitType.Terran_Supply_Depot);
        var barracksCount = CountSelfUnits(UnitType.Terran_Barracks);
        var academyCount = CountSelfUnits(UnitType.Terran_Academy);
        var factoryCount = CountSelfUnits(UnitType.Terran_Factory);
        var marineCount = CountSelfUnits(UnitType.Terran_Marine);
        var medicCount = CountSelfUnits(UnitType.Terran_Medic);

        if (supplyDepotCount == 0)
        {
            TryBuildTerranStructure(UnitType.Terran_Supply_Depot);
            return;
        }

        if (barracksCount == 0)
        {
            TryBuildTerranStructure(UnitType.Terran_Barracks);
            return;
        }

        if (self.SupplyTotal() - self.SupplyUsed() <= 4)
        {
            TryBuildTerranStructure(UnitType.Terran_Supply_Depot);
        }

        if (marineCount <= 20)
        {
            if (marineCount >= 5 && academyCount == 0)
            {
                TryBuildTerranStructure(UnitType.Terran_Academy);
            }

            var targetMedicCount = marineCount / 5;
            var shouldTrainMedic = targetMedicCount > medicCount;

            foreach (var unit in self.GetUnits())
            {
                if (unit.GetUnitType() != UnitType.Terran_Barracks)
                    continue;

                if (!unit.IsCompleted() || !unit.IsIdle())
                    continue;

                if (shouldTrainMedic && academyCount > 0)
                {
                    unit.Train(UnitType.Terran_Medic);
                    shouldTrainMedic = false;
                    continue;
                }

                unit.Train(UnitType.Terran_Marine);
            }

            return;
        }

        if (factoryCount == 0)
        {
            TryBuildTerranStructure(UnitType.Terran_Factory);
            return;
        }

        foreach (var unit in self.GetUnits())
        {
            if (unit.GetUnitType() != UnitType.Terran_Factory)
                continue;

            if (!unit.IsCompleted() || !unit.IsIdle())
                continue;

            unit.Train(UnitType.Terran_Vulture);
        }
    }

    private void HandleTerranArmyMovement()
    {
        if (Game == null)
            return;

        var self = Game.Self();
        if (self.GetRace() != Race.Terran)
            return;

        var selfStart = self.GetStartLocation();
        var targetStart = GetClosestEnemyStartLocation(selfStart);
        if (targetStart == null)
            return;
        var enemyStart = targetStart.Value;

        var targetPosition = new Position(enemyStart.X * 32 + 16, enemyStart.Y * 32 + 16);

        foreach (var unit in self.GetUnits())
        {
            if (!unit.IsCompleted() || !unit.IsIdle())
                continue;

            if (unit.GetUnitType() == UnitType.Terran_Marine)
            {
                unit.Attack(targetPosition);
            }
            else if (unit.GetUnitType() == UnitType.Terran_Medic)
            {
                unit.Move(targetPosition);
            }
            else if (unit.GetUnitType() == UnitType.Terran_Vulture)
            {
                unit.Attack(targetPosition);
            }
        }
    }

    private TilePosition? GetClosestEnemyStartLocation(TilePosition selfStart)
    {
        if (Game == null)
            return null;

        TilePosition? closestStart = null;
        var closestDistanceSquared = int.MaxValue;

        foreach (var startLocation in Game.GetStartLocations())
        {
            if (startLocation.X == selfStart.X && startLocation.Y == selfStart.Y)
                continue;

            var dx = startLocation.X - selfStart.X;
            var dy = startLocation.Y - selfStart.Y;
            var distanceSquared = dx * dx + dy * dy;

            if (distanceSquared < closestDistanceSquared)
            {
                closestDistanceSquared = distanceSquared;
                closestStart = startLocation;
            }
        }

        return closestStart;
    }

    private int CountSelfUnits(UnitType unitType)
    {
        if (Game == null)
            return 0;

        var count = 0;
        foreach (var unit in Game.Self().GetUnits())
        {
            if (unit.GetUnitType() == unitType)
                count++;
        }

        return count;
    }

    private void TryBuildTerranStructure(UnitType unitType)
    {
        if (Game == null)
            return;

        Unit? worker = null;
        foreach (var unit in Game.Self().GetUnits())
        {
            if (!unit.GetUnitType().IsWorker())
                continue;

            if (!unit.IsCompleted())
                continue;

            worker = unit;
            if (unit.IsIdle())
                break;
        }

        if (worker == null)
            return;

        var seedPosition = Game.Self().GetStartLocation();
        var buildTile = BuildLocation.Get(Game, unitType, seedPosition, 32, 2);

        if (buildTile.X >= 0 && buildTile.Y >= 0)
        {
            worker.Build(unitType, buildTile);
        }
    }
}
