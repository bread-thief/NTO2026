using UnityEngine;

namespace _Project.Scripts.Gameplay.Zones
{
    [RequireComponent(typeof(BoxCollider))]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class StreetColliderZone : MonoBehaviour
    {
        private const float DEFAULT_COLLIDER_HEIGHT = 1.5f;
        private const float DEFAULT_STREET_WIDTH = 4.0f;
        private const float CUBE_HEIGHT_SCALE = 0.05f;
        private const float CUBE_Y_OFFSET = 0.03f;

        private BoxCollider _zoneCollider;
        private MeshFilter _highlightMeshFilter;
        private MeshRenderer _highlightMeshRenderer;
        private Material _highlightMaterial;

        public string StreetAddress { get; private set; }

        private void Awake()
        {
            CacheComponents();
        }

        public void Initialize(string streetAddress, Vector3 colliderSize, Material assignedHighlightMaterial)
        {
            StreetAddress = streetAddress;

            _highlightMaterial = assignedHighlightMaterial;

            CacheComponents();

            _zoneCollider.isTrigger = true;

            _zoneCollider.center = Vector3.zero;

            _zoneCollider.size = colliderSize;

            float cubeWidth = colliderSize.x;

            float cubeLength = colliderSize.z;

            float cubeHeight = colliderSize.y * CUBE_HEIGHT_SCALE;

            _highlightMeshFilter.sharedMesh = CreateCubeMesh(cubeWidth, cubeHeight, cubeLength);

            _highlightMeshRenderer.sharedMaterial = _highlightMaterial;

            _highlightMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            _highlightMeshRenderer.receiveShadows = false;

            _highlightMeshRenderer.enabled = false;
        }

        public void Initialize(string streetAddress, bool isHorizontal, float length, Material assignedHighlightMaterial)
        {
            Initialize(streetAddress, isHorizontal, length, DEFAULT_STREET_WIDTH, assignedHighlightMaterial);
        }

        public void Initialize(string streetAddress, bool isHorizontal, float length, float width, Material assignedHighlightMaterial)
        {
            Vector3 computedSize = isHorizontal
                ? new Vector3(length, DEFAULT_COLLIDER_HEIGHT, width)
                : new Vector3(width, DEFAULT_COLLIDER_HEIGHT, length);

            Initialize(streetAddress, computedSize, assignedHighlightMaterial);
        }

        public void SetHighlighted(bool isHighlighted)
        {
            if (_highlightMeshRenderer == null)
            {
                return;
            }

            if (isHighlighted && _highlightMaterial != null)
            {
                _highlightMeshRenderer.sharedMaterial = _highlightMaterial;

                _highlightMeshRenderer.enabled = true;
            }
            else
            {
                _highlightMeshRenderer.enabled = false;
            }
        }

        private void CacheComponents()
        {
            if (_zoneCollider == null)
            {
                _zoneCollider = GetComponent<BoxCollider>();
            }

            if (_highlightMeshFilter == null)
            {
                _highlightMeshFilter = GetComponent<MeshFilter>();
            }

            if (_highlightMeshRenderer == null)
            {
                _highlightMeshRenderer = GetComponent<MeshRenderer>();
            }
        }

        private Mesh CreateCubeMesh(float width, float height, float length)
        {
            Mesh cubeMesh = new Mesh();

            float halfWidth = width * 0.5f;

            float halfLength = length * 0.5f;

            float bottomY = CUBE_Y_OFFSET;

            float topY = CUBE_Y_OFFSET + height;

            Vector3[] vertices = new Vector3[]
            {
                new Vector3(-halfWidth, bottomY, -halfLength),
                new Vector3(halfWidth, bottomY, -halfLength),
                new Vector3(halfWidth, bottomY, halfLength),
                new Vector3(-halfWidth, bottomY, halfLength),
                new Vector3(-halfWidth, topY, -halfLength),
                new Vector3(halfWidth, topY, -halfLength),
                new Vector3(halfWidth, topY, halfLength),
                new Vector3(-halfWidth, topY, halfLength)
            };

            int[] triangles = new int[]
            {
                0, 2, 1,
                0, 3, 2,
                4, 5, 6,
                4, 6, 7,
                0, 1, 5,
                0, 5, 4,
                1, 2, 6,
                1, 6, 5,
                2, 3, 7,
                2, 7, 6,
                3, 0, 4,
                3, 4, 7
            };

            cubeMesh.vertices = vertices;

            cubeMesh.triangles = triangles;

            cubeMesh.RecalculateNormals();

            cubeMesh.RecalculateBounds();

            return cubeMesh;
        }
    }
}