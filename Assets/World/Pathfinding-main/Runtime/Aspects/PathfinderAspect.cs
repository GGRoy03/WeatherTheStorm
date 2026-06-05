using Pathfinding.Components;

using Unity.Entities;
using Unity.Mathematics;

namespace Pathfinding.Aspects
{
    public readonly partial struct PathfinderAspect : IAspect
    {
        public readonly RefRW<Pathfinder> pathfinderRef;
        public readonly EnabledRefRW<Pathfinder> pathfinderEnabledRef;

        /// <summary>
        /// Initiates a navmesh query for specified positions. If exact final position is impossible to get to
        /// closest possible position will be found.
        /// Results of query are uploaded to `PathBuffer` dynamic buffer on same entity.
        /// </summary>
        /// <param name="from">Starting position for navmesh query</param>
        /// <param name="to">Final position for navmesh query</param>
        public void FindPath(float3 from, float3 to)
        {
            ref var pf = ref pathfinderRef.ValueRW;
            pf.from = from;
            pf.to = to;
            pathfinderEnabledRef.ValueRW = true;
        }
    }
}