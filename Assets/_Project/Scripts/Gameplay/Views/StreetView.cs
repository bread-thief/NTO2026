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

        [field: SerializeField]
        public InputHandler InputHandler { get; private set; }

        [field: SerializeField]
        public Camera TargetCamera { get; private set; }

        [field: SerializeField]
        public TMP_Text StreetNameText { get; private set; }

        [field: SerializeField]
        public LayerMask StreetZoneLayer { get; private set; } = ~0;

        [field: SerializeField]
        public string EmptyStreetFallbackText { get; private set; } = string.Empty;

        private StreetColliderZone selectedZone;

        private void Awake()
        {
            if (TargetCamera == null)
            {
                TargetCamera = Camera.main;
            }

            if (InputHandler == null)
            {
                InputHandler = FindAnyObjectByType<InputHandler>();
            }
        }

        private void OnEnable()
        {
            if (InputHandler != null)
            {
                InputHandler.OnMiddlePressed += HandleMiddleButtonPressed;
            }
        }

        private void OnDisable()
        {
            if (InputHandler != null)
            {
                InputHandler.OnMiddlePressed -= HandleMiddleButtonPressed;
            }

            ClearCurrentSelection();
        }

        private void Start()
        {
            ClearCurrentSelection();
        }

        private void HandleMiddleButtonPressed(Vector2 screenPosition)
        {
            if (TargetCamera == null)
            {
                return;
            }

            Ray pointerRay = TargetCamera.ScreenPointToRay(screenPosition);

            if (Physics.Raycast(pointerRay, out RaycastHit hitInfo, MAX_RAYCAST_DISTANCE, StreetZoneLayer, QueryTriggerInteraction.Collide))
            {
                StreetColliderZone hitZone = hitInfo.collider.GetComponent<StreetColliderZone>();

                if (hitZone != null)
                {
                    if (selectedZone == hitZone)
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
            if (selectedZone != null)
            {
                selectedZone.SetHighlighted(false);
            }

            selectedZone = targetZone;

            selectedZone.SetHighlighted(true);

            UpdateStreetText(selectedZone.StreetAddress);
        }

        private void ClearCurrentSelection()
        {
            if (selectedZone != null)
            {
                selectedZone.SetHighlighted(false);
                selectedZone = null;
            }

            UpdateStreetText(EmptyStreetFallbackText);
        }

        private void UpdateStreetText(string displayText)
        {
            if (StreetNameText != null)
            {
                StreetNameText.text = displayText;
            }
        }
    }
}
