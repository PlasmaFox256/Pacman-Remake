using UnityEngine;

public enum CollectibleKind { Pellet, Cherry, PowerPill }

[RequireComponent(typeof(CircleCollider2D))]
public sealed class Collectible : MonoBehaviour
{
    [SerializeField] private CollectibleKind kind;
    private bool consumed;

    public CollectibleKind Kind => kind;

    private void Awake()
    {
        GetComponent<CircleCollider2D>().isTrigger = true;
    }

    public void TryCollect(GameSession session)
    {
        if (consumed || session == null || !session.CanInteract)
            return;

        // A sweep and a trigger can hit the same collectible in one frame.
        consumed = true;
        gameObject.SetActive(false);
        session.Collect(kind);
        Destroy(gameObject);
    }
}
