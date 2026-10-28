using UnityEngine;
using UnityEngine.SceneManagement;

public class HUDController : MonoBehaviour
{
    [SerializeField] private TMPro.TMP_Text scoreText;
    [SerializeField] private TMPro.TMP_Text ghostTimerText;
    [SerializeField] private GameObject[] lifeIndicators;

    public void SetScore(int score)
    {
        scoreText.text = score.ToString("D6");
    }

    public void SetLives(int lives)
    {
        for (int i = 0; i < lifeIndicators.Length; i++)
            lifeIndicators[i].SetActive(i < lives);
    }

    public void SetGhostTimer(float seconds)
    {
        ghostTimerText.gameObject.SetActive(seconds > 0f);
        ghostTimerText.text = Mathf.CeilToInt(seconds).ToString();
    }

    public void ExitToStartScene()
    {
        SceneManager.LoadScene("StartScene");
    }
}
