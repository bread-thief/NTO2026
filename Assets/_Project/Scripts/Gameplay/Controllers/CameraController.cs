using Unity.Cinemachine;
using UnityEngine;

namespace _Project.Scripts.Gameplay.Controllers
{
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera[] _cameras;
        [SerializeField] private Changes[] _changes;

        public void ChangeCamera(int id)
        {
            _cameras[id].Priority = 1;
            foreach (CinemachineCamera camera in _cameras)
            {
                if (camera != _cameras[id])
                {
                    camera.Priority = 0;
                }
            }

            foreach (GameObject obj in _changes[id].ToDisable)
            {
                obj.SetActive(false);
            }
            
            foreach (GameObject obj in _changes[id].ToEnable)
            {
                obj.SetActive(true);
            }
        }

        [System.Serializable]
        public class Changes
        {
            [field: SerializeField] public GameObject[] ToDisable { get; private set; }
            [field: SerializeField] public GameObject[] ToEnable { get; private set; }
        }
    }
}
