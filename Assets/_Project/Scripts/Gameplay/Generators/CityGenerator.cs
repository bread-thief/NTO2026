using System;
using System.Collections.Generic;
using UnityEngine;

namespace _Project.Scripts.Gameplay.Generators
{
    public class CityGenerator : MonoBehaviour
    {
        private const float TARGET_ROAD_COVERAGE = 0.75f;
        private const int OUTER_ROAD_EXITS = 4;
        private const int MAX_GENERATION_ATTEMPTS = 80;
        private const int MAX_DEAD_END_COUNT = 3;
        private const float DEAD_END_SPAWN_CHANCE = 0.08f;

        [SerializeField] private Vector2 _size = new Vector2(10f, 10f);
        [SerializeField] private float _cellSize = 10f;
        [SerializeField] private bool _generateOnStart = true;
        [SerializeField] private List<CityBlockPreset> _roadPresets = new List<CityBlockPreset>();
        [SerializeField] private List<CityBlockPreset> _emptyPresets = new List<CityBlockPreset>();

        public event Action OnGenerated;

        private CityBlockPreset[,] _spawnedPresets;
        private int[,] _spawnedRotations;

        private struct CellPlan
        {
            public CityBlockPreset Preset;
            public int RotationSteps;
            public bool IsPlaced;
        }

        private struct CandidateOption
        {
            public CityBlockPreset Preset;
            public int RotationSteps;
        }

        private int GridWidth => Mathf.Max(3, Mathf.RoundToInt(_size.x));

        private int GridHeight => Mathf.Max(3, Mathf.RoundToInt(_size.y));

        public float CellSize => _cellSize;
        public Vector2Int GetGridDimensions => new Vector2Int(GridWidth, GridHeight);

        private void Start()
        {
            if (_generateOnStart)
            {
                Generate();
            }
        }

        [ContextMenu("Generate City")]
        public void Generate()
        {
            ClearCity();

            if (_roadPresets == null || _roadPresets.Count == 0)
            {
                return;
            }

            if (_emptyPresets == null || _emptyPresets.Count == 0)
            {
                return;
            }

            int width = GridWidth;
            int height = GridHeight;

            for (int attempt = 0; attempt < MAX_GENERATION_ATTEMPTS; attempt++)
            {
                if (TryBuildCityGrid(width, height, out CellPlan[,] grid))
                {
                    InstantiateGrid(grid, width, height);
                    OnGenerated?.Invoke();
                    return;
                }
            }
        }

        [ContextMenu("Clear City")]
        public void ClearCity()
        {
            _spawnedPresets = null;
            _spawnedRotations = null;

            for (int childIndex = transform.childCount - 1; childIndex >= 0; childIndex--)
            {
                Transform childTransform = transform.GetChild(childIndex);

                if (Application.isPlaying)
                {
                    Destroy(childTransform.gameObject);
                }
                else
                {
                    DestroyImmediate(childTransform.gameObject);
                }
            }
        }

        public CityBlockPreset GetSpawnedPresetAt(int columnIndex, int rowIndex)
        {
            if (_spawnedPresets == null)
            {
                return null;
            }

            if (columnIndex < 0 || columnIndex >= _spawnedPresets.GetLength(0))
            {
                return null;
            }

            if (rowIndex < 0 || rowIndex >= _spawnedPresets.GetLength(1))
            {
                return null;
            }

            return _spawnedPresets[columnIndex, rowIndex];
        }

        public int GetSpawnedRotationAt(int columnIndex, int rowIndex)
        {
            if (_spawnedRotations == null)
            {
                return 0;
            }

            if (columnIndex < 0 || columnIndex >= _spawnedRotations.GetLength(0))
            {
                return 0;
            }

            if (rowIndex < 0 || rowIndex >= _spawnedRotations.GetLength(1))
            {
                return 0;
            }

            return _spawnedRotations[columnIndex, rowIndex];
        }

        public Vector3 GetWorldPositionFromCellCoordinates(int columnIndex, int rowIndex)
        {
            return transform.position + new Vector3(columnIndex * _cellSize, 0f, rowIndex * _cellSize);
        }

