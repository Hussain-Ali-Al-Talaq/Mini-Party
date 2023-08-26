using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using System.Collections.Generic;

public class CreateButton : MonoBehaviour
{
    private UnityTransport unityTransport;
    private int MaxPlayers = 8;

    public async void Create()
    {
        unityTransport = FindObjectOfType<UnityTransport>();

        if (UnityServices.State == ServicesInitializationState.Initializing)
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

        Allocation allocation = await RelayService.Instance.CreateAllocationAsync(MaxPlayers);
        string JoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
        
        Debug.Log(JoinCode);

        unityTransport.SetHostRelayData(allocation.RelayServer.IpV4, (ushort)allocation.RelayServer.Port, allocation.AllocationIdBytes, allocation.Key, allocation.ConnectionData);

        NetworkManager.Singleton.StartHost();

        SceneManager.instance.LoadScene("Game");
    }
}
