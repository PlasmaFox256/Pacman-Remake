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
    [SerializeField] private AnimationClip deathAnimation;
    [SerializeField] private GameSession gameSession;

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
    private Vector2Int spawnGridPosition;
    private Vector3 spawnScale;
    public bool IsDying { get; private set; }

    private static readonly System.Collections.Generic.IComparer<RaycastHit2D> HitDistanceComparer =
        System.Collections.Generic.Comparer<RaycastHit2D>.Create((a, b) => a.distance.CompareTo(b.distance));

    private void Awake()
    {
        animator = GetComponent<Animator>();
        spawnScale = transform.localScale;
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
        spawnGridPosition = gridPosition;
        if (gameSession == null) gameSession = FindAnyObjectByType<GameSession>();
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
        if (IsDying)
            return;
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
        if (IsDying)
            return;
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

            // Teleport only after reaching the mouth, never lerp across the maze.
            // Begin the inward step before applying any buffered turn.
            if (TryTeleportTunnel(currentInput))
                TryStartMove(currentInput);
        }
    }

    private bool TryStartMove(Key input)
    {
        Vector2Int direction = InputDirection(input);
        if (direction == Vector2Int.zero)
            return false;

        TryTeleportTunnel(input);
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

    private bool TryTeleportTunnel(Key input)
    {
        if (!levelGenerator.TryGetTunnelExit(gridPosition, InputDirection(input), out Vector2Int exit))
            return false;

        gridPosition = exit;
        targetGridPosition = exit;
        transform.position = GridToWorld(exit);
        lerpStart = transform.position;
        lerpTarget = transform.position;
        lerpProgress = 0f;
        isLerping = false;
        wallCollisionPlayed = false;
        Physics2D.SyncTransforms();
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
        if (delta.sqrMagnitude > 0f && !SweepInteractions(delta.normalized, delta.magnitude))
            return false;
        transform.position = position;
        return true;
    }

    private bool SweepInteractions(Vector2 direction, float distance)
    {
        // Trigger callbacks run on physics ticks. Also sweep each lerp segment
        // so a slow rendered frame cannot skip a pellet or a ghost.
        Physics2D.SyncTransforms();
        int count = playerCollider.Cast(direction, ContactFilter2D.noFilter, wallHits, distance);
        System.Array.Sort(wallHits, 0, count, HitDistanceComparer);
        for (int i = 0; i < count; i++)
        {
            Collider2D other = wallHits[i].collider;
            if (other == null || !other.gameObject.activeInHierarchy)
                continue;
            GhostController ghost = other.GetComponentInParent<GhostController>();
            if (ghost != null && ghost.State == GhostState.Normal)
            {
                // Keep the death effect at the contact, rather than the last cell.
                Vector2 contactPosition = wallHits[i].centroid;
                transform.position = new Vector3(contactPosition.x, contactPosition.y, transform.position.z);
            }
            HandleInteraction(other);
            if (IsDying)
                return false;
        }
        return true;
    }

    private void HandleInteraction(Collider2D other)
    {
        if (gameSession == null || IsDying)
            return;
        Collectible collectible = other.GetComponentInParent<Collectible>();
        if (collectible != null) collectible.TryCollect(gameSession);
        GhostController ghost = other.GetComponentInParent<GhostController>();
        if (ghost != null) gameSession.HandleGhostCollision(ghost);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!enabled || IsDying)
            return;
        if (enabled && levelGenerator != null && levelGenerator.IsWallCollider(other))
            StopAtWall(other.ClosestPoint(transform.position));
        else
            HandleInteraction(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // A ghost that becomes Normal while overlapping must become dangerous.
        if (enabled && !IsDying && other.GetComponentInParent<GhostController>() != null)
            HandleInteraction(other);
    }

    public void BeginDeath(float duration)
    {
        IsDying = true;
        isLerping = false;
        lastInput = currentInput = Key.None;
        SetMoving(false);
        playerCollider.enabled = false;
        if (collisionAudio != null) collisionAudio.Stop();
        SpawnDustBurst(transform.position, 24, "Death Dust", true);
        if (animator != null && deathAnimation != null)
        {
            animator.speed = deathAnimation.length / Mathf.Max(0.01f, duration);
            animator.Play("Death", 0, 0f);
            animator.Update(0f);
        }
    }

    public void Respawn()
    {
        gridPosition = targetGridPosition = spawnGridPosition;
        transform.position = GridToWorld(gridPosition);
        transform.localScale = spawnScale;
        transform.rotation = Quaternion.identity;
        lerpStart = lerpTarget = transform.position;
        lerpProgress = 0f;
        isLerping = false;
        lastInput = currentInput = Key.None;
        wallCollisionPlayed = false;
        if (animator != null)
        {
            animator.speed = 1f;
            animator.Play("WalkRight", 0, 0f);
            animator.Update(0f);
            animator.speed = 0f;
        }
        // Death animates the scale; explicitly restore it after sampling the animator.
        transform.localScale = spawnScale;
        IsDying = false;
        playerCollider.enabled = true;
        Physics2D.SyncTransforms();
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
        Vector3 position = new Vector3(contactPoint.x, contactPoint.y, transform.position.z);
        SpawnDustBurst(position, 8, "Wall Collision Dust");
    }

    private void SpawnDustBurst(Vector3 position, int count, string effectName, bool radial = false)
    {
        if (dustParticles == null)
            return;
        // Keep each burst at its contact/death spot after the player moves away.
        ParticleSystem puff = Instantiate(dustParticles, position, Quaternion.identity);
        puff.name = effectName;
        puff.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = puff.main;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;
        main.maxParticles = Mathf.Max(main.maxParticles, count);
        if (radial)
        {
            ParticleSystem.ShapeModule shape = puff.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.35f;
            main.startSpeed = 0.8f;
        }
        ParticleSystem.EmissionModule emission = puff.emission;
        emission.enabled = false;
        puff.Play();
        puff.Emit(count);
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
