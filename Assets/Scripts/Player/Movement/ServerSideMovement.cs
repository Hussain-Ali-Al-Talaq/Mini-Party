using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class ServerSideMovement : NetworkBehaviour
{
    public bool IsDisabled;

    private float timer;
    private float minTimeBetweenTicks;

    private const float ServerTickRate = 60f;
    private const int BufferSize = 1024;
    private const float Gravite = 9.8f;

    private MovementStates.PositionPayLoad[] PositionBuffer;
    private Queue<MovementStates.InputPayLoad> InputQueue;

    private float Velocity;

    [SerializeField] private Transform GlobalPlayerTransform;
    [SerializeField] private CapsuleCollider CapsuleCollider;
    [SerializeField] private PlayerMovement PlayerMovement;

    [SerializeField] private LayerMask PlayerLayer;
    [SerializeField] private LayerMask LayersExceptPlayer;




    private void Start()
    {
        if (!(IsServer || IsHost)) return;

        minTimeBetweenTicks = 1f / ServerTickRate;

        PositionBuffer = new MovementStates.PositionPayLoad[BufferSize];
        InputQueue = new Queue<MovementStates.InputPayLoad>();
    }

    private void Update()
    {
        if (!(IsServer || IsHost) || IsDisabled) return;

        timer += Time.deltaTime;

        while (timer >= minTimeBetweenTicks)
        {
            timer -= minTimeBetweenTicks;
            HandelTick();
        }
    }
    private void HandelTick()
    {
        int BufferIndex = -1;
        while (InputQueue.Count > 0)
        {
            MovementStates.InputPayLoad inputPayLoad = InputQueue.Dequeue();
            inputPayLoad.InputVector = Vector3.Normalize(inputPayLoad.InputVector);
            BufferIndex = inputPayLoad.Tick % BufferSize;

            PositionBuffer[BufferIndex] = ProcessMovement(inputPayLoad);
        }

        if(BufferIndex != -1)
        {
            PlayerMovement.RecivePlayerPositionClientRpc(PositionBuffer[BufferIndex], new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new List<ulong>() { OwnerClientId } } });
        }
    }
    private MovementStates.PositionPayLoad ProcessMovement(MovementStates.InputPayLoad inputPayLoad)
    {
        float MovementSpeed = PlayerMovement.GetMovementSpeed();

        //Must Be The Same On Client Side 
        Vector3 PositionOffset = inputPayLoad.InputVector.normalized * MovementSpeed * minTimeBetweenTicks;
        if (!Overlap(0, 0, PositionOffset, 1 * MovementSpeed * minTimeBetweenTicks, out RaycastHit raycastHit))
        {
            GlobalPlayerTransform.position += PositionOffset;
        }
        else
        {
            //Try To move in one x dir
            Vector3 xOffset = Vector3.right * inputPayLoad.InputVector.normalized.x * MovementSpeed * minTimeBetweenTicks;
                
            if (!Overlap(0, 0, xOffset, 1 * MovementSpeed * minTimeBetweenTicks, out RaycastHit x))
            {
                GlobalPlayerTransform.position += xOffset;
            }

            //Try To move in one z dir    
            Vector3 zOffset = Vector3.forward * inputPayLoad.InputVector.normalized.z * MovementSpeed * minTimeBetweenTicks;
                
            if (!Overlap(0, 0, zOffset, 1 * MovementSpeed * minTimeBetweenTicks, out RaycastHit y))
            {
                GlobalPlayerTransform.position += zOffset;
            }
        }

        //Process Jump
        ProcessJump(inputPayLoad.Jump);

        return new MovementStates.PositionPayLoad
        {
            Tick = inputPayLoad.Tick,
            Position = GlobalPlayerTransform.position,
        };
    }

    private void ProcessJump(bool JumpButtonActive)
    {
        //Apply Gravity if the player Won't Hit The Floor When The Velocity Is Applyed (Aka After one itration)
        if (Overlap(-0.05f, 0, Vector3.up * (Velocity * minTimeBetweenTicks - Gravite * minTimeBetweenTicks), Velocity * minTimeBetweenTicks - Gravite * minTimeBetweenTicks, out RaycastHit raycast) && Velocity <= 0)
        {
            Velocity = 0;

            //snap To floor
            GlobalPlayerTransform.position = new Vector3(GlobalPlayerTransform.position.x, raycast.point.y + CapsuleCollider.height / 2 + 0.05f, GlobalPlayerTransform.position.z);
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
                Velocity = PlayerMovement.GetJumpHeight();
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
                    float x = Mathf.Abs(Mathf.Abs(GlobalPlayerTransform.position.x) - Mathf.Abs(raycast.point.x)) / CapsuleCollider.radius;
                    float xAngle = Mathf.Acos(x);

                    float xHitPoint = Mathf.Sin(xAngle) * CapsuleCollider.radius;
                    float xoffset = float.PositiveInfinity;

                    if (xHitPoint != 0.5f)
                    {
                        xoffset = (GlobalPlayerTransform.position.y + CapsuleCollider.height / 4 + xHitPoint) - raycast.point.y;
                    }


                    float z = Mathf.Abs(Mathf.Abs(raycast.point.z) - Mathf.Abs(GlobalPlayerTransform.position.z)) / CapsuleCollider.radius;
                    float zAngle = Mathf.Acos(z);

                    float zHitPoint = Mathf.Sin(zAngle) * CapsuleCollider.radius;
                    float zoffset = float.PositiveInfinity;

                    if (zHitPoint != 0.5f)
                    {
                        zoffset = (GlobalPlayerTransform.position.y + CapsuleCollider.height / 4 + zHitPoint) - raycast.point.y;
                    }

                    GlobalPlayerTransform.position = new Vector3(GlobalPlayerTransform.position.x, GlobalPlayerTransform.position.y + Mathf.Min(xoffset, zoffset), GlobalPlayerTransform.position.z);
                }
            }
        }

        //apply Velocity
        GlobalPlayerTransform.position += Vector3.up * (Velocity * minTimeBetweenTicks);
    }

    private bool Overlap(float HeightOffset, float WidthOffset, Vector3 Direction, float DistanceOffset, out RaycastHit raycast)
    {
        bool DidHit1 = Physics.SphereCast(GlobalPlayerTransform.position + Vector3.up * ((CapsuleCollider.height / 4) + HeightOffset), CapsuleCollider.radius + WidthOffset, Direction.normalized, out RaycastHit raycast1, Mathf.Abs(DistanceOffset), LayersExceptPlayer);
        bool DidHit2 = Physics.SphereCast(GlobalPlayerTransform.position - Vector3.up * ((CapsuleCollider.height / 4) + HeightOffset), CapsuleCollider.radius + WidthOffset, Direction.normalized, out RaycastHit raycast2, Mathf.Abs(DistanceOffset), LayersExceptPlayer);
        bool DidHitMiddel = Physics.SphereCast(GlobalPlayerTransform.position, CapsuleCollider.radius + WidthOffset, Direction.normalized, out RaycastHit raycastMiddel, Mathf.Abs(DistanceOffset), LayersExceptPlayer);

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
                if (!(DidHit1 && raycastMiddel.distance > raycast1.distance))
                {
                    if (!(DidHit2 && raycastMiddel.distance > raycast2.distance))
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

    [ServerRpc]
    public void RecivePlayerInputServerRpc(MovementStates.InputPayLoad inputPayLoad)
    {
        InputQueue.Enqueue(inputPayLoad);
    }
    [ServerRpc]
    public void RecivePlayerRotationServerRpc(Vector3 Rotation)
    {
        GlobalPlayerTransform.forward = Rotation;
    }

}
