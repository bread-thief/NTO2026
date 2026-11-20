using System;
using System.Collections.Generic;
using _Project.Scripts.Gameplay.Controllers;
using _Project.Scripts.Gameplay.Drivers;
using _Project.Scripts.Gameplay.Generators;
using _Project.Scripts.Gameplay.Obstacles;
using UnityEngine;

namespace _Project.Scripts.Gameplay.Controllers
{
	public class DeliveryShiftController : MonoBehaviour
	{
		private const int MINUTES_PER_DELIVERY = 60;
		private const int PLAYER_MINUTE_TO_GAME_MINUTE = 5;

		[SerializeField] private TimeController _timeController;
		[SerializeField] private RoutePlannerController _routePlanner;
		[SerializeField] private RouteVehicleDriver _vehicleDriver;
		[SerializeField] private RoadObstacleController _obstacleController;
		[SerializeField] private CityGenerator _generator;

		public event Action<int> OnDeliveryCompleted;
		public event Action OnShiftCompleted;
		public event Action OnShiftFailed;

		public int CompletedDeliveries => _routePlanner != null ? _routePlanner.CompletedTrips : 0;
		public bool IsShiftRunning { get; private set; }

		private readonly List<Vector3> _lastCompletedRoute = new List<Vector3>();
		private RouteVehicleDriver _subscribedDriver;

		private void Awake()
		{
			if (_timeController == null)
			{
				_timeController = FindAnyObjectByType<TimeController>();
			}

			if (_routePlanner == null)
			{
				_routePlanner = FindAnyObjectByType<RoutePlannerController>();
			}

			if (_vehicleDriver == null)
			{
				_vehicleDriver = FindAnyObjectByType<RouteVehicleDriver>();
			}

			if (_obstacleController == null)
			{
				_obstacleController = FindAnyObjectByType<RoadObstacleController>();
			}

			if (_generator == null)
			{
				_generator = FindAnyObjectByType<CityGenerator>();
			}
		}

		private void OnEnable()
		{
			if (_routePlanner != null)
			{
				_routePlanner.OnRouteCompleted += HandleRouteCompleted;
				_routePlanner.OnRouteEndpointsConfigured += HandleRouteEndpointsConfigured;
			}

			if (_timeController != null)
			{
				_timeController.OnShiftEnded += HandleShiftEnded;
			}

			TryResubscribeVehicle();
		}

		private void OnDisable()
		{
			if (_routePlanner != null)
			{
				_routePlanner.OnRouteCompleted -= HandleRouteCompleted;
				_routePlanner.OnRouteEndpointsConfigured -= HandleRouteEndpointsConfigured;
			}

			if (_timeController != null)
			{
				_timeController.OnShiftEnded -= HandleShiftEnded;
			}

			UnsubscribeVehicle();
		}

		private void Start()
		{
			IsShiftRunning = true;

			if (_timeController != null)
			{
				_timeController.StartShift();
			}

			TryResubscribeVehicle();
		}

		private void HandleRouteEndpointsConfigured(RouteVehicleDriver driver)
		{
			_vehicleDriver = driver;
			TryResubscribeVehicle();
		}

		private void TryResubscribeVehicle()
		{
			if (_vehicleDriver == null)
			{
				_vehicleDriver = FindAnyObjectByType<RouteVehicleDriver>();
			}

			if (_vehicleDriver == null)
			{
				return;
			}

			if (_subscribedDriver == _vehicleDriver)
			{
				return;
			}

			UnsubscribeVehicle();

			_vehicleDriver.OnDestinationReached += HandleDestinationReached;
			_subscribedDriver = _vehicleDriver;
		}

		private void UnsubscribeVehicle()
		{
			if (_subscribedDriver != null)
			{
				_subscribedDriver.OnDestinationReached -= HandleDestinationReached;
				_subscribedDriver = null;
			}
		}

		private void HandleRouteCompleted(IReadOnlyList<Vector3> route)
		{
			_lastCompletedRoute.Clear();

			if (route == null)
			{
				return;
			}

			for (int index = 0; index < route.Count; index++)
			{
				_lastCompletedRoute.Add(route[index]);
			}
		}

		private void HandleDestinationReached()
		{
			if (_timeController == null || !IsShiftRunning)
			{
				return;
			}

			_timeController.AddMinutes(MINUTES_PER_DELIVERY);
			OnDeliveryCompleted?.Invoke(CompletedDeliveries);

			SpawnObstacleAfterDelivery();
		}

		private void SpawnObstacleAfterDelivery()
		{
			if (_obstacleController == null || _routePlanner == null || _generator == null)
			{
				return;
			}

			if (_lastCompletedRoute.Count < 2)
			{
				return;
			}

			Vector2Int targetCell = ResolveReachedTargetCell();

			_obstacleController.SpawnObstacleOnRoute(_lastCompletedRoute, targetCell);
		}

		private Vector2Int ResolveReachedTargetCell()
		{
			if (_routePlanner == null || _generator == null)
			{
				return Vector2Int.zero;
			}

			DeliveryRouteNode targetNode = _routePlanner.IsCurrentTargetPointB
				? _routePlanner.PointBNode
				: _routePlanner.PointANode;

			if (targetNode == null)
			{
				return Vector2Int.zero;
			}

			Vector3 worldPosition = targetNode.RoadNavMeshPosition;

			return _generator.GetCellCoordinatesFromWorldPosition(worldPosition);
		}

		private void HandleShiftEnded()
		{
			if (!IsShiftRunning)
			{
				return;
			}

			IsShiftRunning = false;

			if (CompletedDeliveries > 0)
			{
				OnShiftCompleted?.Invoke();
			}
			else
			{
				OnShiftFailed?.Invoke();
			}
		}

		public void ApplyPlayerMinutePenalty(int playerMinutes)
		{
			if (_timeController == null)
			{
				return;
			}

			_timeController.AddMinutesForPlayerMinute(playerMinutes, PLAYER_MINUTE_TO_GAME_MINUTE);
		}

		public void CompleteDelivery()
		{
			HandleDestinationReached();
		}
	}
}