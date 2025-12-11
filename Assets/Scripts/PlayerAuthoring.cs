using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.VisualScripting;
using Unity.Mathematics;
using System;
using Unity.VisualScripting.FullSerializer;
using Unity.Collections;
using Unity.Transforms;

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
        state.RequireForUpdate<InitializeCameraTargetTag>();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (CameraTargetSingleton.Instance == null) return;
        var CameraTargetTransform = CameraTargetSingleton.Instance.transform;

        var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);
        foreach (var (cameraTarget, entity) in SystemAPI.Query<RefRW<CameraTarget>>().WithAll<InitializeCameraTargetTag, PlayerTag>().WithEntityAccess())
        { 
            cameraTarget.ValueRW.CameraTransform = CameraTargetTransform;
            ecb.RemoveComponent<InitializeCameraTargetTag>(entity);
        }

        ecb.Playback(state.EntityManager);
    }
}

[UpdateAfter(typeof(TransformSystemGroup))]
public partial struct MoveCameraTarget : ISystem 
{ 
    public void OnUpdate(ref SystemState state) 
    {
        foreach (var (transform, cameraPredictionOffset, cameraTarget) in SystemAPI.Query<LocalToWorld, CharacterMoveDirection, 
            CameraTarget>().WithAll<PlayerTag>().WithNone<InitializeCameraTargetTag>())
        {
            Vector3 updPosition = new(transform.Position.x + cameraPredictionOffset.Value.x, transform.Position.y + cameraPredictionOffset.Value.y, transform.Position.z);
            cameraTarget.CameraTransform.Value.position = updPosition;
        }
    }
}

public partial class PlayerInputSystem : SystemBase
{
    private PlayerInputs _input;

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
        var currentInput = (float2)_input.Player.Move.ReadValue<Vector2>();
        foreach (var direction in SystemAPI.Query<RefRW<CharacterMoveDirection>>().WithAll<PlayerTag>())
        {
            direction.ValueRW.Value = currentInput;
        }
    }
}