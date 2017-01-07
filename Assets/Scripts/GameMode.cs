using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

[NetworkSettings(channel = 2, sendInterval = 0.333f)]
public class GameMode : NetworkBehaviour 
{
    public static GameMode singleton { get; private set; }

    //Time since start of game based on Server Time
    public float serverGameTime;

    [ClientRpc]
    void RpcSyncTime(float time)
    {
        serverGameTime = time;
    }

    void Start()
    {
        if (singleton == null) { singleton = this; }
    }

    void Update()
    {
        serverGameTime += Time.deltaTime;

        if (isServer) { RpcSyncTime(serverGameTime); }
    }

    void OnDestroy()
    {
        if (singleton == this) { singleton = null; }
    }
}
