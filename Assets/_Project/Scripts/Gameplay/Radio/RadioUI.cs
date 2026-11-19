using System.Collections;
using UnityEngine;
using TMPro;

namespace _Project.Scripts.Gameplay.Radio
{
    public class RadioUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _textMeshProUGUI;
        [SerializeField] private AudioSource _audioSource;
        private bool _isTyping;
        private bool _canSkip;

        private void Skip()
        {
            if (_isTyping)
            {
                _isTyping = false;
            }
            else
            {
                _canSkip = true;
            }
        }

        public IEnumerator PrintText(string text)
        {
            _textMeshProUGUI.text = text;
            _textMeshProUGUI.maxVisibleCharacters = 0;
            _isTyping = true;
            for (int i = 1; i <= text.Length; i++)
            {
                if (!_isTyping) break;
                _textMeshProUGUI.maxVisibleCharacters = i;
                _audioSource.PlayOneShot(_audioSource.clip);
                yield return new WaitForSeconds(0.1f);
            }
            
            if (_isTyping == false)
            {
                _textMeshProUGUI.maxVisibleCharacters = text.Length;
                yield return new WaitForEndOfFrame();
            }
            else
            {
                _textMeshProUGUI.maxVisibleCharacters = text.Length;
                yield return new WaitForSeconds(0.3f);
            }
            
            _isTyping = false;
            _canSkip = false;
            yield return new WaitUntil((() => _canSkip));
        }
    }
}