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
        private const float NAVMESH_RECOVERY_RADIUS = 1.5f;
        private const float MIN_SPEED_THRESHOLD = 0.002f;
        private const float STUCK_TIME_THRESHOLD = 2.0f;
        private const float SPEED_SCALE_MULTIPLIER = 0.1f;

        [SerializeField] private NavMeshAgent _vehicleAgent;
        [SerializeField] private float _waypointReachDistance = 0.02f;
        [SerializeField] private float _finalDestinationDistance = 0.02f;
        [SerializeField] private float _vehicleAngularSpeed = 360f;
        [SerializeField] private float _vehicleAcceleration = 20f;
        [SerializeField] private float _vehicleSpeed = 1.5f;

        public event Action OnDestinationReached;
        public event Action<int> OnWaypointReached;

        public bool IsDriving => _isDriving;
        public float CurrentSpeed => _baseSpeed;
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
        private float _baseSpeed;
        private Vector3 _segmentStartPosition;

        private void Awake()
        {
            if (_vehicleAgent == null)
            {
                _vehicleAgent = GetComponent<NavMeshAgent>();
            }

            _baseSpeed = _vehicleSpeed;
            _lastPosition = transform.position;
            ConfigureRoadOnlyAreaMask();
            ConfigureStrictPathing();
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

            UpdateRotation();
            UpdateStuckDetection();

            bool isLastWaypoint = _currentWaypointIndex >= _activeWaypoints.Count - 1;
            float requiredDistance = isLastWaypoint ? _finalDestinationDistance : _waypointReachDistance;

            Vector3 currentTarget = _activeWaypoints[_currentWaypointIndex];
            Vector3 flatVehiclePosition = new Vector3(transform.position.x, 0f, transform.position.z);
            Vector3 flatTargetPosition = new Vector3(currentTarget.x, 0f, currentTarget.z);
            float horizontalDistance = Vector3.Distance(flatVehiclePosition, flatTargetPosition);

            bool hasPassedWaypoint = HasPassedTargetPlane(flatVehiclePosition, flatTargetPosition);

            if (horizontalDistance <= requiredDistance || hasPassedWaypoint)
            {
                transform.position = new Vector3(currentTarget.x, transform.position.y, currentTarget.z);
                AdvanceToNextWaypoint();
            }
        }

        private bool HasPassedTargetPlane(Vector3 flatVehiclePos, Vector3 flatTargetPos)
        {
            Vector3 flatStartPos = new Vector3(_segmentStartPosition.x, 0f, _segmentStartPosition.z);
            Vector3 segmentDir = flatTargetPos - flatStartPos;

            if (segmentDir.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            Vector3 toVehicle = flatVehiclePos - flatTargetPos;
            return Vector3.Dot(segmentDir.normalized, toVehicle) >= 0f;
        }

        private void UpdateRotation()
        {
            Vector3 direction = CurrentTargetPosition - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                _vehicleAngularSpeed * Time.deltaTime
            );
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
            ConfigureStrictPathing();

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
            _segmentStartPosition = transform.position;
            _isDriving = true;
            _stuckTimer = 0f;
            _lastPosition = transform.position;
            _vehicleAgent.updatePosition = true;
            _vehicleAgent.updateRotation = false;
            _vehicleAgent.speed = _baseSpeed;
            _vehicleAgent.isStopped = false;
            _vehicleAgent.SetDestination(_activeWaypoints[_currentWaypointIndex]);
        }

        public void StopAndReset()
        {
            _isDriving = false;
            _activeWaypoints.Clear();
            _currentWaypointIndex = 0;
            _stuckTimer = 0f;

            if (_vehicleAgent == null)
            {
                _vehicleAgent = GetComponent<NavMeshAgent>();
            }

            if (_vehicleAgent == null)
            {
                return;
            }

            _vehicleAgent.isStopped = true;
            _vehicleAgent.ResetPath();
            _vehicleAgent.velocity = Vector3.zero;
            _vehicleAgent.updatePosition = false;
            _vehicleAgent.updateRotation = false;
        }

        public void TeleportAndStop(Vector3 targetWorldPosition)
        {
            _isDriving = false;
            _activeWaypoints.Clear();
            _currentWaypointIndex = 0;
            _stuckTimer = 0f;

            if (_vehicleAgent == null)
            {
                _vehicleAgent = GetComponent<NavMeshAgent>();
            }

            if (_vehicleAgent == null)
            {
                transform.position = targetWorldPosition;
                _lastPosition = targetWorldPosition;
                return;
            }

            _vehicleAgent.updatePosition = true;
            _vehicleAgent.updateRotation = false;
            _vehicleAgent.isStopped = true;
            _vehicleAgent.ResetPath();
            _vehicleAgent.velocity = Vector3.zero;

            if (TryGetValidNavMeshPosition(targetWorldPosition, out Vector3 validPosition))
            {
                _vehicleAgent.Warp(validPosition);
                _lastPosition = validPosition;
            }
            else
            {
                _vehicleAgent.Warp(targetWorldPosition);
                _lastPosition = targetWorldPosition;
            }

            _vehicleAgent.updatePosition = false;
        }

        public void StopAndReset(Vector3 resetWorldPosition)
        {
            TeleportAndStop(resetWorldPosition);
        }

        public void SetSpeed(float speed)
        {
            _vehicleSpeed = Mathf.Max(0.005f, speed * SPEED_SCALE_MULTIPLIER);
            _baseSpeed = _vehicleSpeed;

            if (_vehicleAgent != null)
            {
                _vehicleAgent.speed = _baseSpeed;
            }
        }

        private void ConfigureStrictPathing()
        {
            if (_vehicleAgent == null)
            {
                return;
            }

            _vehicleAgent.updateRotation = false;
            _vehicleAgent.autoBraking = false;
            _vehicleAgent.stoppingDistance = 0f;
            _vehicleAgent.angularSpeed = _vehicleAngularSpeed;
            _vehicleAgent.acceleration = _vehicleAcceleration;
            _vehicleAgent.speed = _baseSpeed;
        }

        private void UpdateStuckDetection()
        {
            if (Time.deltaTime <= 0f)
            {
                return;
            }

            float currentSpeed = Vector3.Distance(transform.position, _lastPosition) / Time.deltaTime;
            _lastPosition = transform.position;

            if (currentSpeed < MIN_SPEED_THRESHOLD)
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
            _segmentStartPosition = _activeWaypoints[reachedIndex];
            _currentWaypointIndex++;

            if (_currentWaypointIndex >= _activeWaypoints.Count)
            {
                _isDriving = false;
                _stuckTimer = 0f;

                if (_vehicleAgent != null && _vehicleAgent.isActiveAndEnabled && _vehicleAgent.isOnNavMesh)
                {
                    _vehicleAgent.isStopped = true;
                    _vehicleAgent.ResetPath();
                    _vehicleAgent.velocity = Vector3.zero;
                }

                OnWaypointReached?.Invoke(reachedIndex);
                OnDestinationReached?.Invoke();
                return;
            }

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

            if (TryGetValidNavMeshPosition(referencePosition, out Vector3 validPosition))
            {
                _vehicleAgent.Warp(validPosition);
                return _vehicleAgent.isOnNavMesh;
            }

            return false;
        }

        private bool TryGetValidNavMeshPosition(Vector3 referencePosition, out Vector3 validPosition)
        {
            int roadAreaIndex = NavMesh.GetAreaFromName(ROAD_AREA_NAME);
            int preferredMask = roadAreaIndex != INVALID_AREA_INDEX ? (1 << roadAreaIndex) : NavMesh.AllAreas;

            if (NavMesh.SamplePosition(referencePosition, out NavMeshHit hitInfo, NAVMESH_RECOVERY_RADIUS, preferredMask))
            {
                validPosition = hitInfo.position;
                return true;
            }

            if (NavMesh.SamplePosition(referencePosition, out NavMeshHit fallbackHitInfo, NAVMESH_RECOVERY_RADIUS, NavMesh.AllAreas))
            {
                validPosition = fallbackHitInfo.position;
                return true;
            }

            validPosition = referencePosition;
            return false;
        }
    }
}