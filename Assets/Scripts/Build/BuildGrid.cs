using System;
using System.Collections;

using Unity.Mathematics;

using UnityEngine;
using UnityEngine.InputSystem;

namespace WeatherTheStorm.Build
{
    public class BuildGrid : MonoBehaviour
    {
        [Header("Visuals")]
                                            private GameObject m_GhostObject;
                                            private GameObject m_ObjectToPlace;

                                            private bool       m_ShowCells;

        [Header("Inputs")]
        [SerializeField] private InputActionAsset m_InputMap;
        [SerializeField] private bool             m_ClearSelectionOnPlace;
                         private InputAction      m_PointerPositionAction;
                         private InputAction      m_PointerMainAction;
                         private InputAction      m_RotateShapeCWAction;
                         private InputAction      m_RotateShapeCCWAction;

    #if UNITY_EDITOR
        [Header("Debug")]
        [SerializeField] private bool m_DebugCellValidity;
        [SerializeField] private bool m_DebugGridCells;    // Not really, but whatever.
#endif

        //
        // ...
        //

        [SerializeField] private int     m_CellCountX;
        [SerializeField] private int     m_CellCountZ;
        [SerializeField] private int     m_CellSize;
        [SerializeField] private Vector3 m_GridOrigin;

        private OccupancyGrid2D           m_Grid;
        private OccupancyGrid2D.CellShape m_GhostShape;

        //
        //
        //

        [Header("Ghost")]
        [SerializeField, Range(0.0f, 1.0f)] private float m_GhostObjectAlpha;
        [SerializeField] private Color m_ValidGhostColor;
        [SerializeField] private Color m_InvalidGhostColor;

        [Header("Grid Cells")]
        [SerializeField] private GameObject m_GridCellPrefab;
        [SerializeField] private Transform  m_CellAppendTransform;

        private void Start()
        {
            m_PointerPositionAction = m_InputMap.FindAction("PointerOnScreen");
            m_PointerMainAction     = m_InputMap.FindAction("PointerMain");
            m_RotateShapeCWAction   = m_InputMap.FindAction("RotateShapeCW");
            m_RotateShapeCCWAction  = m_InputMap.FindAction("RotateShapeCCW");

             m_Grid = new OccupancyGrid2D(
                cellCountX: m_CellCountX,
                cellCountZ: m_CellCountZ,
                cellSize:   m_CellSize,
                gridOrigin: m_GridOrigin
                );

            for(int cellX = 0; cellX < m_CellCountX; ++cellX)
            {
                for(int cellZ = 0; cellZ < m_CellCountZ; ++cellZ)
                {
                    var cellIndex = new OccupancyGrid2D.CellIndex(x: cellX, z: cellZ);
                    var position  = m_Grid.CellIndexToPosition(cellIndex);

                    Instantiate(m_GridCellPrefab, position, Quaternion.identity, m_CellAppendTransform);
                }
            }
        }

        //
        // TODO:
        // x) Continue cleanups
        // x) Tool for shapes
        // x) Tool for gen
        // x) Mess around with assets for a while
        // x) Data Driven.
        //


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

            m_GhostShape = new OccupancyGrid2D.CellShape(shapePoints);

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




        private void Update()
        {
            m_CellAppendTransform.gameObject.SetActive(m_DebugGridCells);

            if (m_GhostObject != null)
            {
                Vector2 pointerScreenPosition = m_PointerPositionAction.ReadValue<Vector2>();
                Ray     pointerRay            = Camera.main.ScreenPointToRay(pointerScreenPosition);
                Vector3 pointerWorldPosition  = PointerToWorldPosition(pointerRay, m_GridOrigin, Vector3.up);

                var  cellIndex = m_Grid.PositionToCellIndex(pointerWorldPosition);
                bool isBlocked = m_Grid.IsShapeBlocked(m_GhostShape, cellIndex);

                if(!isBlocked)
                {
                    Vector3 ghostPos  = m_Grid.CellIndexToPosition(cellIndex);
                    bool    isPlacing = m_PointerMainAction.WasPressedThisFrame();

                    if (isPlacing)
                    {
                        Instantiate(m_ObjectToPlace, ghostPos, Quaternion.identity);

                        m_Grid.PlaceShape(m_GhostShape, cellIndex);

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


    #if UNITY_EDITOR

        private void OnDrawGizmosSelected()
        {
            //Gizmos.color = Color.cyan;

            //var points = m_GhostShape.Points();
            //for (int pointIdx = 0; pointIdx < points.Length; ++pointIdx)
            //{
            //    Vector3 position = new Vector3(x: points[pointIdx][0], y: 0.0f, z: points[pointIdx][1]);

            //    Gizmos.DrawWireCube(position, Vector3.one);
            //}

            if(m_DebugCellValidity)
            {

            }
        }

    #endif
    }

}