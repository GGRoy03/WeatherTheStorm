using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

public enum TowerTargetMode
{
    Closest  = 0,
    Farthest = 1,
}

public struct TowerTargetClosestConfig : IComponentData
{
    public float SqrRadius;
}

public partial struct TowerTargetSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (config, transform, towerEntity) in
            SystemAPI.Query<RefRO<TowerTargetClosestConfig>, RefRO<LocalTransform>>().WithEntityAccess())
        {
            //
            // TODO:
            // x) Profile queries once we have enough informations.
            // x) Add spatial partioning once we need it.
            // x) Is burst activated?
            // x) How do I thread this?
            // x) Turret rotation logic, is hierarchy accessible?
            // x) What is baking?
            //

            Entity closestEnemy       = Entity.Null;
            float  closestSqrDistance = float.MaxValue;
            foreach(var (enemyTransform, enemyEntity) in
                SystemAPI.Query<RefRO<LocalTransform>>().WithAll<EnemyTag>().WithEntityAccess())
            {
                float3 towerPosition   = transform.ValueRO.Position;
                float3 enemyPosition   = enemyTransform.ValueRO.Position;
                float  distanceToEnemy = math.distancesq(towerPosition, enemyPosition);
                float  sqrRadius       = config.ValueRO.SqrRadius;

                if (distanceToEnemy < closestSqrDistance)
                {
                    closestSqrDistance = distanceToEnemy;
                    closestEnemy       = enemyEntity;
                }
            }

            //
            // Then the question is: What do we do -> Tehcnically, we have both the
            // enemy/tower entity. We have to feed them somewhere... I don't really know.
            //
        }
    }
}

