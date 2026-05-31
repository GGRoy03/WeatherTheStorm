using Unity.Entities;
using UnityEngine;

namespace WeatherTheStorm.Tower
{
    public class TowerAuthoring : MonoBehaviour
    {
        public GameObject DoubleTurret;
        public GameObject Rocket;

        class Baker : Baker<TowerAuthoring>
        {
            public override void Bake(TowerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new Config
                {
                    DoubleTurret = GetEntity(authoring.DoubleTurret, TransformUsageFlags.Dynamic),
                    Rocket       = GetEntity(authoring.Rocket      , TransformUsageFlags.Dynamic),
                });
            }
        }
    }

    public struct Config : IComponentData
    {
        public Entity DoubleTurret;
        public Entity Rocket;
    }
}