using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;


namespace WeatherTheStorm.Enemy
{
    public struct PathAgent : IComponentData
    {
        public float3 From;
        public float3 Destination;
        public float  SqrMinDistanceRequired;

        public int    AgentID;
        public int    PathID;
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
        }
    }


    //[BurstCompile]
    //[WithAll(typeof(EnemyTag))]
    //public partial struct MoveToJob : IJobEntity
    //{
    //    public float deltaTime;

    //    void Execute(in PathAgent agent, ref LocalTransform transform, ref PathPosition path)
    //    {
    //        float3 direction = math.normalize(agent.Destination - transform.Position);
    //        transform.Position += agent.Speed * deltaTime * direction;
    //        path.Position       = transform.Position;
    //    }
    //}

    [BurstCompile]
    public partial struct FindPathJob : IJobEntity
    {
        void Execute(in PathAgent pathAgent)
        {
            var fromPos = pathAgent.From;
            var destPos = pathAgent.Destination;
            var minDist = pathAgent.SqrMinDistanceRequired;

            //
            // TODO:
            // Does this handle float errors at all?
            //

            if(!math.all(fromPos == destPos))
            {
                var lengthRemaining = math.distancesq(fromPos, destPos);
                if(lengthRemaining > minDist)
                {
                    //
                    // TODO:
                    // This means this agent needs a path request.
                    // I want a path: From -> Dest
                    // The orignal code does it from a system approach. Why.
                    // Why don't we keep it local to the agent?
                    // Could it be a funneling thing? Or simply trying to separate the system?
                    //
                }
            }
        }
    }
}