using UnityEngine;

namespace _Project.Scripts.Gameplay.Obstacles
{
	public static class ObstacleFactory
	{
		public static RoadObstacle CreateDefault(Vector3 worldPosition, ObstacleConfig config, Transform parent = null)
		{
			GameObject root = new GameObject($"Obstacle_{config.DisplayName}");

			if (parent != null)
			{
				root.transform.SetParent(parent, true);
			}

			root.transform.position = worldPosition;
			root.transform.localScale = config.SpawnScale;

			BoxCollider collider = root.AddComponent<BoxCollider>();
			collider.isTrigger = true;
			collider.size = new Vector3(1f, 2f, 1f);

			RoadObstacle obstacle = root.AddComponent<RoadObstacle>();
			obstacle.ApplyConfig(config);

			return obstacle;
		}
	}
}