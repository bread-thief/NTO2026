using System.Collections;
using System.Collections.Generic;
using _Project.Scripts.Gameplay.Controllers;
using _Project.Scripts.Gameplay.Obstacles;
using _Project.Scripts.Gameplay.Radio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Time = UnityEngine.Time;

namespace _Project.Scripts.Meta.UI
{
	public class ShiftResultView : MonoBehaviour
	{
		private const int MAX_SCORE = 5;
		private const float STAR_ANIM_DURATION = 0.4f;
		private const float STAR_ANIM_DELAY = 0.15f;
		private const float TEXT_DELAY = 0.4f;

		[SerializeField] private GameObject _panelRoot;
		[SerializeField] private Transform _starsContainer;
		[SerializeField] private GameObject _starPrefab;
		[SerializeField] private Sprite _fullStarSprite;
		[SerializeField] private Sprite _halfStarSprite;
		[SerializeField] private Sprite _emptyStarSprite;
		[SerializeField] private TMP_Text _scoreText;
		[SerializeField] private TMP_Text[] _statsTexts;
		[SerializeField] private GameObject _buttonsContainer;
		[SerializeField] private Button _restartButton;
		[SerializeField] private Button _quitButton;
		[SerializeField] private int _sceneIdToRestart = 0;

		[SerializeField] private TimeController _timeController;
		[SerializeField] private RoutePlannerController _routePlannerController;
		[SerializeField] private RoadObstacleController _obstacleController;
		[SerializeField] private CameraController _cameraController;
		[SerializeField] private FadeController _fadeController;
		[SerializeField] private Radio _radio;

		[SerializeField] private int _penaltyPerAccident = 1;
		[SerializeField] private int _penaltyPerMissedOrder = 2;
		[SerializeField] private int _penaltyForOvertimeMinutes = 1;
		[SerializeField] private int _overtimeThresholdMinutes = 30;

		private readonly List<GameObject> _spawnedStars = new List<GameObject>();
		private bool _isShowing;

		private void Awake()
		{
			if (_timeController == null)
			{
				_timeController = FindAnyObjectByType<TimeController>();
			}

			if (_routePlannerController == null)
			{
				_routePlannerController = FindAnyObjectByType<RoutePlannerController>();
			}

			if (_obstacleController == null)
			{
				_obstacleController = FindAnyObjectByType<RoadObstacleController>();
			}

			if (_cameraController == null)
			{
				_cameraController = FindAnyObjectByType<CameraController>();
			}

			if (_fadeController == null)
			{
				_fadeController = FindAnyObjectByType<FadeController>();
			}

			if (_radio == null)
			{
				_radio = FindAnyObjectByType<Radio>();
			}

			if (_panelRoot != null)
			{
				_panelRoot.SetActive(false);
			}

			if (_restartButton != null)
			{
				_restartButton.onClick.AddListener(HandleRestartClicked);
			}

			if (_quitButton != null)
			{
				_quitButton.onClick.AddListener(HandleQuitClicked);
			}
		}

		private void OnEnable()
		{
			if (_timeController != null)
			{
				_timeController.OnShiftEnded += HandleShiftEnded;
			}

			if (_routePlannerController != null)
			{
				_routePlannerController.OnAllTripsCompleted += HandleAllTripsCompleted;
			}
		}

		private void OnDisable()
		{
			if (_timeController != null)
			{
				_timeController.OnShiftEnded -= HandleShiftEnded;
			}

			if (_routePlannerController != null)
			{
				_routePlannerController.OnAllTripsCompleted -= HandleAllTripsCompleted;
			}
		}

		private void HandleShiftEnded()
		{
			ShowResults();
		}

		private void HandleAllTripsCompleted()
		{
			ShowResults();
		}

		public void ShowResults()
		{
			if (_isShowing)
			{
				return;
			}

			_isShowing = true;
			StopAllCoroutines();
			StartCoroutine(ShowResultsRoutine());
		}

		private IEnumerator ShowResultsRoutine()
		{
			if (_radio != null)
			{
				_radio.ShutDownRadio();
			}

			if (_fadeController != null)
			{
				yield return _fadeController.FadeRoutine(1f, 1f);
			}

			if (_cameraController != null)
			{
				yield return _cameraController.ResetCameraWithAnimationRoutine();
			}

			if (_panelRoot != null)
			{
				_panelRoot.SetActive(true);
			}

			ClearStars();

			if (_scoreText != null)
			{
				_scoreText.gameObject.SetActive(false);
			}

			if (_statsTexts != null)
			{
				for (int index = 0; index < _statsTexts.Length; index++)
				{
					if (_statsTexts[index] != null)
					{
						_statsTexts[index].gameObject.SetActive(false);
					}
				}
			}

			if (_buttonsContainer != null)
			{
				_buttonsContainer.SetActive(false);
			}

			float score = CalculateScore();
			SpawnStars();
			yield return AnimateStars(score);

			if (_scoreText != null)
			{
				_scoreText.gameObject.SetActive(true);
				_scoreText.text = $"{score:0.#}/{MAX_SCORE}";
			}

			yield return new WaitForSecondsRealtime(TEXT_DELAY);

			yield return ShowStatsRoutine();

			if (_buttonsContainer != null)
			{
				_buttonsContainer.SetActive(true);
			}

			if (_fadeController != null)
			{
				yield return _fadeController.FadeRoutine(0f, 1f);
			}
		}

