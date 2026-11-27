using System;
using _Project.Scripts.Gameplay.Configs;
using UnityEngine;
using UnityTime = UnityEngine.Time;

namespace _Project.Scripts.Gameplay.Controllers
{
    public class TimeController : MonoBehaviour
    {
        private const float REAL_SECONDS_PER_GAME_MINUTE = 0.5f;

        [SerializeField] private Time _shiftStart;
        [SerializeField] private Time _shiftEnd;
        [SerializeField] private float _realSecondsPerGameMinute = REAL_SECONDS_PER_GAME_MINUTE;
        [SerializeField] private bool _autoAdvanceTime = true;
        [SerializeField] private Time _currentTime;

        public event Action<Time> OnTimeChanged;
        public event Action OnShiftStarted;
        public event Action OnShiftEnded;
        public event Action<int> OnMinutesAdded;

        public Time CurrentTime => _currentTime;
        public Time ShiftStart => _shiftStart;
        public Time ShiftEnd => _shiftEnd;
        public bool IsShiftActive { get; private set; }
        public bool IsShiftEnded { get; private set; }
        public float RealSecondsPerGameMinute => _realSecondsPerGameMinute;

        private float _accumulatedSeconds;

        private void Awake()
        {
            if (_shiftStart == null)
            {
                _shiftStart = new Time(10, 0);
            }

            if (_shiftEnd == null)
            {
                _shiftEnd = new Time(17, 0);
            }

            if (_currentTime == null)
            {
                _currentTime = new Time(_shiftStart.Hour, _shiftStart.Minute);
            }

            IsShiftActive = false;
            IsShiftEnded = false;
        }

        public void ApplyShiftConfig(ShiftConfig config)
        {
            if (config == null)
            {
                return;
            }

            _shiftStart = new Time(config.ShiftStart.Hour, config.ShiftStart.Minute);
            _shiftEnd = new Time(config.ShiftEnd.Hour, config.ShiftEnd.Minute);
            _currentTime = new Time(_shiftStart.Hour, _shiftStart.Minute);
            _accumulatedSeconds = 0f;
            IsShiftActive = false;
            IsShiftEnded = false;
        }

        private void Start()
        {
            StartShift();
        }

        private void Update()
        {
            if (!IsShiftActive || !_autoAdvanceTime || _realSecondsPerGameMinute <= 0f)
            {
                return;
            }

            _accumulatedSeconds += UnityTime.deltaTime;

            while (_accumulatedSeconds >= _realSecondsPerGameMinute)
            {
                _accumulatedSeconds -= _realSecondsPerGameMinute;
                AdvanceMinutes(1, false);
            }
        }

        public void StartShift()
        {
            _currentTime = new Time(_shiftStart.Hour, _shiftStart.Minute);
            _accumulatedSeconds = 0f;
            IsShiftActive = true;
            IsShiftEnded = false;
            OnShiftStarted?.Invoke();
            OnTimeChanged?.Invoke(_currentTime);
        }

        public void AddMinutes(int minutes)
        {
            if (minutes <= 0)
            {
                return;
            }

            AdvanceMinutes(minutes, true);
        }

        public void AddMinutesForPlayerMinute(int playerMinutes, int gameMinutesPerPlayerMinute)
        {
            if (playerMinutes <= 0 || gameMinutesPerPlayerMinute <= 0)
            {
                return;
            }

            AddMinutes(playerMinutes * gameMinutesPerPlayerMinute);
        }

        public void SetTime(Time time)
        {
            if (time == null)
            {
                return;
            }

            _currentTime = new Time(time.Hour, time.Minute);
            OnTimeChanged?.Invoke(_currentTime);
            CheckShiftEnd();
        }

        public void SetTime(int hour, int minute)
        {
            SetTime(new Time(hour, minute));
        }

        public void ResetShift()
        {
            StartShift();
        }

        public void PauseTime()
        {
            _autoAdvanceTime = false;
        }

        public void ResumeTime()
        {
            _autoAdvanceTime = true;
        }

        public bool HasTimeFor(Time targetTime)
        {
            return _currentTime.TotalMinutes < targetTime.TotalMinutes;
        }

        public int GetRemainingMinutes()
        {
            float remaining = _shiftEnd.TotalMinutes - _currentTime.TotalMinutes;

            if (remaining < 0f)
            {
                remaining += 24 * 60;
            }

            return Mathf.RoundToInt(remaining);
        }

        private void AdvanceMinutes(int minutes, bool notify)
        {
            if (minutes <= 0)
            {
                return;
            }

            _currentTime = _currentTime.AddMinutes(minutes);

            if (notify)
            {
                OnMinutesAdded?.Invoke(minutes);
            }

            OnTimeChanged?.Invoke(_currentTime);
            CheckShiftEnd();
        }

        private void CheckShiftEnd()
        {
            if (!IsShiftActive || IsShiftEnded)
            {
                return;
            }

            bool ended;

            float current = _currentTime.TotalMinutes;
            float start = _shiftStart.TotalMinutes;
            float end = _shiftEnd.TotalMinutes;

            if (start <= end)
            {
                ended = current >= end;
            }
            else
            {
                ended = current >= end && current < start;
            }

            if (ended)
            {
                IsShiftActive = false;
                IsShiftEnded = true;
                OnShiftEnded?.Invoke();
            }
        }
    }
}