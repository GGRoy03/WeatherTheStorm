using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;


public struct EnemyTag : IComponentData { }


public struct PathAgent : IComponentData
{
    public float3 Destination;
    public float  Speed;
}


public partial struct MoveToDestinationSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;

        foreach (var (agent, transform) in
            SystemAPI.Query<RefRO<PathAgent>, RefRW<LocalTransform>>())
        {
            float3 dir = math.normalize(agent.ValueRO.Destination - transform.ValueRO.Position);
            transform.ValueRW.Position += agent.ValueRO.Speed * dt * dir;
        }
    }
}