using UnityEngine;

public class BuildHUD : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private GridSystem m_Grid;

    public void OnBuildObject(GameObject objectToBuild)
    {
        m_Grid.SetGhostObject(objectToBuild);
    }
}
