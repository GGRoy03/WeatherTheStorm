using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

using UnityEngine;

namespace WeatherTheStorm.Enemy
{

    public class EnemySystem : MonoBehaviour
    {
        [Header("Prototype")]
        [SerializeField] private GameObject m_EnemyPrefab;

        [Header("Scale")]
        [SerializeField, Min(0)] private int                      m_MaximumEnemyCount;
                                 private int                      m_EnemyCount;
                                 private NativeArray<AABB>        m_LocalBounds;
                                 private NativeArray<float3>      m_WorldPositions;
                                 private NativeArray<EnemyHandle> m_Handles;

        //
        // Transient data that must be allocated/de-allocated every frame
        // Maybe we should allocate this all at once anyway. Like, some sort of transient
        // data structure. Since we are the producers, we should know the exact amount we have
        // to allocate for arrays, and for queues it's whatever anyway.
        //

        private NativeArray<AABB>        m_OutWorldBounds;
        private NativeArray<EnemyHandle> m_OutBVHHandles;
        private NativeArray<BVHNode>     m_OutBVHNodes;


        private void Start()
        {
            m_LocalBounds    = new NativeArray<AABB>(m_MaximumEnemyCount, Allocator.Persistent);
            m_WorldPositions = new NativeArray<float3>(m_MaximumEnemyCount, Allocator.Persistent);
            m_Handles        = new NativeArray<EnemyHandle>(m_MaximumEnemyCount, Allocator.Persistent);

            for (int enemyIdx = 0; enemyIdx < m_MaximumEnemyCount; ++enemyIdx)
            {
                InstantiateEnemy(m_EnemyPrefab);
            }
        }

        private void OnDestroy()
        {
            if (m_LocalBounds.IsCreated)    m_LocalBounds.Dispose();
            if (m_WorldPositions.IsCreated) m_WorldPositions.Dispose();
            if (m_Handles.IsCreated)        m_Handles.Dispose();

            if (m_OutWorldBounds.IsCreated) m_OutWorldBounds.Dispose();
            if (m_OutBVHHandles.IsCreated)  m_OutBVHHandles.Dispose();
            if (m_OutBVHNodes.IsCreated)    m_OutBVHNodes.Dispose();
        }


        public JobHandle ScheduleEnemyMoveJob(
            out int                               outEnemyCount,
            out NativeArray<float3>.ReadOnly      outEnemyPositions,
            out NativeArray<EnemyHandle>.ReadOnly outEnemyHandles
            )
        {

            outEnemyCount     = m_EnemyCount;
            outEnemyPositions = m_WorldPositions.AsReadOnly();
            outEnemyHandles   = m_Handles.AsReadOnly();

            var stubDependency = new JobHandle();
            return stubDependency;
        }

        public JobHandle ScheduleComputeWorldBounds(
            in  JobHandle                    dependency,
            in  NativeArray<float3>.ReadOnly enemyPositions,
            out NativeArray<AABB>            outWorldBounds
            )
        {
            if(m_OutWorldBounds.IsCreated)
            {
                m_OutWorldBounds.Dispose();
            }
            m_OutWorldBounds = new NativeArray<AABB>(m_EnemyCount, Allocator.TempJob);

            var computeWorldBoundsJobHandle = new EnemyWorldBoundsJob()
            {
                Positions   = enemyPositions,
                LocalBounds = m_LocalBounds.AsReadOnly(),
                WorldBounds = m_OutWorldBounds,
            }.Schedule(m_EnemyCount, 32, dependency);

            outWorldBounds = m_OutWorldBounds;

            return computeWorldBoundsJobHandle;
        }

        
        public JobHandle ScheduleBuildBVHJob(
            in  JobHandle          dependency,
            in  NativeArray<AABB>  worldBounds,
            out EnemyBVH           outEnemyBVH
            )
        {
            if (m_OutBVHHandles.IsCreated)
            {
                m_OutBVHHandles.Dispose();
            }
            m_OutBVHHandles = new NativeArray<EnemyHandle>(m_EnemyCount, Allocator.TempJob);

            if(m_OutBVHNodes.IsCreated)
            {
                m_OutBVHNodes.Dispose();
            }
            m_OutBVHNodes = new NativeArray<BVHNode>(m_EnemyCount * 2 - 1, Allocator.TempJob);

            //
            // NOTE:
            // x) The reason we have to copy is that the build job will reorder the memory internally.
            //    If we wanted to preserve the world bounds array for some other job, we'd do the same
            //    thing.
            //

            for(int enemyIdx = 0; enemyIdx < m_EnemyCount; ++enemyIdx)
            {
                m_OutBVHHandles[enemyIdx] = m_Handles[enemyIdx];
            }

            var buildBVHJobHandle = new BuildBVHJob()
            {
                EnemyHandles = m_OutBVHHandles,
                WorldBounds  = worldBounds,
                OutNodes     = m_OutBVHNodes,
            }.Schedule(dependency);

            outEnemyBVH = new(nodes: m_OutBVHNodes.AsReadOnly(), handles: m_OutBVHHandles.AsReadOnly());

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

            //
            // TODO:
            // x) Factor this out.
            //

            GameObject gameObject = Instantiate(prefab);

            var collider = gameObject.GetComponent<BoxCollider>();
            if(collider != null)
            {
                Bounds  worldBounds = collider.bounds;
                Vector3 localCenter = collider.transform.InverseTransformPoint(worldBounds.center);
                Vector3 localExtent = collider.transform.InverseTransformDirection(worldBounds.extents);

                //
                // TODO: Check min stuff.
                //

                AABB localBounds = new()
                {
                    Center  = localCenter,
                    Extents = localExtent,
                };

                float3 position = new()
                {
                    x = UnityEngine.Random.Range(-10.0f, 10.0f),
                    y = 0.0f,
                    z = UnityEngine.Random.Range(-10.0f, 10.0f),
                };

                m_LocalBounds[handle.Index]    = localBounds;
                m_WorldPositions[handle.Index] = position;
                m_Handles[handle.Index]        = handle;

                gameObject.transform.position = position;
            }

            //
            // TODO:
            // x) Should actually check if we create an entity.
            //

            ++m_EnemyCount;
        }
    }

    public struct EnemyHandle
    {
        public int Index;
        public int Version;
        public static EnemyHandle Null => new() { Index = -1, Version = -1 };
    }
}