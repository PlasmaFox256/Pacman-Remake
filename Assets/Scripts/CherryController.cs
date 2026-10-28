using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class CherryController : MonoBehaviour
{
    [SerializeField] private GameObject cherryPrefab;
    [SerializeField] private Sprite boundsMaskSprite;
    [SerializeField] private Tilemap levelTilemap;
    [SerializeField] private Camera levelCamera;
    [SerializeField, Min(0.01f)] private float moveSpeed = 5f;
    [SerializeField, Min(0.1f)] private float outsidePadding = 1f;

    private const float SpawnDelay = 5f;
    private const int CherrySortingOrder = 32760;
    private GameObject activeCherry;
    private SpriteMask levelMask;
    private Bounds levelBounds;
    private Vector3 startPosition;
    private Vector3 endPosition;
    private Vector3 previousSpawnPosition;
    private bool hasPreviousSpawn;
    private bool waitingToSpawn = true;
    private float spawnTimer = SpawnDelay;
    private float travelProgress;
    private int sortingLayer;

    private void Start()
    {
        if (levelTilemap == null) levelTilemap = FindFirstObjectByType<Tilemap>();
        if (levelCamera == null) levelCamera = Camera.main;
        if (levelTilemap == null || levelCamera == null || cherryPrefab == null || boundsMaskSprite == null)
        {
            Debug.LogError("CherryController needs a level Tilemap, camera, big-coin prefab and mask sprite.", this);
            enabled = false;
            return;
        }

        if (!TryGetLevelBounds(out levelBounds))
        {
            Debug.LogError("CherryController cannot find any level tiles.", this);
            enabled = false;
            return;
        }

        // Use the highest existing sorting layer, above all the level sprites.
        SortingLayer[] layers = SortingLayer.layers;
        sortingLayer = layers[layers.Length - 1].id;
        var maskObject = new GameObject("Bonus Cherry Level Mask");
        // Keep the mask in world space even when this component's owner moves.
        maskObject.transform.position = levelBounds.center;
        Vector2 spriteSize = boundsMaskSprite.bounds.size;
        maskObject.transform.localScale = new Vector3(levelBounds.size.x / spriteSize.x,
            levelBounds.size.y / spriteSize.y, 1f);
        levelMask = maskObject.AddComponent<SpriteMask>();
        levelMask.sprite = boundsMaskSprite;
        levelMask.isCustomRangeActive = true;
        levelMask.backSortingLayerID = sortingLayer;
        levelMask.frontSortingLayerID = sortingLayer;
        levelMask.backSortingOrder = CherrySortingOrder - 1;
        levelMask.frontSortingOrder = CherrySortingOrder + 1;
    }

    private void Update()
    {
        Tick(Time.deltaTime);
    }

    private void Tick(float deltaTime)
    {
        if (activeCherry == null)
        {
            // Unity's null comparison also detects a cherry destroyed externally.
            if (!waitingToSpawn)
            {
                waitingToSpawn = true;
                spawnTimer = SpawnDelay;
            }

            spawnTimer -= deltaTime;
            if (spawnTimer <= 0f) SpawnCherry();
            return;
        }

        float distance = Vector3.Distance(startPosition, endPosition);
        travelProgress += deltaTime * moveSpeed / distance;
        activeCherry.transform.position = Vector3.Lerp(startPosition, endPosition, travelProgress);
        if (travelProgress >= 1f)
        {
            Destroy(activeCherry);
            activeCherry = null;
            waitingToSpawn = true;
            spawnTimer = SpawnDelay;
        }
    }

    private void SpawnCherry()
    {
        Vector3 center = levelBounds.center;
        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
        startPosition = center + direction * (DistanceToEdge(levelBounds, center, direction) + outsidePadding);
        if (hasPreviousSpawn && Vector3.Distance(startPosition, previousSpawnPosition) < 0.5f)
        {
            // Enforce a different start even if random selection repeats.
            direction = new Vector3(-direction.y, direction.x, 0f);
            startPosition = center + direction * (DistanceToEdge(levelBounds, center, direction) + outsidePadding);
        }
        previousSpawnPosition = startPosition;
        hasPreviousSpawn = true;

        Vector3 exitDirection = -direction;
        float exitDistance = DistanceToEdge(levelBounds, center, exitDirection);
        // The camera may show extra space around the maze. Continue beyond both
        // its view and the maze before destroying the masked, hidden cherry.
        float depth = Vector3.Dot(center - levelCamera.transform.position, levelCamera.transform.forward);
        for (int x = 0; x <= 1; x++)
            for (int y = 0; y <= 1; y++)
                exitDistance = Mathf.Max(exitDistance, Vector3.Distance(center,
                    levelCamera.ViewportToWorldPoint(new Vector3(x, y, depth))));

        endPosition = center + exitDirection * (exitDistance + outsidePadding);
        activeCherry = Instantiate(cherryPrefab, startPosition, Quaternion.identity);
        activeCherry.name = "Bonus Cherry";
        foreach (SpriteRenderer sprite in activeCherry.GetComponentsInChildren<SpriteRenderer>())
        {
            sprite.sortingLayerID = sortingLayer;
            sprite.sortingOrder = CherrySortingOrder;
            sprite.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        }
        travelProgress = 0f;
        waitingToSpawn = false;
    }

    private bool TryGetLevelBounds(out Bounds result)
    {
        // Ignore unused padding left in Tilemap.cellBounds after editing tiles.
        bool foundTile = false;
        result = default;
        foreach (Vector3Int cell in levelTilemap.cellBounds.allPositionsWithin)
        {
            if (!levelTilemap.HasTile(cell)) continue;
            Vector3 corner = levelTilemap.CellToWorld(cell);
            Vector3 oppositeCorner = levelTilemap.CellToWorld(cell + new Vector3Int(1, 1, 0));
            if (!foundTile) result = new Bounds(corner, Vector3.zero);
            result.Encapsulate(corner);
            result.Encapsulate(oppositeCorner);
            foundTile = true;
        }
        return foundTile;
    }

    private static float DistanceToEdge(Bounds bounds, Vector3 center, Vector3 direction)
    {
        float x = Mathf.Abs(direction.x) < 0.00001f ? float.PositiveInfinity
            : ((direction.x > 0f ? bounds.max.x : bounds.min.x) - center.x) / direction.x;
        float y = Mathf.Abs(direction.y) < 0.00001f ? float.PositiveInfinity
            : ((direction.y > 0f ? bounds.max.y : bounds.min.y) - center.y) / direction.y;
        return Mathf.Min(x, y);
    }

    private void OnDestroy()
    {
        if (activeCherry != null) Destroy(activeCherry);
        if (levelMask != null) Destroy(levelMask.gameObject);
    }
}
