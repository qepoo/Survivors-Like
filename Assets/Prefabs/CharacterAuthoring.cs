using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Entities;
using Unity.Physics;
using Unity.Burst;
using Unity.Rendering;
using System;

public struct InitializeCharacterFlag : IComponentData, IEnableableComponent { }

public struct CharacterMoveDirection : IComponentData {
    public float2 Value;
}

public struct CharacterMoveSpeed : IComponentData {
    public float Value;
}

[MaterialProperty("_FacingDirection")]
public struct FacingDirectionOverride : IComponentData {
    public float Value;
}

[MaterialProperty("_AnimationIndex")]
    public struct AnimationIndexOverride : IComponentData {
    public float Value;
}

public class CharacterAuthoring : MonoBehaviour
{
    public float MoveSpeed;

    private class Baker : Baker<CharacterAuthoring>
    {
        public override void Bake(CharacterAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic); //registrates new entity
            AddComponent<InitializeCharacterFlag>(entity); //adds a component to an entity
            AddComponent<CharacterMoveDirection>(entity);

            AddComponent(entity, new CharacterMoveSpeed { 
                Value = authoring.MoveSpeed 
            });

            AddComponent(entity, new FacingDirectionOverride
            {
                Value = 1
            });
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

public partial struct CharacterMovementSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (velocity, facingDirection, moveDirection, speed) in SystemAPI.Query<RefRW<PhysicsVelocity>, RefRW<FacingDirectionOverride>, CharacterMoveDirection, CharacterMoveSpeed>())
        {
            var movement2D = speed.Value * moveDirection.Value;
            velocity.ValueRW.Linear = new float3(movement2D, 0f);

            if (math.abs(movement2D.x) > 0.15f)
            {
                facingDirection.ValueRW.Value = math.sign(movement2D.x); //if there is any velocity, facing direction is being updated due to the actual moveDirection
            }
        }
    }
}

public partial struct GlobalTimeUpdateSystem : ISystem
{
    private static int globalTimeShaderPropertyID;

    public void OnCreate(ref SystemState state)
    {
        globalTimeShaderPropertyID = Shader.PropertyToID("_GlobalTime"); //sets a shader property ID as a value to an int variable
    }

    public void OnUpdate(ref SystemState state)
    {
        Shader.SetGlobalFloat(globalTimeShaderPropertyID, (float)SystemAPI.Time.ElapsedTime); //timer responsible for animation update that ticks from the start of the world
    }
}