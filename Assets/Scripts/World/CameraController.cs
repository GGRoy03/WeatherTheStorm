using UnityEngine;

using Unity.Cinemachine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CinemachineCamera m_VirtualCamera;
    [SerializeField] private Transform         m_CameraTarget;
                     private GameObject        m_VirtualCameraGameObject;

    [Header("Boundaries")]
    [SerializeField] private float m_BoundaryMinX;
    [SerializeField] private float m_BoundaryMaxX;
    [SerializeField] private float m_BoundaryMinZ;
    [SerializeField] private float m_BoundaryMaxZ;

    [Header("Inputs")]
    [SerializeField] private InputActionAsset m_InputActionAsset;
                     private InputAction      m_PointerPositionAction;
                     private InputAction      m_MoveCameraAction;
                     private InputAction      m_CameraRotateAction;

    //
    // NOTE:
    // I assume settings are stored as scriptable objects usually? We'll keep it simple for now
    // and just stuff them here.
    //

    [Header("Settings")]
    [SerializeField] private float m_ScreenSideZoneSize;
    [SerializeField] private float m_ScreenSideMoveSpeed;
    [SerializeField] private float m_CameraMoveSpeed;
    [SerializeField] private float m_CameraRotateSpeed;


    private float m_TargetCameraRotation;
    private float m_CurrentCameraRotation;
    private float m_CameraRotationVelocity;


    private void Start()
    {
        m_VirtualCameraGameObject = m_VirtualCamera.gameObject;

        InputActionMap inputMap = m_InputActionAsset.FindActionMap("Camera");
        m_PointerPositionAction = inputMap.FindAction("PointerPosition");
        m_MoveCameraAction      = inputMap.FindAction("Move");
        m_CameraRotateAction    = inputMap.FindAction("Rotate");
    }

    private void Update()
    {
        Vector2 mousePosition = m_PointerPositionAction.ReadValue<Vector2>();
        Vector2 moveInput     = m_MoveCameraAction.ReadValue<Vector2>().normalized;
        float   rotateInput   = m_CameraRotateAction.ReadValue<float>();

        HandleScreenSideMove(mousePosition);
        HandleMove(moveInput);
        HandleRotation(rotateInput);
    }

    private void HandleScreenSideMove(Vector2 mousePosition)
    {
        int yPos = 0;
        int xPos = 0;

        if(mousePosition.x >= 0.0f && mousePosition.x <= m_ScreenSideZoneSize)
        {
            xPos = -1;
        }
        else if(mousePosition.x <= Screen.width && mousePosition.x >= Screen.width - m_ScreenSideZoneSize)
        {
            xPos = 1;
        }

        if (mousePosition.y >= 0.0f && mousePosition.y <= m_ScreenSideZoneSize)
        {
            yPos = -1;
        }
        else if(mousePosition.y <= Screen.height && mousePosition.y >= Screen.height - m_ScreenSideZoneSize)
        {
            yPos = 1;
        }

        if(xPos != 0 || yPos != 0)
        {
            var moveVector = new Vector3(xPos, 0.0f, yPos);
            MoveTargetRelativeToCamera(moveVector, m_ScreenSideMoveSpeed);
        }
    }

    private void HandleMove(Vector2 moveInput)
    {
        if (moveInput.sqrMagnitude > 0.0f)
        {
            Vector3 moveVector = new(moveInput.x, 0.0f, moveInput.y);
            MoveTargetRelativeToCamera(moveVector, m_CameraMoveSpeed);
        }
    }

    //
    // NOTE:
    // I don't know if rotation is something we actually want to handle?
    //

    private void HandleRotation(float rotateInput)
    {
        if(rotateInput != 0.0f)
        {
            m_TargetCameraRotation += rotateInput * m_CameraRotateSpeed;
        }

        m_CurrentCameraRotation = Mathf.SmoothDamp(m_CurrentCameraRotation, m_TargetCameraRotation, ref m_CameraRotationVelocity, 1.0f, Mathf.Infinity, Time.deltaTime);

        m_VirtualCameraGameObject.transform.eulerAngles = new()
        {
            x = m_VirtualCameraGameObject.transform.eulerAngles.x,
            y = m_CurrentCameraRotation,
            z = 0.0f,
        };
    }

    private void MoveTargetRelativeToCamera(Vector3 direction, float speed)
    {
        Vector3 cameraForward = new()
        {
            x = m_VirtualCameraGameObject.transform.forward.x,
            y = 0.0f,
            z = m_VirtualCameraGameObject.transform.forward.z,
        };
        cameraForward = cameraForward.normalized;

        Vector3 cameraRight = new()
        {
            x = m_VirtualCameraGameObject.transform.right.x,
            y = 0.0f,
            z = m_VirtualCameraGameObject.transform.right.z,
        };
        cameraRight = cameraRight.normalized;

        Vector3 relativeDirection = (direction.z * cameraForward) + (direction.x * cameraRight);
        float   cameraSpeed       = Time.deltaTime * speed;

        m_CameraTarget.Translate(relativeDirection * cameraSpeed);
    }
}