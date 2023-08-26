using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : NetworkBehaviour
{
    public bool IsDisabled;

    private float timer;
    private int currentTick;
    private float minTimeBetweenTicks;

    private const float ServerTickRate = 60f;
    private const int BufferSize = 1024;
    private const float Gravite = 9.8f;

    private MovementStates.InputPayLoad[] InputBuffer;
    private MovementStates.PositionPayLoad[] PositionBuffer;
    private MovementStates.PositionPayLoad LatestServerPositionState;
    private MovementStates.PositionPayLoad LastProcessedPositionState;
    private Vector3 InputVector;

    private bool JumpButtonActive;
    private float Velocity;

    private PlayerInputAsset PlayerInput;

    [SerializeField] private float MovementSpeed = 5;
    [SerializeField] private float RotationSpeed = 0.1f;
    [SerializeField] private float JumpHeight = 5f;

    [SerializeField] private Transform LocalPlayerTransform;
    [SerializeField] private CapsuleCollider CapsuleCollider;

    [SerializeField] private Transform GlobalPlayerTransform;
    [SerializeField] private ServerSideMovement ServerMovement;

    [SerializeField] private LayerMask LayersExceptPlayer;



    private void Start()
    {
        if (!IsLocalPlayer) return;

        minTimeBetweenTicks = 1f / ServerTickRate;

        InputBuffer = new MovementStates.InputPayLoad[BufferSize];
        PositionBuffer = new MovementStates.PositionPayLoad[BufferSize];


        PlayerInput = new PlayerInputAsset();
        PlayerInput.Player.Enable();

    }

    private void Update()
    {
        if (!IsLocalPlayer || IsDisabled) return;

        if (!PlayerInput.Player.enabled)
        {
            PlayerInput.Player.Enable();
        }

        InputVector = new Vector3(PlayerInput.Player.Movement.ReadValue<Vector2>().x, 0,
                                  PlayerInput.Player.Movement.ReadValue<Vector2>().y);

        JumpButtonActive = PlayerInput.Player.Jump.WasPressedThisFrame();

        timer += Time.deltaTime;

        while (timer >= minTimeBetweenTicks)
        {
            timer -= minTimeBetweenTicks;
            HandelTick();
            currentTick++;
        }
    }

    private void HandelTick()
    {
        if (!LatestServerPositionState.Equals(default(MovementStates.PositionPayLoad)) &&
            (LastProcessedPositionState.Equals(default(MovementStates.PositionPayLoad))
            || !LatestServerPositionState.Equals(LastProcessedPositionState)))
        {
            HandleServerReconciliation();
        }

        int BufferIndex = currentTick % BufferSize;

        MovementStates.InputPayLoad inputPayLoad = new MovementStates.InputPayLoad();
        inputPayLoad.Tick = currentTick;
        inputPayLoad.InputVector = Vector3.Normalize(InputVector);
        inputPayLoad.Jump = JumpButtonActive;
        

        InputBuffer[BufferIndex] = inputPayLoad;

        PositionBuffer[BufferIndex] = ProcessMovement(inputPayLoad);

        Vector3 Rotation = ProcessRotation();

        if (IsServer || IsHost)
        {
            GlobalPlayerTransform.position = LocalPlayerTransform.position;
            GlobalPlayerTransform.forward = Rotation;
        }
        else
        {
            ServerMovement.RecivePlayerInputServerRpc(inputPayLoad);
            ServerMovement.RecivePlayerRotationServerRpc(Rotation);
        }
    }
    private MovementStates.PositionPayLoad ProcessMovement(MovementStates.InputPayLoad inputPayLoad)
    {
        //Must Be The Same On Server Side 
        Vector3 PositionOffset = inputPayLoad.InputVector.normalized * MovementSpeed * minTimeBetweenTicks;
        if (!Overlap(0, 0, PositionOffset, 1 * MovementSpeed * minTimeBetweenTicks, out RaycastHit raycastHit))
        {
            LocalPlayerTransform.position += PositionOffset;
        }
        else
        {
            //Try To move in one x dir
            Vector3 xOffset = Vector3.right * inputPayLoad.InputVector.normalized.x * MovementSpeed * minTimeBetweenTicks;

            if (!Overlap(0, 0, xOffset, 1 * MovementSpeed * minTimeBetweenTicks, out RaycastHit x))
            {
                LocalPlayerTransform.position += xOffset;
            }

            //Try To move in one z dir    
            Vector3 zOffset = Vector3.forward * inputPayLoad.InputVector.normalized.z * MovementSpeed * minTimeBetweenTicks;

            if (!Overlap(0, 0, zOffset, 1 * MovementSpeed * minTimeBetweenTicks, out RaycastHit z))
            {
                LocalPlayerTransform.position += zOffset;
            }
        }


        //Process Jump
        ProcessJump();

        return new MovementStates.PositionPayLoad
        {
            Tick = inputPayLoad.Tick,
            Position = LocalPlayerTransform.position,
        };
    }

    private void HandleServerReconciliation()
    {
        LastProcessedPositionState = LatestServerPositionState;

        int ServerStateBufferIndex = LatestServerPositionState.Tick % BufferSize;
        float PositionError = Vector3.Distance(LatestServerPositionState.Position, PositionBuffer[ServerStateBufferIndex].Position);


        if (PositionError > 0.001f)
        {
            Debug.Log("Reconciling");

            LocalPlayerTransform.position = LatestServerPositionState.Position;
            PositionBuffer[ServerStateBufferIndex] = LatestServerPositionState;

            int tickToProcess = LatestServerPositionState.Tick + 1;

            while (tickToProcess < currentTick)
            {
                int bufferIndex = tickToProcess % BufferSize;
                PositionBuffer[bufferIndex] = ProcessMovement(InputBuffer[bufferIndex]);

                tickToProcess++;
            }
        }
    }

    private Vector3 ProcessRotation()
    {
        if (InputVector != Vector3.zero)
        {
            LocalPlayerTransform.forward = Vector3.Slerp(LocalPlayerTransform.forward, InputVector.normalized, RotationSpeed);
        }
        return LocalPlayerTransform.forward;
    }
    private void ProcessJump()
    {
        //Apply Gravity if the player Won't Hit The Floor When The Velocity Is Applyed (Aka After one itration)
        if (Overlap(-0.05f, 0, Vector3.up * (Velocity * minTimeBetweenTicks - Gravite * minTimeBetweenTicks), Velocity * minTimeBetweenTicks - Gravite * minTimeBetweenTicks, out RaycastHit raycast) && Velocity <= 0)
        {
            Velocity = 0;

            //snap To floor
            LocalPlayerTransform.position = new Vector3(LocalPlayerTransform.position.x, raycast.point.y + CapsuleCollider.height / 2 + 0.05f, LocalPlayerTransform.position.z);
        }
        else
        {
            Velocity -= Gravite * minTimeBetweenTicks;
        }

        bool DidJump = false;

        //Try To Jump
        if (JumpButtonActive)
        {
            bool CanJump = Overlap(0, 0, Vector3.up * (Velocity * minTimeBetweenTicks - Gravite * minTimeBetweenTicks), Velocity * minTimeBetweenTicks - Gravite * minTimeBetweenTicks, out raycast);

            if (CanJump)
            {
                Velocity = JumpHeight;
                DidJump = true;
            }

        }

        //Check Hit roof
        if (Velocity > 0)
        {
            if (Overlap(0, 0, Vector3.up * Velocity * minTimeBetweenTicks, Velocity * minTimeBetweenTicks, out raycast))
            {
                Velocity = 0;

                //snap To Celling
                if (!DidJump)
                {

                    float x = Mathf.Abs(Mathf.Abs(LocalPlayerTransform.position.x) - Mathf.Abs(raycast.point.x)) / CapsuleCollider.radius;
                    float xAngle = Mathf.Acos(x);

                    float xHitPoint = Mathf.Sin(xAngle) * CapsuleCollider.radius;
                    float xoffset = float.PositiveInfinity;

                    if(xHitPoint != 0.5f)
                    {
                        xoffset = (LocalPlayerTransform.position.y + CapsuleCollider.height / 4 + xHitPoint) - raycast.point.y;
                    }


                    float z = Mathf.Abs(Mathf.Abs(raycast.point.z) - Mathf.Abs(LocalPlayerTransform.position.z)) / CapsuleCollider.radius;
                    float zAngle = Mathf.Acos(z);

                    float zHitPoint = Mathf.Sin(zAngle) * CapsuleCollider.radius;
                    float zoffset = float.PositiveInfinity;

                    if (zHitPoint != 0.5f)
                    {
                        zoffset = (LocalPlayerTransform.position.y + CapsuleCollider.height / 4 + zHitPoint) - raycast.point.y;
                    }

                    LocalPlayerTransform.position = new Vector3(LocalPlayerTransform.position.x, LocalPlayerTransform.position.y + Mathf.Min(xoffset, zoffset), LocalPlayerTransform.position.z);
                }
            }
        }

        //apply Velocity
        LocalPlayerTransform.position += Vector3.up * (Velocity * minTimeBetweenTicks);
    }

    private bool Overlap(float HeightOffset, float WidthOffset, Vector3 Direction, float DistanceOffset, out RaycastHit raycast)
    {
        bool DidHit1 = Physics.SphereCast(LocalPlayerTransform.position + Vector3.up * ((CapsuleCollider.height / 4) + HeightOffset), CapsuleCollider.radius + WidthOffset, Direction.normalized, out RaycastHit raycast1, Mathf.Abs(DistanceOffset), LayersExceptPlayer);
        bool DidHit2 = Physics.SphereCast(LocalPlayerTransform.position - Vector3.up * ((CapsuleCollider.height / 4) + HeightOffset), CapsuleCollider.radius + WidthOffset, Direction.normalized, out RaycastHit raycast2, Mathf.Abs(DistanceOffset), LayersExceptPlayer);
        bool DidHitMiddel = Physics.SphereCast(LocalPlayerTransform.position, CapsuleCollider.radius + WidthOffset, Direction.normalized, out RaycastHit raycastMiddel, Mathf.Abs(DistanceOffset), LayersExceptPlayer);

        if (Direction.y != 0)
        {
            // if dir is up or down 
            if (DidHit1)
            {
                raycast = raycast1;
                return true;
            }

            if (DidHit2)
            {
                raycast = raycast2;
                return true;
            }

        }
        else
        {   
            if (DidHitMiddel)
            {   
                if(!(DidHit1 && raycastMiddel.distance > raycast1.distance))
                {
                    if(!(DidHit2 && raycastMiddel.distance > raycast2.distance))
                    {
                        raycast = raycastMiddel;
                        return true;
                    }
                }
            }

            if (DidHit1)
            {
                if (!(DidHitMiddel && raycast1.distance > raycastMiddel.distance))
                {
                    if (!(DidHit2 && raycast1.distance > raycast2.distance))
                    {
                        raycast = raycast1;
                        return true;
                    }
                }
            }

            if (DidHit2)
            {
                if (!(DidHit1 && raycast2.distance > raycast1.distance))
                {
                    if (!(DidHitMiddel && raycast2.distance > raycastMiddel.distance))
                    {
                        raycast = raycast2;
                        return true;
                    }
                }
            }
        }

        raycast = raycast2;
        return false;
    }


    [ClientRpc]
    public void RecivePlayerPositionClientRpc(MovementStates.PositionPayLoad positionPayLoad, ClientRpcParams clientRpcParams)
    {
        LatestServerPositionState = positionPayLoad;
    }

    public float GetMovementSpeed()
    {
        return MovementSpeed;
    }

    public float GetJumpHeight()
    {
        return JumpHeight;
    }

    public void SetLayersExceptPLayer(LayerMask layersExceptPLayer)
    {
        LayersExceptPlayer = layersExceptPLayer;
    }


}
