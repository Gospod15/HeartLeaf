using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TreeScript : MonoBehaviour
{
    [Header("Tree Settings")]
    public float health = 3f;
    public ItemData logItem;
    public GameObject logPrefab;
    public int dropAmount = 3;

    [Header("Fall Settings")]
    [SerializeField] private float fallDuration = 0.65f;
    [SerializeField] private float destroyDelayAfterFall = 0.25f;

    private bool isChopped = false;
    private Vector2Int initialGridPos;

    private void Start()
    {
        Tilemap map = WorldGenerator.instance != null ? WorldGenerator.instance.groundTilemap : null;
        if (map != null)
        {
            Vector3Int cell = map.WorldToCell(transform.position);
            initialGridPos = new Vector2Int(cell.x, cell.y);
        }
        else
        {
            initialGridPos = new Vector2Int(Mathf.FloorToInt(transform.position.x), Mathf.FloorToInt(transform.position.y));
        }
    }

    public void TakeDamage(float damage)
    {
        TakeDamage(damage, transform.position);
    }

    public void TakeDamage(float damage, Vector3 attackerPosition)
    {
        if (isChopped) return;

        health -= damage;

        if (health <= 0f)
        {
            ChopDown(attackerPosition);
        }
    }

    private void ChopDown(Vector3 attackerPosition)
    {
        isChopped = true;

        Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }

        float fallDirection = attackerPosition.x <= transform.position.x ? -1f : 1f;
        StartCoroutine(FallAndDestroyRoutine(fallDirection));
    }

    private IEnumerator FallAndDestroyRoutine(float directionSign)
    {
        Quaternion startRot = transform.rotation;
        Quaternion targetRot = startRot * Quaternion.Euler(0f, 0f, 90f * directionSign);

        float elapsed = 0f;
        while (elapsed < fallDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fallDuration);
            float easedT = t * t;
            transform.rotation = Quaternion.Lerp(startRot, targetRot, easedT);
            yield return null;
        }

        transform.rotation = targetRot;

        SpawnLogs();

        if (destroyDelayAfterFall > 0f)
            yield return new WaitForSeconds(destroyDelayAfterFall);

        if (TryGetComponent(out WorldObject worldObj))
        {
            worldObj.DestroyWorldObject();
        }
        else
        {
            if (WorldSaveManager.instance != null)
                WorldSaveManager.instance.RecordNaturalChange(initialGridPos, 1);

            if (WorldGenerator.instance != null)
                WorldGenerator.instance.InvalidateCellCache(initialGridPos);

            Destroy(gameObject);
        }
    }

    private void SpawnLogs()
    {
        for (int i = 0; i < dropAmount; i++)
        {
            Vector3 randomPos = transform.position + new Vector3(
                Random.Range(-0.5f, 0.5f),
                Random.Range(-0.3f, 0.3f),
                0f
            );

            if (logItem != null)
            {
                ItemInWorldManager.SpawnDrop(logItem, randomPos, 1);
            }
            else if (logPrefab != null)
            {
                Instantiate(logPrefab, randomPos, Quaternion.identity);
            }
        }
    }
}