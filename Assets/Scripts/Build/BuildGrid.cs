using System;
using System.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

//
// TODO:
// x) General code simplification, some parts are still quite rough with a lot of code duplication.
// x) File organization, self explanatory.
//
// BUGS:
// x) The ghost object doens't quite work when overlapping the same object (sometimes).
//    We probably simply need to inflate the ghost object. What happens when we deal with different
//    objects and one of them is smaller than the bigger one and it just eats it? It should probably
//    always appear in front probably?
//

public class BuildGrid : MonoBehaviour
{
    //
    // NOTE:
    // Will be removed at some point, mostly for prototyping.
    //

    [Header("Generation")]
    [SerializeField] private uint     m_CellCountX;
    [SerializeField] private uint     m_CellCountZ;
    [SerializeField] private uint     m_CellSize;
    [SerializeField] private Vector3  m_GridOrigin;
                     private BitArray m_OccupancySet;

    [Header("Visuals")]
    [SerializeField]                    private GameObject m_GridCellObject;
    [SerializeField, Range(0.0f, 1.0f)] private float      m_GhostObjectAlpha;
    [SerializeField]                    private Color      m_ValidGhostColor;
    [SerializeField]                    private Color      m_InvalidGhostColor;
                                        private GameObject m_GhostObject;
                                        private GameObject m_ObjectToPlace;
                                        private BuildShape m_GhostShape;

    [Header("Inputs")]
    [SerializeField] private InputActionAsset m_InputMap;
    [SerializeField] private bool             m_ClearSelectionOnPlace;
                     private InputAction      m_PointerPositionAction;
                     private InputAction      m_PointerMainAction;
                     private InputAction      m_RotateShapeCWAction;
                     private InputAction      m_RotateShapeCCWAction;

    // =============================================================
    // [Section] : Cell Index
    // NOTE:
    // The reason we're taking in parameters instead of using what's
    // already on the class is simply because I anticipate that we're
    // going to have many build regions.
    // =============================================================

    private struct CellIndex
    {
        public int m_PosX;
        public int m_PosZ;

        public CellIndex(int x, int z)
        {
            m_PosX = x;
            m_PosZ = z;
        }

        public CellIndex(Vector3 worldPosition, float minX, float minZ, uint cellSize)
        {
            m_PosX = (int)Mathf.Floor((worldPosition.x - minX) / cellSize);
            m_PosZ = (int)Mathf.Floor((worldPosition.z - minZ) / cellSize);
        }

        public readonly bool IsInsideGrid(uint cellCountX, uint cellCountZ)
        {
            bool result = m_PosX >= 0 && m_PosX < cellCountX &&
                          m_PosZ >= 0 && m_PosZ < cellCountZ;
            return result;
        }

        public readonly Vector3 ToWorldPosition(Vector3 start, uint cellSize)
        {
            Vector3 result = new()
            {
                x = start.x + (m_PosX * cellSize) + (cellSize / 2.0f),
                y = start.y,
                z = start.z + (m_PosZ * cellSize) + (cellSize / 2.0f),
            };

            return result;
        }

        public readonly int ToNativeIndexUnsafe(uint cellCountX)
        {
            int result = (int)(m_PosZ * cellCountX + m_PosX);
            return result;
        }
    };


    // =============================================================
    // [Section] : Visual
    // =============================================================


    private void SetGhostColor(Color color, GameObject gameObject)
    {
        Renderer[] renderers = gameObject.GetComponentsInChildren<Renderer>();
        for (int rendererIdx = 0; rendererIdx < renderers.Length; ++rendererIdx)
        {
            Renderer renderer = renderers[rendererIdx];
            for (int materialIdx = 0; materialIdx < renderer.materials.Length; ++materialIdx)
            {
                Material material = renderer.materials[materialIdx];

                Color transparentColor = new(color.r, color.g, color.b, m_GhostObjectAlpha);
                material.color = transparentColor;
            }
        }
    }


    // =============================================================
    // [Section] : ...
    // =============================================================


