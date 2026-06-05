using Pathfinding.Data;
using Pathfinding.Utility;
using Unity.Entities;

namespace Pathfinding.Components
{
    public struct PathRequestsSingleton : IComponentData
    {
        public NativeWorkQueue<PathQuery> requests;
    }
}