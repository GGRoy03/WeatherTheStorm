using Pathfinding.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Authoring;

namespace Pathfinding.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct DrawFoundPathSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsDebugDisplayData>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            SystemAPI.GetSingleton<PhysicsDebugDisplayData>();

            foreach (var path in SystemAPI.Query<DynamicBuffer<PathBuffer>>())
            {
                if (path.IsEmpty)
                {
                    continue;
                }

                var pathArray = path.AsNativeArray().Reinterpret<float3>();

                for (var i = 0; i < pathArray.Length - 1; i++)
                {
                    var pos = pathArray[i];
                    var nextPos = pathArray[i + 1];
                    PhysicsDebugDisplaySystem.Line(pos, nextPos, Unity.DebugDisplay.ColorIndex.Green);
                }
            }
        }
    }
}