    public void SetGhostObject(GameObject objectToPlace)
    {
        //
        // NOTE:
        // Experimental, this would be passed in since this code wouldn't know what the object's 
        // shape is.
        //

        int2[] shapePoints =
        {
            new int2(x:  0, y:  0),
            //new int2(x: -1, y:  0),
            //new int2(x: -1, y: -1),
            //new int2(x:  1, y:  0),
            //new int2(x:  0, y:  1),
        };

        m_GhostShape = new BuildShape(shapePoints);

        //
        // NOTE:
        // We might need to accept two versions of the same object here. The ghost and the actual object.
        // The reason is: If we instantiate an object, its script will run, meaning that even if it's not
        // placed it will have real logic runnning. So we need to figure out if that's the correct
        // approach or if there's a simpler one.
        //

        GameObject ghostInstance = Instantiate(objectToPlace);
        if (ghostInstance != null)
        {
            //
            // TODO:
            // x) Explain how this works.
            //

            Renderer[] renderers = ghostInstance.GetComponentsInChildren<Renderer>();
            for(int rendererIdx = 0; rendererIdx < renderers.Length; ++rendererIdx)
            {
                Renderer renderer = renderers[rendererIdx];
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                for (int materialIdx = 0; materialIdx < renderer.materials.Length; ++materialIdx)
                {
                    Material material = renderer.materials[materialIdx];
                    material.renderQueue = 3000;
                    material.SetFloat("_Surface", 1);
                    material.SetFloat("_Blend", 0);
                    material.SetFloat("_ZWrite", 0);
                    material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    material.SetOverrideTag("RenderType", "Transparent");
                }
            }

            m_GhostObject   = ghostInstance;
            m_ObjectToPlace = objectToPlace;
        }
    }

    public void ClearGhostObject()
    {
        m_GhostObject   = null;
        m_ObjectToPlace = null;
    }

    private void OnEnable()
    {
        m_InputMap.Enable();
    }

    private void OnDisable()
    {
        m_InputMap.Disable();
    }


    private void Start()
    {
        m_PointerPositionAction = m_InputMap.FindAction("PointerOnScreen");
        m_PointerMainAction     = m_InputMap.FindAction("PointerMain");
        m_RotateShapeCWAction   = m_InputMap.FindAction("RotateShapeCW");
        m_RotateShapeCCWAction  = m_InputMap.FindAction("RotateShapeCCW");

        //
        // TODO:
        // x) Clean this up.
        //

        float gridStartX = ((int)m_GridOrigin.x - (m_CellCountX * m_CellSize / 2));
        float gridStartZ = ((int)m_GridOrigin.z - (m_CellCountZ * m_CellSize / 2));
        for (uint Z = 0; Z < m_CellCountZ; Z++)
        {
            for (uint X = 0; X < m_CellCountX; X++)
            {
                Vector3 position = new(gridStartX + (X * m_CellSize), 0.0f, gridStartZ + (Z * m_CellSize));
                Instantiate(m_GridCellObject, position, Quaternion.identity, this.transform);
            }
        }

        //
        // NOTE:
        // Experimental, seems to work decently well.
        //

        m_OccupancySet = new BitArray((int)(m_CellCountX * m_CellCountZ));
    }

    private void Update()
    {
        if (m_GhostObject != null)
        {
            Vector2 pointerScreenPosition = m_PointerPositionAction.ReadValue<Vector2>();
            Ray     pointerRay            = Camera.main.ScreenPointToRay(pointerScreenPosition);
            Vector3 pointerWorldPosition  = PointerToWorldPosition(pointerRay, m_GridOrigin, Vector3.up);

            float     minX      = ((int)m_GridOrigin.x - (m_CellCountX * m_CellSize / 2)) - (m_CellSize / 2.0f);
            float     minZ      = ((int)m_GridOrigin.z - (m_CellCountZ * m_CellSize / 2)) - (m_CellSize / 2.0f);
            CellIndex cellIndex = new(pointerWorldPosition, minX, minZ, m_CellSize);

            bool isFreeSpace = IsFreeSpace(m_GhostShape, cellIndex);
            if(isFreeSpace)
            {
                Vector3 ghostPos  = cellIndex.ToWorldPosition(new(minX, m_GridOrigin.y, minZ), m_CellSize);
                bool    isPlacing = m_PointerMainAction.WasPressedThisFrame();

                if (isPlacing)
                {
                    Instantiate(m_ObjectToPlace, ghostPos, Quaternion.identity);

                    FillOccupancySet(m_GhostShape, cellIndex);

                    if (m_ClearSelectionOnPlace)
                    {
                        ClearGhostObject();
                    }
                }
                else
                {
                    SetGhostColor(m_ValidGhostColor, m_GhostObject);
                    m_GhostObject.transform.position = ghostPos;
                }
            }
            else
            {
                SetGhostColor(m_InvalidGhostColor, m_GhostObject);
                m_GhostObject.transform.position = pointerWorldPosition;
            }
        }

        if(m_RotateShapeCWAction.WasPressedThisFrame())
        {
            m_GhostShape.RotateCW();
        }
        
        if(m_RotateShapeCCWAction.WasPressedThisFrame())
        {
            m_GhostShape.RotateCCW();
        }
    }

