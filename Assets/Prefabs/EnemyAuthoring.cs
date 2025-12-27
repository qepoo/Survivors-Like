using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.VisualScripting;
using UnityEngine;

public struct EnemyTag : IComponentData { }

class EnemyAuthoring : MonoBehaviour
{
    class EnemyAuthoringBaker : Baker<EnemyAuthoring>
    {
        public override void Bake(EnemyAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<EnemyTag>(entity);
        }
    }
}

[BurstCompile]
[UpdateAfter(typeof(TransformSystemGroup))]
public partial struct PlayerFollowSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        foreach (var playerPos in SystemAPI.Query<LocalToWorld>().WithAll<PlayerTag>())
        {
            foreach (var (enemyPos, enemyDirection) in SystemAPI.Query<LocalToWorld, RefRW<CharacterMoveDirection>>().WithAll<EnemyTag>()) //dont nest queries, store player pos once and re-use it
            {
                float x = playerPos.Position.x - enemyPos.Position.x; //float2-float2 (update)
                float y = playerPos.Position.y - enemyPos.Position.y;

                enemyDirection.ValueRW.Value = math.normalizesafe(new float2 (x, y));
            }
        }
    }
}