        public Vector2Int GetCellCoordinatesFromWorldPosition(Vector3 worldPosition)
        {
            Vector3 localPosition = worldPosition - transform.position;
            int columnIndex = Mathf.RoundToInt(localPosition.x / _cellSize);
            int rowIndex = Mathf.RoundToInt(localPosition.z / _cellSize);

            return new Vector2Int(columnIndex, rowIndex);
        }

        private bool TryBuildCityGrid(int width, int height, out CellPlan[,] grid)
        {
            grid = new CellPlan[width, height];

            bool[,,] connections = new bool[width, height, 4];
            bool[,] hasRoad = new bool[width, height];

            BuildDenseBlockNetwork(connections, hasRoad, width, height);

            ExpandRoadCoverage(connections, hasRoad, width, height);

            bool hasDeadEndPreset = HasPresetWithRoadCount(1);

            if (hasDeadEndPreset)
            {
                TrimExcessDeadEnds(connections, hasRoad, width, height, MAX_DEAD_END_COUNT);
                AddRareDeadEnds(connections, hasRoad, width, height);
            }
            else
            {
                TrimExcessDeadEnds(connections, hasRoad, width, height, 0);
            }

            AddOuterWorldExits(connections, hasRoad, width, height);

            if (!VerifySingleConnectedComponent(connections, hasRoad, width, height))
            {
                return false;
            }

            return ResolvePresetsForGrid(grid, connections, hasRoad, width, height);
        }

        private void BuildDenseBlockNetwork(bool[,,] connections, bool[,] hasRoad, int width, int height)
        {
            List<int> verticalLines = PickRoadAxisLines(width);
            List<int> horizontalLines = PickRoadAxisLines(height);

            for (int lineIndex = 0; lineIndex < verticalLines.Count; lineIndex++)
            {
                int columnIndex = verticalLines[lineIndex];

                for (int rowIndex = 0; rowIndex < height - 1; rowIndex++)
                {
                    AddRoadEdge(connections, hasRoad, new Vector2Int(columnIndex, rowIndex), new Vector2Int(columnIndex, rowIndex + 1));
                }
            }

            for (int lineIndex = 0; lineIndex < horizontalLines.Count; lineIndex++)
            {
                int rowIndex = horizontalLines[lineIndex];

                for (int columnIndex = 0; columnIndex < width - 1; columnIndex++)
                {
                    AddRoadEdge(connections, hasRoad, new Vector2Int(columnIndex, rowIndex), new Vector2Int(columnIndex + 1, rowIndex));
                }
            }
        }

        private List<int> PickRoadAxisLines(int axisLength)
        {
            List<int> lines = new List<int>();
            int cursor = UnityEngine.Random.Range(0, 2);

            while (cursor < axisLength)
            {
                lines.Add(cursor);
                int step = UnityEngine.Random.value < 0.7f ? 2 : 3;
                cursor += step;
            }

            if (lines.Count == 0)
            {
                lines.Add(0);
            }

            int lastLine = lines[lines.Count - 1];

            if (axisLength - 1 - lastLine >= 2)
            {
                lines.Add(axisLength - 1);
            }

            return lines;
        }

