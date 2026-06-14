using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;

using UnityEngine;

using WeatherTheStorm.Enemy;
using WeatherTheStorm.Tower;
using WeatherTheStorm.Helpers;

namespace WeatherTheStorm.Projectile
{
    public class ProjectileSystem : MonoBehaviour
    {
        [Header("Prototype")]
        [SerializeField] private ProjectileAuthoring m_StubProjectile;

        [Header("Scale")]
        [SerializeField, Min(0)] private int m_MaximumProjectileCount;

        //
        // Retained Data
        //

        private NativeArray<ProjectileConfig> m_Configs;
        private NativeArray<ProjectileTarget> m_Targets;
        private Transform[]                   m_Transforms;
        private BoxCollider[]                 m_BoxColliders;
        private int                           m_ProjectileCount;

        //
        // Transient Data
        //

        private NativeArray<AABB>             m_LocalBounds;
        private NativeArray<AABB>             m_WorldBounds;
        private NativeArray<float3>           m_Positions;
        private NativeArray<float3>           m_Scales;
        private NativeQueue<int>              m_ProjectilesToDelete;


        void Start()
        {
            m_Configs      = MemoryHelpers.PermanentAlloc<ProjectileConfig>(m_MaximumProjectileCount);
            m_Targets      = MemoryHelpers.PermanentAlloc<ProjectileTarget>(m_MaximumProjectileCount);
            m_Transforms   = new Transform[m_MaximumProjectileCount];
            m_BoxColliders = new BoxCollider[m_MaximumProjectileCount];
            m_ProjectileCount = 0;
        }

        private void OnDestroy()
        {
            MemoryHelpers.SafeReleaseUnmanaged(m_Configs);
            MemoryHelpers.SafeReleaseUnmanaged(m_Targets);
            MemoryHelpers.SafeReleaseUnmanaged(m_LocalBounds);
            MemoryHelpers.SafeReleaseUnmanaged(m_WorldBounds);
            MemoryHelpers.SafeReleaseUnmanaged(m_Positions);
            MemoryHelpers.SafeReleaseUnmanaged(m_Scales);
            MemoryHelpers.SafeReleaseUnmanaged(m_ProjectilesToDelete);
        }

        //
        // NOTE:
        // X) This is one to one copy of OnFrameEnter from the projectile system :/
        //

        public void OnFrameEnter(
            out NativeArray<float3>.ReadOnly outProjectileScales
            )
        {
            m_Positions = MemoryHelpers.RefreshTransientAlloc(m_ProjectileCount, m_Positions);
            m_Scales    = MemoryHelpers.RefreshTransientAlloc(m_ProjectileCount, m_Scales);
            for (int projectileIdx = 0; projectileIdx < m_ProjectileCount; ++projectileIdx)
            {
                m_Positions[projectileIdx] = m_Transforms[projectileIdx].position;
                m_Scales[projectileIdx]    = m_Transforms[projectileIdx].lossyScale;
            }

            //
            // NOTE:
            // x) This might be a bit weird, because is there really something that modifies
            //    the colliders at runtime? It's easier to do this, so we stick with this.
            //

            m_LocalBounds = MemoryHelpers.RefreshTransientAlloc(m_ProjectileCount, m_LocalBounds);
            for (int projectileIdx = 0; projectileIdx < m_ProjectileCount; ++projectileIdx)
            {
                var boxCollider = m_BoxColliders[projectileIdx];

                m_LocalBounds[projectileIdx] = new AABB()
                {
                    Center  = boxCollider.center,
                    Extents = boxCollider.size * 0.5f,
                };
            }

            outProjectileScales = m_Scales.AsReadOnly();
        }

        public void OnFrameLeave(
            NativeQueue<CreateProjectileRequest> requestQueue
            )
        {
            var transforms          = m_Transforms;
            var projectilePositions = m_Positions;
            for (int projectileIdx = 0; projectileIdx < m_ProjectileCount; ++projectileIdx)
            {
                transforms[projectileIdx].position = projectilePositions[projectileIdx];
            }

            //
            //
            //

            NativeArray<int> toDelete = m_ProjectilesToDelete.ToArray(Allocator.Temp);
            toDelete.Sort();

            for(int deleteIdx = toDelete.Length - 1; deleteIdx >= 0; --deleteIdx)
            {
                int swapIndex   = --m_ProjectileCount;
                int toDeleteIdx = toDelete[deleteIdx];

                //
                // Would be returned to the pool.
                //

                Destroy(m_Transforms[toDeleteIdx].gameObject);

                m_Configs[toDeleteIdx]      = m_Configs[swapIndex];
                m_Targets[toDeleteIdx]      = m_Targets[swapIndex];
                m_Transforms[toDeleteIdx]   = m_Transforms[swapIndex];
                m_BoxColliders[toDeleteIdx] = m_BoxColliders[swapIndex];
            }

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

                        m_Configs[index]      = new ProjectileConfig(damage: 5.0f);
                        m_Targets[index]      = new ProjectileTarget(handle: request.Target, velocity: 1.0f);
                        m_Transforms[index]   = projectileObject.transform;
                        m_BoxColliders[index] = projectileObject.GetComponent<BoxCollider>();

                        projectileObject.transform.position = request.Position;
                    }
                }
            }
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


        public JobHandle ScheduleProjectileWorldBoundsJob(
            in  JobHandle                    dependency,
            in  NativeArray<float3>.ReadOnly projectilePositions,
            in  NativeArray<float3>.ReadOnly projectileScales,
            out NativeArray<AABB>.ReadOnly   outWorldBounds
            )
        {
            m_WorldBounds = MemoryHelpers.RefreshTransientAlloc(m_ProjectileCount, m_WorldBounds);

            var computeWorldBoundsJobHandle = new WorldBoundsJob()
            {
                Positions   = projectilePositions,
                Scales      = projectileScales,
                LocalBounds = m_LocalBounds.AsReadOnly(),
                WorldBounds = m_WorldBounds,
            }.Schedule(m_ProjectileCount, 32, dependency);

            outWorldBounds = m_WorldBounds.AsReadOnly();

            return computeWorldBoundsJobHandle;
        }

        public JobHandle ScheduleProjectileCollision(
            in JobHandle                    dependency,
            in NativeArray<AABB>.ReadOnly   worldBounds,
            in NativeArray<float3>.ReadOnly projectilePositions,
            in EnemyBVH                     enemyBVH
            )
        {
            m_ProjectilesToDelete = MemoryHelpers.RefreshTransientAlloc(m_ProjectilesToDelete);
            var checkProjectileCollisionJob = new CheckProjectileCollisionJob()
            {
                EnemyBVH    = enemyBVH,
                Configs     = m_Configs.AsReadOnly(),
                WorldBounds = worldBounds,
                Positions   = projectilePositions,

                ProjectileToDelete = m_ProjectilesToDelete.AsParallelWriter()
            }.Schedule(m_ProjectileCount, 32, dependency);

            return checkProjectileCollisionJob;
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