using System;
using System.Collections.Generic;
using _Project.Scripts.Gameplay.Generators;
using _Project.Scripts.Gameplay.Zones;
using UnityEngine;

namespace _Project.Scripts.Gameplay.Namers
{
    public class CityStreetNamer : MonoBehaviour
    {
        private const string PREFIX_LANE = "пер.";
        private const string PREFIX_STREET = "ул.";
        private const string PREFIX_AVENUE = "пр.";
        private const string PREFIX_HIGHWAY = "ш.";

        private const int MIN_STREET_CELLS = 3;
        private const int MIN_AVENUE_CELLS = 5;
        private const int MIN_HIGHWAY_CELLS = 7;

        private const float ROAD_WIDTH_RATIO = 0.85f;
        private const float COLLIDER_Y_OFFSET = 0.25f;

        [SerializeField] private CityGenerator _generator;
        [SerializeField] private LayerMask _streetZoneLayer;
        [SerializeField] private Material _streetHighlightMaterial;
        [SerializeField]
        private List<string> _streetNameWords = new List<string>()
        {
            "Мира", "Лесная", "Советская", "Центральная", "Победы",
            "Молодёжная", "Садовая", "Школьная", "Новая", "Набережная",
            "Зелёная", "Речная", "Гагарина", "Пушкина", "Лермонтова",
            "Строителей", "Парковая", "Солнечная", "Юности", "Геологов"
        };

        private readonly Dictionary<Vector2Int, string> _cellStreetAddresses = new Dictionary<Vector2Int, string>();
        private readonly List<StreetColliderZone> _spawnedZones = new List<StreetColliderZone>();

        private struct StreetSegment
        {
            public List<Vector2Int> Cells;
            public bool IsHorizontal;
            public bool TouchesBorder;
            public int IntersectionCount;
        }

        private void Awake()
        {
            if (_generator == null)
            {
                _generator = GetComponent<CityGenerator>();
            }
        }

        private void OnEnable()
        {
            if (_generator != null)
            {
                _generator.OnGenerated += AssignStreetNames;
            }
        }

        private void OnDisable()
        {
            if (_generator != null)
            {
                _generator.OnGenerated -= AssignStreetNames;
            }
        }

        private void Start()
        {
            if (_generator != null)
            {
                Vector2Int dimensions = _generator.GetGridDimensions;

                if (dimensions.x > 0 && dimensions.y > 0)
                {
                    AssignStreetNames();
                }
            }
        }

        public void AssignStreetNames()
        {
            ClearSpawnedZones();
            _cellStreetAddresses.Clear();

            if (_generator == null || _streetNameWords == null || _streetNameWords.Count == 0)
            {
                return;
            }

            Vector2Int dimensions = _generator.GetGridDimensions;
            int width = dimensions.x;
            int height = dimensions.y;

            if (width <= 0 || height <= 0)
            {
                return;
            }

            List<StreetSegment> segments = BuildAllStreetSegments(width, height);

            if (segments.Count == 0)
            {
                return;
            }

            segments.Sort((first, second) => second.Cells.Count.CompareTo(first.Cells.Count));

            List<string> uniqueWords = CollectUniqueWords();

            if (uniqueWords.Count == 0)
            {
                return;
            }

            ShuffleWords(uniqueWords);

            int layerIndex = GetSingleLayerIndex(_streetZoneLayer);

            for (int index = 0; index < segments.Count; index++)
            {
                StreetSegment segment = segments[index];
                string baseWord = uniqueWords[index % uniqueWords.Count];
                string prefix = DetermineRoadPrefix(segment);
                string fullAddress = $"{prefix} {baseWord}";

                for (int cellIndex = 0; cellIndex < segment.Cells.Count; cellIndex++)
                {
                    Vector2Int cell = segment.Cells[cellIndex];

                    if (!_cellStreetAddresses.ContainsKey(cell))
                    {
                        _cellStreetAddresses[cell] = fullAddress;
                    }
                }

                SpawnSegmentColliderZone(segment, fullAddress, layerIndex);
            }

            FillRemainingRoadCellsFromNeighbors(width, height);
        }