        private void ExpandRoadCoverage(bool[,,] connections, bool[,] hasRoad, int width, int height)
        {
            int targetRoadCells = Mathf.RoundToInt(width * height * TARGET_ROAD_COVERAGE);
            int currentRoadCells = CountRoadCells(hasRoad, width, height);
            int safetyIterations = width * height * 6;

            while (currentRoadCells < targetRoadCells && safetyIterations > 0)
            {
                safetyIterations--;

                List<Vector2Int> expandableNodes = new List<Vector2Int>();

                for (int columnIndex = 0; columnIndex < width; columnIndex++)
                {
                    for (int rowIndex = 0; rowIndex < height; rowIndex++)
                    {
                        if (hasRoad[columnIndex, rowIndex])
                        {
                            expandableNodes.Add(new Vector2Int(columnIndex, rowIndex));
                        }
                    }
                }

                if (expandableNodes.Count == 0)
                {
                    break;
                }

                Vector2Int baseNode = expandableNodes[UnityEngine.Random.Range(0, expandableNodes.Count)];
                Vector2Int[] offsets = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
                ShuffleOffsets(offsets);

                for (int offsetIndex = 0; offsetIndex < offsets.Length; offsetIndex++)
                {
                    Vector2Int stepOffset = offsets[offsetIndex];
                    Vector2Int firstNode = baseNode + stepOffset;
                    Vector2Int secondNode = firstNode + stepOffset;

                    if (IsInsideGrid(secondNode, width, height) && !hasRoad[firstNode.x, firstNode.y] && hasRoad[secondNode.x, secondNode.y])
                    {
                        AddRoadEdge(connections, hasRoad, baseNode, firstNode);
                        AddRoadEdge(connections, hasRoad, firstNode, secondNode);
                        currentRoadCells = CountRoadCells(hasRoad, width, height);
                        break;
                    }

                    if (IsInsideGrid(firstNode, width, height) && !hasRoad[firstNode.x, firstNode.y])
                    {
                        Vector2Int perpendicularLeft = new Vector2Int(-stepOffset.y, stepOffset.x);
                        Vector2Int perpendicularRight = new Vector2Int(stepOffset.y, -stepOffset.x);
                        Vector2Int sideTarget = UnityEngine.Random.value < 0.5f ? firstNode + perpendicularLeft : firstNode + perpendicularRight;

                        if (IsInsideGrid(sideTarget, width, height) && hasRoad[sideTarget.x, sideTarget.y])
                        {
                            AddRoadEdge(connections, hasRoad, baseNode, firstNode);
                            AddRoadEdge(connections, hasRoad, firstNode, sideTarget);
                            currentRoadCells = CountRoadCells(hasRoad, width, height);
                            break;
                        }
                    }
                }
            }
        }

        private void AddRareDeadEnds(bool[,,] connections, bool[,] hasRoad, int width, int height)
        {
            int currentDeadEnds = CountCurrentDeadEnds(connections, hasRoad, width, height);

            if (currentDeadEnds >= MAX_DEAD_END_COUNT)
            {
                return;
            }

            List<Vector2Int> candidates = new List<Vector2Int>();

            for (int columnIndex = 0; columnIndex < width; columnIndex++)
            {
                for (int rowIndex = 0; rowIndex < height; rowIndex++)
                {
                    if (!hasRoad[columnIndex, rowIndex])
                    {
                        continue;
                    }

                    int degree = GetNodeDegree(connections, columnIndex, rowIndex);

                    if (degree == 2 || degree == 3)
                    {
                        candidates.Add(new Vector2Int(columnIndex, rowIndex));
                    }
                }
            }

            ShuffleNodeList(candidates);

            for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
            {
                if (currentDeadEnds >= MAX_DEAD_END_COUNT)
                {
                    break;
                }

                if (UnityEngine.Random.value > DEAD_END_SPAWN_CHANCE)
                {
                    continue;
                }

                Vector2Int baseNode = candidates[candidateIndex];
                Vector2Int[] offsets = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
                ShuffleOffsets(offsets);

                for (int offsetIndex = 0; offsetIndex < offsets.Length; offsetIndex++)
                {
                    Vector2Int targetNode = baseNode + offsets[offsetIndex];

                    if (!IsInsideGrid(targetNode, width, height) || hasRoad[targetNode.x, targetNode.y])
                    {
                        continue;
                    }

                    AddRoadEdge(connections, hasRoad, baseNode, targetNode);
                    currentDeadEnds++;
                    break;
                }
            }
        }

