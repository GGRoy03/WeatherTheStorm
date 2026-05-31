using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;


namespace WeatherTheStorm.Enemy
{
    public struct PathAgent : IComponentData
    {
        public float3 Destination;
        public float  Speed;
    }

    public struct PathPosition : IComponentData
    {
        public Vector3 Position;
    }


    public partial struct EnemyPathfindingSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            state.Dependency = new MoveToJob
            {
                deltaTime = SystemAPI.Time.DeltaTime
            }.ScheduleParallel(state.Dependency);
        }
    }


    [BurstCompile]
    [WithAll(typeof(EnemyTag))]
    public partial struct MoveToJob : IJobEntity
    {
        public float deltaTime;

        void Execute(in PathAgent agent, ref LocalTransform transform, ref PathPosition path)
        {
            float3 direction = math.normalize(agent.Destination - transform.Position);
            transform.Position += agent.Speed * deltaTime * direction;
            path.Position       = transform.Position;
        }
    }
}