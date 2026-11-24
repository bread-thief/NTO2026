using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace _Project.Scripts.Gameplay.Radio
{
    public class Radio : MonoBehaviour
    {
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip[] _music;
        [SerializeField] private bool _playOnStart = true;
        [SerializeField] private RadioUI _radioUI;
        
        private List<AudioClip> _shuffledPlaylist = new List<AudioClip>();
        private int _currentIndex;
        private AudioClip _currentTrack;
        
        public bool IsPlayingMusic { get; private set; }

        private void Start()
        {
            if (_playOnStart && _music.Length > 0)
            {
                ShufflePlaylist();
                PlayCurrentTrack();
            }
        }
        
        private void Update()
        {
            if (IsPlayingMusic && !_audioSource.isPlaying && _audioSource.time == 0f)
            {
                IsPlayingMusic = false;
                NextTrack();
            }
        }
        
        private void PlayCurrentTrack()
        {
            AudioClip clip = 
            _currentTrack = _shuffledPlaylist[_currentIndex];

            _audioSource.clip = clip;
            _audioSource.Play();
            IsPlayingMusic = true;
        }
        
        private void ShufflePlaylist()
        {
            _shuffledPlaylist.Clear();
            _shuffledPlaylist.AddRange(_music);
            _currentIndex = 0;
            
            for (int i = _shuffledPlaylist.Count - 1; i > 0; i--)
            {
                int randomIndex = Random.Range(0, i + 1);
                AudioClip temp = _shuffledPlaylist[i];
                _shuffledPlaylist[i] = _shuffledPlaylist[randomIndex];
                _shuffledPlaylist[randomIndex] = temp;
            }
            
            if (_shuffledPlaylist.Count > 1 && _shuffledPlaylist[0] == _currentTrack)
            {
                int lastIdx = _shuffledPlaylist.Count - 1;
                AudioClip temp = _shuffledPlaylist[0];
                _shuffledPlaylist[0] = _shuffledPlaylist[lastIdx];
                _shuffledPlaylist[lastIdx] = temp;
            }
        }
        
        public void NextTrack()
        {
            if (_music.Length == 0) return;
            
            _currentIndex++;
            if (_currentIndex >= _shuffledPlaylist.Count)
            {
                ShufflePlaylist();
            }

            PlayCurrentTrack();
        }
        
        public void SendMessageByRadio(string message)
        {
            StartCoroutine(Message(message));
        }

        public void ChangeMusic(int musicID)
        {
            _audioSource.clip = _music[musicID]; 
            _audioSource.Play();
            IsPlayingMusic = true;
        }

        
        // Deprecated. Best practice is using ShufflePlaylist()
        public void ChangeMusicByRandom()
        {
            _audioSource.PlayOneShot(_music[Random.Range(0, _music.Length - 1)]);
            IsPlayingMusic = true;
        }

        public IEnumerator Message(string message)
        {
            IsPlayingMusic = false;
            _audioSource.Pause();
            yield return _radioUI.PrintText(message);
            _audioSource.UnPause();
            IsPlayingMusic = true;
        }
    }
}
