using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(PlayerStats))]
public class PlayerSaveable : MonoBehaviour, ISaveablePlayer
{
    private PlayerStats stats;
    private Rigidbody2D rb;
    private ISaveableInventory cachedInventory;

    private PlayerSaveData pendingData;
    private Func<string, ItemData> pendingResolver;

    private void Awake()
    {
        CacheComponents();
    }

    private IEnumerator Start()
    {
        yield return null;
        CacheComponents();

        if (pendingData != null && cachedInventory != null)
        {
            cachedInventory.RestoreInventory(pendingData.mainInventory, pendingData.armoryInventory, pendingResolver);
            pendingData = null;
            pendingResolver = null;
        }
    }

    private void CacheComponents()
    {
        if (stats == null) stats = GetComponent<PlayerStats>();
        if (rb == null) rb = GetComponent<Rigidbody2D>();

        if (cachedInventory == null)
        {
            if (InventoryManager.instance is ISaveableInventory inv)
                cachedInventory = inv;
            else
                cachedInventory = FindFirstObjectByType<InventoryManager>() as ISaveableInventory;
        }
    }

    public PlayerSaveData CaptureState()
    {
        CacheComponents();

        PlayerSaveData data = new PlayerSaveData
        {
            hasData = true,
            posX = transform.position.x,
            posY = transform.position.y,
            currentHealth = stats != null ? stats.CurrentHealth : 100f,
            currentFeed = stats != null ? stats.CurrentFeed : 100f,
            currentStamina = stats != null ? stats.CurrentStamina : 100f
        };

        if (cachedInventory != null)
        {
            cachedInventory.CaptureInventory(data.mainInventory, data.armoryInventory);
        }

        return data;
    }

    public void RestoreState(PlayerSaveData data, Func<string, ItemData> itemResolver)
    {
        if (data == null || !data.hasData) return;

        CacheComponents();

        Vector2 savedPos = new Vector2(data.posX, data.posY);
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.position = savedPos;
        }
        transform.position = new Vector3(savedPos.x, savedPos.y, transform.position.z);

        Camera cam = Camera.main;
        if (cam != null && cam.transform.parent != transform)
        {
            cam.transform.position = new Vector3(savedPos.x, savedPos.y, cam.transform.position.z);
        }

        if (stats != null)
        {
            stats.RestoreStats(data.currentHealth, data.currentFeed, data.currentStamina);
        }

        pendingData = data;
        pendingResolver = itemResolver;

        if (cachedInventory != null)
        {
            cachedInventory.RestoreInventory(data.mainInventory, data.armoryInventory, itemResolver);
        }
    }
}