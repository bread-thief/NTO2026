using UnityEngine;

namespace _Project.Scripts.Gameplay.Generators
{
	public class CityMapSpriteRenderer : MonoBehaviour
	{
		private const float SPRITE_HEIGHT_OFFSET = 0.01f;

		[SerializeField] private CityGenerator _generator;
		[SerializeField] private float _spriteScale = 1f;
		[SerializeField] private int _sortingOrder = 0;
		[SerializeField] private string _sortingLayerName = "Default";

		private void Awake()
		{
			if (_generator == null)
			{
				_generator = FindAnyObjectByType<CityGenerator>();
			}
		}

		private void OnEnable()
		{
			if (_generator != null)
			{
				_generator.OnGenerated += HandleCityGenerated;
			}
		}

		private void OnDisable()
		{
			if (_generator != null)
			{
				_generator.OnGenerated -= HandleCityGenerated;
			}
		}

		private void HandleCityGenerated()
		{
			ClearMap();

			if (_generator == null)
			{
				return;
			}

			Vector2Int dimensions = _generator.GetGridDimensions;
			float cellSize = _generator.CellSize;

			Vector3 originWorldPosition = _generator.GetWorldPositionFromCellCoordinates(0, 0);

			for (int columnIndex = 0; columnIndex < dimensions.x; columnIndex++)
			{
				for (int rowIndex = 0; rowIndex < dimensions.y; rowIndex++)
				{
					CityBlockPreset preset = _generator.GetSpawnedPresetAt(columnIndex, rowIndex);

					if (preset == null || preset.MapSprite == null)
					{
						continue;
					}

					int rotation = _generator.GetSpawnedRotationAt(columnIndex, rowIndex);
					Vector3 worldPosition = _generator.GetWorldPositionFromCellCoordinates(columnIndex, rowIndex);

					SpawnSprite(preset.MapSprite, worldPosition, originWorldPosition, rotation, cellSize, columnIndex, rowIndex);
				}
			}
		}

		private void SpawnSprite(Sprite sprite, Vector3 worldPosition, Vector3 originWorldPosition, int rotationSteps, float cellSize, int columnIndex, int rowIndex)
		{
			GameObject spriteObject = new GameObject($"MapSprite_{columnIndex}_{rowIndex}");

			Vector3 localOffset = worldPosition - originWorldPosition;

			spriteObject.transform.SetParent(transform, false);
			spriteObject.transform.localPosition = new Vector3(localOffset.x, localOffset.z, SPRITE_HEIGHT_OFFSET);
			spriteObject.transform.localRotation = Quaternion.Euler(0f, 0f, -rotationSteps * 90f);

			SpriteRenderer spriteRenderer = spriteObject.AddComponent<SpriteRenderer>();
			spriteRenderer.sprite = sprite;
			spriteRenderer.sortingLayerName = _sortingLayerName;
			spriteRenderer.sortingOrder = _sortingOrder;

			Vector2 spriteSize = sprite.bounds.size;

			if (spriteSize.x <= Mathf.Epsilon || spriteSize.y <= Mathf.Epsilon)
			{
				spriteObject.transform.localScale = Vector3.one * _spriteScale;
				return;
			}

			float scaleX = (cellSize / spriteSize.x) * _spriteScale;
			float scaleY = (cellSize / spriteSize.y) * _spriteScale;

			spriteObject.transform.localScale = new Vector3(scaleX, scaleY, 1f);
		}

		private void ClearMap()
		{
			for (int childIndex = transform.childCount - 1; childIndex >= 0; childIndex--)
			{
				Transform child = transform.GetChild(childIndex);

				if (Application.isPlaying)
				{
					Destroy(child.gameObject);
				}
				else
				{
					DestroyImmediate(child.gameObject);
				}
			}
		}
	}
}