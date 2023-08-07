using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class ServerSideMovement : NetworkBehaviour
{
    private float timer;
    private int currentTick;
    private float minTimeBetweenTicks;

    private const float ServerTickRate = 60f;
    private const int BufferSize = 1024;

    private MovementStates.PositionPayLoad[] PositionBuffer;
    private Queue<MovementStates.InputPayLoad> InputQueue;

    [SerializeField] private Transform GlobalPlayerTransform;
    [SerializeField] private PlayerMovement PlayerMovement;


    private void Start()
    {
        if (!(IsServer || IsHost)) return;

        minTimeBetweenTicks = 1f / ServerTickRate;

        PositionBuffer = new MovementStates.PositionPayLoad[BufferSize];
        InputQueue = new Queue<MovementStates.InputPayLoad>();
    }

    private void Update()
    {
        if (!(IsServer || IsHost)) return;

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
        if (!CapsuleCast(PositionOffset, out RaycastHit raycast))
        {
            GlobalPlayerTransform.position += PositionOffset;
        }
        else
        {
            //Try To move in one x dir
            Vector3 xOffset = Vector3.right * inputPayLoad.InputVector.normalized.x * MovementSpeed * minTimeBetweenTicks;
                
            if (!CapsuleCast(xOffset, out RaycastHit x))
            {
                GlobalPlayerTransform.position += xOffset;
            }

            //Try To move in one z dir    
            Vector3 zOffset = Vector3.forward * inputPayLoad.InputVector.normalized.z * MovementSpeed * minTimeBetweenTicks;
                
            if (!CapsuleCast(zOffset, out RaycastHit z))
            {
                GlobalPlayerTransform.position += zOffset;
            }
        }

        return new MovementStates.PositionPayLoad
        {
            Tick = inputPayLoad.Tick,
            Position = GlobalPlayerTransform.position,
        };
    }
    private bool CapsuleCast(Vector3 PositionOffset, out RaycastHit raycast)
    {
        return Physics.CapsuleCast(GlobalPlayerTransform.position - Vector3.up *
            GlobalPlayerTransform.GetComponent<CapsuleCollider>().height / 2,
            GlobalPlayerTransform.position + Vector3.up *
            GlobalPlayerTransform.GetComponent<CapsuleCollider>().height / 2,
            GlobalPlayerTransform.GetComponent<CapsuleCollider>().radius, PositionOffset.normalized, out raycast, 1 * PlayerMovement.GetMovementSpeed() * minTimeBetweenTicks);
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
