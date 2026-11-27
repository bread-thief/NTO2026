using UnityEngine;

namespace _Project.Scripts.Gameplay.Obstacles
{
    [CreateAssetMenu(fileName = "ObstacleConfig", menuName = "_Project/ObstacleConfig")]
    public class ObstacleConfig : ScriptableObject
    {
        [field: SerializeField] public string DisplayName { get; private set; } = "Авария";
        [field: SerializeField] public int PenaltyMinutes { get; private set; } = 30;
        [field: SerializeField] public int RemovalDelayMinutes { get; private set; } = 60;
        [field: SerializeField] public Vector3 SpawnScale { get; private set; } = Vector3.one;
        [field: SerializeField] public Color DebugColor { get; private set; } = Color.red;
        [field: SerializeField] public bool IsActive { get; private set; } = true;
        [field: SerializeField] public int Weight { get; private set; } = 1;
    }
}