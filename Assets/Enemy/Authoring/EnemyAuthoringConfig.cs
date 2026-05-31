using Unity.Entities;
using UnityEngine;

namespace WeatherTheStorm.Enemy
{
    public class EnemyAuthoring : MonoBehaviour
    {
        public GameObject BasicEnemy;

        class Baker : Baker<EnemyAuthoring>
        {
            public override void Bake(EnemyAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new Config
                {
                    BasicEnemy = GetEntity(authoring.BasicEnemy, TransformUsageFlags.Dynamic)
                });
            }
        }

        public struct Config : IComponentData
        {
            public Entity BasicEnemy;
        }
    }

    public struct EnemyTag : IComponentData { };
}