        public string GetLandmarkNearCell(Vector2Int cell, string excludedAddress)
        {
            if (_generator == null || _generator.GetSpawnedPresetAt(cell.x, cell.y) == null)
            {
                return GetRandomStreetWord();
            }

            CityBlockPreset preset = _generator.GetSpawnedPresetAt(cell.x, cell.y);
            int rotation = _generator.GetSpawnedRotationAt(cell.x, cell.y);

            Vector2Int gridDimensions = _generator.GetGridDimensions;
            Vector2Int[] offsets =
            {
                new Vector2Int(0, 1),
                new Vector2Int(1, 0),
                new Vector2Int(0, -1),
                new Vector2Int(-1, 0)
            };
            Direction[] directions = { Direction.North, Direction.East, Direction.South, Direction.West };

            List<string> perpendicular = new List<string>();
            List<string> other = new List<string>();

            for (int index = 0; index < offsets.Length; index++)
            {
                if (!preset.HasRoad(directions[index], rotation))
                {
                    continue;
                }

                Vector2Int neighbor = cell + offsets[index];

                if (neighbor.x < 0 || neighbor.x >= gridDimensions.x || neighbor.y < 0 || neighbor.y >= gridDimensions.y)
                {
                    continue;
                }

                if (!_cellStreetAddresses.TryGetValue(neighbor, out string neighborAddress))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(neighborAddress) || neighborAddress == excludedAddress)
                {
                    continue;
                }

                bool isHorizontalDirection = directions[index] == Direction.East || directions[index] == Direction.West;
                bool isBlockedHorizontal = IsAddressHorizontal(excludedAddress);

                if (isHorizontalDirection != isBlockedHorizontal)
                {
                    if (!perpendicular.Contains(neighborAddress))
                    {
                        perpendicular.Add(neighborAddress);
                    }
                }
                else if (!other.Contains(neighborAddress))
                {
                    other.Add(neighborAddress);
                }
            }

            if (perpendicular.Count > 0)
            {
                return perpendicular[UnityEngine.Random.Range(0, perpendicular.Count)];
            }

            if (other.Count > 0)
            {
                return other[UnityEngine.Random.Range(0, other.Count)];
            }

            return GetRandomStreetWord();
        }

        private bool IsAddressHorizontal(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return false;
            }

            Vector2Int gridDimensions = _generator.GetGridDimensions;

