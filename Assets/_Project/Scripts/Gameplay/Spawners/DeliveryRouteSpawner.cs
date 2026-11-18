using System.Collections.Generic;
using _Project.Scripts.Gameplay.Bakers;
using _Project.Scripts.Gameplay.Controllers;
using _Project.Scripts.Gameplay.Drivers;
using _Project.Scripts.Gameplay.Generators;
using _Project.Scripts.Gameplay.Namers;
using UnityEngine;
using UnityEngine.AI;

namespace _Project.Scripts.Gameplay.Spawners
{
    public class DeliveryRouteSpawner : MonoBehaviour
    {
        private const string ROAD_AREA_NAME = "Road";
        private const int INVALID_AREA_INDEX = -1;
        private const float NAVMESH_SAMPLE_RADIUS = 15f;
        private const int MAX_PAIR_ATTEMPTS = 250;

        [field: SerializeField] public CityGenerator Generator { get; private set; }
        [field: SerializeField] public CityNavMeshBaker NavMeshBaker { get; private set; }
        [field: SerializeField] public CityStreetNamer StreetNamer { get; private set; }
        [field: SerializeField] public RoutePlannerController RoutePlanner { get; private set; }
        [field: SerializeField] public DeliveryRouteNode PointAPrefab { get; private set; }
        [field: SerializeField] public DeliveryRouteNode PointBPrefab { get; private set; }
        [field: SerializeField] public RouteVehicleDriver VehiclePrefab { get; private set; }
        [field: SerializeField] public int MinimumDistanceInCells { get; private set; } = 5;

        public DeliveryRouteNode SpawnedPointA { get; private set; }
        public DeliveryRouteNode SpawnedPointB { get; private set; }
        public RouteVehicleDriver SpawnedVehicle { get; private set; }

        private bool hasSpawnedForCurrentCity;

        private void Awake()
        {
            ResolveMissingSceneReferences();
        }

        private void OnEnable()
        {
            ResolveMissingSceneReferences();

            if (Generator != null)
            {
                Generator.OnGenerated += HandleCityGenerated;
            }

            if (NavMeshBaker != null)
            {
                NavMeshBaker.OnNavMeshBaked += HandleNavMeshBaked;
            }
        }

        private void OnDisable()
        {
            if (Generator != null)
            {
                Generator.OnGenerated -= HandleCityGenerated;
            }

            if (NavMeshBaker != null)
            {
                NavMeshBaker.OnNavMeshBaked -= HandleNavMeshBaked;
            }
        }

        private void Start()
        {
            ResolveMissingSceneReferences();

            if (!hasSpawnedForCurrentCity && NavMeshBaker != null && NavMeshBaker.IsNavMeshBaked)
            {
                SpawnDeliveryRoute();
            }
        }

        private void HandleCityGenerated()
        {
            hasSpawnedForCurrentCity = false;
            ClearSpawnedObjects();
        }

        private void HandleNavMeshBaked()
        {
            SpawnDeliveryRoute();
        }

