using _Project.Scripts.Gameplay.Controllers;
using _Project.Scripts.Gameplay.Obstacles;
using TMPro;
using UnityEngine;
using Time = _Project.Scripts.Gameplay.Controllers.Time;
using UnityTime = UnityEngine.Time;

namespace _Project.Scripts.Gameplay.Views
{
    public class ObstacleNotificationView : MonoBehaviour
    {
        [SerializeField] private TimeController _timeController;
        [SerializeField] private RoadObstacleController _obstacleController;
        //[SerializeField] private TMP_Text _messageText;
        [SerializeField] private TMP_Text _timeText;
        [SerializeField] private float _messageDuration = 5f;
        [SerializeField] private Radio.Radio _radio;

        private float _messageTimer;

        private void Awake()
        {
            if (_timeController == null)
            {
                _timeController = FindAnyObjectByType<TimeController>();
            }

            if (_obstacleController == null)
            {
                _obstacleController = FindAnyObjectByType<RoadObstacleController>();
            }
        }

        private void OnEnable()
        {
            if (_timeController != null)
            {
                _timeController.OnTimeChanged += HandleTimeChanged;
            }

            if (_obstacleController != null)
            {
                _obstacleController.OnObstacleSpawned += HandleObstacleSpawned;
                _obstacleController.OnObstacleRemoved += HandleObstacleRemoved;
                _obstacleController.OnPenaltyApplied += HandlePenaltyApplied;
            }
        }

        private void OnDisable()
        {
            if (_timeController != null)
            {
                _timeController.OnTimeChanged -= HandleTimeChanged;
            }

            if (_obstacleController != null)
            {
                _obstacleController.OnObstacleSpawned -= HandleObstacleSpawned;
                _obstacleController.OnObstacleRemoved -= HandleObstacleRemoved;
                _obstacleController.OnPenaltyApplied -= HandlePenaltyApplied;
            }
        }

        private void Update()
        {
            if (_messageTimer <= 0f)
            {
                return;
            }

            _messageTimer -= UnityTime.deltaTime;

            //if (_messageTimer <= 0f && _messageText != null)
            //{
            //    _messageText.text = string.Empty;
            //}
        }

        private void HandleTimeChanged(Time currentTime)
        {
            if (_timeText != null)
            {
                _timeText.text = currentTime.ToString();
            }
        }

        private void HandleObstacleSpawned(RoadObstacle obstacle)
        {
            if (obstacle == null)
            {
                return;
            }

            ShowMessage($"{obstacle.BuildDisplayMessage()}. Будет убрано в {obstacle.RemovalTime}");
        }

        private void HandleObstacleRemoved(RoadObstacle obstacle)
        {
            if (obstacle == null)
            {
                return;
            }

            ShowMessage($"Проезд по {obstacle.BlockedStreetAddress} восстановлен");
        }

        private void HandlePenaltyApplied(RoadObstacle obstacle, int penaltyMinutes)
        {
            ShowMessage($"Авария! Потеряно {penaltyMinutes} минут");
        }

        public void ShowMessage(string message)
        {
            //if (_messageText == null)
            //{
            //    return;
            //}
            
            _radio.SendMessageByRadio(message);
            
            //_messageText.text = message;
            //_messageTimer = _messageDuration;
            
        }
    }
}