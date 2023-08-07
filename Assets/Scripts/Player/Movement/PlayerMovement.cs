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

    private PlayerInputAsset PlayerInput;

    [SerializeField] private float MovementSpeed = 5;
    [SerializeField] private float RotationSpeed = 0.1f;
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
        if (!CapsuleCast(PositionOffset,out RaycastHit raycast))
        {
            LocalPlayerTransform.position += PositionOffset;
        }
        else
        {
            //Try To move in one x dir
            Vector3 xOffset = Vector3.right * inputPayLoad.InputVector.normalized.x * MovementSpeed * minTimeBetweenTicks;
                
            if (!CapsuleCast(xOffset, out RaycastHit x))
            {
                LocalPlayerTransform.position += xOffset;
            }

            //Try To move in one z dir    
            Vector3 zOffset = Vector3.forward * inputPayLoad.InputVector.normalized.z * MovementSpeed * minTimeBetweenTicks;

            if (!CapsuleCast(zOffset, out RaycastHit z))
            {
                LocalPlayerTransform.position += zOffset;
            }
        }

        return new MovementStates.PositionPayLoad
        {
            Tick = inputPayLoad.Tick,
            Position = LocalPlayerTransform.position,
        };
    }

    private bool CapsuleCast(Vector3 PositionOffset, out RaycastHit raycast)
    {
        return Physics.CapsuleCast(LocalPlayerTransform.position - Vector3.up *
            LocalPlayerTransform.GetComponent<CapsuleCollider>().height / 2,
            LocalPlayerTransform.position + Vector3.up *
            LocalPlayerTransform.GetComponent<CapsuleCollider>().height / 2,
            LocalPlayerTransform.GetComponent<CapsuleCollider>().radius, PositionOffset.normalized, out raycast, 1 * MovementSpeed * minTimeBetweenTicks);
    }

    private void HandleServerReconciliation()
    {
        LastProcessedState = LatestServerState;

        int ServerStateBufferIndex = LatestServerState.Tick % BufferSize;
        float PositionError = Vector3.Distance(LatestServerState.Position, PositionBuffer[ServerStateBufferIndex].Position);


        if(PositionError > 0.001f)
        {
            Debug.Log("Reconciling");

            LocalPlayerTransform.position = LatestServerState.Position;
            PositionBuffer[ServerStateBufferIndex] = LatestServerState;

            int tickToProcess = LatestServerState.Tick + 1;

            while(tickToProcess < currentTick) 
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
