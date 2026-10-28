using UnityEngine;

public enum GhostState { Normal, Scared, Recovering, Dead }

[RequireComponent(typeof(Animator), typeof(CircleCollider2D))]
public sealed class GhostController : MonoBehaviour
{
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private Color normalColor;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;

    public GhostState State { get; private set; } = GhostState.Normal;
    public bool IsPaused { get; private set; }

    private void Awake()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        normalColor = spriteRenderer.color;
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
        GetComponent<CircleCollider2D>().isTrigger = true;
    }

    private void Start()
    {
        SetState(State);
    }

    public void SetState(GhostState state)
    {
        State = state;
        spriteRenderer.color = state == GhostState.Normal ? normalColor : Color.white;
        animator.enabled = true;
        animator.speed = IsPaused ? 0f : 1f;
        animator.Play(state.ToString(), 0, 0f);
    }

    public void SetPaused(bool paused)
    {
        IsPaused = paused;
        animator.speed = paused ? 0f : 1f;
    }

    public void ResetToSpawn()
    {
        transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        SetPaused(false);
        SetState(GhostState.Normal);
    }
}
