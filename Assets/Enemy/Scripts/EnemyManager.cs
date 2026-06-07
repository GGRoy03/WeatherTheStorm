using Unity.Collections;
using Unity.Mathematics;

using UnityEngine;

namespace WeatherTheStorm.Enemy
{
    public struct EnemyHandle
    {
        public int Index;
        public int Version;
        public static EnemyHandle Null => new() { Index = -1, Version = -1 };
    }

    public class EnemyManager : MonoBehaviour
    {
        [SerializeField, Min(0)] private int        m_MaximumEnemyCount;
        [SerializeField]         private GameObject m_EnemyPrefab;
                                 private int        m_EnemyCount;

        private NativeArray<AABB>        m_LocalBounds;
        private NativeArray<float3>      m_WorldPositions;
        private NativeArray<EnemyHandle> m_Handles;

        private EnemyBoundingVolumeHierarchy m_FrameBVH;

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

        private void Update()
        {
            //
            // Let's just force on the main thread for now, this can be done in parallel.
            //

            var worldBoundsArray = new NativeArray<AABB>(m_EnemyCount, Allocator.Temp);
            for(int enemyIdx = 0; enemyIdx < m_EnemyCount; ++enemyIdx)
            {
                float3 position    = m_WorldPositions[enemyIdx];
                AABB   localBounds = m_LocalBounds[enemyIdx];

                AABB worldBounds = new()
                {
                    Center  = localBounds.Center + position,
                    Extents = localBounds.Center,
                };
                worldBoundsArray[enemyIdx] = worldBounds;
            }

            //
            // Then here we should simply have a valid BVH. So now we have to do a couple of things
            // x) Write queries into that BVH
            // x) Expose it to the tower code.
            //

            m_FrameBVH = new();
            m_FrameBVH.Construct(m_Handles, worldBoundsArray);
        }

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

        private void OnDestroy()
        {
            if (m_LocalBounds.IsCreated)
            {
                m_LocalBounds.Dispose();
            }

            if (m_WorldPositions.IsCreated)
            {
                m_WorldPositions.Dispose();
            }

            if(m_Handles.IsCreated)
            {
                m_Handles.Dispose();
            }
        }

        public NativeArray<float3>.ReadOnly GetEnemyPositions()
        {
            var result = m_WorldPositions.AsReadOnly();
            return result;
        }

        public NativeArray<EnemyHandle>.ReadOnly GetEnenyHandles()
        {
            var result = m_Handles.AsReadOnly();
            return result;
        }

        public EnemyBoundingVolumeHierarchy GetEnemyBVH()
        {
            var result = m_FrameBVH;
            return result;
        }
    }
}