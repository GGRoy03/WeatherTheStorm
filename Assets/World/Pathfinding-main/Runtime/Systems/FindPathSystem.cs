using Pathfinding.Components;
using Pathfinding.Data;
using Pathfinding.Utility;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.Experimental.AI;

namespace Pathfinding.Systems
{
    [CreateAfter(typeof(PathfinderSystem))]
    [UpdateInGroup(typeof(PathfindingSystemGroup))]
    public unsafe partial struct FindPathSystem : ISystem
    {
        private EntityQuery _query;

        private NavMeshWorld _navMeshWorld;
        private NavMeshQuery _navMeshQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder().WithAll<Pathfinder, PathBuffer>().Build();

            _navMeshWorld = NavMeshWorld.GetDefaultWorld();
            _navMeshQuery = new(_navMeshWorld, Allocator.Persistent);

            SystemAPI.GetSingletonRW<PathRequestsSingleton>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            _navMeshQuery.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var pathRequests = SystemAPI.GetSingleton<PathRequestsSingleton>();

            pathRequests.requests.Update();

            state.Dependency = new FindPathJob()
            {
                pathRequests = pathRequests.requests.AsParallelWriter(),
                navMeshQuery = _navMeshQuery,
                findPathWriteHandle = SystemAPI.GetComponentTypeHandle<Pathfinder>()
            }.ScheduleParallel(_query, state.Dependency);
        }

        [BurstCompile]
        private struct FindPathJob : IJobChunk
        {
            [ReadOnly] public NavMeshQuery navMeshQuery;
            public NativeWorkQueue<PathQuery>.ParallelWriter pathRequests;
            public ComponentTypeHandle<Pathfinder> findPathWriteHandle;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);

                var findPaths = chunk.GetComponentDataPtrRW(ref findPathWriteHandle);
                var mask = chunk.GetEnabledMask(ref findPathWriteHandle);

                while (enumerator.NextEntityIndex(out var i))
                {
                    ref var findPath = ref findPaths[i];

                    ref var sourcePos = ref findPath.from;
                    ref var targetPos = ref findPath.to;

                    if (math.all(sourcePos == targetPos))
                    {
                        continue;
                    }

                    var length = math.distancesq(sourcePos, targetPos);
                    if (length <= findPath.requiredMinDistanceSq)
                    {
                        // don't destroy, WalkPathSystem needs to give control back
                        continue;
                    }

                    var pathId = pathRequests.TryAdd(out var pathRequest);

                    if (pathId == 0)
                    {
                        //Debug.LogError("Too much requests");
                        continue;
                    }

                    //Debug.Log($"Firing off request with id {pathId} from {sourcePos} to {targetPos}");
                    findPath.pathId = pathId;
                    findPath.pathStatus = PathQueryStatus.InProgress;
                    // findPath.pathWalkerIndex = 0;

                    pathRequest->pathRequestId = pathId;
                    pathRequest->from =
                        navMeshQuery.MapLocation(sourcePos, new float3(10, 10, 10), findPath.agentId, 1 << 0);
                    pathRequest->to =
                        navMeshQuery.MapLocation(targetPos, new float3(10, 10, 10), findPath.agentId, 1 << 0);
                    pathRequest->areaMask = 1 << 0;
                    // todo, actual danger, not setting this screws up every request afterwards
                    pathRequest->status = default;

                    mask[i] = false;
                }
            }
        }
    }
}