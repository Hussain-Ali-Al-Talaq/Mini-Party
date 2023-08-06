using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : NetworkBehaviour
{
    //Client Side Prediction

    private float timer;
    private int currentTick;
    private float minTimeBetweenTicks;

    private const float ServerTickRate = 60f;
    private const int BufferSize = 1024;

    private MovementStates.InputPayLoad[] InputBuffer;
    private MovementStates.PositionPayLoad[] PositionBuffer;
    private MovementStates.PositionPayLoad LatestServerState;
    private MovementStates.PositionPayLoad LastProcessedState;
    private Vector3 InputVector;

    private Vector3 PlayerPosition;

    private PlayerInputAsset PlayerInput;

    [SerializeField] private float MovementSpeed = 5;
    [SerializeField] private Transform LocalPlayerTransform;

    [SerializeField] private Transform GlobalPlayerTransform;
    [SerializeField] private ServerSideMovement ServerMovement;
    

    private void Start()
    {
        if (!IsLocalPlayer) return;

        minTimeBetweenTicks = 1f / ServerTickRate;

        InputBuffer = new MovementStates.InputPayLoad[BufferSize];
        PositionBuffer = new MovementStates.PositionPayLoad[BufferSize];

        PlayerInput = new PlayerInputAsset();
        PlayerInput.Player.Enable();
        PlayerInput.Player.Movement.ReadValue<Vector2>();
    }

    private void Update()
    {
        if (!IsLocalPlayer) return;

        if (!PlayerInput.Player.enabled)
        {
            PlayerInput.Player.Enable();
        }

        InputVector = new Vector3(PlayerInput.Player.Movement.ReadValue<Vector2>().x,0,
                                  PlayerInput.Player.Movement.ReadValue<Vector2>().y);
        
        
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
        if (!LatestServerState.Equals(default(MovementStates.PositionPayLoad)) && 
            (LastProcessedState.Equals(default(MovementStates.PositionPayLoad)) 
            || !LatestServerState.Equals(LastProcessedState)))
        {
            HandleServerReconciliation();
        }

        int BufferIndex = currentTick % BufferSize;

        MovementStates.InputPayLoad inputPayLoad = new MovementStates.InputPayLoad();
        inputPayLoad.Tick = currentTick;
        inputPayLoad.InputVector = Vector3.Normalize(InputVector);
        
        InputBuffer[BufferIndex] = inputPayLoad;

        PositionBuffer[BufferIndex] = ProcesseMovement(inputPayLoad);

        if (IsServer || IsHost)
        {
            GlobalPlayerTransform.position = LocalPlayerTransform.position;
        }
        else
        {
            ServerMovement.RecivePlayerInputServerRpc(inputPayLoad);
        }
    }
    private MovementStates.PositionPayLoad ProcesseMovement(MovementStates.InputPayLoad inputPayLoad)
    {
        //Must Be The Same On Server Side 
        Vector3 PositionOffset = inputPayLoad.InputVector * MovementSpeed * minTimeBetweenTicks;
        if (!Physics.CapsuleCast(PlayerPosition - Vector3.up * LocalPlayerTransform.GetComponent<CapsuleCollider>().height / 2, PlayerPosition + Vector3.up * LocalPlayerTransform.GetComponent<CapsuleCollider>().height / 2, LocalPlayerTransform.GetComponent<CapsuleCollider>().radius, PositionOffset.normalized, out RaycastHit raycast, 1))
        {
            PlayerPosition += PositionOffset;
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
                    PlayerPosition += PositionOffset;
                }
            }
            else if (raycast.distance > 0.04f)
            {
                // if the distence to closeset player / collisison is less than 0.1
                PlayerPosition += PositionOffset.normalized * 0.04f;
            }
        }

        LocalPlayerTransform.position = PlayerPosition;

        return new MovementStates.PositionPayLoad
        {
            Tick = inputPayLoad.Tick,
            Position = LocalPlayerTransform.position,
        };
    }

    private void HandleServerReconciliation()
    {
        LastProcessedState = LatestServerState;

        int ServerStateBufferIndex = LatestServerState.Tick % BufferSize;
        float PositionError = Vector3.Distance(LatestServerState.Position, PositionBuffer[ServerStateBufferIndex].Position);


        if(PositionError > 0.001f)
        {
            Debug.Log("Reconciling");

            PlayerPosition = LatestServerState.Position;
            PositionBuffer[ServerStateBufferIndex] = LatestServerState;

            int tickToProcess = LatestServerState.Tick + 1;

            while(tickToProcess < currentTick) 
            {
                int bufferIndex = tickToProcess % BufferSize;
                PositionBuffer[bufferIndex] = ProcesseMovement(InputBuffer[bufferIndex]);

                tickToProcess++;
            }
        }
    }

    [ClientRpc]
    public void RecivePlayerPositionClientRpc(MovementStates.PositionPayLoad positionPayLoad, ClientRpcParams clientRpcParams)
    {
        LatestServerState = positionPayLoad;
    }
    public float GetMovementSpeed()
    {
        return MovementSpeed;
    }
    
}
