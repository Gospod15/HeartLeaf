using UnityEngine;

public class WorldObject : MonoBehaviour
{
    [Tooltip("Unique ID for player-built objects. Leave empty for natural trees, ores, and rocks")]
    public string objectId;

    [HideInInspector] public Vector2Int gridPosition;
    [HideInInspector] public bool isPlayerBuilt = false;

    private bool isDestroyed = false;

    public void DestroyWorldObject()
    {
        if (isDestroyed) return;
        isDestroyed = true;

        if (WorldSaveManager.instance != null)
        {
            if (isPlayerBuilt)
                WorldSaveManager.instance.RemovePlacedObject(gridPosition);
            else
                WorldSaveManager.instance.RecordNaturalChange(gridPosition, 1);
        }

        if (WorldGenerator.instance != null)
        {
            WorldGenerator.instance.InvalidateCellCache(gridPosition);
        }

        Destroy(gameObject);
    }
}