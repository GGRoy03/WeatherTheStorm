using System;
using System.Collections;

using Unity.Mathematics;

using UnityEngine;

namespace WeatherTheStorm.Build
{
    public class OccupancyGrid2D
    {
        private int      m_CellCountX;
        private int      m_CellCountZ;
        private int      m_CellSize;
        private Vector3  m_GridOrigin;
        private BitArray m_OccupancySet;

        public OccupancyGrid2D(int cellCountX, int cellCountZ, int cellSize, Vector3 gridOrigin)
        {
            Debug.Assert(cellCountX > 0);
            Debug.Assert(cellCountZ > 0);
            Debug.Assert(cellSize   > 0);

            m_CellCountX   = cellCountX;
            m_CellCountZ   = cellCountZ;
            m_CellSize     = cellSize;
            m_GridOrigin   = gridOrigin;
            m_OccupancySet = new BitArray(m_CellCountX * m_CellCountZ);
        }

        public CellIndex PositionToCellIndex(Vector3 worldPosition)
        {
            //
            // TODO:
            // x) Check if this is right.
            // x) Remove code dup
            //

            float minX = (m_GridOrigin.x - (m_CellCountX * m_CellSize / 2)) - (m_CellSize / 2.0f);
            float minZ = (m_GridOrigin.z - (m_CellCountZ * m_CellSize / 2)) - (m_CellSize / 2.0f);

            CellIndex result = new(
                x: (int)Mathf.Floor((worldPosition.x - minX) / m_CellSize),
                z: (int)Mathf.Floor((worldPosition.z - minZ) / m_CellSize)
            );
            return result;
        }

        public Vector3 CellIndexToPosition(CellIndex index)
        {
            float minX = (m_GridOrigin.x - (m_CellCountX * m_CellSize / 2)) - (m_CellSize / 2.0f);
            float minZ = (m_GridOrigin.z - (m_CellCountZ * m_CellSize / 2)) - (m_CellSize / 2.0f);

            Vector3 result = new()
            {
                x = minX + (index.PosX * m_CellSize) + (m_CellSize / 2.0f),
                y = m_GridOrigin.y,
                z = minZ + (index.PosZ * m_CellSize) + (m_CellSize / 2.0f),
            };
            return result;
        }

        public bool IsInsideGrid(CellIndex index)
        {
            bool result = index.PosX >= 0 && index.PosX < m_CellCountX &&
                          index.PosZ >= 0 && index.PosZ < m_CellCountZ;
            return result;
        }

        public void PlaceShape(CellShape shape, CellIndex center)
        {
            Debug.Assert(!IsShapeBlocked(shape, center));

            var points = shape.Points;
            foreach(var point in points)
            {
                var index = new CellIndex(
                    x: center.PosX + point[0],
                    z: center.PosZ + point[1]
                    );
                Debug.Assert(IsInsideGrid(index));

                int rawIndex = ToRawIndex(index);
                m_OccupancySet[rawIndex] = true;
            }
        }

        public bool IsShapeBlocked(CellShape shape, CellIndex center)
        {
            bool result = false;

            var points = shape.Points;
            foreach(var point in points)
            {
                var index = new CellIndex(
                    x: center.PosX + point[0],
                    z: center.PosZ + point[1]
                    );

                if(IsCellOccupied(index))
                {
                    result = true;
                    break;
                }
            }

            return result;
        }

        private bool IsCellOccupied(CellIndex index)
        {
            bool result = true;

            if(IsInsideGrid(index))
            {
                int rawIndex = ToRawIndex(index);
                result = m_OccupancySet[rawIndex];
            }

            return result;
        }

        private int ToRawIndex(CellIndex index)
        {
            Debug.Assert(IsInsideGrid(index));

            int result = index.PosZ * m_CellCountX + index.PosX;
            return result;
        }

        public struct CellIndex
        {
            public int PosX;
            public int PosZ;

            public CellIndex(int x, int z)
            {
                PosX = x;
                PosZ = z;
            }
        };

        public struct CellShape
        {
            private int2[] m_Points;
            public ReadOnlySpan<int2> Points => m_Points;

            public CellShape(int2[] points)
            {
                m_Points = points;
            }

            public void RotateCW()
            {
                RotatePoints(Mathf.PI * -0.5f);
            }

            public void RotateCCW()
            {
                RotatePoints(Mathf.PI * 0.5f);
            }

            private void RotatePoints(float angle)
            {
                float2x2 rotationMatrix = float2x2.Rotate(angle);

                for (int pointIdx = 0; pointIdx < m_Points.Length; ++pointIdx)
                {
                    int2 point = m_Points[pointIdx];
                    int2 transformed = (int2)math.mul(rotationMatrix, (float2)point);

                    m_Points[pointIdx] = transformed;
                }
            }
        }
    }
}
