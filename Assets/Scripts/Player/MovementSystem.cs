using System;
using Camera;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;

namespace Player
{
    public class MovementSystem : ComponentSystem
    {
        private InputManager _inputManager;
        private BuildPhysicsWorld _buildPhysicsWorld;
    
        protected override void OnCreate()
        {
            base.OnCreate();
        
            _inputManager = new InputManager();
            _inputManager.Enable();
        
            _buildPhysicsWorld = World.GetOrCreateSystem<BuildPhysicsWorld>();
        }

        protected override void OnUpdate()
        {
            Entities
                .WithAll<PlayerTag>()
                .ForEach((
                    ref Translation playerTranslation, 
                    ref Rotation rotation, 
                    ref PhysicsVelocity velocity,
                    ref PhysicsCollider physicsCollider,
                    ref PhysicsMass physicsMass) =>
                {
                    var cameraTranslation = GetCameraTranslation();
                    FixRotation(ref physicsMass);
                    HandleMovement(ref velocity, ref rotation, playerTranslation, cameraTranslation);
                    HandleJumping(playerTranslation, ref velocity);
                });
        }

        private Translation GetCameraTranslation()
        {
            var cameraTranslation = new Translation();
            Entities
                .WithAll<CameraTag>()
                .ForEach((ref Translation innerCameraTranslation) =>
                {
                    cameraTranslation = innerCameraTranslation;
                });
            return cameraTranslation;
        }

        private static void FixRotation(ref PhysicsMass physicsMass)
        {
            physicsMass.InverseInertia = new float3(0f);
        }
    
        private void HandleMovement(
            ref PhysicsVelocity velocity,
            ref Rotation rotation,
            Translation playerTranslation, 
            Translation cameraTranslation)
        {
            const float acceleration = 0.2f;
            var input = _inputManager.Player.Movement.ReadValue<Vector2>();
            
            var vectorFromCameraToPlayer = playerTranslation.Value - cameraTranslation.Value;
            var flatVectorFromCameraToPlayer = new float2(vectorFromCameraToPlayer.x, vectorFromCameraToPlayer.z);
            var forwardDirection = math.normalizesafe(flatVectorFromCameraToPlayer);
            var sidewaysDirection = new float2(forwardDirection.y, -forwardDirection.x);
            var forwardMovement = forwardDirection * input.y;
            var sidewaysMovement = sidewaysDirection * input.x;

            var compoundMovement = forwardMovement + sidewaysMovement;
            

            var movement = compoundMovement * acceleration;
            var threeDimensionalMovement = new float3(movement.x, 0f, movement.y);

        
            const float maxVelocity = 5.0f;
            const float maxVelocitySquared = maxVelocity * maxVelocity;
            var squaredVelocityLength = math.lengthsq(velocity.Linear);
            if (squaredVelocityLength < maxVelocitySquared)
            {
                velocity.Linear += threeDimensionalMovement;
            }

            var squaredMovementLength = math.lengthsq(threeDimensionalMovement);
            if (squaredMovementLength > float.Epsilon)
            {
                rotation.Value = quaternion.LookRotationSafe(threeDimensionalMovement, new float3(0f, 1f, 0f));
            }
        }

        private void HandleJumping(Translation translation, ref PhysicsVelocity velocity)
        {
            if (!_inputManager.Player.Jumping.triggered || !IsOnGround(translation))
            {
                return;
            }
        
            var direction = new float3(0f, 1f, 0f);
            const float jumpingVelocity = 5f; 
            var movement = direction * jumpingVelocity;

            velocity.Linear += movement;
        }

        private bool IsOnGround(Translation translation)
        {
            var collisionWorld = _buildPhysicsWorld.PhysicsWorld.CollisionWorld;
            var start = translation.Value;
        
            const float height = 1f;
            const float groundDetectionThreshold = 0.3f;
            const float yDetectionRange = height / 2 + groundDetectionThreshold;
            var end = new float3(start.x, start.y - yDetectionRange, start.z);
        
            var rayCastInput = new RaycastInput
            {
                Start = start,
                End = end,
                Filter = new CollisionFilter
                {
                    BelongsTo = 1u << PhysicsLayerBits.Player,
                    CollidesWith = 1u << PhysicsLayerBits.Terrain,
                }
            };
        
            return collisionWorld.CastRay(rayCastInput);
        }
    }
}
