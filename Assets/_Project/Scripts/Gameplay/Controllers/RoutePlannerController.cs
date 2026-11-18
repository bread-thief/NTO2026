using System;
using System.Collections.Generic;
using _Project.Scripts.Gameplay.Drivers;
using _Project.Scripts.Gameplay.Generators;
using _Project.Scripts.Gameplay.Handlers;
using UnityEngine;
using UnityEngine.AI;

namespace _Project.Scripts.Gameplay.Controllers
{
    public class RoutePlannerController : MonoBehaviour
    {
        private const int CURVE_SEGMENTS_PER_CORNER = 6;
        private const int MAX_CELL_SNAP_SEARCH_RADIUS = 2;
        private const float CORNER_ROUNDING_RATIO = 0.42f;
        private const float STRAIGHT_ALIGNMENT_DOT = 0.98f;
        private const float DEFAULT_RAYCAST_DISTANCE = 1000f;
        private const float DEFAULT_ROAD_SNAP_RADIUS = 5f;
        private const float DEFAULT_LINE_HEIGHT = 0.25f;
        private const float LINE_ERASE_OFFSET = 1.5f;
        private const float LINE_SEGMENT_LENGTH = 0.5f;
        private const string ROAD_AREA_NAME = "Road";

        [field: SerializeField] public Camera MainCamera { get; private set; }

        [field: SerializeField] public CityGenerator Generator { get; private set; }

        [field: SerializeField] public InputHandler PlayerInputHandler { get; private set; }

        [field: SerializeField] public DeliveryRouteNode PointANode { get; private set; }

        [field: SerializeField] public DeliveryRouteNode PointBNode { get; private set; }

        [field: SerializeField] public RouteVehicleDriver VehicleDriver { get; private set; }

        [field: SerializeField] public LineRenderer RouteLineRenderer { get; private set; }

        [field: SerializeField] public LayerMask RoadClickLayers { get; private set; }

        [field: SerializeField] public float MaxRaycastDistance { get; private set; } = DEFAULT_RAYCAST_DISTANCE;

        [field: SerializeField] public float RoadSnapRadius { get; private set; } = DEFAULT_ROAD_SNAP_RADIUS;

        [field: SerializeField] public float LineHeight { get; private set; } = DEFAULT_LINE_HEIGHT;

        [field: SerializeField] public float LineEraseOffset { get; private set; } = LINE_ERASE_OFFSET;

        [field: SerializeField] public float LineSegmentLength { get; private set; } = LINE_SEGMENT_LENGTH;

        [field: SerializeField] public bool AutoStartVehicleOnComplete { get; private set; } = true;

        [field: SerializeField, Min(1)] public int MaxTrips { get; private set; } = 5;

        public event Action<IReadOnlyList<Vector3>> OnRouteCompleted;

        public bool IsRouteCompleted { get; private set; }

        public int CompletedTrips { get; private set; }

        public bool IsCurrentTargetPointB { get; private set; } = true;

        private readonly List<Vector2Int> routeCellWaypoints = new List<Vector2Int>();

        private readonly List<Vector3> smoothedWorldWaypoints = new List<Vector3>();

        private readonly List<float> smoothedWaypointDistances = new List<float>();

        private readonly HashSet<Vector2Int> visitedCells = new HashSet<Vector2Int>();

        private int roadAreaMask = NavMesh.AllAreas;

        private float totalSmoothedPathLength;

        private bool isVehicleDriving;

        private void Awake()
        {
            if (MainCamera == null)
            {
                MainCamera = Camera.main;
            }

            if (Generator == null)
            {
                Generator = FindAnyObjectByType<CityGenerator>();
            }

            if (PlayerInputHandler == null)
            {
                PlayerInputHandler = FindAnyObjectByType<InputHandler>();
            }

            int roadAreaIndex = NavMesh.GetAreaFromName(ROAD_AREA_NAME);

            if (roadAreaIndex >= 0)
            {
                roadAreaMask = 1 << roadAreaIndex;
            }
        }

        private void OnEnable()
        {
            if (PlayerInputHandler == null)
            {
                PlayerInputHandler = FindAnyObjectByType<InputHandler>();
            }

            if (PlayerInputHandler != null)
            {
                PlayerInputHandler.OnPrimaryPressed += HandlePrimaryInput;
                PlayerInputHandler.OnSecondaryPressed += HandleSecondaryInput;
            }

            if (VehicleDriver != null)
            {
                VehicleDriver.OnDestinationReached += HandleVehicleDestinationReached;
            }
        }

