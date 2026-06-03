using Unity.Entities;

using UnityEngine;

namespace WeatherTheStorm.Tower
{
    public class TurretDoubleAuthoring : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private GameObject m_Projectile;

        [Header("Audio")]
        [SerializeField] private AudioClip  m_TurningSound;
        [SerializeField] private AudioClip  m_ProjectileFireSound;

        public class Baker : Baker<TurretDoubleAuthoring>
        {
            public override void Bake(TurretDoubleAuthoring authoring)
            {
                var towerEntity = GetEntity(TransformUsageFlags.Dynamic);
                {
                    AddComponent(towerEntity, new AttackCooldown
                    {
                        Duration = 1.5f,
                        Elapsed  = 0.0f,
                    });
                    AddComponent(towerEntity, new TargetingConfig
                    {
                        Mode     = TargetMode.Closest,
                        SqrRange = 16.0f,
                    });
                    AddComponent(towerEntity, new TowerProjectile
                    {
                        Prefab    = GetEntity(authoring.m_Projectile, TransformUsageFlags.Dynamic),
                        FireSound = authoring.m_ProjectileFireSound,
                    });
                    AddComponent(towerEntity, new TowerAudio
                    {
                        TurningSound = authoring.m_TurningSound,
                    });
                }
            }
        }
    }
}