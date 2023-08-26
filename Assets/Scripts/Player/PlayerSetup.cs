using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerSetup : NetworkBehaviour
{
    [SerializeField] private GameObject[] ObjectsToDisableIfLocal;
    [SerializeField] private GameObject[] ObjectsToDisableIfNotLocal;

    [SerializeField] private Collider GlobalPlayerCollider;
    [SerializeField] private ServerSideMovement ServerMovement;

    [SerializeField] private GameObject LocalPlayerGameObject;
    [SerializeField] private PlayerMovement PlayerMovement;

    [SerializeField] private LayerMask ServerPlayerLayer;
    [SerializeField] private LayerMask LayersExceptServerPlayer;

    private bool start;

    private void Start()
    {
        if (start) return;
        start = true;

        if (!IsLocalPlayer)
        {
            foreach(GameObject obj in ObjectsToDisableIfNotLocal) 
            {
                obj.SetActive(false);
            }
            GlobalPlayerCollider.enabled = true;
        }
        else
        {
            foreach (GameObject obj in ObjectsToDisableIfLocal)
            {
                obj.SetActive(false);
            }
            GlobalPlayerCollider.enabled = false;

        }

        if(IsOwnedByServer)
        {
            //Idk Why It Throws An Error When You Assin It Dirctly 
            LocalPlayerGameObject.layer = 8;
            PlayerMovement.SetLayersExceptPLayer(LayersExceptServerPlayer);

        }
        
        ServerMovement.IsDisabled = true;
        PlayerMovement.IsDisabled = true;

        if (IsServer)
        {
            SceneManager.instance.AddPlayerSetup(this);
        }
    }
    public void EnablePlayer()
    {
        if (IsServer)
        {
            ServerMovement.IsDisabled = false;
        }
        if (IsLocalPlayer)
        {
            PlayerMovement.IsDisabled = false;
        }
    }
    [ClientRpc]
    public void EnablePlayerClientRpc(ClientRpcParams clientRpcParams)
    {
        if (!start) Start();

        EnablePlayer();
    }
}