        private void OnDisable()
        {
            if (PlayerInputHandler != null)
            {
                PlayerInputHandler.OnPrimaryPressed -= HandlePrimaryInput;
                PlayerInputHandler.OnSecondaryPressed -= HandleSecondaryInput;
            }

            if (VehicleDriver != null)
            {
                VehicleDriver.OnDestinationReached -= HandleVehicleDestinationReached;
            }
        }

        private void Update()
        {
            if (!isVehicleDriving)
            {
                return;
            }

            UpdateLineVisual();
        }

        private void Start()
        {
            if (PointANode != null && PointBNode != null)
            {
                ResetRoutePlanning();
            }
        }

        public void ConfigureRouteEndpoints(DeliveryRouteNode startNode, DeliveryRouteNode endNode, RouteVehicleDriver driver)
        {
            if (VehicleDriver != null)
            {
                VehicleDriver.OnDestinationReached -= HandleVehicleDestinationReached;
            }

            PointANode = startNode;
            PointBNode = endNode;
            VehicleDriver = driver;

            if (VehicleDriver != null)
            {
                VehicleDriver.OnDestinationReached += HandleVehicleDestinationReached;
            }

            CompletedTrips = 0;
            IsCurrentTargetPointB = true;
            visitedCells.Clear();

            ResetRoutePlanning();
        }

        public void ResetRoutePlanning()
        {
            IsRouteCompleted = false;
            isVehicleDriving = false;
            totalSmoothedPathLength = 0f;
            routeCellWaypoints.Clear();
            smoothedWorldWaypoints.Clear();
            smoothedWaypointDistances.Clear();

            if (PointANode != null && PointBNode != null && Generator != null)
            {
                PointANode.SnapToRoadNavMesh();
                PointBNode.SnapToRoadNavMesh();

                Vector3 startPosition = IsCurrentTargetPointB
                    ? PointANode.RoadNavMeshPosition
                    : PointBNode.RoadNavMeshPosition;

                if (TryResolveNearestRoadCell(startPosition, out Vector2Int startCell))
                {
                    routeCellWaypoints.Add(startCell);
                }

                if (VehicleDriver != null)
                {
                    VehicleDriver.StopAndReset(startPosition);
                }
            }

            UpdateMarkerVisibility();
            RebuildSmoothedPath();
        }

        public IReadOnlyList<Vector3> GetPlannedWaypoints()
        {
            return smoothedWorldWaypoints;
        }

        private void HandleVehicleDestinationReached()
        {
            isVehicleDriving = false;
            CompletedTrips++;

            RegisterVisitedCellsFromRoute();
            ClearLineVisual();

            if (CompletedTrips >= MaxTrips)
            {
                if (PointANode != null)
                {
                    PointANode.SetMarkerActive(false);
                }

                if (PointBNode != null)
                {
                    PointBNode.SetMarkerActive(false);
                }

                return;
            }

            IsCurrentTargetPointB = !IsCurrentTargetPointB;

            ResetRoutePlanning();
        }

        private void RegisterVisitedCellsFromRoute()
        {
            for (int index = 0; index < routeCellWaypoints.Count; index++)
            {
                visitedCells.Add(routeCellWaypoints[index]);
            }
        }

        private void ClearLineVisual()
        {
            if (RouteLineRenderer == null)
            {
                return;
            }

            RouteLineRenderer.positionCount = 0;
        }

        private void UpdateMarkerVisibility()
        {
            if (PointANode == null || PointBNode == null)
            {
                return;
            }

            if (CompletedTrips >= MaxTrips)
            {
                PointANode.SetMarkerActive(false);
                PointBNode.SetMarkerActive(false);
                return;
            }

            if (IsRouteCompleted)
            {
                return;
            }

            if (IsCurrentTargetPointB)
            {
                PointANode.SetMarkerActive(false);
                PointBNode.SetMarkerActive(true);
            }
            else
            {
                PointANode.SetMarkerActive(true);
                PointBNode.SetMarkerActive(false);
            }
        }

        private void HandlePrimaryInput(Vector2 screenPosition)
        {
            TryAddRoadStepFromScreenPosition(screenPosition);
        }

