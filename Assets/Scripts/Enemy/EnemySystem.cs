using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

using UnityEngine;

using WeatherTheStorm.Helpers;

namespace WeatherTheStorm.Enemy
{
    public class EnemySystem : MonoBehaviour
    {
        [Header("Prototype")]
        [SerializeField] private GameObject m_EnemyPrefab;

        [Header("Scale")]
        [SerializeField, Min(0)] private int m_MaximumEnemyCount;

        //
        // Retained Data
        //

        private int                      m_EnemyCount;
        private NativeArray<EnemyHandle> m_Handles;
        private Transform[]              m_Transforms;
        private BoxCollider[]            m_BoxColliders;

        //
        // Transient Data
        //

        private NativeArray<float3>      m_Positions;
        private NativeArray<float3>      m_Scales;
        private NativeArray<AABB>        m_LocalBounds;
        private NativeArray<AABB>        m_WorldBounds;
        private NativeArray<EnemyHandle> m_BVHHandles;
        private NativeArray<BVHNode>     m_BVHNodes;

        private void Start()
        {
            m_Handles      = MemoryHelpers.PermanentAlloc<EnemyHandle>(m_MaximumEnemyCount);
            m_Transforms   = new Transform[m_MaximumEnemyCount];
            m_BoxColliders = new BoxCollider[m_MaximumEnemyCount];

            for (int enemyIdx = 0; enemyIdx < m_MaximumEnemyCount; ++enemyIdx)
            {
                InstantiateEnemy(m_EnemyPrefab);
            }
        }

        private void OnDestroy()
        {
            MemoryHelpers.SafeReleaseUnmanaged(m_Handles);
            MemoryHelpers.SafeReleaseUnmanaged(m_Positions);
            MemoryHelpers.SafeReleaseUnmanaged(m_Scales);
            MemoryHelpers.SafeReleaseUnmanaged(m_LocalBounds);
            MemoryHelpers.SafeReleaseUnmanaged(m_WorldBounds);
            MemoryHelpers.SafeReleaseUnmanaged(m_BVHHandles);
            MemoryHelpers.SafeReleaseUnmanaged(m_BVHNodes);
        }

        public void OnFrameEnter(
            out NativeArray<float3>.ReadOnly      outEnemyPositions,
            out NativeArray<float3>.ReadOnly      outEnemyScales,
            out NativeArray<EnemyHandle>.ReadOnly outEnemyHandles,
            out int                               outEnemyCount
            )
        {
            m_Positions = MemoryHelpers.RefreshTransientAlloc(m_EnemyCount, m_Positions);
            m_Scales    = MemoryHelpers.RefreshTransientAlloc(m_EnemyCount, m_Scales);
            for (int enemyIdx = 0; enemyIdx < m_EnemyCount; ++enemyIdx)
            {
                m_Positions[enemyIdx] = m_Transforms[enemyIdx].position;
                m_Scales[enemyIdx]    = m_Transforms[enemyIdx].lossyScale;
            }

            //
            // NOTE:
            // x) This might be a bit weird, because is there really something that modifies
            //    the colliders at runtime? It's easier to do this, so we stick with this.
            //

            m_LocalBounds = MemoryHelpers.RefreshTransientAlloc(m_EnemyCount, m_LocalBounds);
            for (int enemyIdx = 0; enemyIdx < m_EnemyCount; ++enemyIdx)
            {
                var boxCollider = m_BoxColliders[enemyIdx];

                m_LocalBounds[enemyIdx] = new AABB()
                {
                    Center  = boxCollider.center,
                    Extents = boxCollider.size * 0.5f,
                };
            }

            outEnemyPositions = m_Positions.AsReadOnly();
            outEnemyScales    = m_Scales.AsReadOnly();
            outEnemyHandles   = m_Handles.AsReadOnly();
            outEnemyCount     = m_EnemyCount;
        }

        public void OnFrameLeave()
        {
            for(int enemyIdx = 0; enemyIdx < m_EnemyCount; ++enemyIdx)
            {
                m_Transforms[enemyIdx].position = m_Positions[enemyIdx];
            }
        }

        public JobHandle ScheduleComputeWorldBounds(
            in  JobHandle                    dependency,
            in  NativeArray<float3>.ReadOnly enemyPositions,
            in  NativeArray<float3>.ReadOnly enemyScales,
            out NativeArray<AABB>            outWorldBounds
            )
        {
            m_WorldBounds = MemoryHelpers.RefreshTransientAlloc(m_EnemyCount, m_WorldBounds);

            var computeWorldBoundsJobHandle = new WorldBoundsJob()
            {
                Positions   = enemyPositions,
                Scales      = enemyScales,
                LocalBounds = m_LocalBounds.AsReadOnly(),
                WorldBounds = m_WorldBounds,
            }.Schedule(m_EnemyCount, 32, dependency);

            outWorldBounds = m_WorldBounds;

            return computeWorldBoundsJobHandle;
        }

        
        public JobHandle ScheduleBuildBVHJob(
            in  JobHandle          dependency,
            in  NativeArray<AABB>  worldBounds,
            out EnemyBVH           outEnemyBVH
            )
        {
            m_BVHHandles = MemoryHelpers.RefreshTransientAlloc(m_EnemyCount, m_BVHHandles);
            m_BVHNodes   = MemoryHelpers.RefreshTransientAlloc(m_EnemyCount * 2 - 1, m_BVHNodes);

            //
            // NOTE:
            // x) The reason we have to copy is that the build job will reorder the memory internally.
            //    If we wanted to preserve the world bounds array for some other job, we'd do the same
            //    thing.
            //

            for(int enemyIdx = 0; enemyIdx < m_EnemyCount; ++enemyIdx)
            {
                m_BVHHandles[enemyIdx] = m_Handles[enemyIdx];
            }

            var buildBVHJobHandle = new BuildBVHJob()
            {
                EnemyHandles = m_BVHHandles,
                WorldBounds  = worldBounds,
                OutNodes     = m_BVHNodes,
            }.Schedule(dependency);

            outEnemyBVH = new(nodes: m_BVHNodes.AsReadOnly(), handles: m_BVHHandles.AsReadOnly());

            return buildBVHJobHandle;
        }

        public JobHandle ScheduleEnemyDamageJob(JobHandle dependency)
        {
            var stubHandle = new JobHandle();
            return stubHandle;
        }

        //
        // TODO:
        // x) Rework this!
        //

        private void InstantiateEnemy(GameObject prefab)
        {
            //
            // TODO:
            // x) Allocate real handles.
            // x) Should check if it's a valid handle.
            //

            EnemyHandle handle = new()
            {
                Index   = m_EnemyCount,
                Version = 0,
            };

            var gameObject = Instantiate(prefab);
            var position   = new Vector3(UnityEngine.Random.Range(-50.0f, 50.0f), 0.0f, UnityEngine.Random.Range(-50.0f, 50.0f));

            m_Handles[handle.Index]      = handle;
            m_Transforms[handle.Index]   = gameObject.transform;
            m_BoxColliders[handle.Index] = gameObject.GetComponent<BoxCollider>();

            gameObject.transform.position = position;

            ++m_EnemyCount;
        }
    }

    public struct EnemyHandle
    {
        public int Index;
        public int Version;
        public static EnemyHandle Null => new() { Index = -1, Version = -1 };
    }

    public struct EnemyHealth
    {
        public float MaximumHP;
        public float CurrentHP;

        public EnemyHealth(float maximumHP)
        {
            MaximumHP = maximumHP;
            CurrentHP = 0.0f;
        }
    }
}