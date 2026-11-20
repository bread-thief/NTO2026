using System;
using System.Collections.Generic;
using _Project.Scripts.Gameplay.Controllers;
using _Project.Scripts.Gameplay.Generators;
using _Project.Scripts.Gameplay.Namers;
using UnityEngine;
using static _Project.Scripts.Gameplay.Obstacles.RoadObstacle;
using Time = _Project.Scripts.Gameplay.Controllers.Time;

namespace _Project.Scripts.Gameplay.Obstacles
{
    public class RoadObstacleController : MonoBehaviour
    {
        private const int DEFAULT_PENALTY_MINUTES = 30;
        private const int MIN_ALTERNATIVE_PATH_DISTANCE = 3;
        private const int MAX_ALTERNATIVE_BFS_NODES = 200;
        private const int MAX_PICK_ATTEMPTS = 32;
        private const float OBSTACLE_SPAWN_HEIGHT = 10f;

        [SerializeField] private TimeController _timeController;
        [SerializeField] private CityGenerator _generator;
        [SerializeField] private CityStreetNamer _streetNamer;
        [SerializeField] private GameObject _obstaclePrefab;
        [SerializeField] private int _penaltyMinutes = DEFAULT_PENALTY_MINUTES;
        [SerializeField] private int _removalDelayMinutes = 60;
        [SerializeField] private string[] _landmarkWords;

        public event Action<RoadObstacle> OnObstacleSpawned;
        public event Action<RoadObstacle> OnObstacleRemoved;
        public event Action<RoadObstacle, int> OnPenaltyApplied;

        private readonly List<RoadObstacle> _activeObstacles = new List<RoadObstacle>();
        private readonly List<RoadObstacle> _obstaclesPendingRemoval = new List<RoadObstacle>();

        public IReadOnlyList<RoadObstacle> ActiveObstacles => _activeObstacles;

        private void Awake()
        {
            if (_timeController == null)
            {
                _timeController = FindAnyObjectByType<TimeController>();
            }

            if (_generator == null)
            {
                _generator = FindAnyObjectByType<CityGenerator>();
            }

            if (_streetNamer == null)
            {
                _streetNamer = FindAnyObjectByType<CityStreetNamer>();
            }

            if (_landmarkWords == null || _landmarkWords.Length == 0)
            {
                _landmarkWords = new string[]
                {
                    "шоссе Мира",
                    "проспект Победы",
                    "улица Ленина",
                    "переулок Садовый",
                    "площадь Свободы"
                };
            }
        }

        private void OnEnable()
        {
            if (_timeController != null)
            {
                _timeController.OnTimeChanged += HandleTimeChanged;
                _timeController.OnShiftEnded += HandleShiftEnded;
            }
        }

        private void OnDisable()
        {
            if (_timeController != null)
            {
                _timeController.OnTimeChanged -= HandleTimeChanged;
                _timeController.OnShiftEnded -= HandleShiftEnded;
            }
        }

        public void SpawnObstacleOnRoute(IReadOnlyList<Vector3> travelledRoute, Vector2Int targetCell)
        {
            if (_generator == null || _obstaclePrefab == null || travelledRoute == null || travelledRoute.Count < 2)
            {
                return;
            }

            if (!TryPickObstacleCell(travelledRoute, targetCell, out Vector2Int spawnCell, out ObstaclePlacementType placementType))
            {
                return;
            }

            if (!HasAlternativeRoute(spawnCell, targetCell))
            {
                return;
            }

            string blockedStreet = _streetNamer != null ? _streetNamer.GetStreetAddressAtCell(spawnCell) : string.Empty;

            if (string.IsNullOrWhiteSpace(blockedStreet))
            {
                blockedStreet = $"улица {spawnCell.x}-{spawnCell.y}";
            }

            string landmark = PickLandmark();

            Time removalTime = _timeController != null
                ? _timeController.CurrentTime.AddMinutes(_removalDelayMinutes)
                : new Time(0, 0);

            Vector3 worldPosition = _generator.GetWorldPositionFromCellCoordinates(spawnCell.x, spawnCell.y);
            worldPosition.y = OBSTACLE_SPAWN_HEIGHT;

            GameObject obstacleObject = Instantiate(_obstaclePrefab, worldPosition, Quaternion.identity);

            RoadObstacle obstacle = obstacleObject.GetComponent<RoadObstacle>();

            if (obstacle == null)
            {
                obstacle = obstacleObject.AddComponent<RoadObstacle>();
            }

            obstacle.Initialize(blockedStreet, landmark, placementType, removalTime, _penaltyMinutes);
            obstacle.OnPlayerHit += HandleObstacleHit;

            _activeObstacles.Add(obstacle);
            OnObstacleSpawned?.Invoke(obstacle);
        }