        private void HandleSecondaryInput(Vector2 screenPosition)
        {
            RemoveLastStep();
        }

        private Vector3 GetCurrentTargetPosition()
        {
            if (IsCurrentTargetPointB)
            {
                return PointBNode != null ? PointBNode.RoadNavMeshPosition : Vector3.zero;
            }

            return PointANode != null ? PointANode.RoadNavMeshPosition : Vector3.zero;
        }

        private Vector3 GetCurrentStartPosition()
        {
            if (IsCurrentTargetPointB)
            {
                return PointANode != null ? PointANode.RoadNavMeshPosition : Vector3.zero;
            }

            return PointBNode != null ? PointBNode.RoadNavMeshPosition : Vector3.zero;
        }

        private void TryAddRoadStepFromScreenPosition(Vector2 screenPosition)
        {
            if (IsRouteCompleted)
            {
                return;
            }

            if (CompletedTrips >= MaxTrips)
            {
                return;
            }

            if (MainCamera == null || PointANode == null || PointBNode == null || Generator == null)
            {
                return;
            }

            Ray ray = MainCamera.ScreenPointToRay(screenPosition);

            if (!Physics.Raycast(ray, out RaycastHit hitInfo, MaxRaycastDistance, RoadClickLayers, QueryTriggerInteraction.Collide))
            {
                return;
            }

            if (!TryResolveNearestRoadCell(hitInfo.point, out Vector2Int targetCell))
            {
                return;
            }

            if (!TryResolveNearestRoadCell(GetCurrentTargetPosition(), out Vector2Int destinationCell))
            {
                return;
            }

            if (routeCellWaypoints.Count == 0)
            {
                if (TryResolveNearestRoadCell(GetCurrentStartPosition(), out Vector2Int startCell))
                {
                    routeCellWaypoints.Add(startCell);
                }
                else
                {
                    return;
                }
            }

            Vector2Int currentLastCell = routeCellWaypoints[routeCellWaypoints.Count - 1];

            if (targetCell == currentLastCell)
            {
                return;
            }

            int existingIndex = routeCellWaypoints.IndexOf(targetCell);

            if (existingIndex >= 0)
            {
                int removeStartIndex = existingIndex + 1;

                if (removeStartIndex < routeCellWaypoints.Count)
                {
                    routeCellWaypoints.RemoveRange(removeStartIndex, routeCellWaypoints.Count - removeStartIndex);
                    RebuildSmoothedPath();
                }

                return;
            }

            HashSet<Vector2Int> blockedCells = BuildBlockedCellSet();
            List<Vector2Int> cellStepSequence = FindConnectedRoadCellPath(currentLastCell, targetCell, destinationCell, blockedCells);

            if (cellStepSequence == null || cellStepSequence.Count < 2)
            {
                return;
            }

            for (int index = 1; index < cellStepSequence.Count; index++)
            {
                Vector2Int stepCell = cellStepSequence[index];

                if (routeCellWaypoints.Contains(stepCell))
                {
                    break;
                }

                routeCellWaypoints.Add(stepCell);

                if (stepCell == destinationCell)
                {
                    IsRouteCompleted = true;
                    break;
                }
            }

            UpdateMarkerVisibility();
            RebuildSmoothedPath();

            if (IsRouteCompleted)
            {
                OnRouteCompleted?.Invoke(smoothedWorldWaypoints);

                if (AutoStartVehicleOnComplete && VehicleDriver != null)
                {
                    VehicleDriver.StartFollowingRoute(smoothedWorldWaypoints);
                    isVehicleDriving = true;
                    UpdateLineVisual();
                }
            }
        }

        private HashSet<Vector2Int> BuildBlockedCellSet()
        {
            HashSet<Vector2Int> blockedCells = new HashSet<Vector2Int>();

            for (int index = 0; index < routeCellWaypoints.Count - 1; index++)
            {
                Vector2Int cell = routeCellWaypoints[index];

                if (!visitedCells.Contains(cell))
                {
                    blockedCells.Add(cell);
                }
            }

            return blockedCells;
        }

