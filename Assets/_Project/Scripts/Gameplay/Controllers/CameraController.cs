using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

namespace _Project.Scripts.Gameplay.Controllers
{
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private RoutePlannerController _routePlannerController;
        [SerializeField] private DeliveryShiftController _deliveryShiftController;
        [SerializeField] private FadeController _fadeController;
        [SerializeField] private CinemachineBrain _cinemachineBrain;
        [SerializeField] private Changes[] _changes;
        [SerializeField] private GameObject _zonesPanel;
        [SerializeField] private int _cameraIDAfterReset;
        public bool CanMove { get; private set; } = true;
        
        private void OnEnable()
        {
            _deliveryShiftController.OnDeliveryCompleted += ResetCameraWithAnimation;
        }
        
        private void OnDisable()
        {
            _deliveryShiftController.OnDeliveryCompleted -= ResetCameraWithAnimation;
        }

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
                foreach (Changes changes in _changes)
                {
                    changes.Camera.Priority = 0;
                    changes.CameraButton.interactable = true;
                }

                _changes[id].Camera.Priority = 1;
                _changes[id].CameraButton.interactable = false;
            }
        }
        
        public void ResetCamera() => ChangeCamera(_cameraIDAfterReset);

        public void ResetCameraWithAnimation(int i = 0)
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
            [field: SerializeField] public Button CameraButton { get; private set; }
        }
    }
}
