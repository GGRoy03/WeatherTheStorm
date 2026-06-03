using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace WeatherTheStorm.Tower
{
    public class MissileAuthoring : MonoBehaviour
    {
        [Header("")]
        [SerializeField] private AudioClip m_LaunchSound;
        [SerializeField] private AudioClip m_HitSound;

        public class Baker : Baker<MissileAuthoring>
        {
            public override void Bake(MissileAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                {
                    AddComponent(entity, new HomingProjectile
                    {
                        Speed        = 5.0f,
                        LastKnownPos = float3.zero,
                    });
                    AddComponent(entity, new ProjectileMissile
                    {
                        Damage          = 5.0f,
                        ExplosionRadius = 1.0f,
                    });
                    AddComponent(entity, new ProjectileRequest
                    {

                    });
                    SetComponentEnabled<ProjectileRequest>(entity, false);
                }
            }
        }
    }
}
