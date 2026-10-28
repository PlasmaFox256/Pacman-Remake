using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
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
    [SerializeField] private ParticleSystem dustParticles;

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
    private CircleCollider2D playerCollider;
    private readonly RaycastHit2D[] wallHits = new RaycastHit2D[32];

    private void Awake()
    {
        animator = GetComponent<Animator>();
        // Physics only detects contacts. All movement and response use our lerp.
        GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        playerCollider = GetComponent<CircleCollider2D>();
        playerCollider.isTrigger = true;
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
        if (lastInput != input)
            wallCollisionPlayed = false;
        lastInput = input;
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
                    Vector3 wallTarget = GridToWorld(gridPosition + InputDirection(lastInput));
                    Vector2 direction = wallTarget - transform.position;
                    if (TryFindWall(direction.normalized, direction.magnitude, out RaycastHit2D hit))
                        PlayWallCollision(hit.point);
                }
                return;
            }

            if (deltaTime <= 0f)
                return;

            float timeToTarget = (1f - lerpProgress) / moveSpeed;
            if (deltaTime < timeToTarget)
            {
                lerpProgress += deltaTime * moveSpeed;
                MoveToLerpPosition(Vector3.Lerp(lerpStart, lerpTarget, lerpProgress));
                return;
            }

            if (!MoveToLerpPosition(lerpTarget))
                return;
            gridPosition = targetGridPosition;
            isLerping = false;
            deltaTime -= timeToTarget;
        }
    }

    private bool TryStartMove(Key input)
    {
        Vector2Int direction = InputDirection(input);
        if (direction == Vector2Int.zero)
            return false;

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

    private static Vector2Int InputDirection(Key input)
    {
        switch (input)
        {
            case Key.W: return Vector2Int.down;
            case Key.A: return Vector2Int.left;
            case Key.S: return Vector2Int.up;
            case Key.D: return Vector2Int.right;
            default: return Vector2Int.zero;
        }
    }

    private bool TryFindWall(Vector2 direction, float distance, out RaycastHit2D hit)
    {
        // Update transform-driven colliders before querying, even between tiles
        // crossed in the same frame. Sweeping prevents tunnelling on slow frames.
        Physics2D.SyncTransforms();
        ContactFilter2D filter = ContactFilter2D.noFilter;
        int count = playerCollider.Cast(direction, filter, wallHits, distance);
        hit = default;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            if (!levelGenerator.IsWallCollider(wallHits[i].collider))
                continue;
            if (!found || wallHits[i].distance < hit.distance)
            {
                hit = wallHits[i];
                found = true;
            }
        }
        return found;
    }

    private bool MoveToLerpPosition(Vector3 position)
    {
        Vector2 delta = position - transform.position;
        if (delta.sqrMagnitude > 0f && TryFindWall(delta.normalized, delta.magnitude, out RaycastHit2D hit))
        {
            StopAtWall(hit.point);
            return false;
        }
        transform.position = position;
        return true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (enabled && levelGenerator != null && levelGenerator.IsWallCollider(other))
            StopAtWall(other.ClosestPoint(transform.position));
    }

    private void StopAtWall(Vector2 contactPoint)
    {
        transform.position = GridToWorld(gridPosition);
        targetGridPosition = gridPosition;
        lerpProgress = 0f;
        isLerping = false;
        SetMoving(false);
        PlayWallCollision(contactPoint);
    }

    private void PlayWallCollision(Vector2 contactPoint)
    {
        if (wallCollisionPlayed)
            return;
        wallCollisionPlayed = true;
        if (collisionAudio != null && collideClip != null)
            collisionAudio.PlayOneShot(collideClip);
        if (dustParticles == null)
            return;

        // Reuse the existing small dust effect, independently of running dust.
        Vector3 position = new Vector3(contactPoint.x, contactPoint.y, transform.position.z);
        ParticleSystem puff = Instantiate(dustParticles, position, Quaternion.identity);
        puff.name = "Wall Collision Dust";
        puff.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = puff.main;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;
        ParticleSystem.EmissionModule emission = puff.emission;
        emission.enabled = false;
        puff.Play();
        puff.Emit(8);
    }

    private void SetMoving(bool moving)
    {
        if (animator != null)
            animator.speed = moving ? 1f : 0f;

        if (isMoving == moving)
            return;

        isMoving = moving;
        if (dustParticles != null)
        {
            if (moving) dustParticles.Play();
            else dustParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        if (movementAudio != null)
        {
            if (moving && movingClip != null) movementAudio.Play();
            else movementAudio.Stop();
        }
    }

    private void OnDisable()
    {
        SetMoving(false);
        if (dustParticles != null)
            dustParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
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