        private void TrimExcessDeadEnds(bool[,,] connections, bool[,] hasRoad, int width, int height, int allowedDeadEnds)
        {
            bool changed = true;
            int safetyIterations = width * height * 4;

            while (changed && safetyIterations > 0)
            {
                safetyIterations--;
                changed = false;

                int currentDeadEnds = CountCurrentDeadEnds(connections, hasRoad, width, height);

                if (currentDeadEnds <= allowedDeadEnds)
                {
                    break;
                }

                for (int columnIndex = 0; columnIndex < width; columnIndex++)
                {
                    for (int rowIndex = 0; rowIndex < height; rowIndex++)
                    {
                        if (!hasRoad[columnIndex, rowIndex])
                        {
                            continue;
                        }

                        if (GetNodeDegree(connections, columnIndex, rowIndex) != 1)
                        {
                            continue;
                        }

                        RemoveSingleNodeEdges(connections, hasRoad, columnIndex, rowIndex, width, height);
                        changed = true;

                        if (CountCurrentDeadEnds(connections, hasRoad, width, height) <= allowedDeadEnds)
                        {
                            return;
                        }
                    }
                }
            }
        }

        private void AddOuterWorldExits(bool[,,] connections, bool[,] hasRoad, int width, int height)
        {
            List<KeyValuePair<Vector2Int, Direction>> borderOptions = new List<KeyValuePair<Vector2Int, Direction>>();

            for (int columnIndex = 0; columnIndex < width; columnIndex++)
            {
                if (hasRoad[columnIndex, height - 1])
                {
                    borderOptions.Add(new KeyValuePair<Vector2Int, Direction>(new Vector2Int(columnIndex, height - 1), Direction.North));
                }

                if (hasRoad[columnIndex, 0])
                {
                    borderOptions.Add(new KeyValuePair<Vector2Int, Direction>(new Vector2Int(columnIndex, 0), Direction.South));
                }
            }

            for (int rowIndex = 0; rowIndex < height; rowIndex++)
            {
                if (hasRoad[width - 1, rowIndex])
                {
                    borderOptions.Add(new KeyValuePair<Vector2Int, Direction>(new Vector2Int(width - 1, rowIndex), Direction.East));
                }

                if (hasRoad[0, rowIndex])
                {
                    borderOptions.Add(new KeyValuePair<Vector2Int, Direction>(new Vector2Int(0, rowIndex), Direction.West));
                }
            }

            for (int index = borderOptions.Count - 1; index > 0; index--)
            {
                int swapIndex = UnityEngine.Random.Range(0, index + 1);
                KeyValuePair<Vector2Int, Direction> temporary = borderOptions[index];
                borderOptions[index] = borderOptions[swapIndex];
                borderOptions[swapIndex] = temporary;
            }

            int exitsToCreate = Mathf.Min(OUTER_ROAD_EXITS, borderOptions.Count);

            for (int exitIndex = 0; exitIndex < exitsToCreate; exitIndex++)
            {
                Vector2Int cell = borderOptions[exitIndex].Key;
                Direction exitDirection = borderOptions[exitIndex].Value;
                connections[cell.x, cell.y, (int)exitDirection] = true;
            }
        }

        private bool VerifySingleConnectedComponent(bool[,,] connections, bool[,] hasRoad, int width, int height)
        {
            Vector2Int startNode = new Vector2Int(-1, -1);
            int totalRoadNodes = 0;

            for (int columnIndex = 0; columnIndex < width; columnIndex++)
            {
                for (int rowIndex = 0; rowIndex < height; rowIndex++)
                {
                    if (hasRoad[columnIndex, rowIndex])
                    {
                        totalRoadNodes++;

                        if (startNode.x < 0)
                        {
                            startNode = new Vector2Int(columnIndex, rowIndex);
                        }
                    }
                }
            }

            if (totalRoadNodes == 0)
            {
                return false;
            }

            HashSet<Vector2Int> visitedNodes = new HashSet<Vector2Int>();
            Queue<Vector2Int> pendingQueue = new Queue<Vector2Int>();

            visitedNodes.Add(startNode);
            pendingQueue.Enqueue(startNode);

            while (pendingQueue.Count > 0)
            {
                Vector2Int current = pendingQueue.Dequeue();

                for (int directionIndex = 0; directionIndex < 4; directionIndex++)
                {
                    if (!connections[current.x, current.y, directionIndex])
                    {
                        continue;
                    }

                    Vector2Int neighbor = current + GetOffsetFromDirection((Direction)directionIndex);

                    if (!IsInsideGrid(neighbor, width, height) || !hasRoad[neighbor.x, neighbor.y])
                    {
                        continue;
                    }

                    if (!visitedNodes.Contains(neighbor))
                    {
                        visitedNodes.Add(neighbor);
                        pendingQueue.Enqueue(neighbor);
                    }
                }
            }

            return visitedNodes.Count == totalRoadNodes;
        }

