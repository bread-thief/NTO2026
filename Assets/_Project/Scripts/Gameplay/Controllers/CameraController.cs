using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

namespace _Project.Scripts.Gameplay.Controllers
{
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private RoutePlannerController _routePlannerController;
        [SerializeField] private FadeController _fadeController;
        [SerializeField] private CinemachineBrain _cinemachineBrain;
        [SerializeField] private CinemachineCamera[] _cameras;
        [SerializeField] private Changes[] _changes;
        [SerializeField] private GameObject _zonesPanel;
        [SerializeField] private int _cameraIDAfterReset;
        public bool CanMove { get; private set; } = true;

        private void Update()
        {
            if (_routePlannerController != null && _routePlannerController.IsDragging || _routePlannerController.IsVehicleDriving || _cinemachineBrain.IsBlending)
            {
                CanMove = false;
                _zonesPanel.SetActive(false);
            }
            else
            {
                CanMove = true;
                _zonesPanel.SetActive(true);
            }
        }

        public void ChangeCamera(int id)
        {
            if (CanMove && !_cinemachineBrain.IsBlending)
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
        
        public void ResetCamera() => ChangeCamera(_cameraIDAfterReset);

        public void ResetCameraWithAnimation()
        {
            StartCoroutine(ResetCameraWithAnimationRoutine());
        }

        public IEnumerator ResetCameraWithAnimationRoutine()
        {
            yield return _fadeController.FadeRoutine(1f, 1f);
            ResetCamera();
            yield return new WaitForSecondsRealtime(1.5f);
            yield return _fadeController.FadeRoutine(0f, 1f);
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
