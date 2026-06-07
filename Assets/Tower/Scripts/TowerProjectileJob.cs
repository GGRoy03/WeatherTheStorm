using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

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

    //
    // NOTE:
    // x) IJob is probably better here, this is light work.
    //

    [BurstCompile]
    public struct TowerTryFireJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int>                TargetIndices;
                   public NativeArray<TowerCooldown>      Cooldowns;
                   public float                           DeltaTime;
                   public NativeQueue<int>.ParallelWriter CreateProjectileQueue;

        public void Execute(int index)
        {
            int target = TargetIndices[index];
            if (target != -1)
            {
                var cooldown = Cooldowns[index];
                if (cooldown.Elapsed >= cooldown.Duration)
                {
                    CreateProjectileQueue.Enqueue(index);
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