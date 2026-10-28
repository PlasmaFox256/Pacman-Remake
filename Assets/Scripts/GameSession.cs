using System.Collections;
using UnityEngine;

public sealed class GameSession : MonoBehaviour
{
    [SerializeField] private PacStudentController player;
    [SerializeField] private HUDController hud;
    [SerializeField] private MusicManager music;
    [SerializeField] private GhostController[] ghosts;
    [SerializeField] private AudioSource effectsAudio;
    [SerializeField] private AudioSource cherryAudio;
    [SerializeField] private AudioClip pelletClip;
    [SerializeField] private AudioClip powerPillClip;
    [SerializeField] private AudioClip cherryClip;
    [SerializeField, Range(1f, 2f)] private float cherryPitch = 1.3f;
    [SerializeField] private AudioClip deathClip;
    [SerializeField, Min(1)] private int startingLives = 3;

    private const float PowerDuration = 10f;
    private const float RecoveryDuration = 3f;
    public int Score { get; private set; }
    public int Lives { get; private set; }
    public float PowerTimeRemaining { get; private set; }
    public bool IsDying { get; private set; }
    public bool CanInteract => !IsDying;

    private void Start()
    {
        Lives = startingLives;
        hud.SetScore(Score);
        hud.SetLives(Lives);
        hud.SetGhostTimer(0f);
    }

    private void Update()
    {
        if (!IsDying)
            TickPowerTimer(Time.deltaTime);
    }

    public void Collect(CollectibleKind kind)
    {
        if (!CanInteract)
            return;

        Score += kind == CollectibleKind.Pellet ? 10 : kind == CollectibleKind.Cherry ? 100 : 50;
        hud.SetScore(Score);
        if (kind == CollectibleKind.Pellet && pelletClip != null)
        {
            effectsAudio.pitch = 1f;
            effectsAudio.PlayOneShot(pelletClip);
        }
        else if (kind == CollectibleKind.Cherry && cherryClip != null)
        {
            cherryAudio.pitch = cherryPitch;
            cherryAudio.PlayOneShot(cherryClip);
        }
        else if (kind == CollectibleKind.PowerPill)
        {
            if (powerPillClip != null)
            {
                effectsAudio.pitch = 1f;
                effectsAudio.PlayOneShot(powerPillClip);
            }
            PowerTimeRemaining = PowerDuration;
            SetLivingGhostsState(GhostState.Scared);
            hud.SetGhostTimer(PowerTimeRemaining);
            music.SetFrenzy(true);
        }
    }

    private void TickPowerTimer(float deltaTime)
    {
        if (PowerTimeRemaining <= 0f)
            return;

        float previous = PowerTimeRemaining;
        PowerTimeRemaining = Mathf.Max(0f, PowerTimeRemaining - deltaTime);
        if (PowerTimeRemaining <= 0f)
        {
            SetLivingGhostsState(GhostState.Normal);
            music.SetFrenzy(false);
        }
        else if (previous > RecoveryDuration && PowerTimeRemaining <= RecoveryDuration)
            SetLivingGhostsState(GhostState.Recovering);

        hud.SetGhostTimer(PowerTimeRemaining);
    }

    private void SetLivingGhostsState(GhostState state)
    {
        foreach (GhostController ghost in ghosts)
            if (ghost != null && ghost.State != GhostState.Dead)
                ghost.SetState(state);
    }

    public void HandleGhostCollision(GhostController ghost)
    {
        if (CanInteract && ghost != null && ghost.State == GhostState.Normal)
            StartCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        IsDying = true;
        Lives = Mathf.Max(0, Lives - 1);
        hud.SetLives(Lives);
        PowerTimeRemaining = 0f;
        hud.SetGhostTimer(0f);
        foreach (GhostController ghost in ghosts)
            if (ghost != null) ghost.SetPaused(true);

        music.PauseForDeath();
        float duration = deathClip != null ? deathClip.length : 1f;
        player.BeginDeath(duration);
        effectsAudio.Stop();
        cherryAudio.Stop();
        effectsAudio.pitch = 1f;
        if (deathClip != null) effectsAudio.PlayOneShot(deathClip);

        yield return new WaitForSeconds(duration);

        foreach (GhostController ghost in ghosts)
            if (ghost != null) ghost.ResetToSpawn();
        player.Respawn();
        music.SetFrenzy(false);
        IsDying = false;
    }
}
