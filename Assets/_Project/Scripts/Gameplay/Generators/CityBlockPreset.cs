using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace _Project.Scripts.Gameplay.Generators
{
    [DisallowMultipleComponent]
    public class CityBlockPreset : MonoBehaviour
    {
        private const float GIZMO_HEIGHT_OFFSET = 0.5f;
        private const float GIZMO_LINE_LENGTH = 4f;
        private const float GIZMO_SPHERE_RADIUS = 0.25f;

        [SerializeField] private bool _northRoad;
        [SerializeField] private bool _eastRoad;
        [SerializeField] private bool _southRoad;
        [SerializeField] private bool _westRoad;
        [SerializeField, Min(1)] private int _weight = 1;

        public int Weight => _weight;

        public int RoadCount
        {
            get
            {
                int count = 0;

                if (_northRoad)
                {
                    count++;
                }

                if (_eastRoad)
                {
                    count++;
                }

                if (_southRoad)
                {
                    count++;
                }

                if (_westRoad)
                {
                    count++;
                }

                return count;
            }
        }

        public bool HasRoad(Direction direction, int rotationSteps = 0)
        {
            int normalizedRotation = ((rotationSteps % 4) + 4) % 4;
            int originalDirectionIndex = ((int)direction - normalizedRotation + 4) % 4;
            Direction originalDirection = (Direction)originalDirectionIndex;

            if (originalDirection == Direction.North)
            {
                return _northRoad;
            }

            if (originalDirection == Direction.East)
            {
                return _eastRoad;
            }

            if (originalDirection == Direction.South)
            {
                return _southRoad;
            }

            return _westRoad;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (Application.isPlaying)
            {
                return;
            }

            Vector3 origin = transform.position + Vector3.up * GIZMO_HEIGHT_OFFSET;

            DrawDirectionAxis(origin, transform.forward, Color.blue, "North");
            DrawDirectionAxis(origin, transform.right, Color.red, "East");
            DrawDirectionAxis(origin, -transform.forward, Color.cyan, "South");
            DrawDirectionAxis(origin, -transform.right, Color.yellow, "West");
        }

        private void DrawDirectionAxis(Vector3 origin, Vector3 directionVector, Color axisColor, string labelText)
        {
            Vector3 endPoint = origin + directionVector * GIZMO_LINE_LENGTH;

            Gizmos.color = axisColor;
            Gizmos.DrawLine(origin, endPoint);
            Gizmos.DrawSphere(endPoint, GIZMO_SPHERE_RADIUS);

            Handles.Label(endPoint + Vector3.up * 0.3f, labelText);
        }
#endif
    }
}