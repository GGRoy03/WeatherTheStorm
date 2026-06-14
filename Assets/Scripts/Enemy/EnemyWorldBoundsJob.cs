using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace WeatherTheStorm.Enemy
{
    [BurstCompile]
    public struct EnemyWorldBoundsJob : IJobParallelFor
    {
        public NativeArray<float3>.ReadOnly Positions;
        public NativeArray<float3>.ReadOnly Scales;
        public NativeArray<AABB>.ReadOnly   LocalBounds;
        public NativeArray<AABB>            WorldBounds;

        public void Execute(int index)
        {
            var position    = Positions[index];
            var scale       = Scales[index];
            var localBounds = LocalBounds[index];

            AABB worldBounds = new()
            {
                Center  = localBounds.Center  + position,
                Extents = localBounds.Extents * scale ,
            };
            WorldBounds[index] = worldBounds;
        }
    }
}