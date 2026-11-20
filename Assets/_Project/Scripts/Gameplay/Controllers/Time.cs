using System;
using UnityEngine;

namespace _Project.Scripts.Gameplay.Controllers
{
	[Serializable]
	public class Time
	{
		[field: SerializeField, Range(0, 23)] public int Hour {get; private set;}
		[field: SerializeField, Range(0, 60)] public int Minute {get; private set;}
	}
}