        private void RemoveLastStep()
        {
            if (IsRouteCompleted)
            {
                IsRouteCompleted = false;
                isVehicleDriving = false;

                if (VehicleDriver != null)
                {
                    VehicleDriver.StopAndReset(GetCurrentStartPosition());
                }
            }

            if (routeCellWaypoints.Count <= 1)
            {
                UpdateMarkerVisibility();
                return;
            }

            routeCellWaypoints.RemoveAt(routeCellWaypoints.Count - 1);
            UpdateMarkerVisibility();
            RebuildSmoothedPath();
        }

        private bool TryResolveNearestRoadCell(Vector3 worldPosition, out Vector2Int resolvedCell)
        {
            resolvedCell = default;

            if (Generator == null)
            {
                return false;
            }

            Vector2Int gridDimensions = Generator.GetGridDimensions;
            Vector2Int baseCell = Generator.GetCellCoordinatesFromWorldPosition(worldPosition);

            if (IsRoadCell(baseCell, gridDimensions))
            {
                resolvedCell = baseCell;
                return true;
            }

            float closestDistanceSquared = float.MaxValue;
            bool foundCell = false;

            for (int offsetX = -MAX_CELL_SNAP_SEARCH_RADIUS; offsetX <= MAX_CELL_SNAP_SEARCH_RADIUS; offsetX++)
            {
                for (int offsetY = -MAX_CELL_SNAP_SEARCH_RADIUS; offsetY <= MAX_CELL_SNAP_SEARCH_RADIUS; offsetY++)
                {
                    Vector2Int candidateCell = new Vector2Int(baseCell.x + offsetX, baseCell.y + offsetY);

                    if (!IsRoadCell(candidateCell, gridDimensions))
                    {
                        continue;
                    }

                    Vector3 candidateWorld = Generator.GetWorldPositionFromCellCoordinates(candidateCell.x, candidateCell.y);
                    float distanceSquared = (candidateWorld - worldPosition).sqrMagnitude;

                    if (distanceSquared < closestDistanceSquared)
                    {
                        closestDistanceSquared = distanceSquared;
                        resolvedCell = candidateCell;
                        foundCell = true;
                    }
                }
            }

            return foundCell;
        }

        private bool IsRoadCell(Vector2Int cellCoordinates, Vector2Int gridDimensions)
        {
            if (cellCoordinates.x < 0 || cellCoordinates.x >= gridDimensions.x)
            {
                return false;
            }

            if (cellCoordinates.y < 0 || cellCoordinates.y >= gridDimensions.y)
            {
                return false;
            }

            CityBlockPreset preset = Generator.GetSpawnedPresetAt(cellCoordinates.x, cellCoordinates.y);

            return preset != null && preset.RoadCount > 0;
        }

        private List<Vector2Int> FindConnectedRoadCellPath(
            Vector2Int startCell,
            Vector2Int endCell,
            Vector2Int destinationCell,
            HashSet<Vector2Int> blockedCells)
        {
            Vector2Int gridDimensions = Generator.GetGridDimensions;
            Queue<Vector2Int> frontier = new Queue<Vector2Int>();
            Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();

            frontier.Enqueue(startCell);
            cameFrom[startCell] = startCell;

            Direction[] directions =
            {
                Direction.North,
                Direction.East,
                Direction.South,
                Direction.West
            };

            while (frontier.Count > 0)
            {
                Vector2Int currentCell = frontier.Dequeue();

                if (currentCell == endCell)
                {
                    return ReconstructCellPath(cameFrom, startCell, endCell);
                }

                if (currentCell == destinationCell && currentCell != startCell)
                {
                    continue;
                }

                CityBlockPreset currentPreset = Generator.GetSpawnedPresetAt(currentCell.x, currentCell.y);
                int currentRotation = Generator.GetSpawnedRotationAt(currentCell.x, currentCell.y);

                if (currentPreset == null)
                {
                    continue;
                }

                for (int index = 0; index < directions.Length; index++)
                {
                    Direction direction = directions[index];

                    if (!currentPreset.HasRoad(direction, currentRotation))
                    {
                        continue;
                    }

                    Vector2Int neighborCell = currentCell + GetDirectionOffset(direction);

                    if (cameFrom.ContainsKey(neighborCell))
                    {
                        continue;
                    }

                    if (!IsRoadCell(neighborCell, gridDimensions))
                    {
                        continue;
                    }

                    bool isBlocked = blockedCells.Contains(neighborCell);
                    bool isVisited = visitedCells.Contains(neighborCell);

                    if (isBlocked && !isVisited)
                    {
                        continue;
                    }

                    CityBlockPreset neighborPreset = Generator.GetSpawnedPresetAt(neighborCell.x, neighborCell.y);
                    int neighborRotation = Generator.GetSpawnedRotationAt(neighborCell.x, neighborCell.y);
                    Direction oppositeDirection = GetOppositeDirection(direction);

                    if (neighborPreset == null || !neighborPreset.HasRoad(oppositeDirection, neighborRotation))
                    {
                        continue;
                    }

                    cameFrom[neighborCell] = currentCell;
                    frontier.Enqueue(neighborCell);
                }
            }

            return null;
        }

