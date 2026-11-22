using UnityEngine;
using UnityEngine.Tilemaps;

public class RockScript : MonoBehaviour
{
    [Header("Rock Settings")]
    public float health = 3f;
    public ItemData dropItem;
    public GameObject dropPrefab;
    public int dropAmount = 2;

    private bool isMined = false;
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
        if (isMined || damage <= 0f) return;

        health -= damage;

        if (health <= 0f)
        {
            BreakRock();
        }
    }

    private void BreakRock()
    {
        isMined = true;
        SpawnDrops();

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

    private void SpawnDrops()
    {
        for (int i = 0; i < dropAmount; i++)
        {
            Vector3 randomPos = transform.position + new Vector3(
                Random.Range(-0.45f, 0.45f),
                Random.Range(-0.35f, 0.35f),
                0f
            );

            if (dropItem != null)
            {
                ItemInWorldManager.SpawnDrop(dropItem, randomPos, 1);
            }
            else if (dropPrefab != null)
            {
                Instantiate(dropPrefab, randomPos, Quaternion.identity);
            }
        }
    }
}