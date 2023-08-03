using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using System.Collections.Generic;

public class JoinButton : MonoBehaviour
{
    [SerializeField] private TMP_InputField Text;
    private UnityTransport unityTransport;

    public async void Join()
    {   
        unityTransport = FindObjectOfType<UnityTransport>();

        if(UnityServices.State == ServicesInitializationState.Initializing)
        {
            return;
        }
        if (UnityServices.State == ServicesInitializationState.Uninitialized)
        {
            await UnityServices.InitializeAsync();
        }
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        JoinAllocation allocation;

        try
        {
            allocation = await RelayService.Instance.JoinAllocationAsync(Text.text);
        }
        catch
        {
            Debug.Log("Bad Join Code");
            return;
        }

        unityTransport.SetClientRelayData(allocation.RelayServer.IpV4, (ushort)allocation.RelayServer.Port, allocation.AllocationIdBytes, allocation.Key, allocation.ConnectionData, allocation.HostConnectionData);
        
        NetworkManager.Singleton.StartClient();
    }
}
