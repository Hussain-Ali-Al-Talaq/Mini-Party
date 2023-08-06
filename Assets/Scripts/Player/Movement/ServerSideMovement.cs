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

            PositionBuffer[BufferIndex] = ProcesseMovement(inputPayLoad);
        }

        if(BufferIndex != -1)
        {
            PlayerMovement.RecivePlayerPositionClientRpc(PositionBuffer[BufferIndex], new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new List<ulong>() { OwnerClientId } } });
        }
    }
    private MovementStates.PositionPayLoad ProcesseMovement(MovementStates.InputPayLoad inputPayLoad)
    {
        //Must Be The Same On Client Side 
        Vector3 PositionOffset = inputPayLoad.InputVector * PlayerMovement.GetMovementSpeed() * minTimeBetweenTicks;
        if (!Physics.CapsuleCast(GlobalPlayerTransform.position - Vector3.up * GlobalPlayerTransform.GetComponent<CapsuleCollider>().height / 2, GlobalPlayerTransform.position + Vector3.up * GlobalPlayerTransform.GetComponent<CapsuleCollider>().height / 2, GlobalPlayerTransform.GetComponent<CapsuleCollider>().radius, PositionOffset.normalized, out RaycastHit raycast, 1))
        {
            GlobalPlayerTransform.position += PositionOffset;
        }
        else
        {
            //check distence to closeset player / collisison
            if (raycast.distance > 0.1f)
            {
                float div = Vector3.Magnitude(PositionOffset) / raycast.distance;
                //check if can move as normal
                if (div < 1)
                {
                    GlobalPlayerTransform.position += PositionOffset;
                }
            }
            else if(raycast.distance > 0.04f)
            {
                // if the distence to closeset player / collisison is less than 0.1
                GlobalPlayerTransform.position += PositionOffset.normalized * 0.035f;
            }
        }

        return new MovementStates.PositionPayLoad
        {
            Tick = inputPayLoad.Tick,
            Position = GlobalPlayerTransform.position,
        };
    }

    [ServerRpc]
    public void RecivePlayerInputServerRpc(MovementStates.InputPayLoad inputPayLoad)
    {
        InputQueue.Enqueue(inputPayLoad);
    }

}
