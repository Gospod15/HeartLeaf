using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public class StructureSitePreparer : MonoBehaviour
{
    [Header("Clearing Organics")]
    [SerializeField] private LayerMask obstaclesLayerMask;
    [SerializeField] private float clearPadding = 1.5f;

    [Header("Flot dirt around house")]
    [SerializeField] private bool flattenGroundTiles = true;
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Tilemap riverTilemap;
    [SerializeField] private TileBase grassGroundTile;
    [SerializeField] private TileBase stoneGroundTile;
    [SerializeField] private TileBase[] waterTiles;
    [SerializeField] private TileBase[] sandTiles;
    [SerializeField] private TileBase[] stoneBiomeTiles;

    [Header("autostart")]
    [SerializeField] private bool prepareOnStart = true;

    private IEnumerator Start()
    {
        if (prepareOnStart)
        {
            yield return null;
            Physics2D.SyncTransforms();
            PrepareSite();
        }
    }

    public void PrepareSite()
    {
        Bounds houseBounds = CalculateHouseBounds();
        if (houseBounds.size == Vector3.zero) return;

        ClearObstaclesInArea(houseBounds);

        if (flattenGroundTiles)
        {
            FlattenTerrainUnderHouse(houseBounds);
        }
    }

    private Bounds CalculateHouseBounds()
    {
        Tilemap[] childTilemaps = GetComponentsInChildren<Tilemap>(true);
        Bounds totalBounds = new Bounds(transform.position, Vector3.zero);
        bool hasBounds = false;

        foreach (var tm in childTilemaps)
        {
            tm.CompressBounds();
            if (tm.cellBounds.size.x == 0 || tm.cellBounds.size.y == 0) continue;

            Vector3 minWorld = tm.CellToWorld(tm.cellBounds.min);
            Vector3 maxWorld = tm.CellToWorld(tm.cellBounds.max);

            Bounds tmBounds = new Bounds();
            tmBounds.SetMinMax(minWorld, maxWorld);

            if (!hasBounds)
            {
                totalBounds = tmBounds;
                hasBounds = true;
            }
            else
            {
                totalBounds.Encapsulate(tmBounds);
            }
        }

        if (!hasBounds)
        {
            Collider2D[] cols = GetComponentsInChildren<Collider2D>(true);
            foreach (var col in cols)
            {
                if (!hasBounds)
                {
                    totalBounds = col.bounds;
                    hasBounds = true;
                }
                else
                {
                    totalBounds.Encapsulate(col.bounds);
                }
            }
        }

        return totalBounds;
    }

    private void ClearObstaclesInArea(Bounds bounds)
    {
        Vector2 center = bounds.center;
        Vector2 size = new Vector2(bounds.size.x + clearPadding * 2f, bounds.size.y + clearPadding * 2f);

        bool prevHitTriggers = Physics2D.queriesHitTriggers;
        Physics2D.queriesHitTriggers = true;
        Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f, obstaclesLayerMask);
        Physics2D.queriesHitTriggers = prevHitTriggers;

        foreach (var hit in hits)
        {
            if (hit == null) continue;
            if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
            if (hit.CompareTag("Player") || hit.GetComponentInParent<NPCInteractable>() != null) continue;

            TreeScript tree = hit.GetComponentInParent<TreeScript>();
            if (tree != null) { Destroy(tree.gameObject); continue; }

            ItemInWorldManager worldItem = hit.GetComponentInParent<ItemInWorldManager>();
            if (worldItem != null) { Destroy(worldItem.gameObject); continue; }

            Destroy(hit.gameObject);
        }

        Bounds expandedBounds = new Bounds(center, new Vector3(size.x, size.y, 100f));
        ClearHolderChildren("TreeHolder", expandedBounds);
        ClearHolderChildren("BushHolder", expandedBounds);
        ClearHolderChildren("NaturalItemsHolder", expandedBounds);
    }

    private void ClearHolderChildren(string holderName, Bounds area)
    {
        GameObject holder = GameObject.Find(holderName);
        if (holder == null) return;

        Transform holderTransform = holder.transform;
        for (int i = holderTransform.childCount - 1; i >= 0; i--)
        {
            Transform child = holderTransform.GetChild(i);
            if (area.Contains(child.position))
            {
                Destroy(child.gameObject);
            }
        }
    }

    private void FlattenTerrainUnderHouse(Bounds bounds)
    {
        Tilemap ground = groundTilemap != null
            ? groundTilemap
            : (WorldGenerator.instance != null ? WorldGenerator.instance.groundTilemap : null);

        if (ground == null) return;

        if (riverTilemap == null)
        {
            GameObject riverObj = GameObject.Find("RiverTilemap");
            if (riverObj != null) riverTilemap = riverObj.GetComponent<Tilemap>();
        }

        Vector3 pad = new Vector3(0.5f, 0.5f, 0f);
        Vector3Int minCell = ground.WorldToCell(bounds.min - pad);
        Vector3Int maxCell = ground.WorldToCell(bounds.max + pad);

        TileBase fillTile = DetermineBiomeFillTile(ground, minCell, maxCell);
        if (fillTile == null) return;

        for (int x = minCell.x; x <= maxCell.x; x++)
        {
            for (int y = minCell.y; y <= maxCell.y; y++)
            {
                Vector3Int cellPos = new Vector3Int(x, y, 0);
                TileBase current = ground.GetTile(cellPos);

                bool hasRiver = riverTilemap != null && riverTilemap.HasTile(cellPos);
                if (hasRiver)
                {
                    riverTilemap.SetTile(cellPos, null);
                }

                if (current == null || hasRiver || IsWaterTile(current) || IsSandTile(current))
                {
                    ground.SetTile(cellPos, fillTile);

                    if (WorldSaveManager.instance != null)
                    {
                        byte tileTypeId = (fillTile == stoneGroundTile) ? (byte)1 : (byte)2;
                        WorldSaveManager.instance.RecordNaturalChange(new Vector2Int(x, y), tileTypeId);
                    }
                }
            }
        }
    }

    private TileBase DetermineBiomeFillTile(Tilemap ground, Vector3Int minCell, Vector3Int maxCell)
    {
        int stoneCount = 0;
        int grassCount = 0;

        for (int x = minCell.x - 3; x <= maxCell.x + 3; x++)
        {
            for (int y = minCell.y - 3; y <= maxCell.y + 3; y++)
            {
                Vector3Int pos = new Vector3Int(x, y, 0);
                if (riverTilemap != null && riverTilemap.HasTile(pos)) continue;

                TileBase tile = ground.GetTile(pos);
                if (tile == null || IsWaterTile(tile) || IsSandTile(tile)) continue;

                if (IsStoneTile(tile)) stoneCount++;
                else grassCount++;
            }
        }

        if (stoneCount > grassCount && stoneGroundTile != null)
            return stoneGroundTile;

        return grassGroundTile != null ? grassGroundTile : stoneGroundTile;
    }

    private bool IsWaterTile(TileBase tile)
    {
        if (waterTiles == null) return false;
        for (int i = 0; i < waterTiles.Length; i++)
        {
            if (waterTiles[i] == tile) return true;
        }
        return false;
    }

    private bool IsSandTile(TileBase tile)
    {
        if (sandTiles == null) return false;
        for (int i = 0; i < sandTiles.Length; i++)
        {
            if (sandTiles[i] == tile) return true;
        }
        return false;
    }

    private bool IsStoneTile(TileBase tile)
    {
        if (stoneBiomeTiles == null) return false;
        for (int i = 0; i < stoneBiomeTiles.Length; i++)
        {
            if (stoneBiomeTiles[i] == tile) return true;
        }
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        Bounds b = CalculateHouseBounds();
        if (b.size != Vector3.zero)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(b.center, new Vector3(b.size.x + clearPadding * 2f, b.size.y + clearPadding * 2f, 0f));
        }
    }
}