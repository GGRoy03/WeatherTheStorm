using UnityEngine;

namespace WeatherTheStorm.Tower
{
    [CreateAssetMenu(fileName = "TowerAuthoring", menuName = "Scriptable Objects/TowerAuthoring")]
    public class TowerAuthoring : ScriptableObject
    {
        [Header("...")]
        public GameObject Prefab;
    }
}
