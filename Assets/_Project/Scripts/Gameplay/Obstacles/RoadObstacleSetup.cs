using UnityEngine;

namespace _Project.Scripts.Gameplay.Obstacles
{
    public class RoadObstacleSetup : MonoBehaviour
    {
        [SerializeField] private RoadObstacleController _controller;

        public RoadObstacleController Controller => _controller;
    }
}