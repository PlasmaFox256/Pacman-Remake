using UnityEngine;
using UnityEngine.SceneManagement;

public class StartMenuController : MonoBehaviour
{
    public void LoadLevel1()
    {
        SceneManager.LoadScene("GameScene");
    }
}
