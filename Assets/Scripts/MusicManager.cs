using UnityEngine;

public class MusicManager : MonoBehaviour
{
    public AudioSource audioSource;
    public AudioClip introMusic;
    public AudioClip battleMusic;

    void Start()
    {
        audioSource.clip = introMusic;
        audioSource.loop = false;
        audioSource.Play();

        Invoke(nameof(StartBattleMusic), 3f);
    }

    void StartBattleMusic()
    {
        audioSource.clip = battleMusic;
        audioSource.loop = true;
        audioSource.Play();
    }
}