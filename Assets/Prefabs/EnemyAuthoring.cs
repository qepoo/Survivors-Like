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
public partial struct PlayerFollowSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerTag>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var plrEnt = SystemAPI.GetSingletonEntity<PlayerTag>();
        var plrPos = SystemAPI.GetComponent<LocalTransform>(plrEnt).Position.xy;

        var moveToPlayerFollowJob = new PlayerFollowJob
        {
            playerPos = plrPos
        };

        state.Dependency = moveToPlayerFollowJob.ScheduleParallel(state.Dependency);
    }
}


[BurstCompile]
[UpdateAfter(typeof(TransformSystemGroup))]
public partial struct LookDirectionSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        bool playerFound = false;
        float3 playerPos = float3.zero;

        if (!playerFound)
        {
            foreach (var plrPos in SystemAPI.Query<LocalToWorld>().WithAll<PlayerTag>())
            {
                playerFound = true;
                playerPos = plrPos.Position;
            }
        }

        foreach (var (enemyPos, entity) in SystemAPI.Query<LocalToWorld>().WithAll<EnemyTag>().WithEntityAccess())
        {
            var sprtRenderer = SystemAPI.ManagedAPI.GetComponent<SpriteRenderer>(entity);
            sprtRenderer.flipX = (enemyPos.Position.x < playerPos.x);
        }
    }
}

[BurstCompile]
[WithAll(typeof(EnemyTag))]
public partial struct PlayerFollowJob : IJobEntity
{
    public float2 playerPos;

    private void Execute(ref CharacterMoveDirection direction, in LocalTransform enemyPos)
    {
        float2 directionVector = playerPos - enemyPos.Position.xy;
        direction.Value = math.normalizesafe(directionVector);
    }
}