using System.Collections;
using System.Collections.Generic;
using _Project.Scripts.Meta.UI;
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
            if (IsPlayingMusic && !_audioSource.isPlaying && _audioSource.timeSamples == 0)
            {
                IsPlayingMusic = false;
                NextTrack();
            }
        }

        private void PlayCurrentTrack()
        {
            if (_shuffledPlaylist.Count == 0)
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
            if (_music == null || _music.Length == 0)
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
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            _messageQueue.Enqueue(message);

            if (_messageRoutine == null)
            {
                _messageRoutine = StartCoroutine(ProcessMessageQueue());
            }
        }

        public void ChangeMusic(int musicID)
        {
            if (_music == null || musicID < 0 || musicID >= _music.Length)
            {
                return;
            }

            _audioSource.clip = _music[musicID];
            _audioSource.Play();
            IsPlayingMusic = true;
        }

        public void ChangeMusicByRandom()
        {
            if (_music == null || _music.Length == 0)
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
                _audioSource.Pause();

                yield return _radioUI.PrintText(message);

                _audioSource.UnPause();
                IsPlayingMusic = true;
            }

            _messageRoutine = null;
        }
    }
}