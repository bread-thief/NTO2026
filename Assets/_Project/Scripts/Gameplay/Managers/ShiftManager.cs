using _Project.Scripts.Gameplay.Configs;
using _Project.Scripts.Gameplay.Controllers;
using _Project.Scripts.Gameplay.Spawners;
using UnityEngine;

namespace _Project.Scripts.Gameplay.Bootstrap
{
	public class ShiftManager : MonoBehaviour
	{
		[SerializeField] private ShiftConfig _shiftConfig;
		[SerializeField] private TimeController _timeController;
		[SerializeField] private RoutePlannerController _routePlannerController;
		[SerializeField] private DeliveryRouteSpawner _deliveryRouteSpawner;

		private void Awake()
		{
			if (_timeController == null)
			{
				_timeController = FindAnyObjectByType<TimeController>();
			}

			if (_routePlannerController == null)
			{
				_routePlannerController = FindAnyObjectByType<RoutePlannerController>();
			}

			if (_deliveryRouteSpawner == null)
			{
				_deliveryRouteSpawner = FindAnyObjectByType<DeliveryRouteSpawner>();
			}

			ApplyConfig();
		}

		private void ApplyConfig()
		{
			if (_shiftConfig == null)
			{
				return;
			}

			if (_timeController != null)
			{
				_timeController.ApplyShiftConfig(_shiftConfig);
			}

			if (_routePlannerController != null)
			{
				_routePlannerController.ApplyShiftConfig(_shiftConfig);
			}

			if (_deliveryRouteSpawner != null)
			{
				_deliveryRouteSpawner.ApplyDistanceConfig(
					_shiftConfig.DistanceInCells);
			}
		}
	}
}