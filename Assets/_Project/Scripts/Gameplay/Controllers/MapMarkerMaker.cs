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

        private void Awake()
        {
            if (_inputHandler == null)
            {
                _inputHandler = FindAnyObjectByType<InputHandler>();
            }
        }

        private void OnEnable()
        {
            if (_inputHandler == null)
            {
                return;
            }

            _inputHandler.OnPrimaryPressed += HandleMapClick;
        }

        private void HandleMapClick(Vector2 position)
        {
            Ray ray = _mainCamera.ScreenPointToRay(position);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                if (hit.collider.CompareTag(PIN_TAG))
                {
                    Destroy(hit.collider.gameObject);
                    return;
                }

                if (hit.collider.transform.root.CompareTag(PIN_TAG))
                {
                    Destroy(hit.collider.transform.root.gameObject);
                    return;
                }

                if (((1 << hit.collider.gameObject.layer) & _mapLayer) != 0)
                {
                    SpawnPin(hit.point);
                }
            }
        }

        private void SpawnPin(Vector3 hitPosition)
        {
            if (_pinPrefab == null)
            {
                return;
            }

            Vector3 finalPosition = hitPosition + _spawnOffset;
            Instantiate(_pinPrefab, finalPosition, _pinPrefab.transform.rotation);
        }
    }
}