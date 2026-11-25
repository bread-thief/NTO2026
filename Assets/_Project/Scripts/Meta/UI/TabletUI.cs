using System;
using _Project.Scripts.Gameplay.Controllers;
using _Project.Scripts.Gameplay.Namers;
using TMPro;
using UnityEngine;

namespace _Project.Scripts.Meta.UI
{
    public class TabletUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _textMeshProUGUI;
        [SerializeField] private DeliveryShiftController _deliveryShiftController;
        [SerializeField] private RoutePlannerController _routePlannerController;
        [SerializeField] private CityStreetNamer _cityStreetNamer;
        
        private void OnEnable()
        {
            _deliveryShiftController.OnDeliveryCompleted += UpdateInfo;
        }

        private void OnDisable()
        {
            _deliveryShiftController.OnDeliveryCompleted -= UpdateInfo;
        }

        private void Start()
        {
            UpdateInfo(0);
        }

        private void UpdateInfo(int i)
        {
            string streetA =
                _cityStreetNamer.GetStreetAddressByWorldPosition(_routePlannerController.PointANode
                    .RoadNavMeshPosition);
            string streetB =
                _cityStreetNamer.GetStreetAddressByWorldPosition(_routePlannerController.PointBNode
                    .RoadNavMeshPosition);
            _textMeshProUGUI.text = $"Заказ № {i + 1}\n\nДоставить груз из: {streetA}\nДоставить груз в: {streetB}";
        }
    }
}