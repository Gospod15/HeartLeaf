using System;
using UnityEngine;

[Serializable]
public class WorldSettingsData
{
    public string worldName = "World 1";
    public int worldSeed = 12345;
    public int difficulty = 1;
    public float dayLengthMultiplier = 1f;
    public float lootMultiplier = 1f;
    public bool allowPvP = false;
}

public static class GameSessionContext
{
    private const string DefaultSaveSlot = "world_save";

    public static string CurrentSaveSlot { get; private set; } = DefaultSaveSlot;
    public static WorldSettingsData PendingWorldSettings { get; private set; } = new WorldSettingsData();
    public static bool IsCreatingNewWorld { get; private set; } = false;

    public static void SelectNewWorld(string saveName, WorldSettingsData settings)
    {
        CurrentSaveSlot = SanitizeFileName(saveName);
        PendingWorldSettings = settings ?? new WorldSettingsData();
        if (string.IsNullOrWhiteSpace(PendingWorldSettings.worldName))
            PendingWorldSettings.worldName = saveName;

        IsCreatingNewWorld = true;
    }

    public static void SelectExistingWorld(string saveName)
    {
        CurrentSaveSlot = SanitizeFileName(saveName);
        PendingWorldSettings = new WorldSettingsData { worldName = saveName };
        IsCreatingNewWorld = false;
    }

    public static void ClearNewWorldFlag()
    {
        IsCreatingNewWorld = false;
    }

    public static string SanitizeFileName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return DefaultSaveSlot;

        string clean = rawName.Trim();
        foreach (char c in System.IO.Path.GetInvalidFileNameChars())
        {
            clean = clean.Replace(c, '_');
        }
        return clean;
    }
}