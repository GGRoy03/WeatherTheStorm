using Unity.Entities;
using Unity.Mathematics;

namespace Pathfinding.Components
{
    [InternalBufferCapacity(0)]
    public struct PathBuffer : IBufferElementData
    {
        public float3 position;
    }
}