        private List<Vector2Int> ReconstructCellPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int startCell, Vector2Int endCell)
        {
            List<Vector2Int> reversedPath = new List<Vector2Int>();
            Vector2Int current = endCell;

            while (current != startCell)
            {
                reversedPath.Add(current);
                current = cameFrom[current];
            }

            reversedPath.Add(startCell);
            reversedPath.Reverse();

            return reversedPath;
        }

        private void RebuildSmoothedPath()
        {
            smoothedWorldWaypoints.Clear();
            smoothedWaypointDistances.Clear();
            totalSmoothedPathLength = 0f;

            if (routeCellWaypoints.Count == 0 || Generator == null)
            {
                UpdateLineVisual();
                return;
            }

            Vector3 startWorldPosition = GetCurrentStartPosition();
            Vector3 endWorldPosition = GetCurrentTargetPosition();

            List<Vector3> rawCellCenters = new List<Vector3>(routeCellWaypoints.Count);

            for (int index = 0; index < routeCellWaypoints.Count; index++)
            {
                Vector2Int cell = routeCellWaypoints[index];
                Vector3 centerWorld = Generator.GetWorldPositionFromCellCoordinates(cell.x, cell.y);

                if (index == 0)
                {
                    centerWorld = startWorldPosition;
                }
                else if (IsRouteCompleted && index == routeCellWaypoints.Count - 1)
                {
                    centerWorld = endWorldPosition;
                }
                else
                {
                    centerWorld = SnapWorldPointToRoad(centerWorld);
                }

                rawCellCenters.Add(centerWorld);
            }

            List<Vector3> roundedWaypoints = BuildRoundedRoadWaypoints(rawCellCenters);
            List<Vector3> subdividedWaypoints = SubdivideWaypoints(roundedWaypoints, LineSegmentLength);

            smoothedWorldWaypoints.AddRange(subdividedWaypoints);

            RecalculateWaypointDistances();

            UpdateLineVisual();
        }

        private void RecalculateWaypointDistances()
        {
            smoothedWaypointDistances.Clear();
            totalSmoothedPathLength = 0f;

            if (smoothedWorldWaypoints.Count == 0)
            {
                return;
            }

            smoothedWaypointDistances.Add(0f);

            for (int index = 1; index < smoothedWorldWaypoints.Count; index++)
            {
                float segmentLength = Vector3.Distance(smoothedWorldWaypoints[index - 1], smoothedWorldWaypoints[index]);
                totalSmoothedPathLength += segmentLength;
                smoothedWaypointDistances.Add(totalSmoothedPathLength);
            }
        }

        private List<Vector3> SubdivideWaypoints(List<Vector3> points, float maxSegmentLength)
        {
            List<Vector3> result = new List<Vector3>();

            if (points.Count == 0)
            {
                return result;
            }

            result.Add(points[0]);

            for (int index = 1; index < points.Count; index++)
            {
                Vector3 previousPoint = points[index - 1];
                Vector3 currentPoint = points[index];
                float segmentLength = Vector3.Distance(previousPoint, currentPoint);

                if (segmentLength <= maxSegmentLength || segmentLength <= Mathf.Epsilon)
                {
                    result.Add(currentPoint);
                    continue;
                }

                int subdivisions = Mathf.CeilToInt(segmentLength / maxSegmentLength);

                for (int step = 1; step < subdivisions; step++)
                {
                    float interpolation = (float)step / subdivisions;
                    Vector3 intermediatePoint = Vector3.Lerp(previousPoint, currentPoint, interpolation);
                    result.Add(intermediatePoint);
                }

                result.Add(currentPoint);
            }

            return result;
        }

