using System;
using UnityEngine;
using Time = _Project.Scripts.Gameplay.Controllers.Time;

namespace _Project.Scripts.Gameplay.Obstacles
{
    [RequireComponent(typeof(Collider))]
    public class RoadObstacle : MonoBehaviour
    {
        public enum ObstaclePlacementType
        {
            MiddleOfStreet,
            Intersection,
            StreetEnd,
            Unknown
        }

        [SerializeField] private Time _removalTime;
        [SerializeField] private int _penaltyMinutes = 30;
        [SerializeField] private bool _isActive = true;

        public event Action<RoadObstacle, int> OnPlayerHit;
        public event Action<RoadObstacle> OnRemoved;

        public Time RemovalTime => _removalTime;
        public bool IsActive => _isActive;
        public Vector3 WorldPosition => transform.position;

        public string BlockedStreetAddress { get; private set; } = string.Empty;
        public string ReferenceLandmark { get; private set; } = string.Empty;
        public ObstaclePlacementType PlacementType { get; private set; } = ObstaclePlacementType.Unknown;

        private Collider _collider;

        public void Initialize(string blockedStreetAddress, string referenceLandmark, ObstaclePlacementType placementType, Time removalTime, int penaltyMinutes)
        {
            BlockedStreetAddress = blockedStreetAddress;
            ReferenceLandmark = referenceLandmark;
            PlacementType = placementType;
            _removalTime = removalTime;
            _penaltyMinutes = penaltyMinutes;
            _isActive = true;

            if (_collider == null)
            {
                _collider = GetComponent<Collider>();
            }

            if (_collider != null)
            {
                _collider.isTrigger = true;
            }

            gameObject.SetActive(true);
        }

        public bool ShouldBeRemoved(Time currentTime)
        {
            if (_removalTime == null)
            {
                return false;
            }

            return !currentTime.IsBefore(_removalTime);
        }

        public void Remove()
        {
            if (!_isActive)
            {
                return;
            }

            _isActive = false;
            gameObject.SetActive(false);
            OnRemoved?.Invoke(this);
        }

        public string BuildDisplayMessage()
        {
            switch (PlacementType)
            {
                case ObstaclePlacementType.Intersection:
                    return $"Перекрёсток между {BlockedStreetAddress} и {ReferenceLandmark} перекрыт";
                case ObstaclePlacementType.MiddleOfStreet:
                    return $"Середина {BlockedStreetAddress} перекрыта (ориентир: {ReferenceLandmark})";
                case ObstaclePlacementType.StreetEnd:
                    return $"Конец {BlockedStreetAddress} перекрыт (ориентир: {ReferenceLandmark})";
                default:
                    return $"Участок {BlockedStreetAddress} перекрыт";
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_isActive)
            {
                return;
            }

            if (other.GetComponentInParent<RouteVehicleDriverTag>() == null)
            {
                return;
            }

            OnPlayerHit?.Invoke(this, _penaltyMinutes);
        }
    }
}