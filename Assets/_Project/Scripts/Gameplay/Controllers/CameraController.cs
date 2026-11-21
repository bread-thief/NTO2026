using Unity.Cinemachine;
using UnityEngine;

namespace _Project.Scripts.Gameplay.Controllers
{
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera[] _cameras;
        [SerializeField] private Changes[] _changes;
        public bool CanMove { get; private set; } = true;

        public void ChangeCamera(int id)
        {
            if (CanMove)
            {
                foreach (CinemachineCamera camera in _cameras)
                {
                    camera.Priority = 0;
                }

                _changes[id].Camera.Priority = 1;

                foreach (GameObject obj in _changes[id].ToDisable)
                {
                    obj.SetActive(false);
                }

                foreach (GameObject obj in _changes[id].ToEnable)
                {
                    obj.SetActive(true);
                }
            }
        }

        public void SetCanMove(bool value)
        {
            CanMove = value;
        }

        [System.Serializable]
        public class Changes
        {
            [field: SerializeField] public CinemachineCamera Camera { get; private set; }
            [field: SerializeField] public GameObject[] ToDisable { get; private set; }
            [field: SerializeField] public GameObject[] ToEnable { get; private set; }
        }
    }
}
