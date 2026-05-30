using Unity.Entities;
using UnityEngine;


public class ProjectileAuthoring : MonoBehaviour
{
    public class Baker : Baker<ProjectileAuthoring>
    {
        public override void Bake(ProjectileAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<Projectile>(entity);
            AddComponent<EnemyTag>(entity);
        }
    }

    public struct Projectile : IComponentData { };
}
