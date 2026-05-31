using Unity.Entities;
using UnityEngine;

namespace WeatherTheStorm.Tower
{
    public class RocketAuthoring : MonoBehaviour
    {
        public class Baker : Baker<RocketAuthoring>
        {
            public override void Bake(RocketAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new RocketTag { });
            }
        }
    }
}
