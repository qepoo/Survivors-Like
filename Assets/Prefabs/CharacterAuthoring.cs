using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.Rendering.VirtualTexturing;
using UnityEngine.U2D.Animation;

public struct InitializeCharacterFlag : IComponentData, IEnableableComponent { }

public struct CharacterMoveDirection : IComponentData {
    public float2 Value;
}

public struct CharacterMoveSpeed : IComponentData {
    public float Value;
}

public struct MovementState : IComponentData {
    public short Value;
}

public struct AimPosition : IComponentData {
    public Vector2 Value;

    public short direction; 
}

public struct AnimationFrame : IComponentData {
    public short Value;
}

public struct AnimationUpdateRate : IComponentData {
    public float Value; 
}

public struct AnimationTimeCounter : IComponentData {
    public float Value;
}


public class CharacterAuthoring : MonoBehaviour
{
    public float MoveSpeed;
    public float FrameUpdateRate;
    public SpriteResolver spriteResolver;
    public SpriteLibrary spriteLibrary;


    private class Baker : Baker<CharacterAuthoring>
    {
        public override void Bake(CharacterAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic); //registrates new entity

            AddComponent<InitializeCharacterFlag>(entity); //adds a component to an entity
            AddComponent<CharacterMoveDirection>(entity);
            AddComponent<AimPosition>(entity);

            AddComponent(entity, new CharacterMoveSpeed { 
                Value = authoring.MoveSpeed 
            });

            AddComponent(entity, new MovementState {
                Value = 0
            });

            AddComponent(entity, new AnimationFrame {
                Value = 0
            });

            AddComponent(entity, new AnimationUpdateRate {
                Value = authoring.FrameUpdateRate
            });

            AddComponent(entity, new AnimationTimeCounter {
                Value = 0
            });

            AddComponentObject(entity, authoring.spriteResolver);
            AddComponentObject(entity, authoring.spriteLibrary);
        }
    }
}


[UpdateInGroup(typeof(InitializationSystemGroup))] //system is being executed at the system initialization period
public partial struct InitializeCharacter : ISystem //system turns off the property rsponsible for falling behind the texture
{
    public void OnUpdate(ref SystemState state)
    { 
        foreach(var (mass, flagEnabled) in SystemAPI.Query<RefRW<PhysicsMass>, EnabledRefRW<InitializeCharacterFlag>>())
        {
            mass.ValueRW.InverseInertia = float3.zero;
            flagEnabled.ValueRW = false;
        }
    }
}


[BurstCompile]
public partial struct CharacterMovementSystem : ISystem
{ 
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (velocity, moveDirection, speed, moveState) in SystemAPI.Query<RefRW<PhysicsVelocity>, CharacterMoveDirection, CharacterMoveSpeed, RefRW<MovementState>>())
        {
            var movement2D = speed.Value * moveDirection.Value;
            velocity.ValueRW.Linear = new float3(movement2D, 0f);

            float movementMagnitude = math.sqrt(math.square(movement2D.x) + math.square(movement2D.y));
            moveState.ValueRW.Value = (short)(math.abs(movementMagnitude) > (0.15f) ? 1 : 0);
        }
    }
}


[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial struct AnimationUpdateSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (animFrame, timeCntr, moveState, frameUpdateRate, aimDirection, moveDirection, entity) in SystemAPI.Query<RefRW<AnimationFrame>, RefRW<AnimationTimeCounter>, MovementState, AnimationUpdateRate, AimPosition, CharacterMoveDirection>().WithEntityAccess())
        {
            var resolver = SystemAPI.ManagedAPI.GetComponent<SpriteResolver>(entity);

            ref var time = ref timeCntr.ValueRW.Value;
            ref var frame = ref animFrame.ValueRW.Value;

            time += SystemAPI.Time.DeltaTime;
            float animThreshold = 1f / frameUpdateRate.Value; //invert frame update rate to time per frame

            if (time > animThreshold)
            {
                frame = moveState.Value == 0 || moveDirection.Value.x == 0 ? (short)((frame + 1) % 6) : 
                (short)(Mathf.Round(moveDirection.Value.x) == aimDirection.direction ? ((frame + 1) % 6) : ((frame + 5) % 6));

                time -= animThreshold;
            }

            string category = moveState.Value == 0 ? "Idle" : "Run";

            resolver.SetCategoryAndLabel($"{category}", $"{category}_{frame}");
        }
    }
}