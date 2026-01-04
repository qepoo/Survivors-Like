using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Unity.VisualScripting;
using UnityEngine;
using Unity.Physics.Systems;

public struct EnemyTag : IComponentData { }

public struct AttackData : IComponentData 
{
    public float Damage;
    public float Cooldown;
}

public struct EnemyCooldownTimeStamp : IComponentData, IEnableableComponent
{
    public double Value;
}

class EnemyAuthoring : MonoBehaviour
{
    public float damageValue;
    public float cooldownTime;

    class EnemyAuthoringBaker : Baker<EnemyAuthoring>
    {
        public override void Bake(EnemyAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<EnemyTag>(entity);

            AddComponent(entity, new AttackData{
                Damage = authoring.damageValue,
                Cooldown = authoring.cooldownTime
            });

            AddComponent<EnemyCooldownTimeStamp>(entity);
            SetComponentEnabled<EnemyCooldownTimeStamp>(entity, false);
        }
    }
}

[BurstCompile]
public partial struct EnemyMoveSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerTag>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var plrEnt = SystemAPI.GetSingletonEntity<PlayerTag>();
        var plrPos = SystemAPI.GetComponent<LocalTransform>(plrEnt).Position.xy;

        var moveToPlayerFollowJob = new EnemyMoveJob
        {
            playerPos = plrPos
        };

        state.Dependency = moveToPlayerFollowJob.ScheduleParallel(state.Dependency);
    }
}

[BurstCompile]
[WithAll(typeof(EnemyTag))]
public partial struct EnemyMoveJob : IJobEntity
{
    public float2 playerPos;

    private void Execute(ref CharacterMoveDirection direction, in LocalTransform enemyPos)
    {
        float2 directionVector = playerPos - enemyPos.Position.xy;
        direction.Value = math.normalizesafe(directionVector);
    }
}


[UpdateAfter(typeof(TransformSystemGroup))]
public partial struct EnemyLookDirectionSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var plrEnt = SystemAPI.GetSingletonEntity<PlayerTag>();
        var playerPos = SystemAPI.GetComponent<LocalTransform>(plrEnt).Position.xy;

        foreach (var (enemyPos, entity) in SystemAPI.Query<LocalToWorld>().WithAll<EnemyTag>().WithEntityAccess())
        {
            var sprtRenderer = SystemAPI.ManagedAPI.GetComponent<SpriteRenderer>(entity);
            sprtRenderer.flipX = (enemyPos.Position.x < playerPos.x);
        }
    }
}


[UpdateInGroup(typeof(PhysicsSystemGroup))]
[UpdateAfter(typeof(PhysicsSimulationGroup))]
[UpdateBefore(typeof(AfterPhysicsSystemGroup))]
public partial struct EnemyAttackSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SimulationSingleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var elapsedTime = SystemAPI.Time.ElapsedTime;

        foreach (var (elapsedColldownTimeStamp, cooldownEnabled) in SystemAPI.Query<EnemyCooldownTimeStamp, EnabledRefRW<EnemyCooldownTimeStamp>>())
        {
            if (elapsedColldownTimeStamp.Value > elapsedTime) continue;
            cooldownEnabled.ValueRW = false;
        }

        var enemyAttackJob = new EnemyAttackJob
        {
            PlayerLookup = SystemAPI.GetComponentLookup<PlayerTag>(true),
            AttackDataLookup = SystemAPI.GetComponentLookup<AttackData>(true),
            CooldownLookup = SystemAPI.GetComponentLookup<EnemyCooldownTimeStamp>(),
            DamageBufferLookup = SystemAPI.GetBufferLookup<ThisFrameDamageBuffer>(),
            ElapsedTime = elapsedTime
        };

        var simulationSingleton = SystemAPI.GetSingleton<SimulationSingleton>();
        state.Dependency = enemyAttackJob.Schedule(simulationSingleton, state.Dependency);
    }
}

[BurstCompile]
public partial struct EnemyAttackJob : ICollisionEventsJob
{
    [ReadOnly] public ComponentLookup<PlayerTag> PlayerLookup;
    [ReadOnly] public ComponentLookup<AttackData> AttackDataLookup;
    public ComponentLookup<EnemyCooldownTimeStamp> CooldownLookup;
    public BufferLookup<ThisFrameDamageBuffer> DamageBufferLookup;

    public double ElapsedTime;

    public void Execute(CollisionEvent collisionEvent)
    {
        Entity playerEnt;
        Entity enemyEnt;

        if (PlayerLookup.HasComponent(collisionEvent.EntityA) && AttackDataLookup.HasComponent(collisionEvent.EntityB))
        {
            playerEnt = collisionEvent.EntityA;
            enemyEnt = collisionEvent.EntityB;
        }
        else if (PlayerLookup.HasComponent(collisionEvent.EntityB) && AttackDataLookup.HasComponent(collisionEvent.EntityA))
        {
            playerEnt = collisionEvent.EntityB;
            enemyEnt = collisionEvent.EntityA;
        }
        else
        {
            return;
        }

        if (CooldownLookup.IsComponentEnabled(enemyEnt)) return;

        var attackData = AttackDataLookup[enemyEnt];
        CooldownLookup[enemyEnt] = new EnemyCooldownTimeStamp {Value = ElapsedTime + attackData.Cooldown};
        CooldownLookup.SetComponentEnabled(enemyEnt, true);

        var plrDamageBuffer = DamageBufferLookup[playerEnt];
        plrDamageBuffer.Add(new ThisFrameDamageBuffer
        {
            Value = attackData.Damage
        });
    }
}