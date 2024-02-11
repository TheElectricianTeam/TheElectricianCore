﻿using System.Diagnostics.CodeAnalysis;
using TheElectrician.Extensions;
using TheElectrician.Objects.Consumers.Furnace;

namespace TheElectrician.Systems.PowerFlow;

[PublicAPI]
public static class PowerFlow
{
    private static HashSet<PowerSystem> powerSystems = [];

    public static void Start()
    {
        Debug("PowerFlow: Starting update");
        GetPlugin().StartCoroutine(UpdateEnumerator());
    }

    public static void Destroy()
    {
        Debug("PowerFlow: Stopping update");
        GetPlugin().StopCoroutine(UpdateEnumerator());
    }

    [SuppressMessage("ReSharper", "FunctionRecursiveOnAllPaths")]
    private static IEnumerator UpdateEnumerator()
    {
        yield return new WaitForSeconds(TheConfig.PowerTickTime);
        Update();
        GetPlugin().StartCoroutine(UpdateEnumerator());
    }

    private static void Update()
    {
        FormPowerSystems();
        TransportPowerToStorages();
    }

    private static void TransportPowerToStorages()
    {
        //TODO: The maximum distance between the wires (you cannot connect two wires from different sides of the world)
        //TODO: Checking the wire block (the cable cannot pass through obstacles)

        foreach (var powerSys in powerSystems)
        {
            var storages = powerSys.GetStorages()
                .Where(x => x is not IGenerator && x is not IConsumer && !x.IsFull())
                .OrderBy(x => x.GetPower()).ToList();
            var generators = powerSys.GetConnections().OfType<IGenerator>()
                .Where(x => x.GetPower() > 0).OrderByDescending(x => x.GetPower())
                .ToList();

            if (storages.Count == 0 || generators.Count == 0) continue;
            HashSet<IWire> usedWires = [];
            foreach (var storage in storages)
            {
                // Debug($"storage {storage.GetObjectString()}");
                if (storage.IsFull()) continue;
                foreach (var gen in generators)
                {
                    var toAdd = Min(storage.FreeSpace(), gen.GetPower());
                    if (toAdd == 0) continue;

                    var path = PathFinder.FindBestPath(storage, gen, usedWires);
                    var wires = path.OfType<IWire>();
                    foreach (var wire in wires) usedWires.Add(wire);

                    if (path.Count == 0) continue;
                    toAdd = CalculatePower(toAdd, path);

                    gen.TransferTo(storage, Consts.storagePowerKey, toAdd);
                    return;
                }
            }
        }
    }

    public static float CalculatePower(float initialPower, HashSet<IWireConnectable> path)
    {
        var resultPower = initialPower;
        foreach (var point in path)
        {
            resultPower = Clamp(initialPower, 0, point.GetConductivity());
            resultPower *= 1 - (point.GetPowerLoss() / 100);
            if (resultPower == 0) break;
        }

        // Debug($"Calculated {initialPower}->{resultPower} power. "
        //       + $"PathConductivity: {path.Select(x => x.GetConductivity()).GetString()}");
        return resultPower;
    }

    private static void FormPowerSystems()
    {
        powerSystems.Clear();
        PowerSystem currentPowerSys;
        var allStorages = Library.GetAllObjects<Storage>();
        foreach (var storage in allStorages)
        {
            currentPowerSys = new PowerSystem();
            powerSystems.Add(currentPowerSys);

            GoThroughConnections([storage]);
        }

        var connectedPowerSystems = new HashSet<PowerSystem>(powerSystems);
        foreach (var powerSys in powerSystems)
        {
            var connected = powerSystems.FirstOrDefault(x =>
                x.GetConnections().Any(x1 => powerSys.GetConnections().Contains(x1)));
            if (connected != null && powerSys != connected)
            {
                powerSys.SetConnections(powerSys.GetConnections().Union(connected.GetConnections()).ToHashSet());
                connectedPowerSystems.RemoveWhere(x => x.Equals(connected));
            }
        }

        powerSystems = connectedPowerSystems;

        void GoThroughConnections(HashSet<IPipeableConnectable> connections)
        {
            foreach (var electricObject in connections)
            {
                if (currentPowerSys.GetConnections().Contains(electricObject)) continue;
                currentPowerSys.GetConnections().Add(electricObject as IWireConnectable);

                GoThroughConnections(electricObject.GetConnections());
            }
        }
    }

    public static PowerSystem GetPowerSystem(IWireConnectable element)
    {
        return powerSystems.FirstOrDefault(x => x.ContainsConnection(element));
    }

    public static bool ConsumePower(IConsumer consumer, float amount)
    {
        var powerSys = GetPowerSystem(consumer as IWireConnectable);
        if (powerSys == null) return false;

        return powerSys.ConsumePower(consumer, amount);
    }
}