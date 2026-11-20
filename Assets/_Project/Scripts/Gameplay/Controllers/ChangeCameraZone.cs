using UnityEngine;
using UnityEngine.EventSystems;

namespace _Project.Scripts.Gameplay.Controllers
{
    public class ChangeCameraZone : MonoBehaviour, IPointerEnterHandler
    {
        [SerializeField] private CameraController _cameraController;
        [SerializeField] private int _cameraID;
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            _cameraController.ChangeCamera(_cameraID);
        }
    }
}