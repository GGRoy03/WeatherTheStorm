using UnityEngine.Experimental.AI;

namespace Pathfinding.Data
{
    public unsafe struct PathQuery
    {
        public int pathRequestId;

        public NavMeshLocation from;
        public NavMeshLocation to;

        //public int id;
        //public int key;
        public int areaMask;

        // result data
        public NavMeshLocation* path;
        public int pathLength;
        public PathQueryStatus status;
    }
}