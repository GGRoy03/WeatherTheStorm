using UnityEngine;

using Unity.Collections;
using Unity.Jobs;

using Unity.Mathematics;

using WeatherTheStorm.Projectile;
using WeatherTheStorm.Enemy;

namespace WeatherTheStorm.Tower
{
    public class TowerSystem : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private TowerAuthoring          m_StubTower;
        [SerializeField] private ProjectileAuthoring     m_StubProjectile;


        [Header("Scale")]
        [SerializeField] private int                         m_MaximumTowerCount;
                         private NativeArray<float3>         m_TowerPositions;
                         private NativeArray<TowerTargeting> m_TowerTargetings;
                         private NativeArray<TowerCooldown>  m_TowerCooldowns;
                         private Transform[]                 m_TowerTransforms;
                         private int                         m_TowerCount;


        //
        // Transient data that must be allocated/de-allocated each frame.
        //

        NativeArray<int>                     m_OutEnemyTargetIndices;
        NativeQueue<CreateProjectileRequest> m_OutProjectileRequests;
        NativeArray<Quaternion>              m_TowerTargetRotations;


        void Start()
        {
            m_TowerPositions  = new NativeArray<float3>(m_MaximumTowerCount, Allocator.Persistent);
            m_TowerCooldowns  = new NativeArray<TowerCooldown> (m_MaximumTowerCount, Allocator.Persistent);
            m_TowerTargetings = new NativeArray<TowerTargeting>(m_MaximumTowerCount, Allocator.Persistent);
            m_TowerTransforms = new Transform[m_MaximumTowerCount];
            m_TowerCount      = 0;

            for(int towerIdx = 0; towerIdx < 10; ++towerIdx)
            {
                CreateTower();
            }
        }

        private void OnDestroy()
        {
            if (m_TowerPositions.IsCreated)  m_TowerPositions.Dispose();
            if (m_TowerTargetings.IsCreated) m_TowerTargetings.Dispose();
            if (m_TowerCooldowns.IsCreated)  m_TowerCooldowns.Dispose();

            if (m_OutEnemyTargetIndices.IsCreated) m_OutEnemyTargetIndices.Dispose();
            if (m_OutProjectileRequests.IsCreated) m_OutProjectileRequests.Dispose();
            if (m_TowerTargetRotations.IsCreated)  m_TowerTargetRotations.Dispose();
        }

        public JobHandle ScheduleTowerTargeting(
            in  JobHandle                    dependency,
            in  NativeArray<float3>.ReadOnly enemyPositions,
            in  int                          enemyCount,
            out NativeArray<int>.ReadOnly    outTargetIndices
            )
        {
            if(m_OutEnemyTargetIndices.IsCreated)
            {
                m_OutEnemyTargetIndices.Dispose();
            }
            m_OutEnemyTargetIndices = new NativeArray<int>(m_TowerCount, Allocator.TempJob);

            if(m_TowerTargetRotations.IsCreated)
            {
                m_TowerTargetRotations.Dispose();
            }
            m_TowerTargetRotations = new NativeArray<Quaternion>(m_TowerCount, Allocator.TempJob);

            JobHandle targetingJobHandle = new TowerTargetingJob
            {
                TowerPositions  = m_TowerPositions.AsReadOnly(),
                TowerTargetings = m_TowerTargetings.AsReadOnly(),
                EnemyPositions  = enemyPositions,
                EnemyCount      = enemyCount,
                TargetIndices   = m_OutEnemyTargetIndices,
                TargetRotations = m_TowerTargetRotations,
            }.Schedule(m_TowerCount, 32, dependency);

            outTargetIndices = m_OutEnemyTargetIndices.AsReadOnly();

            return targetingJobHandle;
        }

        public JobHandle ScheduleTowerFiring(
            in  JobHandle                            dependency,
            in  NativeArray<int>.ReadOnly            targetIndices,
            in  NativeArray<EnemyHandle>.ReadOnly    enemyHandles,
            out NativeQueue<CreateProjectileRequest> outCreateProjectileQueue
            )
        {
            //
            // NOTE:
            // x) I mean, why don't we own the projectile creation? This should probably not be
            //    out, we should probably hook into the projectile system or something? We'll see
            //    once projectiles are more complex, it's not really clear right now.
            //

            if (m_OutProjectileRequests.IsCreated)
            {
                m_OutProjectileRequests.Dispose();
            }
            m_OutProjectileRequests = new NativeQueue<CreateProjectileRequest>(Allocator.TempJob);

            var firingJobHandle = new TowerTryFireJob
            {
                TargetIndices         = targetIndices,
                TowerPositions        = m_TowerPositions.AsReadOnly(),
                EnemyHandles          = enemyHandles,
                Cooldowns             = m_TowerCooldowns,
                DeltaTime             = Time.deltaTime,
                CreateProjectileQueue = m_OutProjectileRequests.AsParallelWriter(),
            }.Schedule(m_TowerCount, 32, dependency);

            outCreateProjectileQueue = m_OutProjectileRequests;

            return firingJobHandle;
        }

        public void CreateTower()
        {
            if(m_TowerCount < m_MaximumTowerCount)
            {
                var position    = new Vector3(UnityEngine.Random.Range(-5.0f, 5.0f), 0.0f, UnityEngine.Random.Range(-5.0f, 5.0f));
                var towerObject = Instantiate(m_StubTower.Prefab, position, Quaternion.identity);
                
                if(towerObject != null)
                {
                    int index = m_TowerCount++;

                    m_TowerCooldowns [index] = new(duration: 1.5f);
                    m_TowerPositions [index] = position;
                    m_TowerTargetings[index] = new(mode: TargetMode.Closest, sqrRange: 5.0f);
                    m_TowerTransforms[index] = towerObject.transform;
                }
            }
        }

        public void LateUpdateSetTowerRotation()
        {
            Debug.Assert(m_TowerTargetRotations.Length >= m_TowerCount);

            for (int towerIdx = 0; towerIdx < m_TowerCount; ++towerIdx)
            {
                Quaternion rotation = m_TowerTargetRotations[towerIdx];
                if(rotation.w != 0.0f)
                {
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, rotation, Time.deltaTime);
                }
            }
        }
    }
}