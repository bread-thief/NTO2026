using System.Collections.Generic;
using _Project.Scripts.Gameplay.Handlers;
using UnityEngine;

namespace _Project.Scripts.Gameplay.Controllers
{
    public class MapMarkerMaker : MonoBehaviour
    {
        private const string PIN_TAG = "Pin";

        [SerializeField] private InputHandler _inputHandler;
        [SerializeField] private Camera _mainCamera;
        [SerializeField] private GameObject _pinPrefab;
        [SerializeField] private Vector3 _spawnOffset = new Vector3(0, 0.5f, 0);
        [SerializeField] private LayerMask _mapLayer;
        [SerializeField] private Color[] _stickerColors;

        private readonly List<Color> _availableColors = new List<Color>();
        private readonly Dictionary<GameObject, Color> _usedColors = new Dictionary<GameObject, Color>();
        private bool _canSpawnPin = true;

        public int MaxPinCount => _stickerColors != null ? _stickerColors.Length : 0;
        public int CurrentPinCount => _usedColors.Count;
        public bool HasFreeColor => _availableColors.Count > 0;

        public event System.Action<Color> OnPinRemoved;

        public void SetCanSpawnPin(bool value)
        {
            _canSpawnPin = value;
        }

        private void Awake()
        {
            if (_inputHandler == null)
            {
                _inputHandler = FindAnyObjectByType<InputHandler>();
            }

            BuildAvailableColors();
        }

        private void OnEnable()
        {
            if (_inputHandler == null)
            {
                return;
            }

            _inputHandler.OnPrimaryPressed += HandleMapClick;
        }

        private void OnDisable()
        {
            if (_inputHandler == null)
            {
                return;
            }

            _inputHandler.OnPrimaryPressed -= HandleMapClick;
        }

        private void BuildAvailableColors()
        {
            _availableColors.Clear();

            if (_stickerColors == null || _stickerColors.Length == 0)
            {
                return;
            }

            _availableColors.AddRange(_stickerColors);
        }

        public Color TakeColor()
        {
            if (_availableColors.Count == 0)
            {
                return Color.white;
            }

            int randomIndex = Random.Range(0, _availableColors.Count);
            Color chosenColor = _availableColors[randomIndex];
            _availableColors.RemoveAt(randomIndex);

            return chosenColor;
        }

        public void ReturnColor(Color color)
        {
            if (_stickerColors == null)
            {
                return;
            }

            for (int index = 0; index < _stickerColors.Length; index++)
            {
                if (Approximately(_stickerColors[index], color) && !_availableColors.Contains(color))
                {
                    _availableColors.Add(color);
                    return;
                }
            }
        }

        public GameObject SpawnPin(Vector3 worldPosition, Color pinColor)
        {
            if (_pinPrefab == null)
            {
                return null;
            }

            Vector3 finalPosition = worldPosition + _spawnOffset;
            GameObject pinInstance = Instantiate(_pinPrefab, finalPosition, _pinPrefab.transform.rotation);

            MeshRenderer meshRenderer = pinInstance.GetComponentInChildren<MeshRenderer>();

            if (meshRenderer != null)
            {
                meshRenderer.material.color = pinColor;
            }

            _usedColors[pinInstance] = pinColor;

            return pinInstance;
        }

        private void HandleMapClick(Vector2 position)
        {
            if (!_canSpawnPin)
            {
                return;
            }

            if (_mainCamera == null)
            {
                return;
            }

            Ray ray = _mainCamera.ScreenPointToRay(position);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                if (hit.collider.CompareTag(PIN_TAG))
                {
                    RemovePin(hit.collider.gameObject);
                    return;
                }

                if (hit.collider.transform.root.CompareTag(PIN_TAG))
                {
                    RemovePin(hit.collider.transform.root.gameObject);
                }
            }
        }

        private void RemovePin(GameObject pinObject)
        {
            if (pinObject == null)
            {
                return;
            }

            if (_usedColors.TryGetValue(pinObject, out Color pinColor))
            {
                _usedColors.Remove(pinObject);
                ReturnColor(pinColor);
                OnPinRemoved?.Invoke(pinColor);
            }

            Destroy(pinObject);
        }

        private bool Approximately(Color first, Color second)
        {
            return Mathf.Approximately(first.r, second.r)
                && Mathf.Approximately(first.g, second.g)
                && Mathf.Approximately(first.b, second.b)
                && Mathf.Approximately(first.a, second.a);
        }
    }
}