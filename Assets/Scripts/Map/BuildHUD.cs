using UnityEngine;

public class BuildHUD : MonoBehaviour
{
    [SerializeField] private GridSystem m_Grid;
    [SerializeField] private GameObject m_ObjectToPlace;

    void Start()
    {
        m_Grid.SetGhostObject(m_ObjectToPlace);
    }
}
