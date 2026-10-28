using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

public sealed class PacStudentController : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float moveSpeed = 5f; // Tiles per second.
    [SerializeField] private Tilemap levelTilemap;
    [SerializeField] private TileBase emptyTile;
    [SerializeField] private TileBase ghostWallTile;
    [SerializeField] private Sprite rightFacingSprite;

    private Key lastInput = Key.None;
    private Key currentInput = Key.None;
    private LevelGenerator levelGenerator;
    private Vector2Int gridPosition;
    private Vector2Int targetGridPosition;
    private Vector3 lerpStart;
    private Vector3 lerpTarget;
    private float lerpProgress;
    private bool isLerping;

    private void Start()
    {
        if (levelTilemap == null)
            levelTilemap = FindFirstObjectByType<Tilemap>();

        if (levelTilemap == null || emptyTile == null || ghostWallTile == null)
        {
            Debug.LogError("PacStudent needs a level Tilemap, empty tile and ghost wall tile.", this);
            enabled = false;
            return;
        }

        levelGenerator = levelTilemap.GetComponent<LevelGenerator>();
        if (levelGenerator == null)
            levelGenerator = levelTilemap.gameObject.AddComponent<LevelGenerator>();
        levelGenerator.Initialize(levelTilemap, emptyTile, ghostWallTile);

        gridPosition = levelGenerator.WorldToGrid(transform.position);
        if (!levelGenerator.IsWalkable(gridPosition))
        {
            Debug.LogError("PacStudent must start on a walkable grid position.", this);
            enabled = false;
            return;
        }

        transform.position = GridToWorld(gridPosition);
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && rightFacingSprite != null)
        {
            spriteRenderer.sprite = rightFacingSprite;
            spriteRenderer.flipX = false;
            spriteRenderer.flipY = false;
        }
        transform.rotation = Quaternion.identity;
        // Both inputs remain None: facing right does not start movement.
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.wKey.wasPressedThisFrame) lastInput = Key.W;
            if (keyboard.aKey.wasPressedThisFrame) lastInput = Key.A;
            if (keyboard.sKey.wasPressedThisFrame) lastInput = Key.S;
            if (keyboard.dKey.wasPressedThisFrame) lastInput = Key.D;
        }

        AdvanceMovement(Time.deltaTime);
    }

    private void AdvanceMovement(float deltaTime)
    {
        if (moveSpeed <= 0f)
            return;

        // Carry unused frame time into the next tile, including on slow frames.
        while (deltaTime > 0f)
        {
            if (!isLerping && !TryStartMove(lastInput) && !TryStartMove(currentInput))
                return;

            float timeToTarget = (1f - lerpProgress) / moveSpeed;
            if (deltaTime < timeToTarget)
            {
                lerpProgress += deltaTime * moveSpeed;
                transform.position = Vector3.Lerp(lerpStart, lerpTarget, lerpProgress);
                return;
            }

            transform.position = lerpTarget;
            gridPosition = targetGridPosition;
            isLerping = false;
            deltaTime -= timeToTarget;
        }
    }

    private bool TryStartMove(Key input)
    {
        Vector2Int direction;
        switch (input)
        {
            case Key.W: direction = Vector2Int.down; break;
            case Key.A: direction = Vector2Int.left; break;
            case Key.S: direction = Vector2Int.up; break;
            case Key.D: direction = Vector2Int.right; break;
            default: return false;
        }

        Vector2Int next = gridPosition + direction;
        if (!levelGenerator.IsWalkable(next))
            return false;

        currentInput = input;
        targetGridPosition = next;
        lerpStart = GridToWorld(gridPosition);
        lerpTarget = GridToWorld(next);
        lerpProgress = 0f;
        isLerping = true;

        float angle = input == Key.W ? 90f : input == Key.A ? 180f : input == Key.S ? -90f : 0f;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
        return true;
    }

    private Vector3 GridToWorld(Vector2Int position)
    {
        Vector3 world = levelGenerator.GridToWorld(position);
        world.z = transform.position.z;
        return world;
    }
}
