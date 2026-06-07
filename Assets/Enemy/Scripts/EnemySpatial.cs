using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace WeatherTheStorm.Enemy
{
    //
    // NOTE:
    // X) So some of this can probably be done in parallel given its structure. We'll keep it simple
    //    for now. At least it's burst compiled.
    // 

    [BurstCompile]
    public struct EnemyBoundingVolumeHierarchy
    {
        private struct BVHNode
        {
            public AABB Bounds;
            public int  LeftChild;
            public int  FirstEnemy;
            public int  EnemyCount;
        }

        private NativeArray<EnemyHandle> m_EnemyHandles;
        private NativeArray<BVHNode>     m_BVHNodes;
        private int                      m_BVHNodeCount;

        public void Construct([ReadOnly] NativeArray<EnemyHandle> enemyHandles, NativeArray<AABB> worldBounds)
        {
            Debug.Assert(enemyHandles.Length == worldBounds.Length);

            m_BVHNodes     = new NativeArray<BVHNode>(2 * worldBounds.Length - 1, Allocator.Temp);
            m_EnemyHandles = new NativeArray<EnemyHandle>(enemyHandles.Length, Allocator.Temp);
            m_EnemyHandles.CopyFrom(enemyHandles);

            BVHNode rootNode = new()
            {
                LeftChild  = 0,
                FirstEnemy = 0,
                EnemyCount = m_EnemyHandles.Length,
            };
            m_BVHNodes[m_BVHNodeCount++] = rootNode;

            ComputeNodeBounds(0, worldBounds);
            SubdivideNodeBounds(0, worldBounds);
        }


        public readonly bool CollidesWithAABB(AABB aabb, out EnemyHandle firstHandle)
        {
            bool result     = false;
            int  foundIndex = TryFindOverlappingAABB(aabb, 0);

            if(foundIndex != -1)
            {
                firstHandle = m_EnemyHandles[foundIndex];
                result      = true;
            }
            else
            {
                firstHandle = EnemyHandle.Null;
            }

            return result;
        }

        private readonly int TryFindOverlappingAABB(AABB aabb, int nodeIndex)
        {
            BVHNode node = m_BVHNodes[nodeIndex];
            if(node.Bounds.Contains(aabb))
            {
                if(node.EnemyCount == 0)
                {
                    TryFindOverlappingAABB(aabb, node.LeftChild);
                    TryFindOverlappingAABB(aabb, node.LeftChild + 1);
                }
                else
                {
                    return nodeIndex;
                }
            }

            return -1;
        }

        private void ComputeNodeBounds(int nodeIndex, NativeArray<AABB> worldBounds)
        {
            BVHNode node = m_BVHNodes[nodeIndex];
            Debug.Assert(node.FirstEnemy + node.EnemyCount <= worldBounds.Length);

            float3 Min = new(float.MaxValue, float.MaxValue, float.MaxValue);
            float3 Max = new(float.MinValue, float.MinValue, float.MinValue);
            for (int enemyIdx = node.FirstEnemy; enemyIdx < node.FirstEnemy + node.EnemyCount; ++enemyIdx)
            {
                AABB aabb = worldBounds[enemyIdx];

                Min = math.min(node.Bounds.Min, aabb.Min);
                Max = math.max(node.Bounds.Max, aabb.Max);
            }

            node.Bounds.Center  = Min + ((Max - Min) * 0.5f);
            node.Bounds.Extents = (Max - Min) * 0.5f;

            m_BVHNodes[nodeIndex] = node;
        }

        private void SubdivideNodeBounds(int nodeIndex, NativeArray<AABB> worldBounds)
        {
            //
            // TODO:
            // x) I don't know about that one.
            //

            BVHNode node = m_BVHNodes[nodeIndex];
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
                AABB  bounds       = worldBounds[leftPointer];
                float centerOnAxis = bounds.Min[axisIndex] + ((bounds.Max[axisIndex] - bounds.Min[axisIndex]) * 0.5f);

                if (centerOnAxis < splitPosition)
                {
                    ++leftPointer;
                }
                else
                {
                    AABB        tempBounds = worldBounds[leftPointer];
                    EnemyHandle tempHandle = m_EnemyHandles[leftPointer];

                    worldBounds[leftPointer]    = worldBounds[rightPointer];
                    m_EnemyHandles[leftPointer] = m_EnemyHandles[rightPointer];

                    worldBounds[rightPointer]    = tempBounds;
                    m_EnemyHandles[rightPointer] = tempHandle;

                    --rightPointer;
                }
            }

            int leftCount = leftPointer - node.FirstEnemy;
            if (leftCount > 0 && leftCount < node.EnemyCount)
            {
                int leftChildIndex  = m_BVHNodeCount++;
                int rightChildIndex = m_BVHNodeCount++;

                BVHNode leftChild = new()
                {
                    FirstEnemy = node.FirstEnemy,
                    EnemyCount = leftCount,
                };
                m_BVHNodes[leftChildIndex] = leftChild;

                BVHNode rightChild = new()
                {
                    FirstEnemy = leftPointer,
                    EnemyCount = node.EnemyCount - leftCount,
                };
                m_BVHNodes[rightChildIndex] = rightChild;

                BVHNode rootNode = new()
                {
                    LeftChild  = leftChildIndex,
                    EnemyCount = 0,
                };
                m_BVHNodes[nodeIndex] = rootNode;

                ComputeNodeBounds(leftChildIndex , worldBounds);
                ComputeNodeBounds(rightChildIndex, worldBounds);

                SubdivideNodeBounds(leftChildIndex , worldBounds);
                SubdivideNodeBounds(rightChildIndex, worldBounds);
            }
        }
    }
}