        private bool ResolvePresetsForGrid(CellPlan[,] grid, bool[,,] connections, bool[,] hasRoad, int width, int height)
        {
            for (int columnIndex = 0; columnIndex < width; columnIndex++)
            {
                for (int rowIndex = 0; rowIndex < height; rowIndex++)
                {
                    if (hasRoad[columnIndex, rowIndex])
                    {
                        bool[] requiredSides = new bool[4];

                        for (int directionIndex = 0; directionIndex < 4; directionIndex++)
                        {
                            requiredSides[directionIndex] = connections[columnIndex, rowIndex, directionIndex];
                        }

                        List<CandidateOption> roadCandidates = FindMatchingCandidates(_roadPresets, requiredSides);

                        if (roadCandidates.Count == 0)
                        {
                            return false;
                        }

                        CandidateOption chosenRoad = PickWeightedCandidate(roadCandidates);

                        grid[columnIndex, rowIndex] = new CellPlan
                        {
                            Preset = chosenRoad.Preset,
                            RotationSteps = chosenRoad.RotationSteps,
                            IsPlaced = true
                        };
                    }
                    else
                    {
                        CandidateOption chosenEmpty = PickRandomEmptyCandidate(connections, hasRoad, columnIndex, rowIndex, width, height);

                        if (chosenEmpty.Preset == null)
                        {
                            return false;
                        }

                        grid[columnIndex, rowIndex] = new CellPlan
                        {
                            Preset = chosenEmpty.Preset,
                            RotationSteps = chosenEmpty.RotationSteps,
                            IsPlaced = true
                        };
                    }
                }
            }

            return true;
        }

        private CandidateOption PickRandomEmptyCandidate(bool[,,] connections, bool[,] hasRoad, int columnIndex, int rowIndex, int width, int height)
        {
            List<CityBlockPreset> validEmptyPresets = new List<CityBlockPreset>();

            for (int presetIndex = 0; presetIndex < _emptyPresets.Count; presetIndex++)
            {
                CityBlockPreset preset = _emptyPresets[presetIndex];

                if (preset != null)
                {
                    validEmptyPresets.Add(preset);
                }
            }

            if (validEmptyPresets.Count == 0)
            {
                return default;
            }

            CityBlockPreset selectedPreset = PickWeightedPreset(validEmptyPresets);
            int rotationSteps = FindRotationFacingRoad(hasRoad, columnIndex, rowIndex, width, height);

            return new CandidateOption
            {
                Preset = selectedPreset,
                RotationSteps = rotationSteps
            };
        }

        private int FindRotationFacingRoad(bool[,] hasRoad, int columnIndex, int rowIndex, int width, int height)
        {
            List<int> roadDirections = new List<int>();

            if (rowIndex + 1 < height && hasRoad[columnIndex, rowIndex + 1])
            {
                roadDirections.Add(0);
            }

            if (columnIndex + 1 < width && hasRoad[columnIndex + 1, rowIndex])
            {
                roadDirections.Add(1);
            }

            if (rowIndex - 1 >= 0 && hasRoad[columnIndex, rowIndex - 1])
            {
                roadDirections.Add(2);
            }

            if (columnIndex - 1 >= 0 && hasRoad[columnIndex - 1, rowIndex])
            {
                roadDirections.Add(3);
            }

            if (roadDirections.Count > 0)
            {
                return roadDirections[UnityEngine.Random.Range(0, roadDirections.Count)];
            }

            return UnityEngine.Random.Range(0, 4);
        }

