using Unity.Mathematics;

using UnityEngine;

namespace WeatherTheStorm.Tower
{

    [CreateAssetMenu(fileName = "ProjectileAuthoring", menuName = "Scriptable Objects/ProjectileAuthoring")]
    public class ProjectileAuthoring : ScriptableObject
    {
                          public GameObject m_Prefab;
        [HideInInspector] public AABB       m_LocalBounds;

        private void OnValidate()
        {
            var     boxCollider = m_Prefab.GetComponent<BoxCollider>();
            Bounds  worldBounds = boxCollider.bounds;
            m_LocalBounds.Center  = boxCollider.transform.InverseTransformPoint(worldBounds.center);
            m_LocalBounds.Extents = boxCollider.transform.InverseTransformDirection(boxCollider.size);
        }
    }
}
