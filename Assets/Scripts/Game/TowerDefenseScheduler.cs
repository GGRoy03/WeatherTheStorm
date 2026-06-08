using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

using UnityEngine;

using WeatherTheStorm.Enemy;
using WeatherTheStorm.Tower;
using WeatherTheStorm.Projectile;

public class GameScheduler : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private TowerSystem      m_TowerSystem;
    [SerializeField] private EnemySystem      m_EnemySystem;
    [SerializeField] private ProjectileSystem m_ProjectileSystem;

    private JobHandle                            m_AllComplete;
    private NativeQueue<CreateProjectileRequest> m_ProjectileRequests;

    void Update()
    {
        //
        // NOTE:
        // x) I mean... Obviously this is not pretty, but it's really hard to encode these
        //    sort of cross-systems dependencies. We try to be as explicit as we can, for example
        //    even if a system could use some internal data to do a job, that internal data is
        //    returned in an out parameters such that we can easily see the dependencies between
        //    jobs by simply looking at what data they read in.
        //


        var enemyMoveJobHandle = m_EnemySystem.ScheduleEnemyMoveJob(
            out int                               enemyCount,
            out NativeArray<float3>.ReadOnly      enemyPositions,
            out NativeArray<EnemyHandle>.ReadOnly enemyHandles
            );

        var enemyBoundsJobHandle = m_EnemySystem.ScheduleComputeWorldBounds(
            dependency:     enemyMoveJobHandle,
            enemyPositions: enemyPositions,
            out NativeArray<AABB> enemyWorldBounds
            );

        var enemyBVHJobHandle = m_EnemySystem.ScheduleBuildBVHJob(
            dependency:  enemyBoundsJobHandle,
            worldBounds: enemyWorldBounds,
            out EnemyBVH enemyBVH
            );

        var towerTargetingJobHandle = m_TowerSystem.ScheduleTowerTargeting(
            dependency:     enemyMoveJobHandle,
            enemyPositions: enemyPositions,
            enemyCount:     enemyCount,
            out var towerTargetIndices
            );

        var towerFiringJobHandle = m_TowerSystem.ScheduleTowerFiring(
            dependency:    towerTargetingJobHandle,
            targetIndices: towerTargetIndices,
            enemyHandles:  enemyHandles,
            out m_ProjectileRequests 
            );

        var projectileMoveJobHandle = m_ProjectileSystem.ScheduleProjectileMove(
            dependency:     enemyMoveJobHandle,
            enemyPositions: enemyPositions,
            out var projectilePositions
            );

        var projectileCollisionJobHandle = m_ProjectileSystem.ScheduleProjectileCollision(
            dependency: JobHandle.CombineDependencies(projectileMoveJobHandle, enemyBVHJobHandle),
            positions:  projectilePositions,
            enemyBVH:   enemyBVH
            );

        //m_EnemyDamageJobHandle = m_EnemySystem.ScheduleEnemyDamageJob(
        //    dependency: m_ProjectileCollisionJobHandle
        //    );

        m_AllComplete = JobHandle.CombineDependencies(towerFiringJobHandle, projectileCollisionJobHandle);

    }

    private void LateUpdate()
    {
        m_AllComplete.Complete();

        //
        // NOTE:
        // x) There's something odd here. I think, there's two reasons ->
        // 1) Passing data between the update and the late update shouldn't really be done here
        //    it should be internal to the system, why isn't the tower code the one calling the
        //    instantiating function on the projectile system for example, why does the projectile
        //    system depend on an external queue?
        // 2) Why aren't we just exposing a single entry point for a system's update code?
        //    This would make it easier to not forget things and such.
        //

        m_TowerSystem.LateUpdateSetTowerRotation();

        m_ProjectileSystem.LateUpdateCreateProjectile(m_ProjectileRequests);
        m_ProjectileSystem.LateUpdateSetProjectilePosition();
    }
}
