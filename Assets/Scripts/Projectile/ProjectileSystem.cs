using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;

using UnityEngine;

using WeatherTheStorm.Enemy;
using WeatherTheStorm.Tower;

namespace WeatherTheStorm.Projectile
{
    public class ProjectileSystem : MonoBehaviour
    {
        [Header("Prototype")]
        [SerializeField] private ProjectileAuthoring m_StubProjectile;

        [Header("Scale")]
        [SerializeField, Min(0)] private int m_MaximumProjectileCount;

        private NativeArray<ProjectileConfig> m_Configs;
        private NativeArray<ProjectileTarget> m_Targets;
        private NativeArray<AABB>             m_LocalBounds;
        private NativeArray<float3>           m_Positions;
        private Transform[]                   m_Transforms;
        private int                           m_ProjectileCount;

        void Start()
        {
            m_Transforms      = new Transform[m_MaximumProjectileCount];
            m_Configs         = new NativeArray<ProjectileConfig>(m_MaximumProjectileCount, Allocator.Persistent);
            m_LocalBounds     = new NativeArray<AABB>(m_MaximumProjectileCount, Allocator.Persistent);
            m_Positions       = new NativeArray<float3>(m_MaximumProjectileCount, Allocator.Persistent);
            m_Targets         = new NativeArray<ProjectileTarget>(m_MaximumProjectileCount, Allocator.Persistent);
            m_ProjectileCount = 0;
        }

        private void OnDestroy()
        {
            if (m_Configs.IsCreated)     m_Configs.Dispose();
            if (m_Targets.IsCreated)     m_Targets.Dispose();
            if (m_LocalBounds.IsCreated) m_LocalBounds.Dispose();
            if (m_Positions.IsCreated)   m_Positions.Dispose();
        }

        public JobHandle ScheduleProjectileMove(
            in  JobHandle                    dependency,
            in  NativeArray<float3>.ReadOnly enemyPositions,
            out NativeArray<float3>.ReadOnly outProjectilePosition
            )
        {
            var UpdateProjectilePositionJobHandle = new ProjectilePositionJob()
            {
                ProjectileTargets   = m_Targets.AsReadOnly(),
                EnemyPositions      = enemyPositions,
                ProjectilePositions = m_Positions,
                DeltaTime           = Time.deltaTime
            }.Schedule(m_ProjectileCount, 32, dependency);

            outProjectilePosition = m_Positions.AsReadOnly();

            return UpdateProjectilePositionJobHandle;
        }

        public JobHandle ScheduleProjectileCollision(
            in JobHandle                    dependency,
            in NativeArray<float3>.ReadOnly positions,
            in EnemyBVH                     enemyBVH
            )
        {
            var checkProjectileCollisionJob = new CheckProjectileCollisionJob()
            {
                EnemyBVH    = enemyBVH,
                Configs     = m_Configs.AsReadOnly(),
                LocalBounds = m_LocalBounds.AsReadOnly(),
                Positions   = positions,
            }.Schedule(m_ProjectileCount, 32, dependency);

            return checkProjectileCollisionJob;
        }

        public void LateUpdateSetProjectilePosition()
        {
            var transforms          = m_Transforms;
            var projectilePositions = m_Positions;
            for (int projectileIdx = 0; projectileIdx < m_ProjectileCount; ++projectileIdx)
            {
                transforms[projectileIdx].position = projectilePositions[projectileIdx];
            }
        }

        public void LateUpdateCreateProjectile(NativeQueue<CreateProjectileRequest> requestQueue)
        {
            while(requestQueue.TryDequeue(out CreateProjectileRequest request))
            {
                //
                // TODO:
                // x) Just raw instaniate for now, and do the checking stuff and. Just use the
                //    stub implementation for now. Like we need to fill the buffers as well.
                //

                if(m_ProjectileCount < m_MaximumProjectileCount)
                {
                    var projectileObject = Instantiate(m_StubProjectile.m_Prefab, request.Position, Quaternion.identity);
                    if(projectileObject != null)
                    {
                        int index = m_ProjectileCount++;

                        m_Configs[index]     = new ProjectileConfig(damage: 5.0f);
                        m_Targets[index]     = new ProjectileTarget(handle: request.Target, velocity: 1.0f);
                        m_Positions[index]   = request.Position;
                        m_Transforms[index]  = projectileObject.transform;
                        m_LocalBounds[index] = m_StubProjectile.m_LocalBounds;
                    }
                }
            }
        }
    }

    public struct CreateProjectileRequest
    {
        public float3      Position;
        public EnemyHandle Target;

        public CreateProjectileRequest(float3 position, EnemyHandle target)
        {
            Position = position;
            Target   = target;
        }
    }
}