		private float CalculateScore()
		{
			int accidents = _obstacleController != null ? _obstacleController.TotalPenalties : 0;
			int completedTrips = _routePlannerController != null ? _routePlannerController.CompletedTrips : 0;
			int maxTrips = _routePlannerController != null ? _routePlannerController.MaxTrips : 0;
			int missedOrders = Mathf.Max(0, maxTrips - completedTrips);

			float overtime = 0f;

			if (_timeController != null && _timeController.CurrentTime != null && _timeController.ShiftEnd != null)
			{
				overtime = _timeController.CurrentTime.DifferenceInMinutes(_timeController.ShiftEnd);
				overtime = Mathf.Max(0f, overtime);
			}

			float score = MAX_SCORE;
			score -= accidents * _penaltyPerAccident;
			score -= missedOrders * _penaltyPerMissedOrder;

			if (overtime > _overtimeThresholdMinutes)
			{
				score -= Mathf.Ceil((overtime - _overtimeThresholdMinutes) / 30f) * _penaltyForOvertimeMinutes;
			}

			return Mathf.Clamp(score, 0f, MAX_SCORE);
		}

		private void SpawnStars()
		{
			ClearStars();

			for (int index = 0; index < MAX_SCORE; index++)
			{
				GameObject star = Instantiate(_starPrefab, _starsContainer);
				star.transform.localScale = Vector3.zero;
				_spawnedStars.Add(star);
			}
		}

		private IEnumerator AnimateStars(float score)
		{
			for (int index = 0; index < _spawnedStars.Count; index++)
			{
				GameObject star = _spawnedStars[index];

				if (star == null)
				{
					continue;
				}

				Image image = star.GetComponent<Image>();

				if (image != null)
				{
					float starValue = score - index;

					if (starValue >= 1f)
					{
						image.sprite = _fullStarSprite;
					}
					else if (starValue >= 0.5f)
					{
						image.sprite = _halfStarSprite;
					}
					else
					{
						image.sprite = _emptyStarSprite;
					}
				}

				StartCoroutine(ScaleStarRoutine(star.transform, STAR_ANIM_DURATION));
				yield return new WaitForSecondsRealtime(STAR_ANIM_DELAY);
			}

			yield return new WaitForSecondsRealtime(STAR_ANIM_DURATION);
		}

		private IEnumerator ScaleStarRoutine(Transform target, float duration)
		{
			float elapsed = 0f;

			while (elapsed < duration)
			{
				elapsed += Time.unscaledDeltaTime;
				float t = Mathf.Clamp01(elapsed / duration);
				float scale = 1f - Mathf.Pow(1f - t, 3f);
				target.localScale = Vector3.one * scale;
				yield return null;
			}

			target.localScale = Vector3.one;
		}

		private IEnumerator ShowStatsRoutine()
		{
			if (_statsTexts == null)
			{
				yield break;
			}

			for (int index = 0; index < _statsTexts.Length; index++)
			{
				TMP_Text text = _statsTexts[index];

				if (text == null)
				{
					continue;
				}

				string line = BuildStatLine(index);

				if (string.IsNullOrEmpty(line))
				{
					text.gameObject.SetActive(false);
					continue;
				}

				text.text = line;
				text.gameObject.SetActive(true);

				yield return new WaitForSecondsRealtime(TEXT_DELAY);
			}
		}

		private string BuildStatLine(int index)
		{
			int accidents = _obstacleController != null ? _obstacleController.TotalPenalties : 0;
			int penaltyMinutes = _obstacleController != null ? _obstacleController.TotalPenaltyMinutes : 0;
			int completedTrips = _routePlannerController != null ? _routePlannerController.CompletedTrips : 0;
			int maxTrips = _routePlannerController != null ? _routePlannerController.MaxTrips : 0;

			float shiftMinutes = 0f;

			if (_timeController != null && _timeController.CurrentTime != null && _timeController.ShiftStart != null)
			{
				shiftMinutes = Mathf.Max(0f, _timeController.CurrentTime.DifferenceInMinutes(_timeController.ShiftStart));
			}

			float plannedMinutes = 0f;

			if (_timeController != null && _timeController.ShiftStart != null && _timeController.ShiftEnd != null)
			{
				plannedMinutes = Mathf.Max(0f, _timeController.ShiftEnd.DifferenceInMinutes(_timeController.ShiftStart));
			}

			float avgMinutesPerTrip = 0f;

			if (completedTrips > 0)
			{
				avgMinutesPerTrip = shiftMinutes / completedTrips;
			}

			float travelMinutes = Mathf.Max(0f, shiftMinutes - penaltyMinutes);

			switch (index)
			{
				case 0:
					return $"Заказов выполнено: {completedTrips}/{maxTrips}";
				case 1:
					return $"Аварий: {accidents}";
				case 2:
					return $"Штрафное время: {penaltyMinutes} мин";
				case 3:
					return $"Время в пути: {Mathf.RoundToInt(travelMinutes)} мин";
				case 4:
					return $"Общее время смены: {Mathf.RoundToInt(shiftMinutes)} мин (план {Mathf.RoundToInt(plannedMinutes)})";
				case 5:
					return $"Среднее на заказ: {Mathf.RoundToInt(avgMinutesPerTrip)} мин";
				case 6:
					return $"Осталось времени: {Mathf.RoundToInt(Mathf.Max(0f, plannedMinutes - shiftMinutes))} мин";
				default:
					return string.Empty;
			}
		}

		private void ClearStars()
		{
			for (int index = 0; index < _spawnedStars.Count; index++)
			{
				if (_spawnedStars[index] != null)
				{
					Destroy(_spawnedStars[index]);
				}
			}

			_spawnedStars.Clear();
		}

		private void HandleRestartClicked()
		{
			UnityEngine.SceneManagement.SceneManager.LoadScene(_sceneIdToRestart);
		}

		private void HandleQuitClicked()
		{
			UnityEngine.SceneManagement.SceneManager.LoadScene(0);
		}
	}
}