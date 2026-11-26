using UnityEngine;
using UnityEngine.EventSystems;

namespace _Project.Scripts.Meta.UI
{
    public class ButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _hoverSound;
        [SerializeField] private AudioClip _clickSound;
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_audioSource != null && _hoverSound != null)
            {
                _audioSource.PlayOneShot(_hoverSound);
            }
        }
        public void OnPointerClick(PointerEventData eventData)
        {
            if (_audioSource != null && _clickSound != null)
            {
                _audioSource.PlayOneShot(_clickSound);
            }
        }
    }
}