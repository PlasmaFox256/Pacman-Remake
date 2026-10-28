using UnityEngine;

public class MusicManager : MonoBehaviour
{
    public AudioSource audioSource;
    public AudioClip introMusic;
    public AudioClip battleMusic;
    [SerializeField] private AudioClip frenzyMusic;
    private bool isFrenzy;

    void Start()
    {
        audioSource.clip = introMusic;
        audioSource.loop = false;
        audioSource.Play();

        Invoke(nameof(StartBattleMusic), 3f);
    }

    void StartBattleMusic()
    {
        if (isFrenzy) return;
        audioSource.clip = battleMusic;
        audioSource.loop = true;
        audioSource.Play();
    }

    public void SetFrenzy(bool frenzy)
    {
        CancelInvoke(nameof(StartBattleMusic));
        isFrenzy = frenzy;
        AudioClip clip = frenzy ? frenzyMusic : battleMusic;
        if (audioSource.clip == clip && audioSource.isPlaying)
            return;
        audioSource.clip = clip;
        audioSource.loop = true;
        audioSource.Play();
    }

    public void PauseForDeath()
    {
        CancelInvoke(nameof(StartBattleMusic));
        audioSource.Stop();
    }
}
