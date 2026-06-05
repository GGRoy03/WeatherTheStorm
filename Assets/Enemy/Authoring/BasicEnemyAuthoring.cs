using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;


namespace WeatherTheStorm.Enemy
{
    public class BasicEnemyAuthoring : MonoBehaviour
    {
        public class Baker : Baker<BasicEnemyAuthoring>
        {
            public override void Bake(BasicEnemyAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new EnemyTag { });
                AddComponent(entity, new PathAgent
                {
                    Destination = float3.zero,
                    // Speed       = 1.0f,
                });
                AddComponent(entity, new PathPosition
                {
                    Position = float3.zero,
                });
                AddComponent(entity, new Status
                {
                    Health    = 5.0f,
                    MaxHealth = 5.0f,
                });
            }
        }
    }
}