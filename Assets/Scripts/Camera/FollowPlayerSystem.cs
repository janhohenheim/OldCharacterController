using Player;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;

namespace Camera
{
    [UpdateAfter(typeof(MovementSystem))]
    public class FollowPlayerSystem : ComponentSystem
    {
        private const float MaxCameraDistance = 10f;
        
        private InputManager _inputManager;
        private BuildPhysicsWorld _buildPhysicsWorld;


        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            _buildPhysicsWorld = World.GetOrCreateSystem<BuildPhysicsWorld>();
        }

        protected override void OnCreate()
        {
            base.OnCreate();
        
            _inputManager = new InputManager();
            _inputManager.Enable();
        }
        
        protected override void OnUpdate()
        {
            Entities
                .WithAll<CameraTag>()
                .ForEach((
                    ref Translation cameraTranslation, 
                    ref LocalToWorld localToWorld,
                    ref Rotation cameraRotation) =>
                {
                    var playerTranslation = GetPlayerTranslation();

                    var vectorFromPlayerToNewCameraPosition = CalculateVectorFromPlayerToNewCameraPosition(cameraTranslation, playerTranslation);
                    var newCameraPosition = playerTranslation.Value + vectorFromPlayerToNewCameraPosition;

                    var adjustedCameraPosition = MovePositionBeforeWalls(newCameraPosition, playerTranslation.Value);

                    cameraTranslation.Value = adjustedCameraPosition;
                    cameraRotation.Value = quaternion.LookRotationSafe(-vectorFromPlayerToNewCameraPosition, new float3(0f, 1f, 0f));
                });
        }

        private float3 CalculateVectorFromPlayerToNewCameraPosition(Translation cameraTranslation, Translation playerTranslation)
        {
            const float cameraSensitivity = 2f;

            var vectorFromPlayerToCamera = cameraTranslation.Value - playerTranslation.Value;
            var input = _inputManager.Player.MoveCamera.ReadValue<Vector2>();
            var inputAsEulerAngles = new float3(input.y * 0.01f * cameraSensitivity, input.x * 0.01f * cameraSensitivity, 0f);

            var rotatedVectorFromPlayerToCamera = math.mul(quaternion.Euler(inputAsEulerAngles), vectorFromPlayerToCamera);

            var normalizedVectorFromPlayerToCamera = math.normalizesafe(rotatedVectorFromPlayerToCamera);
            return normalizedVectorFromPlayerToCamera * MaxCameraDistance;
        }

        private float3 MovePositionBeforeWalls(float3 cameraPosition, float3 playerPosition)
        {
            var rayCastInput = new RaycastInput
            {
                Start = playerPosition,
                End = cameraPosition,
                Filter = new CollisionFilter
                {
                    BelongsTo = 1u << PhysicsLayerBits.Player,
                    CollidesWith = 1u << PhysicsLayerBits.Terrain
                }
            };

            if (!_buildPhysicsWorld.PhysicsWorld.CollisionWorld.CastRay(rayCastInput, out var hit))
            {
                return cameraPosition;
            }

            var distanceToWall = MaxCameraDistance * hit.Fraction;
            
            var vectorFromPlayerToCamera = cameraPosition - playerPosition;
            var normalizedVectorFromPlayerToCamera = math.normalizesafe(vectorFromPlayerToCamera);
            var adjustedVectorFromPlayerToCamera = normalizedVectorFromPlayerToCamera * distanceToWall;
            
            return playerPosition + adjustedVectorFromPlayerToCamera;
        }

        private Translation GetPlayerTranslation()
        {
            var playerTranslation = new Translation();
            Entities
                .WithAll<PlayerTag>()
                .ForEach((ref Translation innerPlayerTranslation) =>
                {
                    playerTranslation = innerPlayerTranslation;
                });
            return playerTranslation;
        }
    }
}