        private List<Vector3> BuildRoundedRoadWaypoints(List<Vector3> controlPoints)
        {
            List<Vector3> result = new List<Vector3>();

            if (controlPoints.Count <= 2)
            {
                result.AddRange(controlPoints);
                return result;
            }

            result.Add(controlPoints[0]);

            for (int index = 1; index < controlPoints.Count - 1; index++)
            {
                Vector3 previousPoint = controlPoints[index - 1];
                Vector3 cornerPoint = controlPoints[index];
                Vector3 nextPoint = controlPoints[index + 1];

                Vector3 incomingVector = cornerPoint - previousPoint;
                Vector3 outgoingVector = nextPoint - cornerPoint;

                float incomingLength = incomingVector.magnitude;
                float outgoingLength = outgoingVector.magnitude;

                if (incomingLength <= Mathf.Epsilon || outgoingLength <= Mathf.Epsilon)
                {
                    result.Add(cornerPoint);
                    continue;
                }

                Vector3 incomingDirection = incomingVector / incomingLength;
                Vector3 outgoingDirection = outgoingVector / outgoingLength;

                if (Vector3.Dot(incomingDirection, outgoingDirection) >= STRAIGHT_ALIGNMENT_DOT)
                {
                    result.Add(cornerPoint);
                    continue;
                }

                float cornerTrimDistance = Mathf.Min(incomingLength, outgoingLength) * CORNER_ROUNDING_RATIO;
                Vector3 curveStartPoint = cornerPoint - incomingDirection * cornerTrimDistance;
                Vector3 curveEndPoint = cornerPoint + outgoingDirection * cornerTrimDistance;

                for (int segment = 0; segment <= CURVE_SEGMENTS_PER_CORNER; segment++)
                {
                    float interpolation = (float)segment / CURVE_SEGMENTS_PER_CORNER;
                    Vector3 curvePoint = EvaluateQuadraticBezier(curveStartPoint, cornerPoint, curveEndPoint, interpolation);
                    Vector3 snappedCurvePoint = SnapWorldPointToRoad(curvePoint);

                    if (result.Count == 0 || (snappedCurvePoint - result[result.Count - 1]).sqrMagnitude > 0.001f)
                    {
                        result.Add(snappedCurvePoint);
                    }
                }
            }

            Vector3 finalPoint = controlPoints[controlPoints.Count - 1];

            if (result.Count == 0 || (finalPoint - result[result.Count - 1]).sqrMagnitude > 0.001f)
            {
                result.Add(finalPoint);
            }

            return result;
        }

        private Vector3 EvaluateQuadraticBezier(Vector3 startPoint, Vector3 controlPoint, Vector3 endPoint, float t)
        {
            float oneMinusT = 1f - t;

            return (oneMinusT * oneMinusT * startPoint)
                   + (2f * oneMinusT * t * controlPoint)
                   + (t * t * endPoint);
        }

        private Vector3 SnapWorldPointToRoad(Vector3 candidatePosition)
        {
            if (NavMesh.SamplePosition(candidatePosition, out NavMeshHit hitInfo, RoadSnapRadius, roadAreaMask))
            {
                return hitInfo.position;
            }

            if (NavMesh.SamplePosition(candidatePosition, out NavMeshHit fallbackHitInfo, RoadSnapRadius, NavMesh.AllAreas))
            {
                return fallbackHitInfo.position;
            }

            return candidatePosition;
        }

