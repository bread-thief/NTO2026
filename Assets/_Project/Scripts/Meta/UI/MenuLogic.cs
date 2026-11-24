using UnityEngine;
using UnityEngine.SceneManagement;

namespace _Project.Scripts.Meta.UI
{
    public class MenuLogic : MonoBehaviour
    {
        public void LoadScene(string sceneName) => SceneManager.LoadScene(sceneName);
        public void LoadSceneByID(int sceneID) => SceneManager.LoadScene(sceneID);
        public void Quit() => Application.Quit();
        public void SetPause(bool value)
        {
            Time.timeScale = value ? 0f : 1f;
        }
    }
}