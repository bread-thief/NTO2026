using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _Project.Scripts.Gameplay.Readers
{
    [CreateAssetMenu(fileName = "InputReader", menuName = "_Project/InputReader", order = 0)]
    public class InputReader : ScriptableObject, Controls.IPlayerActions
    {
        public event Action MiddlePressed = delegate { };
        public event Action PrimaryPressed = delegate { };
        public event Action PrimaryReleased = delegate { };
        public event Action SecondaryPressed = delegate { };
        public event Action<Vector2> PositionAction = delegate { };
        public event Action<Vector2> DeltaAction = delegate { };

        private Controls _controls;

        private void OnEnable()
        {
            if (_controls == null)
            {
                _controls = new Controls();
                _controls.Player.SetCallbacks(this);
                _controls.Enable();
            }
        }

        private void OnDisable()
        {
            if (_controls == null)
            {
                return;
            }

            _controls.Player.SetCallbacks(null);
            _controls.Disable();
            _controls = null;
        }

        public void OnPosition(InputAction.CallbackContext context)
        {
            PositionAction?.Invoke(context.ReadValue<Vector2>());
        }
        
        public void OnDelta(InputAction.CallbackContext context)
        {
            DeltaAction?.Invoke(context.ReadValue<Vector2>());
        }

        public void OnPrimary(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                PrimaryPressed?.Invoke();
            }
            else if (context.canceled)
            {
                PrimaryReleased?.Invoke();
            }
        }

        public void OnSecondary(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                SecondaryPressed?.Invoke();
            }
        }

        public void OnMiddle(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                MiddlePressed?.Invoke();
            }
        }
    }
}