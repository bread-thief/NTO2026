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
        [SerializeField] private AudioClip[] _mumbleClips;
        
        [Range(0.7f, 1.0f)]
        [SerializeField] private float _minPitch = 0.8f;
        [Range(0.8f, 1.2f)]
        [SerializeField] private float _maxPitch = 0.95f;

        public IEnumerator PrintText(string text)
        {
            _textBoxPanel.SetActive(true);
            _textMeshProUGUI.text = text;
            _textMeshProUGUI.maxVisibleCharacters = 0;
            for (int i = 1; i <= text.Length; i++)
            {
                _textMeshProUGUI.maxVisibleCharacters = i;
                if (i % 2 == 0)
                {
                    PlayRandomMumble();
                }
                yield return new WaitForSeconds(0.1f);
            }
            
            yield return new WaitForSeconds(5f);
            _textBoxPanel.SetActive(false);
        }
        private void PlayRandomMumble()
        {
            if (_mumbleClips.Length == 0 || _audioSource.isPlaying) return;

            _audioSource.pitch = Random.Range(_minPitch, _maxPitch);
            AudioClip clip = _mumbleClips[Random.Range(0, _mumbleClips.Length)];
            _audioSource.PlayOneShot(clip);
        }
    }
}