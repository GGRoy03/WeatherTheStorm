using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using WeatherTheStorm.Enemy;

namespace WeatherTheStorm.Tower
{
    public enum ProjectileType
    {
        Rocket = 0,
    }

    public struct AttackCooldown : IComponentData
    {
        public float Duration;
        public float Elapsed;
    }

    public struct TargetClosest : IComponentData
    {
        public float SqrRadius;
    }

    public struct Projectile : IComponentData
    {
        public ProjectileType Type;
    }

    public partial struct TargetingSystem : ISystem
    {
        private EntityQuery m_EnemyWithPosQuery;

        private struct ReadyToFireTag : IComponentData
        {
        }

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
            // x) Is it really worth caching the query? If so, why not cache the other ones?
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
                CommandBuffer = cooldownEntityCommandBuffer,
                DeltaTime     = Time.deltaTime,

            }.ScheduleParallel(state.Dependency);
            


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

            var closestTargetingEntityCommandBuffer = SystemAPI
                .GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged)
                .AsParallelWriter();


            JobHandle findClosestHandle = new FindClosestJob
            {
                CommandBuffer = closestTargetingEntityCommandBuffer,
                Config        = SystemAPI.GetSingleton<Config>(),
                Enemies       = enemyWithPos,
            }.ScheduleParallel(state.Dependency);

            state.Dependency = JobHandle.CombineDependencies(towerCooldownTickJob, findClosestHandle);

            enemyWithPos.Dispose(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(ReadyToFireTag))]
        partial struct FindClosestJob : IJobEntity
        {
            [ReadOnly] public NativeArray<EnemyPositionData>     Enemies;
            [ReadOnly] public Config                             Config;
                       public EntityCommandBuffer.ParallelWriter CommandBuffer;

            void Execute([ChunkIndexInQuery] int chunkIndex, Entity entity, in LocalTransform transform, in TargetClosest config, in Projectile projectile)
            {
                Entity closestEnemy       = Entity.Null;
                float  closestSqrDistance = float.MaxValue;
                for (int enemyIdx = 0; enemyIdx < Enemies.Length; ++enemyIdx)
                {
                    float3 towerPosition = transform.Position;
                    float3 enemyPosition = Enemies[enemyIdx].Position;
                    float  sqrDistance   = math.distancesq(towerPosition, enemyPosition);
                    float  sqrRadius     = config.SqrRadius;

                    if (sqrDistance <= sqrRadius && sqrDistance < closestSqrDistance)
                    {
                        closestSqrDistance = sqrDistance;
                        closestEnemy       = Enemies[enemyIdx].Entity;
                    }
                }

                if (closestEnemy != Entity.Null)
                {
                    CommandBuffer.RemoveComponent<ReadyToFireTag>(chunkIndex, entity);

                    //
                    // TODO:
                    // x) We might simply bake the projectile at baking time on the tower entities.
                    //    This simply depends on if we want to tie projectiles to towers.
                    //

                    Entity projectilePrefab = projectile.Type switch
                    {
                        ProjectileType.Rocket => Config.Rocket,
                        _                     => Entity.Null,
                    };

                    //
                    // TODO:
                    // x) I think a better way would be to not specify the damage itself, but specify
                    //    values that allows the projectile to compute its own damage. Let's say each
                    //    tower had a physical damage state and a magical damage state,
                    //    we'd simply forward that to the projectile and let it compute by itself
                    //    its damage formula. For example, rockets could be something like:
                    //    40 + (physical_damage * 10) + (magical_damage * 2)
                    //

                    Entity projectileEntity = CommandBuffer.Instantiate(chunkIndex, projectilePrefab);
                    CommandBuffer.AddComponent(chunkIndex, projectileEntity, new ProjectileDamage
                    {
                        Damage = 10.0f,
                    });
                    CommandBuffer.AddComponent(chunkIndex, projectileEntity, new HomingProjectile
                    {
                        Speed  = 1.0f,
                        Target = closestEnemy,
                    });
                }
            }
        }

        [BurstCompile]
        [WithNone(typeof(ReadyToFireTag))]
        partial struct TickCooldownJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter CommandBuffer;
            public float                              DeltaTime;

            public void Execute([ChunkIndexInQuery] int chunkIndex, Entity entity, ref AttackCooldown cooldown)
            {
                cooldown.Elapsed += DeltaTime;
                if(cooldown.Elapsed >= cooldown.Duration)
                {
                    CommandBuffer.AddComponent<ReadyToFireTag>(chunkIndex, entity);
                    cooldown.Elapsed = 0.0f;
                }
            }
        }
    }
}