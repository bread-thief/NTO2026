using System.Collections;
using _Project.Scripts.Gameplay.Obstacles;
using _Project.Scripts.Gameplay.Views;
using Unity.Cinemachine;
using UnityEngine;

namespace _Project.Scripts.Gameplay.Controllers
{
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private CinemachineBrain _cinemachineBrain;
        [SerializeField] private CinemachineCamera[] _cameras;
        [SerializeField] private Changes[] _changes;
        [SerializeField] private RoutePlannerController _routePlannerController;
        [SerializeField] private DeliveryShiftController _deliveryShiftController;
        [SerializeField] private FadeController _fadeController;
        [SerializeField] private RoadObstacleController _obstacleController;
        [SerializeField] private ObstacleNotificationView _notificationView;
        [SerializeField] private GameObject _zonesPanel;
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

        public void ResetCamera(int i = 0) => ChangeCamera(2);

        public void ResetCameraWithAnimation(int i)
        {
            StartCoroutine(ResetCameraWithAnimationRoutine());
        }

        public IEnumerator ResetCameraWithAnimationRoutine()
        {
            yield return _fadeController.FadeRoutine(1f, 1f);
            ResetCamera();
            yield return new WaitForSecondsRealtime(1.5f);
            yield return _fadeController.FadeRoutine(0f, 1f);
            _notificationView.ShowMessage(_obstacleController.ActiveObstacles[0].BuildDisplayMessage());
            
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
