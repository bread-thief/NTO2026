using System;
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
        
        private void OnEnable()
        {
            _routePlannerController.OnRouteEndpointsConfigured += UpdateInfo;
        }

        private void OnDisable()
        {
            _routePlannerController.OnRouteEndpointsConfigured -= UpdateInfo;
        }

        private void UpdateInfo(RouteVehicleDriver _routeVehicleDriver)
        {
            string routeDescription = _cityStreetNamer.FormatRouteDescription(_routePlannerController.PointANode
                .RoadNavMeshPosition, _routePlannerController.PointBNode
                .RoadNavMeshPosition);
            _textMeshProUGUI.text = $"Заказ № {_routePlannerController.CompletedTrips + 1}\nВсего: {_routePlannerController.MaxTrips}\n\n{routeDescription}";
        }
    }
}