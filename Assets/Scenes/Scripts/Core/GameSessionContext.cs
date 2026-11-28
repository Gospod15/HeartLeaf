using System;
using System.IO;
using UnityEngine;

[Serializable]
public class WorldSettingsData
{
    public string worldName = "New World";
    public string playerName = "Player";
    public int worldSeed = 12345;
    public int difficulty = 1;
}

public static class GameSessionContext
{
    public static string CurrentSaveSlot { get; private set; }
    public static bool IsCreatingNewWorld { get; private set; }
    public static WorldSettingsData PendingWorldSettings { get; private set; }

    public static void StartNewWorld(string worldName, string playerName, int seed, int difficulty = 1)
    {
        string cleanSlot = SanitizeFileName(worldName);
        if (string.IsNullOrWhiteSpace(cleanSlot))
            cleanSlot = "world_" + DateTime.UtcNow.Ticks;

        CurrentSaveSlot = cleanSlot;
        IsCreatingNewWorld = true;
        PendingWorldSettings = new WorldSettingsData
        {
            worldName = string.IsNullOrWhiteSpace(worldName) ? cleanSlot : worldName.Trim(),
            playerName = string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName.Trim(),
            worldSeed = seed,
            difficulty = difficulty
        };
    }

    public static void LoadExistingWorld(string saveSlotName)
    {
        CurrentSaveSlot = SanitizeFileName(saveSlotName);
        IsCreatingNewWorld = false;
        PendingWorldSettings = null;
    }

    public static void ClearNewWorldFlag()
    {
        IsCreatingNewWorld = false;
    }

    public static string SanitizeFileName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName)) return "world_save";

        string trimmed = rawName.Trim();
        char[] invalid = Path.GetInvalidFileNameChars();
        for (int i = 0; i < invalid.Length; i++)
        {
            trimmed = trimmed.Replace(invalid[i], '_');
        }
        return trimmed;
    }
}