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

        [SerializeField] private Camera _mainCamera;
        [SerializeField] private CityGenerator _generator;
        [SerializeField] private InputHandler _playerInputHandler;
        [SerializeField] private DeliveryRouteNode _pointANode;
        [SerializeField] private DeliveryRouteNode _pointBNode;
        [SerializeField] private RouteVehicleDriver _vehicleDriver;
        [SerializeField] private LineRenderer _routeLineRenderer;
        [SerializeField] private LayerMask _roadClickLayers;

        [SerializeField] private float _maxRaycastDistance = DEFAULT_RAYCAST_DISTANCE;
        [SerializeField] private float _roadSnapRadius = DEFAULT_ROAD_SNAP_RADIUS;
        [SerializeField] private float _lineHeight = DEFAULT_LINE_HEIGHT;
        [SerializeField] private float _lineEraseOffset = LINE_ERASE_OFFSET;
        [SerializeField] private float _lineSegmentLength = LINE_SEGMENT_LENGTH;
        [SerializeField] private bool _autoStartVehicleOnComplete = true;
        [SerializeField, Min(1)] private int _maxTrips = 5;

        public event System.Action<System.Collections.Generic.IReadOnlyList<Vector3>> OnRouteCompleted;
        public event System.Action<RouteVehicleDriver> OnRouteEndpointsConfigured;

        public bool IsRouteCompleted { get; private set; }
        public int CompletedTrips { get; private set; }
        public bool IsCurrentTargetPointB { get; private set; } = true;

        public DeliveryRouteNode PointANode => _pointANode;
        public DeliveryRouteNode PointBNode => _pointBNode;
        public RouteVehicleDriver VehicleDriver => _vehicleDriver;
        public System.Collections.Generic.IReadOnlyList<Vector3> SmoothedWorldWaypoints => _smoothedWorldWaypoints;

        private readonly System.Collections.Generic.List<Vector2Int> _routeCellWaypoints = new System.Collections.Generic.List<Vector2Int>();
        private readonly System.Collections.Generic.List<Vector3> _smoothedWorldWaypoints = new System.Collections.Generic.List<Vector3>();
        private readonly System.Collections.Generic.List<float> _smoothedWaypointDistances = new System.Collections.Generic.List<float>();
        private readonly System.Collections.Generic.HashSet<Vector2Int> _visitedCells = new System.Collections.Generic.HashSet<Vector2Int>();

        private int _roadAreaMask = NavMesh.AllAreas;
        private float _totalSmoothedPathLength;
        private bool _isVehicleDriving;
        private bool _isDragging;
        private Vector2Int _lastValidTargetCell;

        private void Awake()
        {
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
            }

            if (_generator == null)
            {
                _generator = FindAnyObjectByType<CityGenerator>();
            }

            if (_playerInputHandler == null)
            {
                _playerInputHandler = FindAnyObjectByType<InputHandler>();
            }

            int roadAreaIndex = NavMesh.GetAreaFromName(ROAD_AREA_NAME);

            if (roadAreaIndex >= 0)
            {
                _roadAreaMask = 1 << roadAreaIndex;
            }
        }

        private void OnEnable()
        {
            if (_playerInputHandler == null)
            {
                _playerInputHandler = FindAnyObjectByType<InputHandler>();
            }

            if (_playerInputHandler != null)
            {
                _playerInputHandler.OnPrimaryPressed += HandlePrimaryPressed;
                _playerInputHandler.OnPrimaryReleased += HandlePrimaryReleased;
            }

            SubscribeVehicle();
        }

        private void OnDisable()
        {
            if (_playerInputHandler != null)
            {
                _playerInputHandler.OnPrimaryPressed -= HandlePrimaryPressed;
                _playerInputHandler.OnPrimaryReleased -= HandlePrimaryReleased;
            }

            UnsubscribeVehicle();
        }

        private void SubscribeVehicle()
        {
            if (_vehicleDriver != null)
            {
                _vehicleDriver.OnDestinationReached -= HandleVehicleDestinationReached;
                _vehicleDriver.OnDestinationReached += HandleVehicleDestinationReached;
            }
        }

        private void UnsubscribeVehicle()
        {
            if (_vehicleDriver != null)
            {
                _vehicleDriver.OnDestinationReached -= HandleVehicleDestinationReached;
            }
        }

        private void Update()
        {
            if (_isVehicleDriving)
            {
                UpdateLineVisual();
                return;
            }

            if (_isDragging)
            {
                UpdateDraggingPath();
            }
        }

        private void Start()
        {
            if (_pointANode != null && _pointBNode != null)
            {
                ResetRoutePlanning();
            }
        }

        public void ConfigureRouteEndpoints(DeliveryRouteNode startNode, DeliveryRouteNode endNode, RouteVehicleDriver driver)
        {
            UnsubscribeVehicle();

            _pointANode = startNode;
            _pointBNode = endNode;
            _vehicleDriver = driver;

            SubscribeVehicle();

            CompletedTrips = 0;
            IsCurrentTargetPointB = true;
            _visitedCells.Clear();
            _isDragging = false;

            ResetRoutePlanning();

            OnRouteEndpointsConfigured?.Invoke(_vehicleDriver);
        }

        public void ResetRoutePlanning()
        {
            IsRouteCompleted = false;
            _isVehicleDriving = false;
            _isDragging = false;
            _totalSmoothedPathLength = 0f;
            _routeCellWaypoints.Clear();
            _smoothedWorldWaypoints.Clear();
            _smoothedWaypointDistances.Clear();

            if (_pointANode != null && _pointBNode != null && _generator != null)
            {
                _pointANode.SnapToRoadNavMesh();
                _pointBNode.SnapToRoadNavMesh();

                Vector3 startPosition = IsCurrentTargetPointB ? _pointANode.RoadNavMeshPosition : _pointBNode.RoadNavMeshPosition;

                if (TryResolveNearestRoadCell(startPosition, out Vector2Int startCell))
                {
                    _routeCellWaypoints.Add(startCell);
                    _lastValidTargetCell = startCell;
                }

                if (_vehicleDriver != null)
                {
                    _vehicleDriver.StopAndReset(startPosition);
                }
            }

            UpdateMarkerVisibility();
            RebuildSmoothedPath();
        }

        private void HandleVehicleDestinationReached()
        {
            _isVehicleDriving = false;
            CompletedTrips++;

            RegisterVisitedCellsFromRoute();
            ClearLineVisual();

            if (CompletedTrips >= _maxTrips)
            {
                if (_pointANode != null)
                {
                    _pointANode.SetMarkerActive(false);
                }

                if (_pointBNode != null)
                {
                    _pointBNode.SetMarkerActive(false);
                }

                return;
            }

            IsCurrentTargetPointB = !IsCurrentTargetPointB;
            ResetRoutePlanning();
        }

        private void RegisterVisitedCellsFromRoute()
        {
            for (int index = 0; index < _routeCellWaypoints.Count; index++)
            {
                _visitedCells.Add(_routeCellWaypoints[index]);
            }
        }

        private void HandlePrimaryPressed(Vector2 screenPosition)
        {
            if (IsRouteCompleted || CompletedTrips >= _maxTrips || _isVehicleDriving)
            {
                return;
            }

            if (TryGetRaycastCell(screenPosition, out Vector2Int clickedCell))
            {
                Vector2Int lastCell = _routeCellWaypoints.Count > 0 ? _routeCellWaypoints[_routeCellWaypoints.Count - 1] : Vector2Int.zero;

                if (_routeCellWaypoints.Count == 0 || clickedCell == lastCell)
                {
                    _isDragging = true;
                }
            }
        }

        private void HandlePrimaryReleased(Vector2 screenPosition)
        {
            if (!_isDragging)
            {
                return;
            }

            _isDragging = false;
            ResetRoutePlanning();
        }

        private void UpdateDraggingPath()
        {
            if (_playerInputHandler == null || _generator == null)
            {
                return;
            }

            Vector2 screenPosition = _playerInputHandler.PointerScreenPosition;

            if (TryGetRaycastCell(screenPosition, out Vector2Int targetCell))
            {
                if (targetCell == _lastValidTargetCell)
                {
                    return;
                }

                _lastValidTargetCell = targetCell;

                Vector2Int currentLastCell = _routeCellWaypoints.Count > 0 ? _routeCellWaypoints[_routeCellWaypoints.Count - 1] : Vector2Int.zero;

                if (targetCell == currentLastCell)
                {
                    return;
                }

                int existingIndex = _routeCellWaypoints.IndexOf(targetCell);

                if (existingIndex >= 0)
                {
                    int removeStartIndex = existingIndex + 1;

                    if (removeStartIndex < _routeCellWaypoints.Count)
                    {
                        _routeCellWaypoints.RemoveRange(removeStartIndex, _routeCellWaypoints.Count - removeStartIndex);
                        RebuildSmoothedPath();
                    }

                    return;
                }

                Vector2Int destinationCell = targetCell;

                if (TryResolveNearestRoadCell(GetCurrentTargetPosition(), out Vector2Int destCell))
                {
                    destinationCell = destCell;
                }

                System.Collections.Generic.List<Vector2Int> cellStepSequence = FindConnectedRoadCellPath(currentLastCell, targetCell, destinationCell);

                if (cellStepSequence != null && cellStepSequence.Count >= 2)
                {
                    for (int index = 1; index < cellStepSequence.Count; index++)
                    {
                        Vector2Int stepCell = cellStepSequence[index];

                        if (_routeCellWaypoints.Contains(stepCell))
                        {
                            break;
                        }

                        _routeCellWaypoints.Add(stepCell);

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
                        OnRouteCompleted?.Invoke(_smoothedWorldWaypoints);

                        if (_autoStartVehicleOnComplete && _vehicleDriver != null)
                        {
                            _vehicleDriver.StartFollowingRoute(_smoothedWorldWaypoints);
                            _isVehicleDriving = true;
                            _isDragging = false;
                            UpdateLineVisual();
                        }
                    }
                }
            }
        }

        private bool TryGetRaycastCell(Vector2 screenPosition, out Vector2Int cell)
        {
            cell = default;

            if (_mainCamera == null)
            {
                return false;
            }

            Ray ray = _mainCamera.ScreenPointToRay(screenPosition);

            if (Physics.Raycast(ray, out RaycastHit hitInfo, _maxRaycastDistance, _roadClickLayers, QueryTriggerInteraction.Collide))
            {
                return TryResolveNearestRoadCell(hitInfo.point, out cell);
            }

            return false;
        }

        private Vector3 GetCurrentTargetPosition()
        {
            if (IsCurrentTargetPointB)
            {
                return _pointBNode != null ? _pointBNode.RoadNavMeshPosition : Vector3.zero;
            }

            return _pointANode != null ? _pointANode.RoadNavMeshPosition : Vector3.zero;
        }

        private Vector3 GetCurrentStartPosition()
        {
            if (IsCurrentTargetPointB)
            {
                return _pointANode != null ? _pointANode.RoadNavMeshPosition : Vector3.zero;
            }

            return _pointBNode != null ? _pointBNode.RoadNavMeshPosition : Vector3.zero;
        }

        private void UpdateMarkerVisibility()
        {
            if (_pointANode == null || _pointBNode == null)
            {
                return;
            }

            if (CompletedTrips >= _maxTrips)
            {
                _pointANode.SetMarkerActive(false);
                _pointBNode.SetMarkerActive(false);
                return;
            }

            if (IsRouteCompleted)
            {
                return;
            }

            if (IsCurrentTargetPointB)
            {
                _pointANode.SetMarkerActive(false);
                _pointBNode.SetMarkerActive(true);
            }
            else
            {
                _pointANode.SetMarkerActive(true);
                _pointBNode.SetMarkerActive(false);
            }
        }

        private bool TryResolveNearestRoadCell(Vector3 worldPosition, out Vector2Int resolvedCell)
        {
            resolvedCell = default;

            if (_generator == null)
            {
                return false;
            }

            Vector2Int gridDimensions = _generator.GetGridDimensions;
            Vector2Int baseCell = _generator.GetCellCoordinatesFromWorldPosition(worldPosition);

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

                    Vector3 candidateWorld = _generator.GetWorldPositionFromCellCoordinates(candidateCell.x, candidateCell.y);
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

            CityBlockPreset preset = _generator.GetSpawnedPresetAt(cellCoordinates.x, cellCoordinates.y);

            return preset != null && preset.RoadCount > 0;
        }

        private System.Collections.Generic.List<Vector2Int> FindConnectedRoadCellPath(Vector2Int startCell, Vector2Int endCell, Vector2Int destinationCell)
        {
            Vector2Int gridDimensions = _generator.GetGridDimensions;
            System.Collections.Generic.Queue<Vector2Int> frontier = new System.Collections.Generic.Queue<Vector2Int>();
            System.Collections.Generic.Dictionary<Vector2Int, Vector2Int> cameFrom = new System.Collections.Generic.Dictionary<Vector2Int, Vector2Int>();

            frontier.Enqueue(startCell);
            cameFrom[startCell] = startCell;

            Direction[] directions = { Direction.North, Direction.East, Direction.South, Direction.West };

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

                CityBlockPreset currentPreset = _generator.GetSpawnedPresetAt(currentCell.x, currentCell.y);
                int currentRotation = _generator.GetSpawnedRotationAt(currentCell.x, currentCell.y);

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

                    CityBlockPreset neighborPreset = _generator.GetSpawnedPresetAt(neighborCell.x, neighborCell.y);
                    int neighborRotation = _generator.GetSpawnedRotationAt(neighborCell.x, neighborCell.y);
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

        private System.Collections.Generic.List<Vector2Int> ReconstructCellPath(System.Collections.Generic.Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int startCell, Vector2Int endCell)
        {
            System.Collections.Generic.List<Vector2Int> reversedPath = new System.Collections.Generic.List<Vector2Int>();
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
            _smoothedWorldWaypoints.Clear();
            _smoothedWaypointDistances.Clear();
            _totalSmoothedPathLength = 0f;

            if (_routeCellWaypoints.Count == 0 || _generator == null)
            {
                UpdateLineVisual();
                return;
            }

            Vector3 startWorldPosition = GetCurrentStartPosition();
            Vector3 endWorldPosition = GetCurrentTargetPosition();

            System.Collections.Generic.List<Vector3> rawCellCenters = new System.Collections.Generic.List<Vector3>(_routeCellWaypoints.Count);

            for (int index = 0; index < _routeCellWaypoints.Count; index++)
            {
                Vector2Int cell = _routeCellWaypoints[index];
                Vector3 centerWorld = _generator.GetWorldPositionFromCellCoordinates(cell.x, cell.y);

                if (index == 0)
                {
                    centerWorld = startWorldPosition;
                }
                else if (IsRouteCompleted && index == _routeCellWaypoints.Count - 1)
                {
                    centerWorld = endWorldPosition;
                }
                else
                {
                    centerWorld = SnapWorldPointToRoad(centerWorld);
                }

                rawCellCenters.Add(centerWorld);
            }

            System.Collections.Generic.List<Vector3> roundedWaypoints = BuildRoundedRoadWaypoints(rawCellCenters);
            System.Collections.Generic.List<Vector3> subdividedWaypoints = SubdivideWaypoints(roundedWaypoints, _lineSegmentLength);

            _smoothedWorldWaypoints.AddRange(subdividedWaypoints);

            RecalculateWaypointDistances();
            UpdateLineVisual();
        }

        private void RecalculateWaypointDistances()
        {
            _smoothedWaypointDistances.Clear();
            _totalSmoothedPathLength = 0f;

            if (_smoothedWorldWaypoints.Count == 0)
            {
                return;
            }

            _smoothedWaypointDistances.Add(0f);

            for (int index = 1; index < _smoothedWorldWaypoints.Count; index++)
            {
                float segmentLength = Vector3.Distance(_smoothedWorldWaypoints[index - 1], _smoothedWorldWaypoints[index]);
                _totalSmoothedPathLength += segmentLength;
                _smoothedWaypointDistances.Add(_totalSmoothedPathLength);
            }
        }

        private System.Collections.Generic.List<Vector3> SubdivideWaypoints(System.Collections.Generic.List<Vector3> points, float maxSegmentLength)
        {
            System.Collections.Generic.List<Vector3> result = new System.Collections.Generic.List<Vector3>();

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

        private System.Collections.Generic.List<Vector3> BuildRoundedRoadWaypoints(System.Collections.Generic.List<Vector3> controlPoints)
        {
            System.Collections.Generic.List<Vector3> result = new System.Collections.Generic.List<Vector3>();

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
            if (NavMesh.SamplePosition(candidatePosition, out NavMeshHit hitInfo, _roadSnapRadius, _roadAreaMask))
            {
                return hitInfo.position;
            }

            if (NavMesh.SamplePosition(candidatePosition, out NavMeshHit fallbackHitInfo, _roadSnapRadius, NavMesh.AllAreas))
            {
                return fallbackHitInfo.position;
            }

            return candidatePosition;
        }

        private float GetVehicleDistanceAlongPath()
        {
            if (_vehicleDriver == null || _smoothedWorldWaypoints.Count < 2)
            {
                return 0f;
            }

            Vector3 vehiclePosition = _vehicleDriver.transform.position;
            Vector3 flatVehiclePosition = new Vector3(vehiclePosition.x, 0f, vehiclePosition.z);

            float closestDistanceSquared = float.MaxValue;
            float closestDistanceAlongPath = 0f;

            for (int index = 1; index < _smoothedWorldWaypoints.Count; index++)
            {
                Vector3 segmentStart = _smoothedWorldWaypoints[index - 1];
                Vector3 segmentEnd = _smoothedWorldWaypoints[index];

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

                    float segmentStartDistance = _smoothedWaypointDistances[index - 1];
                    float segmentEndDistance = _smoothedWaypointDistances[index];
                    closestDistanceAlongPath = Mathf.Lerp(segmentStartDistance, segmentEndDistance, interpolation);
                }
            }

            return closestDistanceAlongPath;
        }

        private void UpdateLineVisual()
        {
            if (_routeLineRenderer == null)
            {
                return;
            }

            if (_smoothedWorldWaypoints.Count == 0)
            {
                _routeLineRenderer.positionCount = 0;
                return;
            }

            float startDistance = 0f;

            if (_isVehicleDriving)
            {
                float vehicleDistance = GetVehicleDistanceAlongPath();
                startDistance = Mathf.Max(0f, vehicleDistance - _lineEraseOffset);
            }

            System.Collections.Generic.List<Vector3> visiblePoints = BuildVisiblePoints(startDistance);

            _routeLineRenderer.useWorldSpace = true;
            _routeLineRenderer.positionCount = visiblePoints.Count;

            for (int index = 0; index < visiblePoints.Count; index++)
            {
                Vector3 point = visiblePoints[index];
                point.y = _lineHeight;
                _routeLineRenderer.SetPosition(index, point);
            }
        }

        private System.Collections.Generic.List<Vector3> BuildVisiblePoints(float startDistance)
        {
            System.Collections.Generic.List<Vector3> points = new System.Collections.Generic.List<Vector3>();

            if (startDistance <= 0f)
            {
                points.AddRange(_smoothedWorldWaypoints);
                return points;
            }

            if (startDistance >= _totalSmoothedPathLength)
            {
                return points;
            }

            int startIndex = FindFirstVisibleWaypointIndex(startDistance);

            if (startIndex >= _smoothedWorldWaypoints.Count)
            {
                return points;
            }

            if (startIndex > 0)
            {
                float previousDistance = _smoothedWaypointDistances[startIndex - 1];
                float currentDistance = _smoothedWaypointDistances[startIndex];
                float segmentLength = currentDistance - previousDistance;

                if (segmentLength > Mathf.Epsilon)
                {
                    float interpolation = (startDistance - previousDistance) / segmentLength;
                    Vector3 previousPoint = _smoothedWorldWaypoints[startIndex - 1];
                    Vector3 currentPoint = _smoothedWorldWaypoints[startIndex];
                    Vector3 clippedPoint = Vector3.Lerp(previousPoint, currentPoint, interpolation);
                    points.Add(clippedPoint);
                }
                else
                {
                    points.Add(_smoothedWorldWaypoints[startIndex]);
                }
            }

            for (int index = startIndex; index < _smoothedWorldWaypoints.Count; index++)
            {
                points.Add(_smoothedWorldWaypoints[index]);
            }

            return points;
        }

        private int FindFirstVisibleWaypointIndex(float distance)
        {
            int low = 0;
            int high = _smoothedWaypointDistances.Count - 1;

            while (low < high)
            {
                int middle = (low + high) / 2;

                if (_smoothedWaypointDistances[middle] < distance)
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

        private void ClearLineVisual()
        {
            if (_routeLineRenderer == null)
            {
                return;
            }

            _routeLineRenderer.positionCount = 0;
        }

        private Vector2Int GetDirectionOffset(Direction direction)
        {
            switch (direction)
            {
                case Direction.North:
                    return Vector2Int.up;
                case Direction.East:
                    return Vector2Int.right;
                case Direction.South:
                    return Vector2Int.down;
                default:
                    return Vector2Int.left;
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