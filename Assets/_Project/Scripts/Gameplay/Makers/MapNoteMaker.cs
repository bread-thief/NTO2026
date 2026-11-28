using System.Collections.Generic;
using _Project.Scripts.Gameplay.Controllers;
using _Project.Scripts.Gameplay.Handlers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Project.Scripts.Gameplay.Makers
{
    public class MapNoteMaker : MonoBehaviour
    {
        [SerializeField] private MapMarkerMaker _mapMarkerMaker;
        [SerializeField] private InputHandler _inputHandler;
        [SerializeField] private Camera _mainCamera;
        [SerializeField] private LayerMask _mapLayer;
        [SerializeField] private float _maxRaycastDistance = 1000f;
        [SerializeField] private GameObject _noteInputPanel;
        [SerializeField] private TMP_Text _hourText;
        [SerializeField] private TMP_Text _minuteText;
        [SerializeField] private Button _upHourButton;
        [SerializeField] private Button _upMinuteButton;
        [SerializeField] private Button _downHourButton;
        [SerializeField] private Button _downMinuteButton;
        [SerializeField] private Button _noteAcceptButton;
        [SerializeField] private Button _noteCancelButton;
        [SerializeField] private Image _noteColorImage;
        [SerializeField] private GameObject _notePrefab;
        [SerializeField] private BoxCollider _boardCollider;
        [SerializeField] private float _slotSpacing = 0.5f;
        [SerializeField] private float _edgeOffset = 0.1f;
        [SerializeField] private float _noteHeight = 0.05f;
        [SerializeField] private float _positionJitter = 0.05f;
        [SerializeField] private float _minRotationJitter = -15f;
        [SerializeField] private float _maxRotationJitter = 15f;

        private readonly List<GameObject> _spawnedNotes = new List<GameObject>();
        private readonly Dictionary<Color, GameObject> _notesByColor = new Dictionary<Color, GameObject>();
        private readonly List<int> _availableSlotIndices = new List<int>();
        private bool _isAwaitingNote;
        private Vector3 _pendingPinWorldPosition;
        private Color _pendingColor;
        private int _hour = 10;
        private int _minute = 0;

        private void Awake()
        {
            if (_inputHandler == null)
            {
                _inputHandler = FindAnyObjectByType<InputHandler>();
            }

            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
            }

            if (_noteInputPanel != null)
            {
                _noteInputPanel.SetActive(false);
            }

            if (_noteAcceptButton != null)
            {
                _noteAcceptButton.onClick.AddListener(ConfirmNote);
            }

            if (_noteCancelButton != null)
            {
                _noteCancelButton.onClick.AddListener(CancelNote);
            }

            if (_upHourButton != null)
            {
                _upHourButton.onClick.AddListener(() => Up(true));
            }

            if (_upMinuteButton != null)
            {
                _upMinuteButton.onClick.AddListener(() => Up());
            }

            if (_downHourButton != null)
            {
                _downHourButton.onClick.AddListener(() => Down(true));
            }

            if (_downMinuteButton != null)
            {
                _downMinuteButton.onClick.AddListener(() => Down());
            }

            BuildAvailableSlots();
        }

        private void Up(bool isHour = false) => ChangeTime(1, isHour);
        private void Down(bool isHour = false) => ChangeTime(-1, isHour);

        private void ChangeTime(int delta, bool isHour)
        {
            if (isHour)
            {
                _hour = (_hour + delta + 24) % 24;
                _hourText.text = $"{_hour:00}";
            }
            else
            {
                _minute = (_minute + delta + 60) % 60;
                _minuteText.text = $"{_minute:00}";
            }
        }

        private void OnEnable()
        {
            if (_inputHandler != null)
            {
                _inputHandler.OnPrimaryPressed += HandlePrimaryPressed;
            }

            if (_mapMarkerMaker != null)
            {
                _mapMarkerMaker.OnPinRemoved += HandlePinRemoved;
            }
        }

        private void OnDisable()
        {
            if (_inputHandler != null)
            {
                _inputHandler.OnPrimaryPressed -= HandlePrimaryPressed;
            }

            if (_mapMarkerMaker != null)
            {
                _mapMarkerMaker.OnPinRemoved -= HandlePinRemoved;
            }
        }

        private void BuildAvailableSlots()
        {
            _availableSlotIndices.Clear();

            if (_boardCollider == null)
            {
                return;
            }

            float perimeter = GetPerimeter();

            if (perimeter <= Mathf.Epsilon)
            {
                return;
            }

            int slotCount = Mathf.Max(1, Mathf.FloorToInt(perimeter / _slotSpacing));

            for (int index = 0; index < slotCount; index++)
            {
                _availableSlotIndices.Add(index);
            }
        }

        private float GetPerimeter()
        {
            Vector3 localHalfSize = _boardCollider.size * 0.5f;

            float width = Mathf.Max(0f, localHalfSize.x * 2f - _edgeOffset * 2f);
            float length = Mathf.Max(0f, localHalfSize.z * 2f - _edgeOffset * 2f);

            return (width + length) * 2f;
        }

        private void HandlePrimaryPressed(Vector2 screenPosition)
        {
            if (_isAwaitingNote)
            {
                return;
            }

            if (_mapMarkerMaker == null || !_mapMarkerMaker.HasFreeColor)
            {
                return;
            }

            if (_mainCamera == null)
            {
                return;
            }

            Ray ray = _mainCamera.ScreenPointToRay(screenPosition);

            if (Physics.Raycast(ray, out RaycastHit hit, _maxRaycastDistance, _mapLayer, QueryTriggerInteraction.Ignore))
            {
                _pendingPinWorldPosition = hit.point;
                OpenNotePanel();
            }
        }

        private void OpenNotePanel()
        {
            if (_noteInputPanel == null)
            {
                return;
            }

            _isAwaitingNote = true;

            if (_mapMarkerMaker != null)
            {
                _mapMarkerMaker.SetCanSpawnPin(false);
                _pendingColor = _mapMarkerMaker.TakeColor();
            }

            if (_noteColorImage != null)
            {
                _noteColorImage.color = _pendingColor;
            }

            _noteInputPanel.SetActive(true);
            _hour = 10;
            _minute = 0;

            _hourText.text = $"{_hour:00}";
            _minuteText.text = $"{_minute:00}";
        }

        private void ConfirmNote()
        {
            if (!_isAwaitingNote)
            {
                return;
            }

            string noteText = $"{_hour:00}:{_minute:00}"; ;

            if (string.IsNullOrWhiteSpace(noteText))
            {
                return;
            }

            _mapMarkerMaker.SpawnPin(_pendingPinWorldPosition, _pendingColor);
            SpawnNote(noteText, _pendingColor);

            CloseNotePanel();
        }

        private void CancelNote()
        {
            CloseNotePanel();
        }

        private void CloseNotePanel()
        {
            _isAwaitingNote = false;

            if (_mapMarkerMaker != null)
            {
                if (!_notesByColor.ContainsKey(_pendingColor))
                {
                    _mapMarkerMaker.ReturnColor(_pendingColor);
                }

                _mapMarkerMaker.SetCanSpawnPin(true);
            }

            if (_noteInputPanel != null)
            {
                _noteInputPanel.SetActive(false);
            }
        }

        private void HandlePinRemoved(Color pinColor)
        {
            if (_notesByColor.TryGetValue(pinColor, out GameObject noteInstance))
            {
                _notesByColor.Remove(pinColor);

                if (noteInstance != null)
                {
                    _spawnedNotes.Remove(noteInstance);
                    Destroy(noteInstance);
                }
            }
        }

        private void SpawnNote(string noteText, Color noteColor)
        {
            if (_notePrefab == null || _boardCollider == null)
            {
                return;
            }

            if (_availableSlotIndices.Count == 0)
            {
                return;
            }

            int randomListIndex = Random.Range(0, _availableSlotIndices.Count);
            int slotIndex = _availableSlotIndices[randomListIndex];
            _availableSlotIndices.RemoveAt(randomListIndex);

            Transform boardTransform = _boardCollider.transform;
            Vector3 localPosition = GetSlotLocalPosition(slotIndex);

            localPosition.x += Random.Range(-_positionJitter, _positionJitter);
            localPosition.z += Random.Range(-_positionJitter, _positionJitter);

            Vector3 worldPosition = boardTransform.TransformPoint(localPosition);
            float randomYaw = Random.Range(_minRotationJitter, _maxRotationJitter);
            Quaternion worldRotation = boardTransform.rotation * Quaternion.Euler(0f, randomYaw, 0f);

            GameObject noteInstance = Instantiate(_notePrefab, worldPosition, worldRotation, boardTransform);

            SpriteRenderer spriteRenderer = noteInstance.GetComponentInChildren<SpriteRenderer>();

            if (spriteRenderer != null)
            {
                spriteRenderer.color = noteColor;
            }

            TMP_Text noteTextComponent = noteInstance.GetComponentInChildren<TMP_Text>();

            if (noteTextComponent != null)
            {
                noteTextComponent.text = noteText;
            }

            _spawnedNotes.Add(noteInstance);
            _notesByColor[noteColor] = noteInstance;
        }

        private Vector3 GetSlotLocalPosition(int slotIndex)
        {
            Vector3 localCenter = _boardCollider.center;
            Vector3 localHalfSize = _boardCollider.size * 0.5f;

            float leftX = localCenter.x - localHalfSize.x + _edgeOffset;
            float rightX = localCenter.x + localHalfSize.x - _edgeOffset;
            float bottomZ = localCenter.z - localHalfSize.z + _edgeOffset;
            float topZ = localCenter.z + localHalfSize.z - _edgeOffset;
            float y = localCenter.y + localHalfSize.y + _noteHeight;

            Vector3[] corners = new Vector3[4]
            {
                new Vector3(leftX, y, bottomZ),
                new Vector3(rightX, y, bottomZ),
                new Vector3(rightX, y, topZ),
                new Vector3(leftX, y, topZ)
            };

            float[] segmentLengths = new float[4];
            float perimeter = 0f;

            for (int index = 0; index < 4; index++)
            {
                segmentLengths[index] = Vector3.Distance(corners[index], corners[(index + 1) % 4]);
                perimeter += segmentLengths[index];
            }

            if (perimeter <= Mathf.Epsilon)
            {
                return localCenter;
            }

            float targetDistance = (slotIndex * _slotSpacing) % perimeter;
            float accumulatedDistance = 0f;
            Vector3 localPosition = corners[0];

            for (int index = 0; index < 4; index++)
            {
                float segmentLength = segmentLengths[index];

                if (targetDistance <= accumulatedDistance + segmentLength)
                {
                    float interpolation = (targetDistance - accumulatedDistance) / segmentLength;
                    localPosition = Vector3.Lerp(corners[index], corners[(index + 1) % 4], interpolation);
                    break;
                }

                accumulatedDistance += segmentLength;
            }

            return localPosition;
        }
    }
}