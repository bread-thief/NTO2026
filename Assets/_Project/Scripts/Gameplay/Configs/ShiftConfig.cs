using _Project.Scripts.Gameplay.Controllers;
using UnityEngine;

namespace _Project.Scripts.Gameplay.Configs
{
    [CreateAssetMenu(fileName = "ShiftConfig", menuName = "_Project/ShiftConfig")]
    public class ShiftConfig : ScriptableObject
    {
        [field: SerializeField] public string ShiftName { get; private set; } = "Обычная смена";
        [field: SerializeField] public Controllers.Time ShiftStart { get; private set; } = new Controllers.Time(10, 0);
        [field: SerializeField] public Controllers.Time ShiftEnd { get; private set; } = new Controllers.Time(17, 0);
        [field: SerializeField, Min(1)] public int MaxTrips { get; private set; } = 5;
        [field: SerializeField, Min(1)] public int DistanceInCells { get; private set; } = 5;
    }
}