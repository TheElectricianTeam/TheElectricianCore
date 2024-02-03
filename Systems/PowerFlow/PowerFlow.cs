﻿using System.Diagnostics.CodeAnalysis;
using TheElectrician.Models;
using TheElectrician.Objects;
using TheElectrician.Systems.Config;

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
        //TODO: Calculate taking into account the conductivity of the wires
        //TODO: The maximum distance between the wires (you cannot connect two wires from different sides of the world)
        //TODO: Checking the wire block (the cable cannot pass through obstacles)

        foreach (var powerSys in powerSystems)
        {
            var storages = powerSys.GetConnections().OfType<Storage>().Where(x => x is not IGenerator && !x.IsFull())
                .OrderBy(x => x.Count(Consts.storagePowerKey)).ToList();
            var generators = powerSys.GetConnections().OfType<IGenerator>()
                .Where(x => x.Count(Consts.storagePowerKey) > 0).OrderByDescending(x => x.Count(Consts.storagePowerKey))
                .ToList();

            if (storages.Count == 0 || generators.Count == 0) continue;
            foreach (var storage in storages)
            {
                if (storage.IsFull()) continue;
                foreach (var gen in generators)
                {
                    //32 should be conductivity of the wire
                    var toAdd = Min(storage.FreeSpace(), gen.Count(Consts.storagePowerKey));
                    if (toAdd == 0) continue;

                    var path = PathFinder.FindBestPath(storage, gen);
                    if (path.Count == 0) continue;
                    toAdd = CalculatePower(toAdd, storage, gen, path);

                    gen.TransferTo(storage, Consts.storagePowerKey, toAdd);
                }
            }
        }
    }

    private static float CalculatePower(float initialPower, IWireConnectable start, IWireConnectable end,
        HashSet<IWireConnectable> path)
    {
        var resultPower = initialPower;
        foreach (var point in path)
        {
            resultPower = Clamp(initialPower, 0, point.GetConductivity());
            resultPower *= 1 - (point.GetPowerLoss() / 100);
            if (resultPower == 0) break;
        }

        // Debug($"Calculated {initialPower}->{resultPower} power from {start} to {end}. Path: {path.GetString()}");
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

            GoThroughConnections(new HashSet<IWireConnectable> { storage });
        }

        var connectedPowerSystems = new HashSet<PowerSystem>(powerSystems);
        foreach (var powerSys in powerSystems)
        {
            var connected =
                powerSystems.FirstOrDefault(x => x.GetConnections().Any(x1 => powerSys.GetConnections().Contains(x1)));
            if (connected != null && powerSys != connected)
            {
                powerSys.SetConnections(powerSys.GetConnections().Union(connected.GetConnections()).ToHashSet());
                connectedPowerSystems.Remove(connected);
            }
        }

        powerSystems = connectedPowerSystems;

        void GoThroughConnections(HashSet<IWireConnectable> connections)
        {
            foreach (var electricObject in connections)
            {
                if (currentPowerSys.GetConnections().Contains(electricObject)) continue;
                currentPowerSys.GetConnections().Add(electricObject);

                GoThroughConnections(electricObject.GetConnections());
            }
        }
    }

    public static PowerSystem GetPowerSystem(IWireConnectable element)
    {
        return powerSystems.FirstOrDefault(x => x.ContainsConnection(element));
    }
}