    private Vector3 PointerToWorldPosition(Ray pointerRay, Vector3 planeOrigin, Vector3 planeNormal)
    {
        Vector3 result = new(float.MaxValue, float.MaxValue, float.MaxValue);

        Plane gridPlane = new(planeNormal, planeOrigin);
        if(gridPlane.Raycast(pointerRay, out float distance))
        {
            result = pointerRay.GetPoint(distance);
        }

        return result;
    }

    // =============================================================
    // [Section] : ...
    // =============================================================

    private bool IsCellOccupied(CellIndex index)
    {
        bool result = true;

        if(index.IsInsideGrid(m_CellCountX, m_CellCountZ))
        {
            int nativeIndex = index.ToNativeIndexUnsafe(m_CellCountX);
            result = m_OccupancySet[nativeIndex];
        }

        return result;
    }

    private bool IsFreeSpace(BuildShape shape, CellIndex center)
    {
        bool result = true;

        var points = shape.Points();
        for (int pointIdx = 0; pointIdx < points.Length; ++pointIdx)
        {

            int2      point  = points[pointIdx];
            int       xIndex = center.m_PosX + point[0];
            int       zIndex = center.m_PosZ + point[1];
            CellIndex index  = new(xIndex, zIndex);

            if(IsCellOccupied(index))
            {
                result = false;
                break;
            }
        }

        return result;
    }

    private void FillOccupancySet(BuildShape shape, CellIndex center)
    {
        Debug.Assert(IsFreeSpace(shape, center));

        var points = shape.Points();
        for (int pointIdx = 0; pointIdx < points.Length; ++pointIdx)
        {
            //
            // Duplicate code.
            //


            int2      point       = points[pointIdx];
            int       xIndex      = center.m_PosX + point[0];
            int       zIndex      = center.m_PosZ + point[1];
            CellIndex index       = new(xIndex, zIndex);
            int       nativeIndex = index.ToNativeIndexUnsafe(m_CellCountX);

            Debug.Assert(m_OccupancySet[nativeIndex] == false);
            m_OccupancySet[nativeIndex] = true;
        }
   }

    // =============================================================
    // [Section] : Shape Reprensenation
    // =============================================================

    struct BuildShape
    {
        private int2[] m_Points;
        public ReadOnlySpan<int2> Points() => m_Points;

        public BuildShape(int2[] points)
        {
            m_Points = points;
        }

        public void RotateCW()
        {
            this.RotatePoints(Mathf.PI * -0.5f);
        }

        public void RotateCCW()
        {
            this.RotatePoints(Mathf.PI * 0.5f);
        }

        private void RotatePoints(float angle)
        {
            float2x2 rotationMatrix = float2x2.Rotate(angle);

            for (int pointIdx = 0; pointIdx < m_Points.Length; ++pointIdx)
            {
                int2 point       = m_Points[pointIdx];
                int2 transformed = (int2)math.mul(rotationMatrix, (float2)point);

                m_Points[pointIdx] = transformed;
            }
        }
    }

    //private void OnDrawGizmosSelected()
    //{
    //    Gizmos.color = Color.cyan;

    //    var points = m_GhostShape.Points();
    //    for (int pointIdx = 0; pointIdx < points.Length; ++pointIdx)
    //    {
    //        Vector3 position = new Vector3(x: points[pointIdx][0], y: 0.0f, z: points[pointIdx][1]);

    //        Gizmos.DrawWireCube(position, Vector3.one);
    //    }
    //}
}