using _Project.Scripts.Gameplay.Drivers;
using UnityEngine;

namespace _Project.Scripts.Gameplay.Obstacles
{
    [RequireComponent(typeof(RouteVehicleDriver))]
    public class RouteVehicleDriverTagInitializer : MonoBehaviour
    {
        private void Awake()
        {
            if (GetComponent<RouteVehicleDriverTag>() == null)
            {
                gameObject.AddComponent<RouteVehicleDriverTag>();
            }
        }
    }
}