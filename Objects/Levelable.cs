﻿using TheElectrician.Models;
using TheElectrician.Models.Settings;
using UnityEngine.Events;

namespace TheElectrician.Objects;

public class Levelable : ElectricObject, ILevelable
{
    public UnityEvent onLevelChanged { get; private set; }
    private LevelableSettings levelableSettings;

    public override void InitSettings(ElectricObjectSettings sett)
    {
        base.InitSettings(sett);
        levelableSettings = GetSettings<LevelableSettings>();
        if (levelableSettings is null)
            DebugError($"Levelable.InitSettings: Settings '{GetSettings()?.GetType().Name}' is not LevelableSettings");
    }

    public override void InitData()
    {
        base.InitData();
        GetLevel();
        onLevelChanged = new UnityEvent();
    }

    public int GetStartLevel() => levelableSettings.startLevel;

    public int GetLevel()
    {
        if (GetMaxLevel() == GetStartLevel()) return GetMaxLevel();
        if (!IsValid()) return 0;

        var level = GetZDO().GetInt(Consts.levelKey, -1);
        if (level == -1)
        {
            level = GetStartLevel();
            SetLevel(level);
        }

        return level;
    }

    public bool SetLevel(int level)
    {
        if (!IsValid()) return false;

        GetZDO().Set(Consts.levelKey, level);
        onLevelChanged?.Invoke();
        return true;
    }

    public bool AddLevel(int amount = 1)
    {
        if (!IsValid()) return false;

        var level = GetLevel();
        return SetLevel(level + amount);
    }

    public bool RemoveLevel(int amount = 1)
    {
        if (!IsValid()) return false;

        var level = GetLevel();
        return SetLevel(level - amount);
    }

    public int GetMaxLevel() => levelableSettings.maxLevel;
}