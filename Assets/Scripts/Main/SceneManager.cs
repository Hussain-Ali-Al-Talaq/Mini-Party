using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneManager : MonoBehaviour
{
    public static SceneManager instance;
    /// <summary>
    /// Must Remove After Trigger
    /// </summary>

    private bool SceneLoaded;
    private Queue<PlayerSetup> playerSetups = new Queue<PlayerSetup>();

    private void Awake()
    {   
        if(instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(this);
        }
    }
    
    public void LoadScene(string Name)
    {
        SceneLoaded = false;
        NetworkManager.Singleton.SceneManager.OnLoad += SceneManager_OnLoad;
        NetworkManager.Singleton.SceneManager.OnLoadComplete += SceneManager_OnLoadComplete;
        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += SceneManager_OnLoadEventCompleted;
        NetworkManager.Singleton.SceneManager.LoadScene(Name, LoadSceneMode.Single);
    }
    private void SceneManager_OnLoad(ulong clientId, string sceneName, LoadSceneMode loadSceneMode, AsyncOperation asyncOperation)
    {
        Debug.Log("Scene Loading");
        NetworkManager.Singleton.SceneManager.OnLoad -= SceneManager_OnLoad;
    }

    private void SceneManager_OnLoadComplete(ulong clientId, string sceneName, LoadSceneMode loadSceneMode)
    {
        Debug.Log("Scene Loaded");
        NetworkManager.Singleton.SceneManager.OnLoadComplete -= SceneManager_OnLoadComplete;
    }
    private void SceneManager_OnLoadEventCompleted(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        Debug.Log("Done, Scene: " + sceneName + ", " + "Clients Count: " + clientsCompleted.Count);
        SceneLoaded = true;
    }

    private void Update()
    {
        if (SceneLoaded)
        {
            while (playerSetups.Count > 0)
            {
                PlayerSetup playerSetup = playerSetups.Dequeue();

                Debug.Log(playerSetup.OwnerClientId);

                playerSetup.EnablePlayerClientRpc(new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new List<ulong> { playerSetup.OwnerClientId } } });
                playerSetup.EnablePlayer();
            }
        }
    }
    public void AddPlayerSetup(PlayerSetup playerSetup)
    {
        playerSetups.Enqueue(playerSetup);
    }
}
