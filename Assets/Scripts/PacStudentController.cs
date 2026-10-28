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
    [SerializeField] private AudioSource movementAudio;
    [SerializeField] private AudioSource collisionAudio;
    [SerializeField] private AudioClip movingClip;
    [SerializeField] private AudioClip collideClip;

    private Key lastInput = Key.None;
    private Key currentInput = Key.None;
    private LevelGenerator levelGenerator;
    private Vector2Int gridPosition;
    private Vector2Int targetGridPosition;
    private Vector3 lerpStart;
    private Vector3 lerpTarget;
    private float lerpProgress;
    private bool isLerping;
    private Animator animator;
    private bool isMoving;
    private bool wallCollisionPlayed;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (movementAudio != null)
        {
            movementAudio.playOnAwake = false;
            movementAudio.loop = true;
            movementAudio.clip = movingClip;
        }
        if (collisionAudio != null)
        {
            collisionAudio.playOnAwake = false;
            collisionAudio.loop = false;
            collisionAudio.clip = collideClip;
        }
    }

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
        if (animator != null)
        {
            animator.enabled = true;
            animator.Play("WalkRight", 0, 0f);
            animator.Update(0f);
            animator.speed = 0f;
        }
        // Both inputs remain None: facing right does not start movement.
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.wKey.wasPressedThisFrame) RecordInput(Key.W);
            if (keyboard.aKey.wasPressedThisFrame) RecordInput(Key.A);
            if (keyboard.sKey.wasPressedThisFrame) RecordInput(Key.S);
            if (keyboard.dKey.wasPressedThisFrame) RecordInput(Key.D);
        }

        AdvanceMovement(Time.deltaTime);
    }

    private void RecordInput(Key input)
    {
        lastInput = input;
        wallCollisionPlayed = false;
    }

    private void AdvanceMovement(float deltaTime)
    {
        if (moveSpeed <= 0f)
        {
            SetMoving(false);
            return;
        }

        // Carry unused frame time into the next tile, including on slow frames.
        while (true)
        {
            if (!isLerping && !TryStartMove(lastInput) && !TryStartMove(currentInput))
            {
                SetMoving(false);
                if (lastInput != Key.None && !wallCollisionPlayed)
                {
                    if (collisionAudio != null && collideClip != null)
                        collisionAudio.PlayOneShot(collideClip);
                    wallCollisionPlayed = true;
                }
                return;
            }

            if (deltaTime <= 0f)
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

        bool directionChanged = currentInput != input;
        currentInput = input;
        targetGridPosition = next;
        lerpStart = GridToWorld(gridPosition);
        lerpTarget = GridToWorld(next);
        lerpProgress = 0f;
        isLerping = true;

        if (directionChanged && animator != null)
        {
            string state = input == Key.W ? "WalkUp" : input == Key.A ? "WalkLeft"
                : input == Key.S ? "WalkDown" : "WalkRight";
            animator.Play(state, 0, 0f);
        }
        wallCollisionPlayed = false;
        SetMoving(true);
        return true;
    }

    private void SetMoving(bool moving)
    {
        if (animator != null)
            animator.speed = moving ? 1f : 0f;

        if (isMoving == moving)
            return;

        isMoving = moving;
        if (movementAudio != null)
        {
            if (moving && movingClip != null) movementAudio.Play();
            else movementAudio.Stop();
        }
    }

    private void OnDisable()
    {
        SetMoving(false);
        if (collisionAudio != null)
            collisionAudio.Stop();
    }

    private Vector3 GridToWorld(Vector2Int position)
    {
        Vector3 world = levelGenerator.GridToWorld(position);
        world.z = transform.position.z;
        return world;
    }
}
