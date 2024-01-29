﻿namespace TheElectrician.Models.Settings;

public class StorageSettings : ElectricObjectSettings
{
    public readonly int capacity;

    public StorageSettings(Type type, int capacity, bool storeOnlyPower = true) : base(type) { this.capacity = capacity; }
}