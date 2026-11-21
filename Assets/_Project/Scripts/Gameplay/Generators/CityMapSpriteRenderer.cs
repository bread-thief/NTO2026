using UnityEngine;

namespace _Project.Scripts.Gameplay.Generators
{
	[RequireComponent(typeof(BoxCollider))]
	public class CityMapSpriteRenderer : MonoBehaviour
	{
		private const float SPRITE_HEIGHT_OFFSET = 0.01f;
		private const float COLLIDER_THICKNESS = 0.1f;

		[SerializeField] private CityGenerator _generator;
		[SerializeField] private float _spriteScale = 1.002f;
		[SerializeField] private int _sortingOrder = 0;
		[SerializeField] private string _sortingLayerName = "Default";

		private BoxCollider _mapCollider;

		private void Awake()
		{
			_mapCollider = GetComponent<BoxCollider>();

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
					SpawnSprite(preset.MapSprite, rotation, cellSize, columnIndex, rowIndex);
				}
			}

			UpdateMapCollider(dimensions, cellSize);
		}

		private void UpdateMapCollider(Vector2Int dimensions, float cellSize)
		{
			if (_mapCollider == null)
			{
				_mapCollider = GetComponent<BoxCollider>();
			}

			if (dimensions.x <= 0 || dimensions.y <= 0)
			{
				_mapCollider.enabled = false;
				return;
			}

			_mapCollider.enabled = true;

			float totalWidth = dimensions.x * cellSize;
			float totalHeight = dimensions.y * cellSize;

			float centerX = (totalWidth - cellSize) * 0.5f;
			float centerY = (totalHeight - cellSize) * 0.5f;

			_mapCollider.center = new Vector3(centerX, centerY, SPRITE_HEIGHT_OFFSET);
			_mapCollider.size = new Vector3(totalWidth, totalHeight, COLLIDER_THICKNESS);
		}

		private void SpawnSprite(Sprite sprite, int rotationSteps, float cellSize, int columnIndex, int rowIndex)
		{
			GameObject spriteObject = new GameObject($"MapSprite_{columnIndex}_{rowIndex}");

			float posX = columnIndex * cellSize;
			float posY = rowIndex * cellSize;

			spriteObject.transform.SetParent(transform, false);
			spriteObject.transform.localPosition = new Vector3(posX, posY, SPRITE_HEIGHT_OFFSET);
			spriteObject.transform.localRotation = Quaternion.Euler(0f, 0f, -rotationSteps * 90f);

			SpriteRenderer spriteRenderer = spriteObject.AddComponent<SpriteRenderer>();
			spriteRenderer.sprite = sprite;
			spriteRenderer.sortingLayerName = _sortingLayerName;
			spriteRenderer.sortingOrder = _sortingOrder;

			float spriteWidth = sprite.rect.width / sprite.pixelsPerUnit;
			float spriteHeight = sprite.rect.height / sprite.pixelsPerUnit;

			if (spriteWidth <= Mathf.Epsilon || spriteHeight <= Mathf.Epsilon)
			{
				spriteObject.transform.localScale = Vector3.one * _spriteScale;
				return;
			}

			float scaleX = (cellSize / spriteWidth) * _spriteScale;
			float scaleY = (cellSize / spriteHeight) * _spriteScale;

			spriteObject.transform.localScale = new Vector3(scaleX, scaleY, 1f);
		}

		private void ClearMap()
		{
			if (_mapCollider != null)
			{
				_mapCollider.enabled = false;
			}

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