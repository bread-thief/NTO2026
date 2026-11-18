using System;
using UnityEngine;
using UnityEngine.InputSystem;
using _Project.Scripts.Gameplay.Readers;

namespace _Project.Scripts.Gameplay.Handlers
{
    public class InputHandler : MonoBehaviour
    {
        [SerializeField] private InputReader _reader;

        public event Action<Vector2> OnPrimaryPressed;
        public event Action<Vector2> OnSecondaryPressed;
        public event Action<Vector2> OnMiddlePressed;

        private Vector2 pointerScreenPosition;

        public Vector2 PointerScreenPosition => pointerScreenPosition;

        private void Awake()
        {
            if (_reader == null)
            {
                _reader = ScriptableObject.CreateInstance<InputReader>();
            }
        }

        private void OnEnable()
        {
            if (_reader != null)
            {
                _reader.PositionAction += HandlePosition;
                _reader.PrimaryPressed += HandlePrimary;
                _reader.SecondaryPressed += HandleSecondary;
                _reader.MiddlePressed += HandleMiddle;
            }
        }

        private void OnDisable()
        {
            if (_reader != null)
            {
                _reader.PositionAction -= HandlePosition;
                _reader.PrimaryPressed -= HandlePrimary;
                _reader.SecondaryPressed -= HandleSecondary;
                _reader.MiddlePressed -= HandleMiddle;
            }
        }

        private void HandlePosition(Vector2 position)
        {
            pointerScreenPosition = position;
        }

        private void HandlePrimary()
        {
            OnPrimaryPressed?.Invoke(pointerScreenPosition);
        }

        private void HandleSecondary()
        {
            OnSecondaryPressed?.Invoke(pointerScreenPosition);
        }

        private void HandleMiddle()
        {
            OnMiddlePressed?.Invoke(pointerScreenPosition);
        }
    }
}