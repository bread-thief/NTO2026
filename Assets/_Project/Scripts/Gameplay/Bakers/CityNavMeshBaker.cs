using System;
using _Project.Scripts.Gameplay.Generators;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace _Project.Scripts.Gameplay.Bakers
{
    public class CityNavMeshBaker : MonoBehaviour
    {
        private const string ROAD_AREA_NAME = "Road";
        private const int INVALID_AREA_INDEX = -1;
        private const int WALKABLE_AREA_INDEX = 0;

        [SerializeField] private CityGenerator _generator;
        [SerializeField] private NavMeshSurface _surface;
        [SerializeField] private LayerMask _includedLayers = ~0;

        public event Action OnNavMeshBaked;

        public bool IsNavMeshBaked { get; private set; }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();

            if (_generator != null)
            {
                _generator.OnGenerated += HandleCityGenerated;
            }
        }

        private void OnDisable()
        {
            if (_generator != null)
            {
                _generator.OnGenerated -= HandleCityGenerated;
            }
        }

        private void Start()
        {
            if (!IsNavMeshBaked && _generator != null && _generator.transform.childCount > 0)
            {
                BakeRoadNavMesh();
            }
        }

        [ContextMenu("Bake Road NavMesh")]
        public void BakeRoadNavMesh()
        {
            ResolveReferences();

            if (_surface == null)
            {
                return;
            }

            int roadAreaIndex = ResolveRoadAreaIndex();

            _surface.layerMask = _includedLayers;
            _surface.defaultArea = roadAreaIndex;

            if (_generator != null && _surface.gameObject != _generator.gameObject)
            {
                _surface.collectObjects = CollectObjects.All;
            }

            if (_generator != null)
            {
                ConfigureNavMeshModifiers(_generator.transform, roadAreaIndex);

                if (_surface.useGeometry == NavMeshCollectGeometry.PhysicsColliders && !HasAnyRoadCollider(_generator.transform))
                {
                    _surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
                }
            }

            _surface.BuildNavMesh();

            IsNavMeshBaked = true;
            OnNavMeshBaked?.Invoke();
        }

        private void HandleCityGenerated()
        {
            IsNavMeshBaked = false;
            BakeRoadNavMesh();
        }

        private void ResolveReferences()
        {
            if (_generator == null)
            {
                _generator = GetComponent<CityGenerator>();

                if (_generator == null)
                {
                    _generator = FindAnyObjectByType<CityGenerator>();
                }
            }

            if (_surface == null)
            {
                _surface = GetComponent<NavMeshSurface>();

                if (_surface == null)
                {
                    _surface = FindAnyObjectByType<NavMeshSurface>();
                }
            }
        }

        private int ResolveRoadAreaIndex()
        {
            int areaIndex = NavMesh.GetAreaFromName(ROAD_AREA_NAME);

            if (areaIndex == INVALID_AREA_INDEX)
            {
                return WALKABLE_AREA_INDEX;
            }

            return areaIndex;
        }

        private void ConfigureNavMeshModifiers(Transform cityRoot, int roadAreaIndex)
        {
            for (int childIndex = 0; childIndex < cityRoot.childCount; childIndex++)
            {
                Transform blockTransform = cityRoot.GetChild(childIndex);
                CityBlockPreset preset = blockTransform.GetComponent<CityBlockPreset>();

                if (preset == null)
                {
                    continue;
                }

                bool isRoadBlock = preset.RoadCount > 0;
                NavMeshModifier[] modifiers = blockTransform.GetComponentsInChildren<NavMeshModifier>(true);

                if (isRoadBlock)
                {
                    if (modifiers.Length == 0)
                    {
                        NavMeshModifier addedModifier = blockTransform.gameObject.AddComponent<NavMeshModifier>();
                        addedModifier.ignoreFromBuild = false;
                        addedModifier.overrideArea = true;
                        addedModifier.area = roadAreaIndex;
                    }
                    else
                    {
                        for (int modifierIndex = 0; modifierIndex < modifiers.Length; modifierIndex++)
                        {
                            NavMeshModifier currentModifier = modifiers[modifierIndex];

                            if (currentModifier.overrideArea && currentModifier.area == roadAreaIndex)
                            {
                                currentModifier.ignoreFromBuild = false;
                            }
                            else
                            {
                                currentModifier.ignoreFromBuild = true;
                            }
                        }
                    }
                }
                else
                {
                    if (modifiers.Length == 0)
                    {
                        NavMeshModifier ignoredModifier = blockTransform.gameObject.AddComponent<NavMeshModifier>();
                        ignoredModifier.ignoreFromBuild = true;
                    }
                    else
                    {
                        for (int modifierIndex = 0; modifierIndex < modifiers.Length; modifierIndex++)
                        {
                            modifiers[modifierIndex].ignoreFromBuild = true;
                        }
                    }
                }
            }
        }

        private bool HasAnyRoadCollider(Transform cityRoot)
        {
            for (int childIndex = 0; childIndex < cityRoot.childCount; childIndex++)
            {
                Transform blockTransform = cityRoot.GetChild(childIndex);
                CityBlockPreset preset = blockTransform.GetComponent<CityBlockPreset>();

                if (preset != null && preset.RoadCount > 0)
                {
                    Collider colliderComponent = blockTransform.GetComponentInChildren<Collider>(true);

                    if (colliderComponent != null && colliderComponent.enabled)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}