using UnityEngine.Experimental.AI;

namespace Pathfinding.Data
{
    public unsafe struct PathQueryResult
    {
        public NavMeshLocation* path;

        public int pathLength;
        public PathQueryStatus status;
    }
}