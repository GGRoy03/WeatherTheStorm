using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

using WeatherTheStorm.Enemy;

namespace WeatherTheStorm.Projectile
{
    public struct ProjectileTarget
    {
        public EnemyHandle Handle;
        public float       Velocity;

        public ProjectileTarget(EnemyHandle handle,  float velocity)
        {
            Handle   = handle;
            Velocity = velocity;
        }
    }

    public struct ProjectileConfig
    {
        public float ExplosionRadius;
        public float ExplosionDamage;
        public float Damage;

        public ProjectileConfig(float damage)
        {
            ExplosionRadius = 0.0f;
            ExplosionDamage = 0.0f;
            Damage          = damage;
        }
    }

    [BurstCompile]
    public struct ProjectilePositionJob : IJobParallelFor
    {
        public NativeArray<ProjectileTarget>.ReadOnly ProjectileTargets;
        public NativeArray<float3>.ReadOnly           EnemyPositions;
        public NativeArray<float3>                    ProjectilePositions;
        public float                                  DeltaTime;

        public void Execute(int index)
        {
            var currentPosition = ProjectilePositions[index];
            var currentTarget   = ProjectileTargets[index];
            var enemyPosition   = EnemyPositions[currentTarget.Handle.Index];

            //
            // TODO:
            // x) Should check if it's a valid target once we have the basic handle system.
            // x) How do we know the target position, uhm, we simply have to query it, no other way
            //    around. That's interesting. I think we should pre-pass and resolve the positions
            //    before we do that. The reason is simply cache contention.
            // x) If we think about it, we want to have a projectile position buffer separated
            //    because of the way we do the other stuff.
            // x) This is bricked.
            //

            if (math.distancesq(enemyPosition, currentPosition) > float.Epsilon)
            {
                float3 direction = math.normalize(enemyPosition - currentPosition);
                ProjectilePositions[index] += (DeltaTime * currentTarget.Velocity * direction);
            }
        }
    }

    [BurstCompile]
    public struct CheckProjectileCollisionJob : IJobParallelFor
    {
        public EnemyBVH                               EnemyBVH;
        public NativeArray<ProjectileConfig>.ReadOnly Configs;
        public NativeArray<AABB>.ReadOnly             WorldBounds;
        public NativeArray<float3>.ReadOnly           Positions;

        public NativeQueue<int>.ParallelWriter        ProjectileToDelete;
         

        public void Execute(int index)
        {
            float3      position    = Positions[index];
            AABB        worldBounds = WorldBounds[index];
            EnemyHandle firstEnemy  = EnemyHandle.Null;
            bool        isColliding = EnemyBVH.CollidesWithAABB(worldBounds, out firstEnemy);

            if(isColliding)
            {
                ProjectileConfig config = Configs[index];

                if(config.ExplosionRadius > 0.0f)
                {
                    //
                    // TODO:
                    // Do an explosion query and return some damage on some handle
                    // given all the enemies we have it.
                    //
                }
                else
                {
                    //
                    // TODO:
                    // Modify the query code to get the corresponding handle.
                    // Now that we have that handle... We can use the index stored on it.
                    // I think at this point we can't be certain the handle is still valid
                    // we'll deal with that later. So here I can already do a damage request since
                    // I have the handle and the damage. So this seems solved... If it works correctly.
                    // Can we try it?
                    //

                    ProjectileToDelete.Enqueue(index);
                }
            }
        }
    }
}
