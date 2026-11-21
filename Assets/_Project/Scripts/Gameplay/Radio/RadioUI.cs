using System.Collections;
using UnityEngine;
using TMPro;

namespace _Project.Scripts.Gameplay.Radio
{
    public class RadioUI : MonoBehaviour
    {
        [SerializeField] private GameObject _textBoxPanel;
        [SerializeField] private TextMeshProUGUI _textMeshProUGUI;
        [SerializeField] private AudioSource _audioSource;

        public IEnumerator PrintText(string text)
        {
            _textBoxPanel.SetActive(true);
            if (_audioSource.clip != null)
            {
                _audioSource.Play();
            }
            _textMeshProUGUI.text = text;
            _textMeshProUGUI.maxVisibleCharacters = 0;
            for (int i = 1; i <= text.Length; i++)
            {
                _textMeshProUGUI.maxVisibleCharacters = i;
                yield return new WaitForSeconds(0.25f);
            }
            
            _audioSource.Stop();
            
            yield return new WaitForSeconds(5f);
            _textBoxPanel.SetActive(false);
        }
    }
}