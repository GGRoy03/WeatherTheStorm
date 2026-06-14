using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

using UnityEngine;

namespace WeatherTheStorm.Enemy
{
    [BurstCompile]
    public struct BuildBVHJob : IJob
    {
        //
        // NOTE:
        // x) Both of these arrays are mutated when this job executes.
        //

        public NativeArray<EnemyHandle> EnemyHandles;
        public NativeArray<AABB>        WorldBounds;

        public  NativeArray<BVHNode> OutNodes;
        private int                  NodeCount;

        public void Execute()
        {
            BVHNode root = new()
            {
                LeftChild  = 0,
                FirstEnemy = 0,
                EnemyCount = EnemyHandles.Length,
            };

            NodeCount            = 0;
            OutNodes[NodeCount++] = root;

            ComputeNodeBounds(0);
            SubdivideNodeBounds(0);
        }

        private void ComputeNodeBounds(int nodeIndex)
        {
            BVHNode node = OutNodes[nodeIndex];
            Debug.Assert(node.FirstEnemy + node.EnemyCount <= WorldBounds.Length);

            float3 Min = new(float.MaxValue, float.MaxValue, float.MaxValue);
            float3 Max = new(float.MinValue, float.MinValue, float.MinValue);
            for (int enemyIdx = node.FirstEnemy; enemyIdx < node.FirstEnemy + node.EnemyCount; ++enemyIdx)
            {
                AABB aabb = WorldBounds[enemyIdx];

                Min = math.min(Min, aabb.Min);
                Max = math.max(Max, aabb.Max);
            }

            node.Bounds.Center  = Min + ((Max - Min) * 0.5f);
            node.Bounds.Extents = Max - node.Bounds.Center;

            OutNodes[nodeIndex] = node;
        }

        private void SubdivideNodeBounds(int nodeIndex)
        {
            //
            // TODO:
            // x) I don't know about that one.
            //

            BVHNode node = OutNodes[nodeIndex];
            if(node.EnemyCount < 2)
            {
                return;
            }

            float3 extent = node.Bounds.Max - node.Bounds.Min;
            int axisIndex = 0;
            if (extent.y > extent.x)          axisIndex = 1;
            if (extent.z > extent[axisIndex]) axisIndex = 2;

            float splitPosition = node.Bounds.Min[axisIndex] + extent[axisIndex] * 0.5f;
            int   leftPointer   = node.FirstEnemy;
            int   rightPointer  = node.FirstEnemy + node.EnemyCount - 1;
            while (leftPointer <= rightPointer)
            {
                AABB  bounds       = WorldBounds[leftPointer];
                float centerOnAxis = bounds.Min[axisIndex] + ((bounds.Max[axisIndex] - bounds.Min[axisIndex]) * 0.5f);

                if (centerOnAxis < splitPosition)
                {
                    ++leftPointer;
                }
                else
                {
                    //
                    // TODO:
                    // x) Simplify this?
                    //

                    AABB        tempBounds = WorldBounds[leftPointer];
                    EnemyHandle tempHandle = EnemyHandles[leftPointer];

                    WorldBounds[leftPointer]  = WorldBounds[rightPointer];
                    EnemyHandles[leftPointer] = EnemyHandles[rightPointer];

                    WorldBounds[rightPointer]  = tempBounds;
                    EnemyHandles[rightPointer] = tempHandle;

                    --rightPointer;
                }
            }

            int leftCount = leftPointer - node.FirstEnemy;
            if (leftCount > 0 && leftCount < node.EnemyCount)
            {
                int leftChildIndex  = NodeCount++;
                int rightChildIndex = NodeCount++;

                //
                // TODO:
                // x) Ugly.
                //

                BVHNode leftChild = new()
                {
                    FirstEnemy = node.FirstEnemy,
                    EnemyCount = leftCount,
                };
                OutNodes[leftChildIndex] = leftChild;

                BVHNode rightChild = new()
                {
                    FirstEnemy = leftPointer,
                    EnemyCount = node.EnemyCount - leftCount,
                };
                OutNodes[rightChildIndex] = rightChild;

                BVHNode rootNode = new()
                {
                    LeftChild  = leftChildIndex,
                    EnemyCount = 0,
                    Bounds     = node.Bounds,
                };
                OutNodes[nodeIndex] = rootNode;

                ComputeNodeBounds(leftChildIndex);
                ComputeNodeBounds(rightChildIndex);

                SubdivideNodeBounds(leftChildIndex);
                SubdivideNodeBounds(rightChildIndex);
            }
        }
    }

    public struct BVHNode
    {
        public AABB Bounds;
        public int  LeftChild;
        public int  FirstEnemy;
        public int  EnemyCount;
    }


    [BurstCompile]
    public struct EnemyBVH
    {
        private NativeArray<EnemyHandle>.ReadOnly m_Handles;
        private NativeArray<BVHNode>.ReadOnly     m_Nodes;

        public EnemyBVH(NativeArray<BVHNode>.ReadOnly nodes, NativeArray<EnemyHandle>.ReadOnly handles)
        {
            m_Nodes   = nodes;
            m_Handles = handles;
        }

        public bool CollidesWithAABB(AABB aabb, out EnemyHandle firstHandle)
        {
            bool result     = false;
            int  foundIndex = TryFindOverlappingAABB(aabb, 0);

            if(foundIndex != -1)
            {
                BVHNode foundNode = m_Nodes[foundIndex];

                firstHandle = m_Handles[foundNode.FirstEnemy];
                result      = true;
            }
            else
            {
                firstHandle = EnemyHandle.Null;
            }

            return result;
        }

        private int TryFindOverlappingAABB(AABB aabb, int nodeIndex)
        {
            BVHNode node = m_Nodes[nodeIndex];
            if(math.all(node.Bounds.Max >= aabb.Min & aabb.Max >= node.Bounds.Min))
            {
                if(node.EnemyCount == 0)
                {
                    int leftNodeIndex = TryFindOverlappingAABB(aabb, node.LeftChild);
                    if (leftNodeIndex != -1)
                    {
                        return leftNodeIndex;
                    }

                    int rightNodeIndex = TryFindOverlappingAABB(aabb, node.LeftChild + 1);
                    if(rightNodeIndex != -1)
                    {
                        return rightNodeIndex;
                    }
                }
                else
                {
                    return nodeIndex;
                }
            }

            return -1;
        }
    }
}