        public void ClearAllObstacles()
        {
            for (int index = _activeObstacles.Count - 1; index >= 0; index--)
            {
                RoadObstacle obstacle = _activeObstacles[index];

                if (obstacle == null)
                {
                    continue;
                }

                obstacle.OnPlayerHit -= HandleObstacleHit;
                obstacle.Remove();
                Destroy(obstacle.gameObject);
            }

            _activeObstacles.Clear();
        }

        private bool TryPickObstacleCell(
            IReadOnlyList<Vector3> route,
            Vector2Int targetCell,
            out Vector2Int spawnCell,
            out ObstaclePlacementType placementType)
        {
            spawnCell = default;
            placementType = ObstaclePlacementType.Unknown;

            List<Vector2Int> candidateCells = new List<Vector2Int>();
            HashSet<Vector2Int> seenCells = new HashSet<Vector2Int>();

            for (int index = 1; index < route.Count - 1; index++)
            {
                Vector3 waypoint = route[index];
                Vector2Int cell = _generator.GetCellCoordinatesFromWorldPosition(waypoint);

                if (cell == targetCell)
                {
                    continue;
                }

                if (!seenCells.Add(cell))
                {
                    continue;
                }

                CityBlockPreset preset = _generator.GetSpawnedPresetAt(cell.x, cell.y);

                if (preset == null || preset.RoadCount == 0)
                {
                    continue;
                }

                candidateCells.Add(cell);
            }

            if (candidateCells.Count == 0)
            {
                candidateCells = CollectNearbyRoadCells(route, targetCell, seenCells);
            }

            if (candidateCells.Count == 0)
            {
                return false;
            }

            for (int attempt = 0; attempt < MAX_PICK_ATTEMPTS; attempt++)
            {
                Vector2Int candidate = candidateCells[UnityEngine.Random.Range(0, candidateCells.Count)];
                CityBlockPreset preset = _generator.GetSpawnedPresetAt(candidate.x, candidate.y);

                if (preset == null || preset.RoadCount == 0)
                {
                    continue;
                }

                spawnCell = candidate;
                placementType = preset.RoadCount >= 3
                    ? ObstaclePlacementType.Intersection
                    : ObstaclePlacementType.MiddleOfStreet;

                return true;
            }

            return false;
        }

        private List<Vector2Int> CollectNearbyRoadCells(
            IReadOnlyList<Vector3> route,
            Vector2Int targetCell,
            HashSet<Vector2Int> seenCells)
        {
            List<Vector2Int> result = new List<Vector2Int>();
            Vector2Int gridDimensions = _generator.GetGridDimensions;

            for (int routeIndex = 0; routeIndex < route.Count; routeIndex++)
            {
                Vector2Int center = _generator.GetCellCoordinatesFromWorldPosition(route[routeIndex]);

                for (int offsetX = -2; offsetX <= 2; offsetX++)
                {
                    for (int offsetY = -2; offsetY <= 2; offsetY++)
                    {
                        Vector2Int cell = new Vector2Int(center.x + offsetX, center.y + offsetY);

                        if (cell == targetCell)
                        {
                            continue;
                        }

                        if (cell.x < 0 || cell.x >= gridDimensions.x || cell.y < 0 || cell.y >= gridDimensions.y)
                        {
                            continue;
                        }

                        CityBlockPreset preset = _generator.GetSpawnedPresetAt(cell.x, cell.y);

                        if (preset == null || preset.RoadCount == 0)
                        {
                            continue;
                        }

                        if (!seenCells.Add(cell))
                        {
                            continue;
                        }

                        result.Add(cell);
                    }
                }
            }

            return result;
        }

