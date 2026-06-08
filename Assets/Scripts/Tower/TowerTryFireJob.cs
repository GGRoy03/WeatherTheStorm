using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using WeatherTheStorm.Enemy;
using WeatherTheStorm.Projectile;

namespace WeatherTheStorm.Tower
{
    public struct TowerCooldown
    {
        public float Elapsed;
        public float Duration;

        public TowerCooldown(float duration)
        {
            Elapsed  = 0.0f;
            Duration = duration;
        }
    }

    [BurstCompile]
    public struct TowerTryFireJob : IJobParallelFor
    {
        public NativeArray<int>.ReadOnly         TargetIndices;
        public NativeArray<float3>.ReadOnly      TowerPositions;
        public NativeArray<EnemyHandle>.ReadOnly EnemyHandles;
        public float                             DeltaTime;

        public NativeArray<TowerCooldown>                          Cooldowns;
        public NativeQueue<CreateProjectileRequest>.ParallelWriter CreateProjectileQueue;

        public void Execute(int index)
        {
            int target = TargetIndices[index];
            if (target != -1)
            {
                var cooldown = Cooldowns[index];
                if (cooldown.Elapsed >= cooldown.Duration)
                {
                    CreateProjectileQueue.Enqueue(new CreateProjectileRequest()
                    {
                        Position = TowerPositions[index],
                        Target   = EnemyHandles[target],
                    });

                    cooldown.Elapsed = 0.0f;
                }
                else
                {
                    cooldown.Elapsed += DeltaTime;
                }

                Cooldowns[index] = cooldown;
            }
        }
    }
}