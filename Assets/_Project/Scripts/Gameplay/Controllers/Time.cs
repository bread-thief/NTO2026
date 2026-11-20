using System;
using UnityEngine;

namespace _Project.Scripts.Gameplay.Controllers
{
	[Serializable]
	public class Time
	{
		[field: SerializeField, Range(0, 23)] public int Hour { get; private set; }
		[field: SerializeField, Range(0, 60)] public int Minute { get; private set; }

		public float TotalMinutes => Hour * 60f + Minute;

		public Time()
		{
			Hour = 0;
			Minute = 0;
		}

		public Time(int hour, int minute)
		{
			Hour = Mathf.Clamp(hour, 0, 23);
			Minute = Mathf.Clamp(minute, 0, 59);
		}

		public static Time FromTotalMinutes(int totalMinutes)
		{
			int normalized = ((totalMinutes % (24 * 60)) + 24 * 60) % (24 * 60);
			return new Time(normalized / 60, normalized % 60);
		}

		public static Time FromTotalMinutes(float totalMinutes)
		{
			return FromTotalMinutes(Mathf.RoundToInt(totalMinutes));
		}

		public Time AddMinutes(int minutes)
		{
			return FromTotalMinutes(Mathf.RoundToInt(TotalMinutes) + minutes);
		}

		public int DifferenceInMinutes(Time other)
		{
			return Mathf.RoundToInt(TotalMinutes - other.TotalMinutes);
		}

		public bool IsAfter(Time other)
		{
			return TotalMinutes > other.TotalMinutes;
		}

		public bool IsBefore(Time other)
		{
			return TotalMinutes < other.TotalMinutes;
		}

		public bool IsBetween(Time start, Time end)
		{
			float current = TotalMinutes;
			float startTotal = start.TotalMinutes;
			float endTotal = end.TotalMinutes;

			if (startTotal <= endTotal)
			{
				return current >= startTotal && current <= endTotal;
			}

			return current >= startTotal || current <= endTotal;
		}

		public override string ToString()
		{
			return $"{Hour:00}:{Minute:00}";
		}
	}
}