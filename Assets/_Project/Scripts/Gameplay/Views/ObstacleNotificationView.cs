using _Project.Scripts.Gameplay.Controllers;
using _Project.Scripts.Gameplay.Obstacles;
using TMPro;
using UnityEngine;
using Time = _Project.Scripts.Gameplay.Controllers.Time;

namespace _Project.Scripts.Gameplay.Views
{
    public class ObstacleNotificationView : MonoBehaviour
    {
        [SerializeField] private TimeController _timeController;
        [SerializeField] private RoadObstacleController _obstacleController;
        [SerializeField] private TMP_Text _timeText;
        [SerializeField] private Radio.Radio _radio;

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

            ShowMessage(obstacle.BuildDisplayMessage());
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
            if (obstacle == null)
            {
                return;
            }

            ShowMessage($"Машина попала под преграду на {obstacle.BlockedStreetAddress}. Потеряно {penaltyMinutes} минут.");
        }

        private void ShowMessage(string message)
        {
            if (_radio == null)
            {
                return;
            }

            _radio.SendMessageByRadio(message);
        }
    }
}