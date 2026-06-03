using Unity.Physics;
using UnityEngine;

namespace WeatherTheStorm.Helpers
{
    public class ECSPhysicsHelpers : MonoBehaviour
    {
        //
        // NOTE: This is a direct mapping between the unity "Physics Layers" and their
        //       mask values. This makes it simple to just convert from the "real world" to the
        //       ECS world using Unity's automatic bakers.
        //

        public readonly static uint AllLayer        = ~0u;
        public readonly static uint EnemyLayer      = 1u << 3;
        public readonly static uint ProjectileLayer = 1u << 6;

        //
        // NOTE: How should we name these filters? It's not quite clear to the user code what this
        //       does.
        //

        public readonly static CollisionFilter ProjectileFilter = new CollisionFilter()
        {
            BelongsTo    = EnemyLayer | ProjectileLayer,
            CollidesWith = EnemyLayer | ProjectileLayer,
        };

        public readonly static CollisionFilter CheckAgainstEnemyFilter = new CollisionFilter()
        {
            BelongsTo    = AllLayer,
            CollidesWith = EnemyLayer,
        };
    }
}