        private List<CandidateOption> FindMatchingCandidates(List<CityBlockPreset> sourcePresets, bool[] requiredSides)
        {
            List<CandidateOption> matching = new List<CandidateOption>();

            for (int presetIndex = 0; presetIndex < sourcePresets.Count; presetIndex++)
            {
                CityBlockPreset preset = sourcePresets[presetIndex];

                if (preset == null)
                {
                    continue;
                }

                for (int rotationSteps = 0; rotationSteps < 4; rotationSteps++)
                {
                    bool isMatch = true;

                    for (int directionIndex = 0; directionIndex < 4; directionIndex++)
                    {
                        if (preset.HasRoad((Direction)directionIndex, rotationSteps) != requiredSides[directionIndex])
                        {
                            isMatch = false;
                            break;
                        }
                    }

                    if (isMatch)
                    {
                        matching.Add(new CandidateOption
                        {
                            Preset = preset,
                            RotationSteps = rotationSteps
                        });
                    }
                }
            }

            return matching;
        }

        private CandidateOption PickWeightedCandidate(List<CandidateOption> candidates)
        {
            int totalWeight = 0;

            for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
            {
                totalWeight += candidates[candidateIndex].Preset.Weight;
            }

            int randomRoll = UnityEngine.Random.Range(0, totalWeight);
            int accumulatedWeight = 0;

            for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
            {
                accumulatedWeight += candidates[candidateIndex].Preset.Weight;

                if (randomRoll < accumulatedWeight)
                {
                    return candidates[candidateIndex];
                }
            }

            return candidates[candidates.Count - 1];
        }

        private CityBlockPreset PickWeightedPreset(List<CityBlockPreset> presets)
        {
            int totalWeight = 0;

            for (int presetIndex = 0; presetIndex < presets.Count; presetIndex++)
            {
                totalWeight += presets[presetIndex].Weight;
            }

            int randomRoll = UnityEngine.Random.Range(0, totalWeight);
            int accumulatedWeight = 0;

            for (int presetIndex = 0; presetIndex < presets.Count; presetIndex++)
            {
                accumulatedWeight += presets[presetIndex].Weight;

                if (randomRoll < accumulatedWeight)
                {
                    return presets[presetIndex];
                }
            }

            return presets[presets.Count - 1];
        }

        private void InstantiateGrid(CellPlan[,] grid, int width, int height)
        {
            _spawnedPresets = new CityBlockPreset[width, height];
            _spawnedRotations = new int[width, height];

            for (int columnIndex = 0; columnIndex < width; columnIndex++)
            {
                for (int rowIndex = 0; rowIndex < height; rowIndex++)
                {
                    CellPlan plan = grid[columnIndex, rowIndex];

                    if (!plan.IsPlaced || plan.Preset == null)
                    {
                        continue;
                    }

                    Vector3 worldPosition = GetWorldPositionFromCellCoordinates(columnIndex, rowIndex);
                    Quaternion worldRotation = Quaternion.Euler(0f, plan.RotationSteps * 90f, 0f);

                    Instantiate(plan.Preset, worldPosition, worldRotation, transform);

                    _spawnedPresets[columnIndex, rowIndex] = plan.Preset;
                    _spawnedRotations[columnIndex, rowIndex] = plan.RotationSteps;
                }
            }
        }

        private void AddRoadEdge(bool[,,] connections, bool[,] hasRoad, Vector2Int firstNode, Vector2Int secondNode)
        {
            Direction forwardDirection = GetDirectionBetween(firstNode, secondNode);
            Direction backwardDirection = GetOppositeDirection(forwardDirection);

            connections[firstNode.x, firstNode.y, (int)forwardDirection] = true;
            connections[secondNode.x, secondNode.y, (int)backwardDirection] = true;

            hasRoad[firstNode.x, firstNode.y] = true;
            hasRoad[secondNode.x, secondNode.y] = true;
        }

