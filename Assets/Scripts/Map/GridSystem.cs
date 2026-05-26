using UnityEngine;
using UnityEngine.InputSystem;

public class GridSystem : MonoBehaviour
{
    [Header("Generation")]
    [SerializeField] private uint    m_CellCountX;
    [SerializeField] private uint    m_CellCountZ;
    [SerializeField] private uint    m_CellSize;
    [SerializeField] private Vector3 m_GridOrigin;

    [Header("Visuals")]
    [SerializeField] private GameObject m_GridCellObject;

    [Header("Inputs")]
    [SerializeField] private InputActionAsset m_InputMap;
                     private InputAction      m_PointerPositionAction;


    //
    // Temp
    //

    private GameObject[] m_GridCells = new GameObject[1000];


    private void Start()
    {
        m_PointerPositionAction = m_InputMap.FindAction("PointerOnScreen");

        //
        // TODO: Both grid-starts are incorrect in some cases, doesn't matter for now.
        //

        float gridStartX = ((int)m_GridOrigin.x - (m_CellCountX * m_CellSize / 2));
        float gridStartZ = ((int)m_GridOrigin.z - (m_CellCountZ * m_CellSize / 2));

        for(uint Z = 0; Z < m_CellCountZ; Z++)
        {
            for (uint X = 0; X < m_CellCountX; X++)
            {
                Vector3 position = new (gridStartX + (X * m_CellSize), 0.0f, gridStartZ + (Z * m_CellSize));
                m_GridCells[Z * m_CellCountX + X] = Instantiate(m_GridCellObject, position, Quaternion.identity, this.transform);
            }
        }
    }

    private void Update()
    {
        Vector2 pointerScreenPosition = m_PointerPositionAction.ReadValue<Vector2>();
        Ray     pointerRay            = Camera.main.ScreenPointToRay(pointerScreenPosition);
        Vector3 pointerWorldPosition  = PointerToWorldPosition(pointerRay, new(0.0f, 0.0f, 0.0f), new(0.0f, 1.0f, 0.0f));

        GridBounds bounds          = new(m_GridOrigin, m_CellCountX, m_CellCountZ, m_CellSize);
        bool       isPointerInGrid = bounds.IsInside(pointerWorldPosition);

        if (isPointerInGrid)
        {
            //
            // TODO:
            // x) Import a cell model instead of full cubes.
            // x) Mess around with the rendering properties to make it semi-transparent
            // x) Basic occupancy logic, note that this grid is only for placing objects into the world and nothing else.
            // x) Placing Queries
            // x) Toggling on/off
            // x) Explain some of the math heavy parts of this code.
            //

            CellIndex cellIndex = new(pointerWorldPosition, bounds, m_CellCountX, m_CellCountZ, m_CellSize);
            uint      gridIndex = cellIndex.ToGridIndex(m_CellCountX);

            GameObject cellObject = m_GridCells[gridIndex];
            Renderer   renderer   = cellObject.GetComponent<Renderer>();
            Material   material   = renderer.material;

            material.color = Color.green;
        }

        Debug.Log(isPointerInGrid);
    }

    private Vector3 PointerToWorldPosition(Ray pointerRay, Vector3 planeOrigin, Vector3 planeNormal)
    {
        // 
        // Ray       :  P = O+tD, where O is the origin, t is the distance and D is the direction.
        // Plane     : (P-S)*N = 0, where S is a known point on the plane, N is the plane's normal and P is any point on the plane.
        // Substitute: 1) (O+tD-S)*N = 0
        //             2) O*N + tD*N - S*N = 0
        //             3) tD*N = S*N-O*N
        //             4) t = (S-O)*N/D*N
        //

        Vector3 result = new(float.MaxValue, float.MaxValue, float.MaxValue);

        float denom = Vector3.Dot(pointerRay.direction, planeNormal);
        if (denom != 0.0f)
        {
            Vector3 originToPlane = planeOrigin - pointerRay.origin;
            float   numer         = Vector3.Dot(originToPlane, planeNormal);
            float   distance      = numer / denom;

            if (distance >= 0.0f)
            {
                result = pointerRay.origin + distance * pointerRay.direction;
            }
        }

        return result;
    }


    struct GridBounds
    {
        public float m_MinX;
        public float m_MaxX;
        public float m_MinZ;
        public float m_MaxZ;

        public GridBounds(Vector3 center, uint cellCountX, uint cellCountZ, uint cellSize)
        {
            m_MinX = ((int)center.x - (cellCountX * cellSize / 2)) - 0.5f;
            m_MaxX = ((int)center.x + (cellCountX * cellSize / 2)) - 0.5f;
            m_MinZ = ((int)center.z - (cellCountZ * cellSize / 2)) - 0.5f;
            m_MaxZ = ((int)center.z + (cellCountZ * cellSize / 2)) - 0.5f;
        }

        public readonly bool IsInside(Vector3 worldPosition)
        {
            bool result = worldPosition.x >= m_MinX && worldPosition.x <= m_MaxX &&
                          worldPosition.z >= m_MinZ && worldPosition.z <= m_MaxZ;
            return result;
        }
    }


    private struct CellIndex
    {
        public uint m_PosX;
        public uint m_PosZ;

        public CellIndex(Vector3 worldPosition, GridBounds bounds, uint cellCountX, uint cellCountZ, uint cellSize)
        {
            Debug.Assert(bounds.IsInside(worldPosition));

            m_PosX = (uint)Mathf.Floor((worldPosition.x - bounds.m_MinX) / cellSize);
            m_PosZ = (uint)Mathf.Floor((worldPosition.z - bounds.m_MinZ) / cellSize);
        }

        //
        // I don't know about that one.
        //

        public readonly uint ToGridIndex(uint cellCountX)
        {
            uint result = m_PosZ * cellCountX + m_PosX;
            return result;
        }
    };
}
