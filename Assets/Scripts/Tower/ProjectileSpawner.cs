using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

public partial struct ProjectileSpawner : ISystem
{
    private bool m_HasSpawned;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<ConfigAuthoring.Config>();
        m_HasSpawned = false;
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if(!m_HasSpawned)
        {
            var prefab = SystemAPI.GetSingleton<ConfigAuthoring.Config>().Prefab;
            state.EntityManager.Instantiate(prefab, 50, Allocator.Temp);

            new RandomPositionJob { SeedOffset = 0 }.Schedule();

            m_HasSpawned = true;
        }
    }


    [WithAll(typeof(ProjectileAuthoring.Projectile))]
    [BurstCompile]
    partial struct RandomPositionJob : IJobEntity
    {
        public uint SeedOffset;

        void Execute([EntityIndexInQuery] int index, ref LocalTransform transform)
        {
            Debug.Log("Running Placement Job");

            var random    = Random.CreateFromIndex(SeedOffset + (uint)index);
            var position  = random.NextFloat2Direction() * 10.0f;
            transform.Position = new float3(position[0], 5.0f, position[1]);
        }
    }
}