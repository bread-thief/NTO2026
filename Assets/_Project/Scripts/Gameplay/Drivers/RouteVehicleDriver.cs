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

        [field: SerializeField] public NavMeshAgent VehicleAgent { get; private set; }
        [field: SerializeField] public float WaypointReachDistance { get; private set; } = WAYPOINT_REACH_THRESHOLD;
        [field: SerializeField] public float FinalDestinationDistance { get; private set; } = FINAL_DESTINATION_THRESHOLD;
        [field: SerializeField] public float VehicleAngularSpeed { get; private set; } = DEFAULT_TURN_SPEED;
        [field: SerializeField] public float VehicleAcceleration { get; private set; } = DEFAULT_ACCELERATION;
        [field: SerializeField] public float VehicleSpeed { get; private set; } = DEFAULT_VEHICLE_SPEED;

        public event Action OnDestinationReached;
        public event Action<int> OnWaypointReached;

        public bool IsDriving => isDriving;

        public int CurrentWaypointIndex => currentWaypointIndex;

        public int TotalWaypointCount => activeWaypoints.Count;

        public Vector3 CurrentTargetPosition
        {
            get
            {
                if (activeWaypoints.Count == 0 || currentWaypointIndex >= activeWaypoints.Count)
                {
                    return transform.position;
                }

                return activeWaypoints[currentWaypointIndex];
            }
        }

        private readonly List<Vector3> activeWaypoints = new List<Vector3>();
        private int currentWaypointIndex;
        private bool isDriving;
        private float stuckTimer;
        private Vector3 lastPosition;

        private void Awake()
        {
            if (VehicleAgent == null)
            {
                VehicleAgent = GetComponent<NavMeshAgent>();
            }

            lastPosition = transform.position;
            ConfigureRoadOnlyAreaMask();
            ConfigureSmoothCornering();
        }

        private void Update()
        {
            if (!isDriving || VehicleAgent == null || activeWaypoints.Count == 0)
            {
                return;
            }

            if (!EnsureAgentIsPlacedOnNavMesh(transform.position))
            {
                return;
            }

            if (VehicleAgent.pathPending)
            {
                return;
            }

            UpdateStuckDetection();

            bool isLastWaypoint = currentWaypointIndex >= activeWaypoints.Count - 1;
            float requiredDistance = isLastWaypoint ? FinalDestinationDistance : WaypointReachDistance;

            Vector3 currentTarget = activeWaypoints[currentWaypointIndex];
            Vector3 flatVehiclePosition = new Vector3(transform.position.x, 0f, transform.position.z);
            Vector3 flatTargetPosition = new Vector3(currentTarget.x, 0f, currentTarget.z);
            float horizontalDistance = Vector3.Distance(flatVehiclePosition, flatTargetPosition);

            if (horizontalDistance <= requiredDistance || VehicleAgent.remainingDistance <= requiredDistance)
            {
                AdvanceToNextWaypoint();
            }
        }

        public void ConfigureRoadOnlyAreaMask()
        {
            if (VehicleAgent == null)
            {
                VehicleAgent = GetComponent<NavMeshAgent>();
            }

            if (VehicleAgent == null)
            {
                return;
            }

            int roadAreaIndex = NavMesh.GetAreaFromName(ROAD_AREA_NAME);

            if (roadAreaIndex != INVALID_AREA_INDEX)
            {
                VehicleAgent.areaMask = 1 << roadAreaIndex;
            }
            else
            {
                VehicleAgent.areaMask = NavMesh.AllAreas;
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

            if (VehicleAgent == null)
            {
                VehicleAgent = GetComponent<NavMeshAgent>();
            }

            if (VehicleAgent == null)
            {
                return;
            }

            ConfigureRoadOnlyAreaMask();
            ConfigureSmoothCornering();

            if (!EnsureAgentIsPlacedOnNavMesh(waypoints[0]))
            {
                return;
            }

            activeWaypoints.Clear();

            for (int index = 0; index < waypoints.Count; index++)
            {
                activeWaypoints.Add(waypoints[index]);
            }

            currentWaypointIndex = 1;
            isDriving = true;
            stuckTimer = 0f;
            lastPosition = transform.position;

            VehicleAgent.speed = VehicleSpeed;
            VehicleAgent.isStopped = false;
            VehicleAgent.SetDestination(activeWaypoints[currentWaypointIndex]);
        }

        public void StopAndReset()
        {
            isDriving = false;
            activeWaypoints.Clear();
            currentWaypointIndex = 0;
            stuckTimer = 0f;

            if (VehicleAgent != null && VehicleAgent.isActiveAndEnabled && VehicleAgent.isOnNavMesh)
            {
                VehicleAgent.isStopped = true;
                VehicleAgent.ResetPath();
            }
        }

        public void StopAndReset(Vector3 resetWorldPosition)
        {
            isDriving = false;
            activeWaypoints.Clear();
            currentWaypointIndex = 0;
            stuckTimer = 0f;

            if (VehicleAgent == null)
            {
                VehicleAgent = GetComponent<NavMeshAgent>();
            }

            if (VehicleAgent != null && EnsureAgentIsPlacedOnNavMesh(resetWorldPosition))
            {
                VehicleAgent.isStopped = true;
                VehicleAgent.ResetPath();
                VehicleAgent.Warp(resetWorldPosition);
            }
            else
            {
                transform.position = resetWorldPosition;
            }

            lastPosition = resetWorldPosition;
        }

        public void SetSpeed(float speed)
        {
            VehicleSpeed = Mathf.Max(0.1f, speed);

            if (VehicleAgent != null)
            {
                VehicleAgent.speed = VehicleSpeed;
            }
        }

        private void ConfigureSmoothCornering()
        {
            if (VehicleAgent == null)
            {
                return;
            }

            VehicleAgent.autoBraking = false;
            VehicleAgent.angularSpeed = VehicleAngularSpeed;
            VehicleAgent.acceleration = VehicleAcceleration;
            VehicleAgent.speed = VehicleSpeed;
        }

        private void UpdateStuckDetection()
        {
            float movement = Vector3.Distance(transform.position, lastPosition);
            lastPosition = transform.position;

            if (movement < MIN_MOVEMENT_THRESHOLD)
            {
                stuckTimer += Time.deltaTime;

                if (stuckTimer >= STUCK_TIME_THRESHOLD)
                {
                    stuckTimer = 0f;
                    RecoverFromStuck();
                }
            }
            else
            {
                stuckTimer = 0f;
            }
        }

        private void RecoverFromStuck()
        {
            if (currentWaypointIndex >= activeWaypoints.Count)
            {
                return;
            }

            if (!EnsureAgentIsPlacedOnNavMesh(transform.position))
            {
                return;
            }

            VehicleAgent.ResetPath();
            VehicleAgent.SetDestination(activeWaypoints[currentWaypointIndex]);
        }

        private void AdvanceToNextWaypoint()
        {
            int reachedIndex = currentWaypointIndex;
            currentWaypointIndex++;

            if (currentWaypointIndex >= activeWaypoints.Count)
            {
                isDriving = false;
                stuckTimer = 0f;

                if (VehicleAgent != null && VehicleAgent.isActiveAndEnabled && VehicleAgent.isOnNavMesh)
                {
                    VehicleAgent.autoBraking = true;
                    VehicleAgent.isStopped = true;
                    VehicleAgent.ResetPath();
                }

                OnWaypointReached?.Invoke(reachedIndex);
                OnDestinationReached?.Invoke();
                return;
            }

            bool isApproachingFinalPoint = currentWaypointIndex == activeWaypoints.Count - 1;
            VehicleAgent.autoBraking = isApproachingFinalPoint;

            if (EnsureAgentIsPlacedOnNavMesh(transform.position))
            {
                VehicleAgent.SetDestination(activeWaypoints[currentWaypointIndex]);
            }

            OnWaypointReached?.Invoke(reachedIndex);
        }

        private bool EnsureAgentIsPlacedOnNavMesh(Vector3 referencePosition)
        {
            if (VehicleAgent == null || !VehicleAgent.isActiveAndEnabled)
            {
                return false;
            }

            if (VehicleAgent.isOnNavMesh)
            {
                return true;
            }

            int roadAreaIndex = NavMesh.GetAreaFromName(ROAD_AREA_NAME);
            int preferredMask = roadAreaIndex != INVALID_AREA_INDEX ? (1 << roadAreaIndex) : NavMesh.AllAreas;

            if (NavMesh.SamplePosition(referencePosition, out NavMeshHit hitInfo, NAVMESH_RECOVERY_RADIUS, preferredMask))
            {
                VehicleAgent.Warp(hitInfo.position);
                return VehicleAgent.isOnNavMesh;
            }

            if (NavMesh.SamplePosition(referencePosition, out hitInfo, NAVMESH_RECOVERY_RADIUS, NavMesh.AllAreas))
            {
                VehicleAgent.Warp(hitInfo.position);
                return VehicleAgent.isOnNavMesh;
            }

            return false;
        }
    }
}