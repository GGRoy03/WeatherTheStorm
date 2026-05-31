using Unity.Entities;
using UnityEngine;
using WeatherTheStorm.Enemy;
using WeatherTheStorm.Tower;

namespace WeatherTheStorm.Tower
{
    public class TurretDoubleAuthoring : MonoBehaviour
    {
        public class Baker : Baker<TurretDoubleAuthoring>
        {
            public override void Bake(TurretDoubleAuthoring authoring)
            {
                //
                // TODO:
                // x) Is dynamic needed if I only ever need to rotate the turret?
                //

                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new TargetClosest
                {
                    SqrRadius = 16.0f,
                });

                AddComponent(entity, new AttackCooldown
                {
                    Duration = 1.5f,
                    Elapsed  = 0.0f,
                });

                AddComponent(entity, new Projectile
                {
                    Type = ProjectileType.Rocket,
                });
            }
        }
    }
}