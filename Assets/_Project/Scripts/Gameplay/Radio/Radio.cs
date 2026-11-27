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

        private readonly List<AudioClip> _shuffledPlaylist = new List<AudioClip>();
        private readonly Queue<string> _messageQueue = new Queue<string>();
        private int _currentIndex;
        private AudioClip _currentTrack;
        private Coroutine _messageRoutine;
        private bool _isShutDown;

        public bool IsPlayingMusic { get; private set; }

        private void Start()
        {
            if (_playOnStart && _music != null && _music.Length > 0)
            {
                ShufflePlaylist();
                PlayCurrentTrack();
            }
        }

        private void Update()
        {
            if (_isShutDown)
            {
                return;
            }

            if (IsPlayingMusic && _audioSource != null && !_audioSource.isPlaying && _audioSource.timeSamples == 0)
            {
                IsPlayingMusic = false;
                NextTrack();
            }
        }

        private void PlayCurrentTrack()
        {
            if (_shuffledPlaylist.Count == 0 || _audioSource == null)
            {
                return;
            }

            _currentTrack = _shuffledPlaylist[_currentIndex];
            _audioSource.clip = _currentTrack;
            _audioSource.Play();
            IsPlayingMusic = true;
        }

        private void ShufflePlaylist()
        {
            _shuffledPlaylist.Clear();
            _shuffledPlaylist.AddRange(_music);
            _currentIndex = 0;

            for (int index = _shuffledPlaylist.Count - 1; index > 0; index--)
            {
                int randomIndex = Random.Range(0, index + 1);
                AudioClip temp = _shuffledPlaylist[index];
                _shuffledPlaylist[index] = _shuffledPlaylist[randomIndex];
                _shuffledPlaylist[randomIndex] = temp;
            }

            if (_shuffledPlaylist.Count > 1 && _shuffledPlaylist[0] == _currentTrack)
            {
                int lastIndex = _shuffledPlaylist.Count - 1;
                AudioClip temp = _shuffledPlaylist[0];
                _shuffledPlaylist[0] = _shuffledPlaylist[lastIndex];
                _shuffledPlaylist[lastIndex] = temp;
            }
        }

        public void NextTrack()
        {
            if (_isShutDown || _music == null || _music.Length == 0)
            {
                return;
            }

            _currentIndex++;

            if (_currentIndex >= _shuffledPlaylist.Count)
            {
                ShufflePlaylist();
            }

            PlayCurrentTrack();
        }

        public void SendMessageByRadio(string message)
        {
            if (_isShutDown || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            _messageQueue.Enqueue(message);

            if (_messageRoutine == null)
            {
                _messageRoutine = StartCoroutine(ProcessMessageQueue());
            }
        }

        public void ShutDownRadio()
        {
            _isShutDown = true;
            _messageQueue.Clear();

            if (_messageRoutine != null)
            {
                StopCoroutine(_messageRoutine);
                _messageRoutine = null;
            }

            if (_audioSource != null)
            {
                _audioSource.Stop();
                _audioSource.clip = null;
            }

            if (_radioUI != null)
            {
                _radioUI.ForceHide();
            }

            IsPlayingMusic = false;
            enabled = false;
        }

        public void ChangeMusic(int musicID)
        {
            if (_isShutDown || _music == null || musicID < 0 || musicID >= _music.Length || _audioSource == null)
            {
                return;
            }

            _audioSource.clip = _music[musicID];
            _audioSource.Play();
            IsPlayingMusic = true;
        }

        public void ChangeMusicByRandom()
        {
            if (_isShutDown || _music == null || _music.Length == 0 || _audioSource == null)
            {
                return;
            }

            _audioSource.PlayOneShot(_music[Random.Range(0, _music.Length)]);
            IsPlayingMusic = true;
        }

        private IEnumerator ProcessMessageQueue()
        {
            while (_messageQueue.Count > 0)
            {
                string message = _messageQueue.Dequeue();

                IsPlayingMusic = false;

                if (_audioSource != null)
                {
                    _audioSource.Pause();
                }

                if (_radioUI != null)
                {
                    yield return _radioUI.PrintText(message);
                }

                if (_audioSource != null)
                {
                    _audioSource.UnPause();
                }

                IsPlayingMusic = true;
            }

            _messageRoutine = null;
        }
    }
}