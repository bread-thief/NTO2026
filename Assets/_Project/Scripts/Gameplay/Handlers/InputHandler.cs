using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _Project.Scripts.Gameplay.Handlers
{
    public class InputHandler : MonoBehaviour
    {
        public event Action<Vector2> OnPrimaryPressed;

        public event Action<Vector2> OnSecondaryPressed;

        public event Action<Vector2> OnMiddlePressed;

        public Vector2 PointerScreenPosition
        {
            get
            {
                Mouse currentMouse = Mouse.current;

                if (currentMouse == null)
                {
                    return Vector2.zero;
                }

                return currentMouse.position.ReadValue();
            }
        }

        private void Update()
        {
            Mouse currentMouse = Mouse.current;

            if (currentMouse == null)
            {
                return;
            }

            Vector2 screenPosition = currentMouse.position.ReadValue();

            if (currentMouse.leftButton.wasPressedThisFrame)
            {
                OnPrimaryPressed?.Invoke(screenPosition);
            }

            if (currentMouse.rightButton.wasPressedThisFrame)
            {
                OnSecondaryPressed?.Invoke(screenPosition);
            }

            if (currentMouse.middleButton.wasPressedThisFrame)
            {
                OnMiddlePressed?.Invoke(screenPosition);
            }
        }
    }
}