        [ContextMenu("Spawn Delivery Route")]
        public void SpawnDeliveryRoute()
        {
            ResolveMissingSceneReferences();
            ClearSpawnedObjects();

            if (Generator == null || PointAPrefab == null || PointBPrefab == null || VehiclePrefab == null)
            {
#if UNITY_EDITOR
                Debug.LogWarning("[DeliveryRouteSpawner] Не назначены обязательные ссылки (Generator, PointAPrefab, PointBPrefab или VehiclePrefab).");
#endif
                return;
            }

            List<Vector2Int> roadCells = CollectAllRoadCells();

            if (roadCells.Count < 2)
            {
                return;
            }

            if (!TryFindValidEndpoints(roadCells, out Vector3 pointAPosition, out Vector3 pointBPosition))
            {
#if UNITY_EDITOR
                Debug.LogWarning("[DeliveryRouteSpawner] Не удалось найти две подходящие дорожные точки.");
#endif
                return;
            }

            SpawnedPointA = Instantiate(PointAPrefab, pointAPosition, Quaternion.identity, transform);
            SpawnedPointA.SnapToRoadNavMesh(pointAPosition);

            SpawnedPointB = Instantiate(PointBPrefab, pointBPosition, Quaternion.identity, transform);
            SpawnedPointB.SnapToRoadNavMesh(pointBPosition);

            Vector3 spawnPosition = SpawnedPointA.RoadNavMeshPosition;
            Vector3 directionToTarget = SpawnedPointB.RoadNavMeshPosition - spawnPosition;
            directionToTarget.y = 0f;

            Quaternion vehicleRotation = Quaternion.identity;

            if (directionToTarget.sqrMagnitude > 0.001f)
            {
                vehicleRotation = Quaternion.LookRotation(directionToTarget.normalized, Vector3.up);
            }

            bool previousPrefabActiveState = VehiclePrefab.gameObject.activeSelf;
            VehiclePrefab.gameObject.SetActive(false);

            SpawnedVehicle = Instantiate(VehiclePrefab, spawnPosition, vehicleRotation, transform);

            VehiclePrefab.gameObject.SetActive(previousPrefabActiveState);

            if (NavMesh.SamplePosition(spawnPosition, out NavMeshHit navMeshHit, NAVMESH_SAMPLE_RADIUS * 2f, NavMesh.AllAreas))
            {
                SpawnedVehicle.transform.position = navMeshHit.position;
            }

            SpawnedVehicle.gameObject.SetActive(true);
            SpawnedVehicle.StopAndReset(SpawnedVehicle.transform.position);

            if (RoutePlanner != null)
            {
                RoutePlanner.ConfigureRouteEndpoints(SpawnedPointA, SpawnedPointB, SpawnedVehicle);
            }

            hasSpawnedForCurrentCity = true;
        }

        public void ClearSpawnedObjects()
        {
            if (SpawnedPointA != null)
            {
                DestroySpawnedObject(SpawnedPointA.gameObject);
                SpawnedPointA = null;
            }

            if (SpawnedPointB != null)
            {
                DestroySpawnedObject(SpawnedPointB.gameObject);
                SpawnedPointB = null;
            }

            if (SpawnedVehicle != null)
            {
                DestroySpawnedObject(SpawnedVehicle.gameObject);
                SpawnedVehicle = null;
            }
        }

        private void DestroySpawnedObject(GameObject targetObject)
        {
            if (Application.isPlaying)
            {
                Destroy(targetObject);
            }
            else
            {
                DestroyImmediate(targetObject);
            }
        }

        private void ResolveMissingSceneReferences()
        {
            if (Generator == null)
            {
                Generator = FindAnyObjectByType<CityGenerator>();
            }

            if (NavMeshBaker == null)
            {
                NavMeshBaker = FindAnyObjectByType<CityNavMeshBaker>();
            }

            if (StreetNamer == null)
            {
                StreetNamer = FindAnyObjectByType<CityStreetNamer>();
            }

            if (RoutePlanner == null)
            {
                RoutePlanner = FindAnyObjectByType<RoutePlannerController>();
            }
        }

        private List<Vector2Int> CollectAllRoadCells()
        {
            List<Vector2Int> roadCells = new List<Vector2Int>();
            Vector2Int gridDimensions = Generator.GetGridDimensions;

            for (int columnIndex = 0; columnIndex < gridDimensions.x; columnIndex++)
            {
                for (int rowIndex = 0; rowIndex < gridDimensions.y; rowIndex++)
                {
                    CityBlockPreset preset = Generator.GetSpawnedPresetAt(columnIndex, rowIndex);

                    if (preset != null && preset.RoadCount > 0)
                    {
                        roadCells.Add(new Vector2Int(columnIndex, rowIndex));
                    }
                }
            }

            return roadCells;
        }

        private bool TryFindValidEndpoints(List<Vector2Int> roadCells, out Vector3 pointAPosition, out Vector3 pointBPosition)
        {
            pointAPosition = Vector3.zero;
            pointBPosition = Vector3.zero;

            int roadAreaMask = ResolveRoadAreaMask();
            int minDistance = Mathf.Max(1, MinimumDistanceInCells);

            if (TrySearchEndpointPair(roadCells, minDistance, true, roadAreaMask, out pointAPosition, out pointBPosition))
            {
                return true;
            }

            if (TrySearchEndpointPair(roadCells, minDistance, true, NavMesh.AllAreas, out pointAPosition, out pointBPosition))
            {
                return true;
            }

            if (TrySearchEndpointPair(roadCells, 1, false, NavMesh.AllAreas, out pointAPosition, out pointBPosition))
            {
                return true;
            }

            return TryFallbackRawCellPositions(roadCells, out pointAPosition, out pointBPosition);
        }

