using System.Collections.Generic;

using UnityEngine;

using Unity.Mathematics;

using WeatherTheStorm.Build;

//
// TODO: Convert to scriptable object once we have it right.
//

public class MapGen : MonoBehaviour
{
    [SerializeField] private MeshFilter m_LevelMesh;
    [SerializeField] private int        m_CellSize;

    [SerializeField] private Material   m_BuildableMaterial;

    private struct BoxPos
    {
        public OccupancyGrid2D.CellIndex index;
        public float                     height;
    }

    private List<BoxPos>    m_GizmoPositions = new();
    private OccupancyGrid2D m_Grid;

    private void Awake()
    {
        m_Grid = new(1000, 1000, m_CellSize, Vector3.zero);

        var point = new int2[1];
        point[0] = new int2(0, 0);
        var cellShape = new OccupancyGrid2D.CellShape(point);

        Debug.Log("Running Generator.");
        Debug.Assert(m_CellSize > 0);

        var       renderer  = m_LevelMesh.GetComponent<MeshRenderer>();
        var       mesh      = m_LevelMesh.sharedMesh;
        Vector3[] vertices  = mesh.vertices;
        int[]     triangles = mesh.triangles;

        //
        // ...
        //

        var meshTransform = m_LevelMesh.transform;
        for (int vertexIdx = 0; vertexIdx < vertices.Length; ++vertexIdx)
        {
            vertices[vertexIdx] = meshTransform.TransformPoint(vertices[vertexIdx]);
        }

        for (int idx = 0; idx < triangles.Length; idx += 3)
        {
            int indexA  = triangles[idx];
            int indexB  = triangles[idx + 1];
            int indexC  = triangles[idx + 2];
            var vertexA = vertices[indexA];
            var vertexB = vertices[indexB];
            var vertexC = vertices[indexC];

            var materialAtTriangle = GetMaterialFromTriangleIndex(idx / 3, mesh, renderer);
            if(materialAtTriangle == m_BuildableMaterial)
            {
                float minX = Mathf.Min(vertexA.x, Mathf.Min(vertexB.x, vertexC.x));
                float maxX = Mathf.Max(vertexA.x, Mathf.Max(vertexB.x, vertexC.x));
                float minZ = Mathf.Min(vertexA.z, Mathf.Min(vertexB.z, vertexC.z));
                float maxZ = Mathf.Max(vertexA.z, Mathf.Max(vertexB.z, vertexC.z));

                float posX = minX;
                while(posX < maxX)
                {
                    float posZ = minZ;
                    while(posZ < maxZ)
                    {
                        var position       = new Vector3(x: posX, y: vertexA.y, z: posZ);
                        var centerPosition = new Vector2(x: position.x + m_CellSize * 0.5f, y: position.z + m_CellSize * 0.5f);

                        //
                        // TODO:
                        // x) So this test is off, or the data is off. It is definitely not aggressive
                        //    enough. we are overlapping cells into the wall. Seems like everything
                        //    is slighlty off as well. The granularity seems to play a big part
                        //    from what I can tell...
                        //

                        bool isInTriangle = IsPointInTriangle(centerPosition, vertexA, vertexB, vertexC);
                        if(isInTriangle)
                        {
                            var gridPos   = new Vector3(centerPosition.x, vertexA.y, centerPosition.y);
                            var cellIndex = m_Grid.PositionToCellIndex(gridPos);

                            m_GizmoPositions.Add(new BoxPos()
                            {
                                index  = cellIndex,
                                height = vertexA.y
                            });
                        }

                        posZ += m_CellSize;
                    }

                    posX += m_CellSize;
                }
            }
        }
    }

    private Material GetMaterialFromTriangleIndex(int triangleIndex, Mesh mesh, MeshRenderer renderer)
    {
        Material result = null;

        int runningIndexCount = 0;
        int indexOffset       = triangleIndex * 3;
        for(int submeshIdx = 0; submeshIdx < mesh.subMeshCount; ++submeshIdx)
        {
            int submeshIndexCount = (int)mesh.GetIndexCount(submeshIdx);
            if (indexOffset < (runningIndexCount + submeshIndexCount))
            {
                result = renderer.sharedMaterials[submeshIdx];
                break;
            }

            runningIndexCount += submeshIndexCount;
        }

        return result;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        foreach(var boxPos in m_GizmoPositions)
        {
            var position = m_Grid.CellIndexToPosition(boxPos.index);
            position.y = boxPos.height;

            Gizmos.DrawWireCube(position, Vector3.one * m_CellSize);
        }
    }

    //
    // TODO:
    // x) Understand this code
    // x) Verify this code
    //

    private float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        float result = (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
        return result;
    }

    private bool IsPointInTriangle(Vector2 point, Vector3 vertexA, Vector3 vertexB, Vector3 vertexC)
    {
        var vertexAXZ = new Vector2(vertexA.x, vertexA.z);
        var vertexBXZ = new Vector2(vertexB.x, vertexB.z);
        var vertexCXZ = new Vector2(vertexC.x, vertexC.z);

        float d1 = Sign(point, vertexAXZ, vertexBXZ);
        float d2 = Sign(point, vertexBXZ, vertexCXZ);
        float d3 = Sign(point, vertexCXZ, vertexAXZ);

        bool hasNegative = (d1 < 0) || (d2 < 0) || (d3 < 0);
        bool hasPositive = (d1 > 0) || (d2 > 0) || (d3 > 0);
        bool result      = !(hasNegative && hasPositive);

        return result;
    }
}
