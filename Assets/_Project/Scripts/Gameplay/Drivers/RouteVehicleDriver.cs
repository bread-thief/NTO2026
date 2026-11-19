using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace _Project.Scripts.Gameplay.Drivers
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class RouteVehicleDriver : MonoBehaviour
    {
        private const string ROAD_AREA_NAME = "Road";
        private const int INVALID_AREA_INDEX = -1;
        private const float WAYPOINT_REACH_THRESHOLD = 1.15f;
        private const float FINAL_DESTINATION_THRESHOLD = 0.5f;
        private const float NAVMESH_RECOVERY_RADIUS = 15f;
        private const float DEFAULT_TURN_SPEED = 420f;
        private const float DEFAULT_ACCELERATION = 24f;
        private const float DEFAULT_VEHICLE_SPEED = 8f;
        private const float MIN_MOVEMENT_THRESHOLD = 0.01f;
        private const float STUCK_TIME_THRESHOLD = 1.5f;

        [SerializeField] private NavMeshAgent _vehicleAgent;
        [SerializeField] private float _waypointReachDistance = WAYPOINT_REACH_THRESHOLD;
        [SerializeField] private float _finalDestinationDistance = FINAL_DESTINATION_THRESHOLD;
        [SerializeField] private float _vehicleAngularSpeed = DEFAULT_TURN_SPEED;
        [SerializeField] private float _vehicleAcceleration = DEFAULT_ACCELERATION;
        [SerializeField] private float _vehicleSpeed = DEFAULT_VEHICLE_SPEED;

        public event Action OnDestinationReached;
        public event Action<int> OnWaypointReached;

        public bool IsDriving => _isDriving;
        public int CurrentWaypointIndex => _currentWaypointIndex;
        public int TotalWaypointCount => _activeWaypoints.Count;

        public Vector3 CurrentTargetPosition
        {
            get
            {
                if (_activeWaypoints.Count == 0 || _currentWaypointIndex >= _activeWaypoints.Count)
                {
                    return transform.position;
                }

                return _activeWaypoints[_currentWaypointIndex];
            }
        }

        private readonly List<Vector3> _activeWaypoints = new List<Vector3>();
        private int _currentWaypointIndex;
        private bool _isDriving;
        private float _stuckTimer;
        private Vector3 _lastPosition;

        private void Awake()
        {
            if (_vehicleAgent == null)
            {
                _vehicleAgent = GetComponent<NavMeshAgent>();
            }

            _lastPosition = transform.position;
            ConfigureRoadOnlyAreaMask();
            ConfigureSmoothCornering();
        }

        private void Update()
        {
            if (!_isDriving || _vehicleAgent == null || _activeWaypoints.Count == 0)
            {
                return;
            }

            if (!EnsureAgentIsPlacedOnNavMesh(transform.position))
            {
                return;
            }

            if (_vehicleAgent.pathPending)
            {
                return;
            }

            UpdateStuckDetection();

            bool isLastWaypoint = _currentWaypointIndex >= _activeWaypoints.Count - 1;
            float requiredDistance = isLastWaypoint ? _finalDestinationDistance : _waypointReachDistance;

            Vector3 currentTarget = _activeWaypoints[_currentWaypointIndex];
            Vector3 flatVehiclePosition = new Vector3(transform.position.x, 0f, transform.position.z);
            Vector3 flatTargetPosition = new Vector3(currentTarget.x, 0f, currentTarget.z);
            float horizontalDistance = Vector3.Distance(flatVehiclePosition, flatTargetPosition);

            if (horizontalDistance <= requiredDistance || _vehicleAgent.remainingDistance <= requiredDistance)
            {
                AdvanceToNextWaypoint();
            }
        }

        public void ConfigureRoadOnlyAreaMask()
        {
            if (_vehicleAgent == null)
            {
                _vehicleAgent = GetComponent<NavMeshAgent>();
            }

            if (_vehicleAgent == null)
            {
                return;
            }

            int roadAreaIndex = NavMesh.GetAreaFromName(ROAD_AREA_NAME);

            if (roadAreaIndex != INVALID_AREA_INDEX)
            {
                _vehicleAgent.areaMask = 1 << roadAreaIndex;
            }
            else
            {
                _vehicleAgent.areaMask = NavMesh.AllAreas;
            }
        }

        public void StartFollowingRoute(IReadOnlyList<Vector3> waypoints)
        {
            StartRoute(waypoints);
        }

        public void StartRoute(IReadOnlyList<Vector3> waypoints)
        {
            if (waypoints == null || waypoints.Count < 2)
            {
                return;
            }

            if (_vehicleAgent == null)
            {
                _vehicleAgent = GetComponent<NavMeshAgent>();
            }

            if (_vehicleAgent == null)
            {
                return;
            }

            ConfigureRoadOnlyAreaMask();
            ConfigureSmoothCornering();

            if (!EnsureAgentIsPlacedOnNavMesh(waypoints[0]))
            {
                return;
            }

            _activeWaypoints.Clear();

            for (int index = 0; index < waypoints.Count; index++)
            {
                _activeWaypoints.Add(waypoints[index]);
            }

            _currentWaypointIndex = 1;
            _isDriving = true;
            _stuckTimer = 0f;
            _lastPosition = transform.position;

            _vehicleAgent.speed = _vehicleSpeed;
            _vehicleAgent.isStopped = false;
            _vehicleAgent.SetDestination(_activeWaypoints[_currentWaypointIndex]);
        }

        public void StopAndReset()
        {
            _isDriving = false;
            _activeWaypoints.Clear();
            _currentWaypointIndex = 0;
            _stuckTimer = 0f;

            if (_vehicleAgent != null && _vehicleAgent.isActiveAndEnabled && _vehicleAgent.isOnNavMesh)
            {
                _vehicleAgent.isStopped = true;
                _vehicleAgent.ResetPath();
            }
        }

        public void StopAndReset(Vector3 resetWorldPosition)
        {
            _isDriving = false;
            _activeWaypoints.Clear();
            _currentWaypointIndex = 0;
            _stuckTimer = 0f;

            if (_vehicleAgent == null)
            {
                _vehicleAgent = GetComponent<NavMeshAgent>();
            }

            if (_vehicleAgent != null && EnsureAgentIsPlacedOnNavMesh(resetWorldPosition))
            {
                _vehicleAgent.isStopped = true;
                _vehicleAgent.ResetPath();
                _vehicleAgent.Warp(resetWorldPosition);
            }
            else
            {
                transform.position = resetWorldPosition;
            }

            _lastPosition = resetWorldPosition;
        }

        public void SetSpeed(float speed)
        {
            _vehicleSpeed = Mathf.Max(0.1f, speed);

            if (_vehicleAgent != null)
            {
                _vehicleAgent.speed = _vehicleSpeed;
            }
        }

        private void ConfigureSmoothCornering()
        {
            if (_vehicleAgent == null)
            {
                return;
            }

            _vehicleAgent.autoBraking = false;
            _vehicleAgent.angularSpeed = _vehicleAngularSpeed;
            _vehicleAgent.acceleration = _vehicleAcceleration;
            _vehicleAgent.speed = _vehicleSpeed;
        }

        private void UpdateStuckDetection()
        {
            float movement = Vector3.Distance(transform.position, _lastPosition);
            _lastPosition = transform.position;

            if (movement < MIN_MOVEMENT_THRESHOLD)
            {
                _stuckTimer += Time.deltaTime;

                if (_stuckTimer >= STUCK_TIME_THRESHOLD)
                {
                    _stuckTimer = 0f;
                    RecoverFromStuck();
                }
            }
            else
            {
                _stuckTimer = 0f;
            }
        }

        private void RecoverFromStuck()
        {
            if (_currentWaypointIndex >= _activeWaypoints.Count)
            {
                return;
            }

            if (!EnsureAgentIsPlacedOnNavMesh(transform.position))
            {
                return;
            }

            _vehicleAgent.ResetPath();
            _vehicleAgent.SetDestination(_activeWaypoints[_currentWaypointIndex]);
        }

        private void AdvanceToNextWaypoint()
        {
            int reachedIndex = _currentWaypointIndex;
            _currentWaypointIndex++;

            if (_currentWaypointIndex >= _activeWaypoints.Count)
            {
                _isDriving = false;
                _stuckTimer = 0f;

                if (_vehicleAgent != null && _vehicleAgent.isActiveAndEnabled && _vehicleAgent.isOnNavMesh)
                {
                    _vehicleAgent.autoBraking = true;
                    _vehicleAgent.isStopped = true;
                    _vehicleAgent.ResetPath();
                }

                OnWaypointReached?.Invoke(reachedIndex);
                OnDestinationReached?.Invoke();
                return;
            }

            bool isApproachingFinalPoint = _currentWaypointIndex == _activeWaypoints.Count - 1;
            _vehicleAgent.autoBraking = isApproachingFinalPoint;

            if (EnsureAgentIsPlacedOnNavMesh(transform.position))
            {
                _vehicleAgent.SetDestination(_activeWaypoints[_currentWaypointIndex]);
            }

            OnWaypointReached?.Invoke(reachedIndex);
        }

        private bool EnsureAgentIsPlacedOnNavMesh(Vector3 referencePosition)
        {
            if (_vehicleAgent == null || !_vehicleAgent.isActiveAndEnabled)
            {
                return false;
            }

            if (_vehicleAgent.isOnNavMesh)
            {
                return true;
            }

            int roadAreaIndex = NavMesh.GetAreaFromName(ROAD_AREA_NAME);
            int preferredMask = roadAreaIndex != INVALID_AREA_INDEX ? (1 << roadAreaIndex) : NavMesh.AllAreas;

            if (NavMesh.SamplePosition(referencePosition, out NavMeshHit hitInfo, NAVMESH_RECOVERY_RADIUS, preferredMask))
            {
                _vehicleAgent.Warp(hitInfo.position);
                return _vehicleAgent.isOnNavMesh;
            }

            if (NavMesh.SamplePosition(referencePosition, out hitInfo, NAVMESH_RECOVERY_RADIUS, NavMesh.AllAreas))
            {
                _vehicleAgent.Warp(hitInfo.position);
                return _vehicleAgent.isOnNavMesh;
            }

            return false;
        }
    }
}