        private bool TrySearchEndpointPair(
            List<Vector2Int> roadCells,
            int minimumCellDistance,
            bool requireDifferentStreets,
            int areaMask,
            out Vector3 pointAPosition,
            out Vector3 pointBPosition)
        {
            pointAPosition = Vector3.zero;
            pointBPosition = Vector3.zero;

            NavMeshPath testPath = new NavMeshPath();

            for (int attemptIndex = 0; attemptIndex < MAX_PAIR_ATTEMPTS; attemptIndex++)
            {
                Vector2Int cellA = roadCells[Random.Range(0, roadCells.Count)];
                Vector2Int cellB = roadCells[Random.Range(0, roadCells.Count)];

                if (cellA == cellB)
                {
                    continue;
                }

                int manhattanDistance = Mathf.Abs(cellA.x - cellB.x) + Mathf.Abs(cellA.y - cellB.y);

                if (manhattanDistance < minimumCellDistance)
                {
                    continue;
                }

                if (requireDifferentStreets && !AreCellsOnDifferentStreets(cellA, cellB))
                {
                    continue;
                }

                Vector3 worldA = Generator.GetWorldPositionFromCellCoordinates(cellA.x, cellA.y);
                Vector3 worldB = Generator.GetWorldPositionFromCellCoordinates(cellB.x, cellB.y);

                if (!NavMesh.SamplePosition(worldA, out NavMeshHit hitA, NAVMESH_SAMPLE_RADIUS, areaMask))
                {
                    continue;
                }

                if (!NavMesh.SamplePosition(worldB, out NavMeshHit hitB, NAVMESH_SAMPLE_RADIUS, areaMask))
                {
                    continue;
                }

                if (!NavMesh.CalculatePath(hitA.position, hitB.position, areaMask, testPath))
                {
                    continue;
                }

                if (testPath.status != NavMeshPathStatus.PathComplete)
                {
                    continue;
                }

                pointAPosition = hitA.position;
                pointBPosition = hitB.position;
                return true;
            }

            return false;
        }

        private bool TryFallbackRawCellPositions(List<Vector2Int> roadCells, out Vector3 pointAPosition, out Vector3 pointBPosition)
        {
            Vector2Int bestCellA = roadCells[0];
            Vector2Int bestCellB = roadCells[roadCells.Count - 1];
            int bestDistance = -1;

            for (int firstIndex = 0; firstIndex < roadCells.Count; firstIndex++)
            {
                for (int secondIndex = firstIndex + 1; secondIndex < roadCells.Count; secondIndex++)
                {
                    Vector2Int firstCell = roadCells[firstIndex];
                    Vector2Int secondCell = roadCells[secondIndex];
                    int distance = Mathf.Abs(firstCell.x - secondCell.x) + Mathf.Abs(firstCell.y - secondCell.y);

                    if (distance > bestDistance)
                    {
                        bestDistance = distance;
                        bestCellA = firstCell;
                        bestCellB = secondCell;
                    }
                }
            }

            pointAPosition = Generator.GetWorldPositionFromCellCoordinates(bestCellA.x, bestCellA.y);
            pointBPosition = Generator.GetWorldPositionFromCellCoordinates(bestCellB.x, bestCellB.y);
            return true;
        }

        private bool AreCellsOnDifferentStreets(Vector2Int cellA, Vector2Int cellB)
        {
            if (StreetNamer == null)
            {
                return true;
            }

            string streetA = StreetNamer.GetStreetAddressAtCell(cellA);
            string streetB = StreetNamer.GetStreetAddressAtCell(cellB);

            if (string.IsNullOrEmpty(streetA) || string.IsNullOrEmpty(streetB))
            {
                return true;
            }

            return !string.Equals(streetA, streetB, System.StringComparison.OrdinalIgnoreCase);
        }

        private int ResolveRoadAreaMask()
        {
            int areaIndex = NavMesh.GetAreaFromName(ROAD_AREA_NAME);

            if (areaIndex == INVALID_AREA_INDEX)
            {
                return NavMesh.AllAreas;
            }

            return 1 << areaIndex;
        }
    }
}