        private void RemoveSingleNodeEdges(bool[,,] connections, bool[,] hasRoad, int columnIndex, int rowIndex, int width, int height)
        {
            Vector2Int current = new Vector2Int(columnIndex, rowIndex);

            for (int directionIndex = 0; directionIndex < 4; directionIndex++)
            {
                if (!connections[columnIndex, rowIndex, directionIndex])
                {
                    continue;
                }

                connections[columnIndex, rowIndex, directionIndex] = false;

                Direction direction = (Direction)directionIndex;
                Vector2Int neighbor = current + GetOffsetFromDirection(direction);

                if (IsInsideGrid(neighbor, width, height))
                {
                    Direction oppositeDirection = GetOppositeDirection(direction);
                    connections[neighbor.x, neighbor.y, (int)oppositeDirection] = false;
                }
            }

            hasRoad[columnIndex, rowIndex] = false;
        }

        private int CountRoadCells(bool[,] hasRoad, int width, int height)
        {
            int count = 0;

            for (int columnIndex = 0; columnIndex < width; columnIndex++)
            {
                for (int rowIndex = 0; rowIndex < height; rowIndex++)
                {
                    if (hasRoad[columnIndex, rowIndex])
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private int CountCurrentDeadEnds(bool[,,] connections, bool[,] hasRoad, int width, int height)
        {
            int count = 0;

            for (int columnIndex = 0; columnIndex < width; columnIndex++)
            {
                for (int rowIndex = 0; rowIndex < height; rowIndex++)
                {
                    if (hasRoad[columnIndex, rowIndex] && GetNodeDegree(connections, columnIndex, rowIndex) == 1)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private int GetNodeDegree(bool[,,] connections, int columnIndex, int rowIndex)
        {
            int degree = 0;

            for (int directionIndex = 0; directionIndex < 4; directionIndex++)
            {
                if (connections[columnIndex, rowIndex, directionIndex])
                {
                    degree++;
                }
            }

            return degree;
        }

        private bool HasPresetWithRoadCount(int targetRoadCount)
        {
            for (int presetIndex = 0; presetIndex < _roadPresets.Count; presetIndex++)
            {
                CityBlockPreset preset = _roadPresets[presetIndex];

                if (preset != null && preset.RoadCount == targetRoadCount)
                {
                    return true;
                }
            }

            return false;
        }

        private void ShuffleOffsets(Vector2Int[] offsets)
        {
            for (int index = offsets.Length - 1; index > 0; index--)
            {
                int swapIndex = UnityEngine.Random.Range(0, index + 1);
                Vector2Int temporary = offsets[index];
                offsets[index] = offsets[swapIndex];
                offsets[swapIndex] = temporary;
            }
        }

        private void ShuffleNodeList(List<Vector2Int> nodes)
        {
            for (int index = nodes.Count - 1; index > 0; index--)
            {
                int swapIndex = UnityEngine.Random.Range(0, index + 1);
                Vector2Int temporary = nodes[index];
                nodes[index] = nodes[swapIndex];
                nodes[swapIndex] = temporary;
            }
        }

        private Direction GetDirectionBetween(Vector2Int fromNode, Vector2Int toNode)
        {
            Vector2Int difference = toNode - fromNode;

            if (difference == Vector2Int.up)
            {
                return Direction.North;
            }

            if (difference == Vector2Int.right)
            {
                return Direction.East;
            }

            if (difference == Vector2Int.down)
            {
                return Direction.South;
            }

            return Direction.West;
        }

        private Direction GetOppositeDirection(Direction direction)
        {
            if (direction == Direction.North)
            {
                return Direction.South;
            }

            if (direction == Direction.East)
            {
                return Direction.West;
            }

            if (direction == Direction.South)
            {
                return Direction.North;
            }

            return Direction.East;
        }

        private Vector2Int GetOffsetFromDirection(Direction direction)
        {
            if (direction == Direction.North)
            {
                return Vector2Int.up;
            }

            if (direction == Direction.East)
            {
                return Vector2Int.right;
            }

            if (direction == Direction.South)
            {
                return Vector2Int.down;
            }

            return Vector2Int.left;
        }

        private bool IsInsideGrid(Vector2Int position, int width, int height)
        {
            return position.x >= 0 && position.x < width && position.y >= 0 && position.y < height;
        }
    }
}