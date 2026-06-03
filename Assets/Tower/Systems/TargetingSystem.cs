using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Transforms;

using UnityEngine;

using WeatherTheStorm.Audio;
using WeatherTheStorm.Enemy;

namespace WeatherTheStorm.Tower
{
    public enum TargetMode
    {
        Closest = 0,
    }

    public struct AttackCooldown : IComponentData, IEnableableComponent
    {
        public float Duration;
        public float Elapsed;
    }

    public struct TargetingConfig : IComponentData
    {
        public TargetMode Mode;
        public float      SqrRange;
    }

    public struct TowerProjectile : IComponentData
    {
        public Entity                      Prefab;
        public UnityObjectRef<AudioClip>   FireSound;
        public UnityObjectRef<AudioSource> AudioSource;
    }

    public struct TowerAudio : IComponentData
    {
        public UnityObjectRef<AudioClip> TurningSound;
    }

    public partial struct TargetingSystem : ISystem
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
            m_EnemyWithPosQuery = SystemAPI.QueryBuilder()
                .WithAll<EnemyTag, LocalTransform>()
                .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var entityCommandBuffer = SystemAPI
                .GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            var entityCommandBuffer1 = SystemAPI
                .GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            //
            //
            //

            JobHandle fireRequestProducerJob = default;
            {
                fireRequestProducerJob = new TickCooldownJob
                {
                    DeltaTime     = Time.deltaTime,
                    CommandBuffer = entityCommandBuffer.AsParallelWriter(),
                }.ScheduleParallel(state.Dependency);
            }

            //
            //
            //

            int enemyWithPosCount = m_EnemyWithPosQuery.CalculateEntityCount();
            var enemyWithPos      = new NativeArray<EnemyPositionData>(enemyWithPosCount, Allocator.TempJob);
            int enemyPosIdx       = 0;
            foreach(var (transform, entity) in
                SystemAPI.Query<RefRO<LocalTransform>>().WithAll<EnemyTag>().WithEntityAccess())
            {
                EnemyPositionData data = new()
                {
                    Entity   = entity,
                    Position = transform.ValueRO.Position,
                };

                enemyWithPos[enemyPosIdx++] = data;
            }

            //
            //
            //

            JobHandle createProjectileRequestProducer = default;
            {
                createProjectileRequestProducer = new FindEnemyTargetJob
                {
                    Enemies       = enemyWithPos,
                    Config        = SystemAPI.GetSingleton<Config>(),
                    CommandBuffer = entityCommandBuffer1.AsParallelWriter(),
                }.ScheduleParallel(fireRequestProducerJob);
            }

            state.Dependency = JobHandle.CombineDependencies(fireRequestProducerJob, createProjectileRequestProducer); ;

            enemyWithPos.Dispose(state.Dependency);
        }

        [BurstCompile]
        partial struct TickCooldownJob : IJobEntity
        {
            public float                              DeltaTime;
            public EntityCommandBuffer.ParallelWriter CommandBuffer;

            public void Execute([ChunkIndexInQuery] int chunkIndex, in Entity entity, ref AttackCooldown cooldown)
            {
                cooldown.Elapsed += DeltaTime;
                if(cooldown.Elapsed >= cooldown.Duration)
                {
                    cooldown.Elapsed = 0.0f;

                    CommandBuffer.SetComponentEnabled<AttackCooldown>(chunkIndex, entity, false);
                }
            }
        }


        [BurstCompile]
        [WithNone(typeof(AttackCooldown))]
        partial struct FindEnemyTargetJob : IJobEntity
        {
            [ReadOnly] public NativeArray<EnemyPositionData> Enemies;
            [ReadOnly] public Config                         Config;
            public EntityCommandBuffer.ParallelWriter        CommandBuffer;

            void Execute([ChunkIndexInQuery] int chunkIndex, in Entity entity, in TargetingConfig targeting, in LocalTransform transform, in TowerProjectile projectile)
            {
                //
                // TODO:
                // x) Should switch on the request mode.
                //

                Entity closestEnemy       = Entity.Null;
                float  closestSqrDistance = float.MaxValue;
                for (int enemyIdx = 0; enemyIdx < Enemies.Length; ++enemyIdx)
                {
                    float3 towerPosition = transform.Position;
                    float3 enemyPosition = Enemies[enemyIdx].Position;
                    float  sqrDistance   = math.distancesq(towerPosition, enemyPosition);
                    float  sqrRadius     = targeting.SqrRange;

                    if (sqrDistance <= sqrRadius && sqrDistance < closestSqrDistance)
                    {
                        closestSqrDistance = sqrDistance;
                        closestEnemy       = Enemies[enemyIdx].Entity;
                    }
                }

                if (closestEnemy != Entity.Null)
                {
                    if (projectile.Prefab != null)
                    {
                        Entity projectileEntity = CommandBuffer.Instantiate(chunkIndex, projectile.Prefab);
                        CommandBuffer.AddComponent(chunkIndex, projectileEntity, new ProjectileTarget
                        {
                            Entity = closestEnemy,
                        });
                    }

                    CommandBuffer.SetComponentEnabled<AttackCooldown>(chunkIndex, entity, true);
                }
            }
        }
    }
}