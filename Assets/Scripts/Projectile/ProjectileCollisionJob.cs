using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;

using WeatherTheStorm.Enemy;

namespace WeatherTheStorm.Projectile
{
    [BurstCompile]
    public struct ProjectileCollisionJob : IJobParallelFor
    {
        public EnemyBVH                               EnemyBVH;
        public NativeArray<ProjectileConfig>.ReadOnly Configs;
        public NativeArray<AABB>.ReadOnly             LocalBounds;
        public NativeArray<float3>.ReadOnly           Positions;
         

        public void Execute(int index)
        {
            float3      position    = Positions[index];
            AABB        worldBounds = new() { Center = LocalBounds[index].Center + position , Extents = LocalBounds[index].Extents};
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
                }
            }
        }
    }
}
