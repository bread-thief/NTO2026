using _Project.Scripts.Gameplay.Controllers;
using _Project.Scripts.Gameplay.Drivers;
using _Project.Scripts.Gameplay.Namers;
using TMPro;
using UnityEngine;

namespace _Project.Scripts.Meta.UI
{
    public class TabletUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _textMeshProUGUI;
        [SerializeField] private RoutePlannerController _routePlannerController;
        [SerializeField] private CityStreetNamer _cityStreetNamer;
        [SerializeField] private TimeController _timeController;

        private void Awake()
        {
            if (_timeController == null)
            {
                _timeController = FindAnyObjectByType<TimeController>();
            }
        }

        private void OnEnable()
        {
            if (_routePlannerController != null)
            {
                _routePlannerController.OnRouteEndpointsConfigured += UpdateInfo;
                _routePlannerController.OnAllTripsCompleted += ClearInfo;
            }

            if (_timeController != null)
            {
                _timeController.OnShiftEnded += ClearInfo;
            }
        }

        private void OnDisable()
        {
            if (_routePlannerController != null)
            {
                _routePlannerController.OnRouteEndpointsConfigured -= UpdateInfo;
                _routePlannerController.OnAllTripsCompleted -= ClearInfo;
            }

            if (_timeController != null)
            {
                _timeController.OnShiftEnded -= ClearInfo;
            }
        }

        private void UpdateInfo(RouteVehicleDriver _routeVehicleDriver)
        {
            if (_textMeshProUGUI == null || _cityStreetNamer == null || _routePlannerController == null)
            {
                return;
            }

            string routeDescription = _cityStreetNamer.FormatRouteDescription(
                _routePlannerController.PointANode.RoadNavMeshPosition,
                _routePlannerController.PointBNode.RoadNavMeshPosition);

            _textMeshProUGUI.text = $"Заказ № {_routePlannerController.CompletedTrips + 1}\nВсего: {_routePlannerController.MaxTrips}\n\n{routeDescription}";
        }

        private void ClearInfo()
        {
            if (_textMeshProUGUI != null)
            {
                _textMeshProUGUI.text = string.Empty;
            }
        }
    }
}