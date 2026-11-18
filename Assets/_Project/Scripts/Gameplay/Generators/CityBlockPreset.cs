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

        [field: SerializeField] public bool NorthRoad { get; private set; }
        [field: SerializeField] public bool EastRoad { get; private set; }
        [field: SerializeField] public bool SouthRoad { get; private set; }
        [field: SerializeField] public bool WestRoad { get; private set; }
        [field: SerializeField, Min(1)] public int Weight { get; private set; } = 1;

        public int RoadCount
        {
            get
            {
                int count = 0;

                if (NorthRoad)
                {
                    count++;
                }

                if (EastRoad)
                {
                    count++;
                }

                if (SouthRoad)
                {
                    count++;
                }

                if (WestRoad)
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
                return NorthRoad;
            }

            if (originalDirection == Direction.East)
            {
                return EastRoad;
            }

            if (originalDirection == Direction.South)
            {
                return SouthRoad;
            }

            return WestRoad;
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