        private float GetVehicleDistanceAlongPath()
        {
            if (VehicleDriver == null || smoothedWorldWaypoints.Count < 2)
            {
                return 0f;
            }

            Vector3 vehiclePosition = VehicleDriver.transform.position;
            Vector3 flatVehiclePosition = new Vector3(vehiclePosition.x, 0f, vehiclePosition.z);

            float closestDistanceSquared = float.MaxValue;
            float closestDistanceAlongPath = 0f;

            for (int index = 1; index < smoothedWorldWaypoints.Count; index++)
            {
                Vector3 segmentStart = smoothedWorldWaypoints[index - 1];
                Vector3 segmentEnd = smoothedWorldWaypoints[index];

                Vector3 flatStart = new Vector3(segmentStart.x, 0f, segmentStart.z);
                Vector3 flatEnd = new Vector3(segmentEnd.x, 0f, segmentEnd.z);

                Vector3 segmentVector = flatEnd - flatStart;
                float segmentLengthSquared = segmentVector.sqrMagnitude;

                float interpolation = 0f;

                if (segmentLengthSquared > Mathf.Epsilon)
                {
                    interpolation = Vector3.Dot(flatVehiclePosition - flatStart, segmentVector) / segmentLengthSquared;
                    interpolation = Mathf.Clamp01(interpolation);
                }

                Vector3 projectedPoint = flatStart + segmentVector * interpolation;
                float distanceSquared = (projectedPoint - flatVehiclePosition).sqrMagnitude;

                if (distanceSquared < closestDistanceSquared)
                {
                    closestDistanceSquared = distanceSquared;

                    float segmentStartDistance = smoothedWaypointDistances[index - 1];
                    float segmentEndDistance = smoothedWaypointDistances[index];
                    closestDistanceAlongPath = Mathf.Lerp(segmentStartDistance, segmentEndDistance, interpolation);
                }
            }

            return closestDistanceAlongPath;
        }

        private void UpdateLineVisual()
        {
            if (RouteLineRenderer == null)
            {
                return;
            }

            if (smoothedWorldWaypoints.Count == 0)
            {
                RouteLineRenderer.positionCount = 0;
                return;
            }

            float startDistance = 0f;

            if (isVehicleDriving)
            {
                float vehicleDistance = GetVehicleDistanceAlongPath();
                startDistance = Mathf.Max(0f, vehicleDistance - LineEraseOffset);
            }

            List<Vector3> visiblePoints = BuildVisiblePoints(startDistance);

            RouteLineRenderer.useWorldSpace = true;
            RouteLineRenderer.positionCount = visiblePoints.Count;

            for (int index = 0; index < visiblePoints.Count; index++)
            {
                Vector3 point = visiblePoints[index];
                point.y = LineHeight;
                RouteLineRenderer.SetPosition(index, point);
            }
        }

        private List<Vector3> BuildVisiblePoints(float startDistance)
        {
            List<Vector3> points = new List<Vector3>();

            if (startDistance <= 0f)
            {
                points.AddRange(smoothedWorldWaypoints);
                return points;
            }

            if (startDistance >= totalSmoothedPathLength)
            {
                return points;
            }

            int startIndex = FindFirstVisibleWaypointIndex(startDistance);

            if (startIndex >= smoothedWorldWaypoints.Count)
            {
                return points;
            }

            if (startIndex > 0)
            {
                float previousDistance = smoothedWaypointDistances[startIndex - 1];
                float currentDistance = smoothedWaypointDistances[startIndex];
                float segmentLength = currentDistance - previousDistance;

                if (segmentLength > Mathf.Epsilon)
                {
                    float interpolation = (startDistance - previousDistance) / segmentLength;
                    Vector3 previousPoint = smoothedWorldWaypoints[startIndex - 1];
                    Vector3 currentPoint = smoothedWorldWaypoints[startIndex];
                    Vector3 clippedPoint = Vector3.Lerp(previousPoint, currentPoint, interpolation);
                    points.Add(clippedPoint);
                }
                else
                {
                    points.Add(smoothedWorldWaypoints[startIndex]);
                }
            }

            for (int index = startIndex; index < smoothedWorldWaypoints.Count; index++)
            {
                points.Add(smoothedWorldWaypoints[index]);
            }

            return points;
        }

        private int FindFirstVisibleWaypointIndex(float distance)
        {
            int low = 0;
            int high = smoothedWaypointDistances.Count - 1;

            while (low < high)
            {
                int middle = (low + high) / 2;

                if (smoothedWaypointDistances[middle] < distance)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            return low;
        }

        private Vector2Int GetDirectionOffset(Direction direction)
        {
            switch (direction)
            {
                case Direction.North:
                    {
                        return Vector2Int.up;
                    }
                case Direction.East:
                    {
                        return Vector2Int.right;
                    }
                case Direction.South:
                    {
                        return Vector2Int.down;
                    }
                default:
                    {
                        return Vector2Int.left;
                    }
            }
        }

        private Direction GetOppositeDirection(Direction direction)
        {
            switch (direction)
            {
                case Direction.North:
                    return Direction.South;
                case Direction.East:
                    return Direction.West;
                case Direction.South:
                    return Direction.North;
                default:
                    return Direction.East;
            }
        }
    }
}