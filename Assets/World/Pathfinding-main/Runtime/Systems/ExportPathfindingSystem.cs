using Pathfinding.Components;
using Pathfinding.Data;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace Pathfinding.Systems
{
    public partial struct ExportPathfindingSystem : ISystem
    {
        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder().WithAll<Pathfinder, PathBuffer>()
                .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState).Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            var pathResults = SystemAPI.GetSingleton<PathResultsSingleton>();

            state.Dependency = new Job()
            {
                findPathReadHandle = SystemAPI.GetComponentTypeHandle<Pathfinder>(true),
                pathBufferWriteHandle = SystemAPI.GetBufferTypeHandle<PathBuffer>(),
                pathResults = pathResults.results,
            }.ScheduleParallel(_query, state.Dependency);
        }


        [BurstCompile]
        private unsafe struct Job : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<Pathfinder> findPathReadHandle;

            [NativeDisableParallelForRestriction] public BufferTypeHandle<PathBuffer> pathBufferWriteHandle;

            [ReadOnly] public NativeParallelHashMap<int, PathQueryResult> pathResults;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var findPaths = chunk.GetComponentDataPtrRO(ref findPathReadHandle);
                var pathBufferAccessor = chunk.GetBufferAccessor(ref pathBufferWriteHandle);

                for (var i = 0; i < chunk.Count; i++)
                {
                    ref var findPath = ref findPaths[i];

                    if (findPath.pathId == 0 || !pathResults.ContainsKey(findPath.pathId))
                        continue;

                    var result = pathResults[findPath.pathId];
                    findPath.pathStatus = result.status;

                    //Debug.Log($"Found path result writing {result.PathLength}");

                    var buffer = pathBufferAccessor[i];
                    buffer.Clear();

                    for (var ind = 0; ind < result.pathLength; ind++)
                    {
                        buffer.Add(new() { position = result.path[ind].position });
                    }
                }
            }
        }
    }
}