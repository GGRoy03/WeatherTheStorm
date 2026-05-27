using System.Collections;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.InputSystem;

public class GridSystem : MonoBehaviour
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

    [Header("Inputs")]
    [SerializeField] private InputActionAsset m_InputMap;
                     private InputAction      m_PointerPositionAction;
                     private InputAction      m_PointerMainAction;

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

        public CellIndex(Vector3 worldPosition, float minX, float minZ, uint cellSize)
        {
            m_PosX = (int)Mathf.Floor((worldPosition.x - minX) / cellSize);
            m_PosZ = (int)Mathf.Floor((worldPosition.z - minZ) / cellSize);
        }

        public bool IsInsideGrid(uint cellCountX, uint cellCountZ)
        {
            bool result = m_PosX >= 0 && m_PosX < cellCountX &&
                          m_PosZ >= 0 && m_PosZ < cellCountZ;
            return result;
        }

        public Vector3 ToWorldPosition(Vector3 start, uint cellSize)
        {
            Vector3 result = new()
            {
                x = start.x + (m_PosX * cellSize) + (cellSize / 2.0f),
                y = start.y,
                z = start.z + (m_PosZ * cellSize) + (cellSize / 2.0f),
            };

            return result;
        }

        public int ToNativeIndexUnsafe(uint cellCountX)
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
        Renderer renderer = gameObject.GetComponent<Renderer>();
        Material material = renderer.material;

        Color transparentColor = new(color.r, color.g, color.b, m_GhostObjectAlpha);
        material.color = transparentColor;
    }


    // =============================================================
    // [Section] : ...
    // =============================================================


    public void SetGhostObject(GameObject objectToPlace)
    {
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
            // x) Should query components in children as well and loop over it.
            //

            if(ghostInstance.TryGetComponent<Renderer>(out var renderer))
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                //
                // TODO:
                // x) Explain all of this.
                //

                Material material = renderer.material;
                material.SetFloat("_Surface", 1);
                material.SetFloat("_Blend", 0);
                material.SetFloat("_ZWrite", 0);
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = 3000;
                material.color       = new Color(material.color.r, material.color.g, material.color.b, m_GhostObjectAlpha);

                m_GhostObject   = ghostInstance;
                m_ObjectToPlace = objectToPlace;
            }
            else
            {
                Debug.Log("No Renderer Attached To Game Object");
            }
        }
    }

    public void ClearGhostObject()
    {
        m_GhostObject   = null;
        m_ObjectToPlace = null;
    }


    private void Start()
    {
        m_PointerPositionAction = m_InputMap.FindAction("PointerOnScreen");
        m_PointerMainAction     = m_InputMap.FindAction("PointerMain");

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
                Vector3 position = new (gridStartX + (X * m_CellSize), 0.0f, gridStartZ + (Z * m_CellSize));
                Instantiate(m_GridCellObject, position, Quaternion.identity, this.transform);
            }
        }

        //
        // NOTE:
        // Experimental
        //

        m_OccupancySet = new BitArray((int)(m_CellCountX * m_CellCountZ));
    }

    private void Update()
    {
        //
        // TODO:
        // x) Clean this up
        //

        if (m_GhostObject != null)
        {
            Vector2 pointerScreenPosition = m_PointerPositionAction.ReadValue<Vector2>();
            Ray     pointerRay            = Camera.main.ScreenPointToRay(pointerScreenPosition);
            Vector3 pointerWorldPosition  = PointerToWorldPosition(pointerRay, m_GridOrigin, Vector3.up);

            float     minX      = ((int)m_GridOrigin.x - (m_CellCountX * m_CellSize / 2)) - (m_CellSize / 2.0f);
            float     minZ      = ((int)m_GridOrigin.z - (m_CellCountZ * m_CellSize / 2)) - (m_CellSize / 2.0f);
            CellIndex cellIndex = new(pointerWorldPosition, minX, minZ, m_CellSize);

            bool isInGrid = cellIndex.IsInsideGrid(m_CellCountX, m_CellCountZ);
            if(isInGrid)
            {
                Vector3 ghostPos    = cellIndex.ToWorldPosition(new(minX, m_GridOrigin.y, minZ), m_CellSize);
                int     nativeIndex = cellIndex.ToNativeIndexUnsafe(m_CellCountX);
                bool    isOccupied  = m_OccupancySet[nativeIndex];
                bool    isPlacing   = m_PointerMainAction.WasPressedThisFrame();

                if (isPlacing && !isOccupied)
                {
                    Instantiate(m_ObjectToPlace, ghostPos, Quaternion.identity);
                    m_OccupancySet[nativeIndex] = true;
                }
                else
                {
                    if(isOccupied)
                    {
                        SetGhostColor(m_InvalidGhostColor, m_GhostObject);
                    }
                    else
                    {
                        SetGhostColor(m_ValidGhostColor, m_GhostObject);
                    }

                    m_GhostObject.transform.position = ghostPos;
                }
            }
            else
            {
                SetGhostColor(m_InvalidGhostColor, m_GhostObject);

                m_GhostObject.transform.position = pointerWorldPosition;
            }
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
}