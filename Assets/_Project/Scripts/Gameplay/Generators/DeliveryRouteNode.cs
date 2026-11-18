using UnityEngine;
using UnityEngine.AI;

namespace _Project.Scripts.Gameplay.Generators
{
    public class DeliveryRouteNode : MonoBehaviour
    {
        private const string ROAD_AREA_NAME = "Road";
        private const int INVALID_AREA_INDEX = -1;
        private const float DEFAULT_SAMPLE_RADIUS = 12f;

        [field: SerializeField] public bool IsDestinationNode { get; private set; }
        [field: SerializeField] public float SampleRadius { get; private set; } = DEFAULT_SAMPLE_RADIUS;
        [field: SerializeField] public GameObject MarkerObject { get; private set; }

        public Vector3 RoadNavMeshPosition { get; private set; }
        public bool HasValidNavMeshPosition { get; private set; }

        private void Awake()
        {
            if (MarkerObject == null)
            {
                MarkerObject = gameObject;
            }

            SnapToRoadNavMesh();
        }

        public bool SnapToRoadNavMesh()
        {
            return SnapToRoadNavMesh(transform.position);
        }

        public bool SnapToRoadNavMesh(Vector3 targetWorldPosition)
        {
            int roadAreaMask = ResolveRoadAreaMask();
            float searchRadius = Mathf.Max(1f, SampleRadius);

            if (NavMesh.SamplePosition(targetWorldPosition, out NavMeshHit roadHit, searchRadius, roadAreaMask))
            {
                RoadNavMeshPosition = roadHit.position;
                transform.position = roadHit.position;
                HasValidNavMeshPosition = true;
                return true;
            }

            if (NavMesh.SamplePosition(targetWorldPosition, out NavMeshHit anyHit, searchRadius * 2f, NavMesh.AllAreas))
            {
                RoadNavMeshPosition = anyHit.position;
                transform.position = anyHit.position;
                HasValidNavMeshPosition = true;
                return true;
            }

            RoadNavMeshPosition = targetWorldPosition;
            transform.position = targetWorldPosition;
            HasValidNavMeshPosition = false;
            return false;
        }

        public void SetMarkerActive(bool isActive)
        {
            if (MarkerObject == null)
            {
                return;
            }

            MarkerObject.SetActive(isActive);
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