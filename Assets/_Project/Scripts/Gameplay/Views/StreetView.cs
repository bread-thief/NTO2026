using _Project.Scripts.Gameplay.Generators;
using _Project.Scripts.Gameplay.Handlers;
using _Project.Scripts.Gameplay.Zones;
using TMPro;
using UnityEngine;

namespace _Project.Scripts.Gameplay.Views
{
    public class StreetView : MonoBehaviour
    {
        private const float MAX_RAYCAST_DISTANCE = 500f;

        [SerializeField] private InputHandler _inputHandler;
        [SerializeField] private Camera _targetCamera;
        [SerializeField] private TMP_Text _streetNameText;
        [SerializeField] private LayerMask _streetZoneLayer = ~0;
        [SerializeField] private string _emptyStreetFallbackText = string.Empty;

        private StreetColliderZone _selectedZone;

        private void Awake()
        {
            if (_targetCamera == null)
            {
                _targetCamera = Camera.main;
            }

            if (_inputHandler == null)
            {
                _inputHandler = FindAnyObjectByType<InputHandler>();
            }
        }

        private void OnEnable()
        {
            if (_inputHandler != null)
            {
                _inputHandler.OnMiddlePressed += HandleMiddleButtonPressed;
            }
        }

        private void OnDisable()
        {
            if (_inputHandler != null)
            {
                _inputHandler.OnMiddlePressed -= HandleMiddleButtonPressed;
            }

            ClearCurrentSelection();
        }

        private void Start()
        {
            ClearCurrentSelection();
        }

        private void HandleMiddleButtonPressed(Vector2 screenPosition)
        {
            if (_targetCamera == null)
            {
                return;
            }

            Ray pointerRay = _targetCamera.ScreenPointToRay(screenPosition);

            if (Physics.Raycast(pointerRay, out RaycastHit hitInfo, MAX_RAYCAST_DISTANCE, _streetZoneLayer, QueryTriggerInteraction.Collide))
            {
                StreetColliderZone hitZone = hitInfo.collider.GetComponent<StreetColliderZone>();

                if (hitZone != null)
                {
                    if (_selectedZone == hitZone)
                    {
                        ClearCurrentSelection();

                        return;
                    }

                    SelectZone(hitZone);

                    return;
                }
            }

            ClearCurrentSelection();
        }

        private void SelectZone(StreetColliderZone targetZone)
        {
            if (_selectedZone != null)
            {
                _selectedZone.SetHighlighted(false);
            }

            _selectedZone = targetZone;

            _selectedZone.SetHighlighted(true);

            UpdateStreetText(_selectedZone.StreetAddress);
        }

        private void ClearCurrentSelection()
        {
            if (_selectedZone != null)
            {
                _selectedZone.SetHighlighted(false);
                _selectedZone = null;
            }

            UpdateStreetText(_emptyStreetFallbackText);
        }

        private void UpdateStreetText(string displayText)
        {
            if (_streetNameText != null)
            {
                _streetNameText.text = displayText;
            }
        }
    }
}