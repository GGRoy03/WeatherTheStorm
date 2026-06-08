using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace WeatherTheStorm.Tower
{
    public enum TargetMode
    {
        Closest = 0,
    }

    public struct TowerTargeting
    {
        public TargetMode Mode;
        public float      SqrRange;

        public TowerTargeting(TargetMode mode,  float sqrRange)
        {
            Mode     = mode;
            SqrRange = sqrRange;
        }
    }

    //
    // NOTE:
    // x) The fact that we check every entity should not matter too much for the
    //    current scale. We are also only loading in their position which might be
    //    fast enough for a really long time.
    //

    [BurstCompile]
    public struct TowerTargetingJob : IJobParallelFor
    {
        public NativeArray<float3>.ReadOnly         TowerPositions;
        public NativeArray<TowerTargeting>.ReadOnly TowerTargetings;

        public NativeArray<float3>.ReadOnly         EnemyPositions;
        public int                                  EnemyCount;

        public NativeArray<int>                     TargetIndices;
        public NativeArray<Quaternion>              TargetRotations;

        public void Execute(int index)
        {
            int bestIndex     = -1;
            var targeting     = TowerTargetings[index];
            var towerPosition = TowerPositions[index];

            switch (targeting.Mode)
            {

            case TargetMode.Closest:
            {
                float closestSqrDistance = float.MaxValue;
                for(int enemyIdx = 0; enemyIdx < EnemyCount; ++enemyIdx)
                {
                    float3 enemyPosition = EnemyPositions[enemyIdx];
                    float  sqrDistance   = math.distancesq(towerPosition, enemyPosition);

                    if(sqrDistance <= targeting.SqrRange && sqrDistance < closestSqrDistance)
                    {
                        closestSqrDistance = sqrDistance;
                        bestIndex          = enemyIdx;
                    }
                }
            } break;

            }

            if(bestIndex != -1)
            {
                //
                // NOTE:
                // Might need to flatten to XZ? Depending on how we animate our towers.
                //

                float3     enemyPositon   = EnemyPositions[bestIndex];
                float3     enemyDirection = enemyPositon - towerPosition;
                Quaternion targetRotation = Quaternion.LookRotation(enemyDirection);

                TargetRotations[index] = targetRotation;
            }

            TargetIndices[index] = bestIndex;
        }
    }
}
