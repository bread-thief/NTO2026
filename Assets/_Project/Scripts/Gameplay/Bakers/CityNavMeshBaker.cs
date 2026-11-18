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

        [field: SerializeField] public CityGenerator Generator { get; private set; }
        [field: SerializeField] public NavMeshSurface Surface { get; private set; }
        [field: SerializeField] public LayerMask IncludedLayers { get; private set; } = ~0;

        public event Action OnNavMeshBaked;

        public bool IsNavMeshBaked { get; private set; }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();

            if (Generator != null)
            {
                Generator.OnGenerated += HandleCityGenerated;
            }
        }

        private void OnDisable()
        {
            if (Generator != null)
            {
                Generator.OnGenerated -= HandleCityGenerated;
            }
        }

        private void Start()
        {
            if (!IsNavMeshBaked && Generator != null && Generator.transform.childCount > 0)
            {
                BakeRoadNavMesh();
            }
        }

        [ContextMenu("Bake Road NavMesh")]
        public void BakeRoadNavMesh()
        {
            ResolveReferences();

            if (Surface == null)
            {
                return;
            }

            int roadAreaIndex = ResolveRoadAreaIndex();

            Surface.layerMask = IncludedLayers;
            Surface.defaultArea = roadAreaIndex;

            if (Generator != null && Surface.gameObject != Generator.gameObject)
            {
                Surface.collectObjects = CollectObjects.All;
            }

            if (Generator != null)
            {
                ConfigureNavMeshModifiers(Generator.transform, roadAreaIndex);

                if (Surface.useGeometry == NavMeshCollectGeometry.PhysicsColliders && !HasAnyRoadCollider(Generator.transform))
                {
                    Surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
                }
            }

            Surface.BuildNavMesh();

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
            if (Generator == null)
            {
                Generator = GetComponent<CityGenerator>();

                if (Generator == null)
                {
                    Generator = FindAnyObjectByType<CityGenerator>();
                }
            }

            if (Surface == null)
            {
                Surface = GetComponent<NavMeshSurface>();

                if (Surface == null)
                {
                    Surface = FindAnyObjectByType<NavMeshSurface>();
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
