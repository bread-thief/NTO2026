using UnityEngine;

namespace _Project.Scripts.Meta.UI
{
    public class PauseLogic : MonoBehaviour
    {
        public void SetPause(bool value)
        {
            Time.timeScale = value ? 0f : 1f;
        }
    }
}