            for (int columnIndex = 0; columnIndex < gridDimensions.x; columnIndex++)
            {
                for (int rowIndex = 0; rowIndex < gridDimensions.y; rowIndex++)
                {
                    Vector2Int cell = new Vector2Int(columnIndex, rowIndex);

                    if (!_cellStreetAddresses.TryGetValue(cell, out string cellAddress))
                    {
                        continue;
                    }

                    if (cellAddress != address)
                    {
                        continue;
                    }

                    CityBlockPreset preset = _generator.GetSpawnedPresetAt(columnIndex, rowIndex);

                    if (preset == null)
                    {
                        continue;
                    }

                    int rotation = _generator.GetSpawnedRotationAt(columnIndex, rowIndex);
                    bool hasEast = preset.HasRoad(Direction.East, rotation);
                    bool hasWest = preset.HasRoad(Direction.West, rotation);

                    if (hasEast || hasWest)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public string GetRandomStreetWord()
        {
            if (_streetNameWords == null || _streetNameWords.Count == 0)
            {
                return string.Empty;
            }

            return _streetNameWords[UnityEngine.Random.Range(0, _streetNameWords.Count)];
        }

        public string FormatRouteDescription(Vector3 pointAPosition, Vector3 pointBPosition, string fromLabel = "из", string toLabel = "в")
        {
            string streetA = GetStreetAddressByWorldPosition(pointAPosition);
            string streetB = GetStreetAddressByWorldPosition(pointBPosition);

            if (string.IsNullOrWhiteSpace(streetA))
            {
                streetA = "неизвестная улица";
            }

            if (string.IsNullOrWhiteSpace(streetB))
            {
                streetB = "неизвестная улица";
            }

            return $"Доставить груз {fromLabel}: {streetA}\nДоставить груз {toLabel}: {streetB}";
        }

        public void ClearSpawnedZones()
        {
            for (int index = _spawnedZones.Count - 1; index >= 0; index--)
            {
                StreetColliderZone zone = _spawnedZones[index];

                if (zone != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(zone.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(zone.gameObject);
                    }
                }
            }

            _spawnedZones.Clear();
        }

        public string GetStreetAddressAtCell(Vector2Int cellCoordinates)
        {
            if (_cellStreetAddresses.TryGetValue(cellCoordinates, out string address))
            {
                return address;
            }

            return string.Empty;
        }

        public string GetStreetAddressByWorldPosition(Vector3 worldPosition)
        {
            if (_generator == null)
            {
                return string.Empty;
            }

            Vector2Int cellCoordinates = _generator.GetCellCoordinatesFromWorldPosition(worldPosition);
            return GetStreetAddressAtCell(cellCoordinates);
        }

        private List<StreetSegment> BuildAllStreetSegments(int width, int height)
        {
            List<StreetSegment> segments = new List<StreetSegment>();
            bool[,] visitedHorizontal = new bool[width, height];
            bool[,] visitedVertical = new bool[width, height];

            for (int rowIndex = 0; rowIndex < height; rowIndex++)
            {
                for (int columnIndex = 0; columnIndex < width; columnIndex++)
                {
                    if (visitedHorizontal[columnIndex, rowIndex])
                    {
                        continue;
                    }

                    if (!HasHorizontalRoad(columnIndex, rowIndex))
                    {
                        continue;
                    }

                    List<Vector2Int> chain = new List<Vector2Int>();
                    int currentColumn = columnIndex;

                    while (currentColumn < width && HasHorizontalRoad(currentColumn, rowIndex))
                    {
                        visitedHorizontal[currentColumn, rowIndex] = true;
                        chain.Add(new Vector2Int(currentColumn, rowIndex));
                        currentColumn++;
                    }

                    if (chain.Count > 0)
                    {
                        segments.Add(CreateSegmentInfo(chain, true, width, height));
                    }
                }
            }

            for (int columnIndex = 0; columnIndex < width; columnIndex++)
            {
                for (int rowIndex = 0; rowIndex < height; rowIndex++)
                {
                    if (visitedVertical[columnIndex, rowIndex])
                    {
                        continue;
                    }

                    if (!HasVerticalRoad(columnIndex, rowIndex))
                    {
                        continue;
                    }

                    List<Vector2Int> chain = new List<Vector2Int>();
                    int currentRow = rowIndex;

                    while (currentRow < height && HasVerticalRoad(columnIndex, currentRow))
                    {
                        visitedVertical[columnIndex, currentRow] = true;
                        chain.Add(new Vector2Int(columnIndex, currentRow));
                        currentRow++;
                    }

                    if (chain.Count > 0)
                    {
                        segments.Add(CreateSegmentInfo(chain, false, width, height));
                    }
                }
            }

            AddIsolatedSingleRoadCells(segments, visitedHorizontal, visitedVertical, width, height);

            return segments;
        }

        private void AddIsolatedSingleRoadCells(
            List<StreetSegment> segments,
            bool[,] visitedHorizontal,
            bool[,] visitedVertical,
            int width,
            int height)
        {
            for (int columnIndex = 0; columnIndex < width; columnIndex++)
            {
                for (int rowIndex = 0; rowIndex < height; rowIndex++)
                {
                    if (visitedHorizontal[columnIndex, rowIndex] || visitedVertical[columnIndex, rowIndex])
                    {
                        continue;
                    }

                    CityBlockPreset preset = _generator.GetSpawnedPresetAt(columnIndex, rowIndex);

                    if (preset == null || preset.RoadCount == 0)
                    {
                        continue;
                    }

                    List<Vector2Int> singleCell = new List<Vector2Int> { new Vector2Int(columnIndex, rowIndex) };
                    segments.Add(CreateSegmentInfo(singleCell, true, width, height));
                }
            }
        }

        private StreetSegment CreateSegmentInfo(List<Vector2Int> cells, bool isHorizontal, int width, int height)
        {
            bool touchesBorder = false;
            int intersectionCount = 0;

            for (int index = 0; index < cells.Count; index++)
            {
                Vector2Int cell = cells[index];

                if (cell.x == 0 || cell.x == width - 1 || cell.y == 0 || cell.y == height - 1)
                {
                    touchesBorder = true;
                }

                CityBlockPreset preset = _generator.GetSpawnedPresetAt(cell.x, cell.y);

                if (preset != null && preset.RoadCount >= 3)
                {
                    intersectionCount++;
                }
            }

            return new StreetSegment
            {
                Cells = cells,
                IsHorizontal = isHorizontal,
                TouchesBorder = touchesBorder,
                IntersectionCount = intersectionCount
            };
        }

        private string DetermineRoadPrefix(StreetSegment segment)
        {
            int cellCount = segment.Cells.Count;

            if (cellCount >= MIN_HIGHWAY_CELLS && segment.TouchesBorder)
            {
                return PREFIX_HIGHWAY;
            }

            if (cellCount >= MIN_AVENUE_CELLS && segment.IntersectionCount >= 2)
            {
                return PREFIX_AVENUE;
            }

            if (cellCount >= MIN_STREET_CELLS)
            {
                return PREFIX_STREET;
            }

            return PREFIX_LANE;
        }

        private void SpawnSegmentColliderZone(StreetSegment segment, string fullAddress, int layerIndex)
        {
            if (segment.Cells == null || segment.Cells.Count == 0)
            {
                return;
            }

            Vector2Int firstCell = segment.Cells[0];
            Vector2Int lastCell = segment.Cells[segment.Cells.Count - 1];

            Vector3 firstWorldPosition = _generator.GetWorldPositionFromCellCoordinates(firstCell.x, firstCell.y);
            Vector3 lastWorldPosition = _generator.GetWorldPositionFromCellCoordinates(lastCell.x, lastCell.y);

            Vector3 centerPosition = (firstWorldPosition + lastWorldPosition) * 0.5f;
            centerPosition.y += COLLIDER_Y_OFFSET;

            float cellSize = _generator.CellSize;
            float totalLength = segment.Cells.Count * cellSize;
            float roadWidth = cellSize * ROAD_WIDTH_RATIO;

            Vector3 worldSize = segment.IsHorizontal
                ? new Vector3(totalLength, 1.0f, roadWidth)
                : new Vector3(roadWidth, 1.0f, totalLength);

            GameObject zoneObject = new GameObject($"StreetZone_{fullAddress}");
            zoneObject.transform.SetParent(transform, true);
            zoneObject.transform.position = centerPosition;
            zoneObject.transform.rotation = Quaternion.identity;
            zoneObject.layer = layerIndex;

            StreetColliderZone colliderZone = zoneObject.AddComponent<StreetColliderZone>();
            colliderZone.Initialize(fullAddress, worldSize, _streetHighlightMaterial);

            _spawnedZones.Add(colliderZone);
        }

        private void FillRemainingRoadCellsFromNeighbors(int width, int height)
        {
            Vector2Int[] offsets = new Vector2Int[]
            {
                Vector2Int.up,
                Vector2Int.right,
                Vector2Int.down,
                Vector2Int.left
            };

            for (int columnIndex = 0; columnIndex < width; columnIndex++)
            {
                for (int rowIndex = 0; rowIndex < height; rowIndex++)
                {
                    Vector2Int cell = new Vector2Int(columnIndex, rowIndex);

                    if (_cellStreetAddresses.ContainsKey(cell))
                    {
                        continue;
                    }

                    CityBlockPreset preset = _generator.GetSpawnedPresetAt(columnIndex, rowIndex);

                    if (preset == null || preset.RoadCount == 0)
                    {
                        continue;
                    }

                    for (int dirIndex = 0; dirIndex < offsets.Length; dirIndex++)
                    {
                        Vector2Int neighbor = cell + offsets[dirIndex];

                        if (_cellStreetAddresses.TryGetValue(neighbor, out string neighborAddress))
                        {
                            _cellStreetAddresses[cell] = neighborAddress;
                            break;
                        }
                    }
                }
            }
        }

        private bool HasHorizontalRoad(int columnIndex, int rowIndex)
        {
            CityBlockPreset preset = _generator.GetSpawnedPresetAt(columnIndex, rowIndex);

            if (preset == null || preset.RoadCount == 0)
            {
                return false;
            }

            int rotationSteps = _generator.GetSpawnedRotationAt(columnIndex, rowIndex);
            bool hasEast = preset.HasRoad(Direction.East, rotationSteps);
            bool hasWest = preset.HasRoad(Direction.West, rotationSteps);

            return hasEast || hasWest;
        }

        private bool HasVerticalRoad(int columnIndex, int rowIndex)
        {
            CityBlockPreset preset = _generator.GetSpawnedPresetAt(columnIndex, rowIndex);

            if (preset == null || preset.RoadCount == 0)
            {
                return false;
            }

            int rotationSteps = _generator.GetSpawnedRotationAt(columnIndex, rowIndex);
            bool hasNorth = preset.HasRoad(Direction.North, rotationSteps);
            bool hasSouth = preset.HasRoad(Direction.South, rotationSteps);

            return hasNorth || hasSouth;
        }

        private List<string> CollectUniqueWords()
        {
            HashSet<string> uniqueSet = new HashSet<string>();
            List<string> result = new List<string>();

            for (int index = 0; index < _streetNameWords.Count; index++)
            {
                string word = _streetNameWords[index];

                if (string.IsNullOrWhiteSpace(word))
                {
                    continue;
                }

                string trimmedWord = word.Trim();

                if (uniqueSet.Add(trimmedWord))
                {
                    result.Add(trimmedWord);
                }
            }

            return result;
        }

        private void ShuffleWords(List<string> words)
        {
            for (int index = words.Count - 1; index > 0; index--)
            {
                int randomIndex = UnityEngine.Random.Range(0, index + 1);
                string temp = words[index];
                words[index] = words[randomIndex];
                words[randomIndex] = temp;
            }
        }

        private int GetSingleLayerIndex(LayerMask mask)
        {
            int maskValue = mask.value;

            if (maskValue == 0)
            {
                return 0;
            }

            for (int layer = 0; layer < 32; layer++)
            {
                if ((maskValue & (1 << layer)) != 0)
                {
                    return layer;
                }
            }

            return 0;
        }
    }
}