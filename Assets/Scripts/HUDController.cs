using UnityEngine;
using UnityEngine.SceneManagement;

public class HUDController : MonoBehaviour
{
    public void ExitToStartScene()
    {
        SceneManager.LoadScene("StartScene");
    }
}
