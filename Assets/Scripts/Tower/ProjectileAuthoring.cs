using Unity.Entities;
using UnityEngine;


public class ProjectileAuthoring : MonoBehaviour
{
    public class Baker : Baker<ProjectileAuthoring>
    {
        public override void Bake(ProjectileAuthoring authoring)
        {
            Debug.Log("Baking Projectile");

            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<Projectile>(entity);
        }
    }

    public struct Projectile : IComponentData { };
}
