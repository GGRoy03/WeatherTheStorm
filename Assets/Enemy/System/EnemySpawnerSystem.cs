using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace WeatherTheStorm.Enemy
{

    public partial struct EnemySpawnerSystem : ISystem
    {
        private bool m_HasSpawned;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EnemyAuthoring.Config>();
            m_HasSpawned = false;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!m_HasSpawned)
            {
                var prefab = SystemAPI.GetSingleton<EnemyAuthoring.Config>().BasicEnemy;
                NativeArray<Entity> entities = state.EntityManager.Instantiate(prefab, 50, Allocator.Temp);

                state.Dependency = new RandomPositionJob { SeedOffset = 0 }.Schedule(state.Dependency);

                m_HasSpawned = true;
            }
        }

        [BurstCompile]
        [WithAll(typeof(EnemyTag))]
        partial struct RandomPositionJob : IJobEntity
        {
            public uint SeedOffset;

            void Execute([EntityIndexInQuery] int index, ref LocalTransform transform)
            {
                var random   = Random.CreateFromIndex(SeedOffset + (uint)index);
                var position = random.NextFloat2Direction() * 10.0f;
                transform.Position = new float3(position[0], 5.0f, position[1]);
            }
        }
    }
}