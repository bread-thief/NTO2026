using System.Collections;
using TMPro;
using UnityEngine;

namespace _Project.Scripts.Gameplay.Radio
{
    public class RadioUI : MonoBehaviour
    {
        [SerializeField] private GameObject _textBoxPanel;
        [SerializeField] private TextMeshProUGUI _textMeshProUGUI;
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private float _darkenDuration = 0.4f;
        [SerializeField] private float _typewriterDelay = 0.1f;
        [SerializeField] private float _messageHoldDuration = 5f;

        public IEnumerator PrintText(string text)
        {
            if (_textBoxPanel == null || _textMeshProUGUI == null)
            {
                yield break;
            }

            _textBoxPanel.SetActive(true);

            if (_audioSource != null && _audioSource.clip != null)
            {
                _audioSource.Play();
            }

            _textMeshProUGUI.text = text;
            _textMeshProUGUI.maxVisibleCharacters = 0;

            yield return new WaitForSecondsRealtime(_darkenDuration);

            for (int i = 1; i <= text.Length; i++)
            {
                _textMeshProUGUI.maxVisibleCharacters = i;
                yield return new WaitForSeconds(_typewriterDelay);
            }

            if (_audioSource != null)
            {
                _audioSource.Stop();
            }

            yield return new WaitForSeconds(_messageHoldDuration);
            _textBoxPanel.SetActive(false);
        }

        public void ForceHide()
        {
            StopAllCoroutines();

            if (_textBoxPanel != null)
            {
                _textBoxPanel.SetActive(false);
            }

            if (_audioSource != null)
            {
                _audioSource.Stop();
            }

            if (_textMeshProUGUI != null)
            {
                _textMeshProUGUI.text = string.Empty;
                _textMeshProUGUI.maxVisibleCharacters = 0;
            }
        }
    }
}