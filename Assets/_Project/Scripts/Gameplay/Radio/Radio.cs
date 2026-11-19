using UnityEngine;

namespace _Project.Scripts.Gameplay.Radio
{
    public class Radio : MonoBehaviour
    {
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip[] _music;

        private void Start()
        {
            _audioSource.PlayOneShot(_music[Random.Range(0, _music.Length - 1)]);
        }
    }
}
