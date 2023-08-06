using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class PlayerSetup : NetworkBehaviour
{
    [SerializeField] private GameObject[] ObjectsToDisableIfLocal;
    [SerializeField] private GameObject[] ObjectsToDisableIfNotLocal;
    [SerializeField] private Collider GlobalPlayerCollider;
    [SerializeField] private Rigidbody GlobalPlayerRigidbody;
    private void Start()
    {
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
        if(!(IsServer || IsHost))
        {
            Destroy(GlobalPlayerRigidbody.gameObject.GetComponent<NetworkRigidbody>());
            Destroy(GlobalPlayerRigidbody);
        }
        else if(IsOwnedByServer)
        {
            Destroy(GlobalPlayerRigidbody.gameObject.GetComponent<NetworkRigidbody>());
            Destroy(GlobalPlayerRigidbody);
        }
    }
}
