using Pathfinding.Data;
using Unity.Collections;
using Unity.Entities;

namespace Pathfinding.Components
{
    public struct PathResultsSingleton : IComponentData
    {
        public NativeParallelHashMap<int, PathQueryResult> results;
    }
}