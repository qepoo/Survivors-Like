using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Unity.VisualScripting;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;
using UnityEngine.U2D.Animation;
using static UnityEngine.EventSystems.EventTrigger;

public struct PlayerTag : IComponentData { }

public struct InitializeCameraTargetTag : IComponentData { }

public struct CameraTarget : IComponentData {
    public UnityObjectRef<Transform> CameraTransform;
}

public class PlayerAuthoring : MonoBehaviour
{
    private class Baker : Baker<PlayerAuthoring>
    {
        public override void Bake(PlayerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<PlayerTag>(entity);
            
            AddComponent<InitializeCameraTargetTag>(entity);
            AddComponent<CameraTarget>(entity);
        }
    }   
}


[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct CameraInitializationSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<InitializeCameraTargetTag>(); //requiered components for a system to run
    }

    public void OnUpdate(ref SystemState state)
    {
        if (CameraTargetSingleton.Instance == null) return; //if cameraTarget is missing, no further operations will be performed
        var CameraTargetTransform = CameraTargetSingleton.Instance.transform;

        var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);
        foreach (var (cameraTarget, entity) in SystemAPI.Query<RefRW<CameraTarget>>().WithAll<InitializeCameraTargetTag, PlayerTag>().WithEntityAccess()) //Query retrieves an entity with cameraTarget transform, and an entity that contains both components above
        { 
            cameraTarget.ValueRW.CameraTransform = CameraTargetTransform;
            ecb.RemoveComponent<InitializeCameraTargetTag>(entity); //doesnt remove a comp, yet sends a request into ecb to do so
        }

        ecb.Playback(state.EntityManager); //performs operations saved in ecb
    }
}


[UpdateAfter(typeof(TransformSystemGroup    ))] //system is being executed AFTER all transforms update
public partial struct MoveCameraTarget : ISystem 
{ 
    public void OnUpdate(ref SystemState state) 
    {
        foreach (var (transform, cameraPredictionOffset, cameraTarget) in SystemAPI.Query<LocalToWorld, CharacterMoveDirection, 
            CameraTarget>().WithAll<PlayerTag>().WithNone<InitializeCameraTargetTag>())
        {
            Vector3 updPosition = new(transform.Position.x + cameraPredictionOffset.Value.x/1.5f, transform.Position.y + cameraPredictionOffset.Value.y/1.5f, transform.Position.z);
            cameraTarget.CameraTransform.Value.position = updPosition;
        }
    }
}


[UpdateAfter(typeof(TransformSystemGroup))]
public partial struct AimTargettingSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (aimPos, entity) in SystemAPI.Query<RefRW<AimPosition>>().WithAll<PlayerTag>().WithEntityAccess())
        {
            var screenMiddlePoint = Screen.width / 2;

            var sprtRenderer = SystemAPI.ManagedAPI.GetComponent<SpriteRenderer>(entity);
            sprtRenderer.flipX = (aimPos.ValueRO.Value.x < screenMiddlePoint);

            aimPos.ValueRW.direction = (short)(sprtRenderer.flipX == true ? -1 : 1);
        }
    }
}


public partial class PlayerInputSystem : SystemBase
{
    private PlayerInputs _input; //creates input map instance

    protected override void OnCreate()
    {   
        _input = new PlayerInputs();
        _input.Enable();
    }

    protected override void OnDestroy()
    {
        _input.Disable();
        _input.Dispose();
    }

    protected override void OnUpdate()
    {
        var movementInput = (float2)_input.Player.Move.ReadValue<Vector2>();
        foreach (var direction in SystemAPI.Query<RefRW<CharacterMoveDirection>>().WithAll<PlayerTag>()) //retrives entities with both components
        {
            direction.ValueRW.Value = movementInput;
        }

        var aimInput = (float2)_input.Player.Aim.ReadValue<Vector2>();
        foreach (var cursorPos in SystemAPI.Query<RefRW<AimPosition>>().WithAll<PlayerTag>())
        { 
            cursorPos.ValueRW.Value = aimInput;
        }
    }
}