using UnityEngine;
using UnityEngine.Tilemaps;

// The maze is already painted in the scene. Build its logical grid from those
// tiles so movement and the visible level always use the same coordinates.
public sealed class LevelGenerator : MonoBehaviour
{
    public const int Empty = 0;
    public const int Wall = 1;
    public const int GhostWall = 7;

    public int[,] levelMap { get; private set; }

    private Tilemap tilemap;
    private BoundsInt bounds;
    private Transform wallColliders;

    public void Initialize(Tilemap source, TileBase emptyTile, TileBase ghostWallTile)
    {
        tilemap = source;
        bounds = tilemap.cellBounds;
        levelMap = new int[bounds.size.y, bounds.size.x];

        for (int row = 0; row < bounds.size.y; row++)
        {
            for (int column = 0; column < bounds.size.x; column++)
            {
                TileBase tile = tilemap.GetTile(ToCell(new Vector2Int(column, row)));
                levelMap[row, column] = tile == ghostWallTile ? GhostWall
                    : tile == emptyTile ? Empty : Wall;
            }
        }

        BuildWallColliders();
    }

    // Full-cell boxes keep collision geometry independent of the wall artwork.
    // Merge horizontal runs so the maze needs fewer colliders.
    private void BuildWallColliders()
    {
        if (wallColliders != null)
            return;

        wallColliders = new GameObject("Wall Colliders").transform;
        wallColliders.SetParent(tilemap.transform, false);
        Vector3 cellSize = tilemap.layoutGrid.cellSize;
        for (int row = 0; row < levelMap.GetLength(0); row++)
        {
            for (int column = 0; column < levelMap.GetLength(1); column++)
            {
                if (levelMap[row, column] == Empty)
                    continue;

                int start = column;
                while (column + 1 < levelMap.GetLength(1) && levelMap[row, column + 1] != Empty)
                    column++;

                BoxCollider2D wall = wallColliders.gameObject.AddComponent<BoxCollider2D>();
                Vector3 first = GridToWorld(new Vector2Int(start, row));
                Vector3 last = GridToWorld(new Vector2Int(column, row));
                wall.offset = wallColliders.InverseTransformPoint((first + last) * 0.5f);
                wall.size = new Vector2((column - start + 1) * cellSize.x, cellSize.y);
            }
        }
    }

    public bool IsWallCollider(Collider2D other)
    {
        return other != null && wallColliders != null && other.transform == wallColliders;
    }

    // Map coordinates are (column, row), with row zero at the top of the maze.
    public Vector2Int WorldToGrid(Vector3 position)
    {
        Vector3Int cell = tilemap.WorldToCell(position);
        return new Vector2Int(cell.x - bounds.xMin, bounds.yMax - 1 - cell.y);
    }

    public Vector3 GridToWorld(Vector2Int position)
    {
        return tilemap.GetCellCenterWorld(ToCell(position));
    }

    public bool IsWalkable(Vector2Int position)
    {
        return position.x >= 0 && position.x < levelMap.GetLength(1)
            && position.y >= 0 && position.y < levelMap.GetLength(0)
            && levelMap[position.y, position.x] == Empty;
    }

    private Vector3Int ToCell(Vector2Int position)
    {
        return new Vector3Int(bounds.xMin + position.x, bounds.yMax - 1 - position.y, bounds.zMin);
    }
}
