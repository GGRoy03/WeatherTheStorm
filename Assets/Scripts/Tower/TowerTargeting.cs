using System.ComponentModel;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;


//
// NOTE:
// x) So now it seems like adding new targeting types is really easy which is good. It's not the most
//    efficient it could be probably, but for a prototype and the scale we're going for it looks good
//    enough at first glance.
// x) The code is also simpler than I thought it would be. Unity does seem to want to separate each
//    system in its own file, but I think this is better organized, because it's quite clear what
//    goes on in the full targeting system.
// 
// TODO:
// x) Clean up the whole file, I don't like the main code's structure currently.
// x) Instantiate projectiles.
// x) Rotate turret towards target.
// x) Test at a decent scale what it looks like (bring back the moving entities)
// x)
//

namespace WeatherTheStorm.Tower
{
    public struct AttackCooldown : IComponentData
    {
        public float Duration;
        public float Elapsed;
    }

    public struct ReadyToFire : IComponentData
    {
    }

    public struct TargetClosest : IComponentData
    {
        public float SqrRadius;
    }

    public partial struct TowerTargetSystem : ISystem
    {
        private EntityQuery m_EnemyWithPosQuery;

        private struct EnemyPositionData
        {
            public Entity  Entity;
            public Vector3 Position;
        }


        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            //
            // TODO:
            // x) Is it really worth caching the query?
            // x) Figure out the state.RequireForUpdate stuff.
            //

            m_EnemyWithPosQuery = SystemAPI.QueryBuilder()
                .WithAll<EnemyTag, LocalTransform>()
                .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            //
            // TODO:
            // x) Since we're using the begin simulation group, we have a frame of lag for tower's
            //    ready to fire state. This is probably fine? Figure it out. It also allows us
            //    to run some jobs widly in parallel because the structural changes are done at
            //    frame boundaries anyways.
            //

            
            var cooldownEntityCommandBuffer = SystemAPI
                .GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged)
                .AsParallelWriter();

            JobHandle towerCooldownTickJob = new TickCooldownJob
            {
                ParallelWriter = cooldownEntityCommandBuffer,
                DeltaTime      = Time.deltaTime,

            }.ScheduleParallel(state.Dependency);
            


            int enemyWithPosCount = m_EnemyWithPosQuery.CalculateEntityCount();
            var enemyWithPos      = new NativeArray<EnemyPositionData>(enemyWithPosCount, Allocator.TempJob);
            int enemyPosIdx       = 0;
            foreach(var (transform, entity) in
                SystemAPI.Query<RefRO<LocalTransform>>().WithAll<EnemyTag>().WithEntityAccess())
            {
                EnemyPositionData data = new EnemyPositionData
                {
                    Entity   = entity,
                    Position = transform.ValueRO.Position,
                };

                enemyWithPos[enemyPosIdx++] = data;
            }

            var closestTargetingEntityCommandBuffer = SystemAPI
                .GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged)
                .AsParallelWriter();


            JobHandle findClosestHandle = new FindClosestJob
            {
                ParallelWriter = closestTargetingEntityCommandBuffer,
                Enemies        = enemyWithPos,
            }.ScheduleParallel(state.Dependency);

            state.Dependency = JobHandle.CombineDependencies(towerCooldownTickJob, findClosestHandle);

            enemyWithPos.Dispose(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(ReadyToFire))]
        partial struct FindClosestJob : IJobEntity
        {
            [Unity.Collections.ReadOnly] public NativeArray<EnemyPositionData>     Enemies;
                                         public EntityCommandBuffer.ParallelWriter ParallelWriter;

            void Execute([ChunkIndexInQuery] int chunkIndex, Entity entity, in LocalTransform transform, in TargetClosest config)
            {
                Debug.Log("Running Targeting Job!");

                Entity closestEnemy       = Entity.Null;
                float  closestSqrDistance = float.MaxValue;
                for (int enemyIdx = 0; enemyIdx < Enemies.Length; ++enemyIdx)
                {
                    float3 towerPosition   = transform.Position;
                    float3 enemyPosition   = Enemies[enemyIdx].Position;
                    float  distanceToEnemy = math.distancesq(towerPosition, enemyPosition);
                    float  sqrRadius       = config.SqrRadius;

                    if (distanceToEnemy <= sqrRadius && distanceToEnemy < closestSqrDistance)
                    {
                        closestSqrDistance = distanceToEnemy;
                        closestEnemy       = Enemies[enemyIdx].Entity;
                    }
                }

                if(closestEnemy != Entity.Null)
                {
                    //
                    // TODO:
                    // x) So the only thing that's missing is the projectile instantiation?
                    //

                    ParallelWriter.RemoveComponent<ReadyToFire>(chunkIndex, entity);

                    Debug.Log("Firing Projectile!");
                }
            }
        }

        [BurstCompile]
        [WithNone(typeof(ReadyToFire))]
        partial struct TickCooldownJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter ParallelWriter;
            public float                              DeltaTime;

            public void Execute([ChunkIndexInQuery] int chunkIndex, Entity entity, ref AttackCooldown cooldown)
            {
                Debug.Log("Executing Tower Cooldown Tick!");

                cooldown.Elapsed += DeltaTime;
                if(cooldown.Elapsed > cooldown.Duration)
                {
                    ParallelWriter.AddComponent<ReadyToFire>(chunkIndex, entity);
                    cooldown.Elapsed = 0.0f;
                }
            }
        }
    }
}