        private bool HasAlternativeRoute(Vector2Int blockedCell, Vector2Int targetCell)
        {
            if (_generator == null)
            {
                return false;
            }

            Vector2Int gridDimensions = _generator.GetGridDimensions;
            Queue<Vector2Int> frontier = new Queue<Vector2Int>();
            HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

            frontier.Enqueue(blockedCell);
            visited.Add(blockedCell);

            int exploredNodes = 0;

            while (frontier.Count > 0)
            {
                Vector2Int current = frontier.Dequeue();
                exploredNodes++;

                if (exploredNodes >= MAX_ALTERNATIVE_BFS_NODES)
                {
                    break;
                }

                CityBlockPreset currentPreset = _generator.GetSpawnedPresetAt(current.x, current.y);
                int currentRotation = _generator.GetSpawnedRotationAt(current.x, current.y);

                if (currentPreset == null)
                {
                    continue;
                }

                Direction[] directions = { Direction.North, Direction.East, Direction.South, Direction.West };

                for (int index = 0; index < directions.Length; index++)
                {
                    Direction direction = directions[index];

                    if (!currentPreset.HasRoad(direction, currentRotation))
                    {
                        continue;
                    }

                    Vector2Int neighbor = current + GetDirectionOffset(direction);

                    if (neighbor.x < 0 || neighbor.x >= gridDimensions.x || neighbor.y < 0 || neighbor.y >= gridDimensions.y)
                    {
                        continue;
                    }

                    if (visited.Contains(neighbor))
                    {
                        continue;
                    }

                    CityBlockPreset neighborPreset = _generator.GetSpawnedPresetAt(neighbor.x, neighbor.y);

                    if (neighborPreset == null || neighborPreset.RoadCount == 0)
                    {
                        continue;
                    }

                    int neighborRotation = _generator.GetSpawnedRotationAt(neighbor.x, neighbor.y);
                    Direction opposite = GetOppositeDirection(direction);

                    if (!neighborPreset.HasRoad(opposite, neighborRotation))
                    {
                        continue;
                    }

                    visited.Add(neighbor);
                    frontier.Enqueue(neighbor);
                }
            }

            return visited.Count >= MIN_ALTERNATIVE_PATH_DISTANCE;
        }

        private string PickLandmark()
        {
            if (_landmarkWords == null || _landmarkWords.Length == 0)
            {
                return string.Empty;
            }

            return _landmarkWords[UnityEngine.Random.Range(0, _landmarkWords.Length)];
        }

        private void HandleObstacleHit(RoadObstacle obstacle, int penaltyMinutes)
        {
            if (obstacle == null || _timeController == null)
            {
                return;
            }

            _timeController.AddMinutes(penaltyMinutes);
            OnPenaltyApplied?.Invoke(obstacle, penaltyMinutes);

            obstacle.OnPlayerHit -= HandleObstacleHit;
            _activeObstacles.Remove(obstacle);
            obstacle.Remove();
            Destroy(obstacle.gameObject);
        }

        private void HandleTimeChanged(Time currentTime)
        {
            _obstaclesPendingRemoval.Clear();

            for (int index = 0; index < _activeObstacles.Count; index++)
            {
                RoadObstacle obstacle = _activeObstacles[index];

                if (obstacle == null)
                {
                    continue;
                }

                if (obstacle.ShouldBeRemoved(currentTime))
                {
                    _obstaclesPendingRemoval.Add(obstacle);
                }
            }

            for (int index = 0; index < _obstaclesPendingRemoval.Count; index++)
            {
                RoadObstacle obstacle = _obstaclesPendingRemoval[index];

                obstacle.OnPlayerHit -= HandleObstacleHit;
                _activeObstacles.Remove(obstacle);
                obstacle.Remove();
                OnObstacleRemoved?.Invoke(obstacle);
                Destroy(obstacle.gameObject);
            }
        }

        private void HandleShiftEnded()
        {
            ClearAllObstacles();
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