using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public struct PlayerInputState
{
    //Input ID set by Client
    public int id;

    //Expected deltaTime for simulating input
    public float simDuration;

    //Registers Keypress for movement (1, 0, -1)
    public float forwardInput;
    public float rightInput;

    //Keypress for Firing Weapon
    public bool fireInput;

    //Flag to monitor if the Client and the Server have received this input
    public bool clientReceived;
    public bool serverReceived;
}

[NetworkSettings(channel = 2, sendInterval = 0.333f)]
public class PlayerCommand : NetworkBehaviour
{
    //Prefab of Default Player Character
    //TODO:Setup a method to allow easy changing of Player Prefab
    [SyncVar]
    public GameObject pawnPrefab;
    
    //Game Object the player is actively Controlling
    public GameObject pawn;

    //Component for controlling Character Actions
    public PlayerActions action { private set; get; }

    //Polls for Keystates and returns a struct containing actions (Command Pattern?)
    [Client]
    private PlayerInputState GetInput()
    {
        PlayerInputState inputState = new PlayerInputState();

        //Default ID
        inputState.id = -1;
        //set Duration to current DeltaTime
        inputState.simDuration = Time.deltaTime;
        //Set Forward/Backward Movement States
        inputState.forwardInput = Input.GetAxisRaw("Vertical");
        //Set Left/Right Movement States
        inputState.rightInput = Input.GetAxisRaw("Horizontal");
        //Set Fire Button State
        //inputState.fireInput = Input.GetButton("Space");

        return inputState;
    }

    //Sets this entity to control given GameObject
    private void Possess(GameObject candidate)
    {
        action = candidate.GetComponent<PlayerActions>();
    }

    private void Exorcise()
    {
        pawn = null;
        action = null;
    }
    
    //Called when a Player is assigned a Pawn
    protected virtual void OnPawnAssigned(GameObject assignedPawn)
    {
        Possess(assignedPawn);
    }

    //Called when Player is assigned a new Pawn
    protected virtual void OnPawnChanged(GameObject newPawn)
    {
        Debug.Log("Client receiving new Pawn");
        pawn = newPawn;
        if(newPawn)
        {
            OnPawnAssigned(newPawn);
        }
        else
        {
            OnPawnRemoved();
        }
    }

    //TODO: Do something when players Pawn is removed
    protected virtual void OnPawnRemoved()
    {

    }

    [ClientRpc]
    void RpcAssignPawn(GameObject newPawn)
    {
        pawn = newPawn;
        OnPawnChanged(pawn);
    }

    //Instantiates and sets control of pawn to a player
    [Server]
    private GameObject SpawnPlayer()
    {
        //Get spawn points from Network Manager
        var playerStart = NetworkManager.singleton.GetStartPosition();
        //Spawn Player at Start Point and set Facing
        var babyPawn = Instantiate(pawnPrefab, playerStart.position, playerStart.rotation) as GameObject;

        //Give player control of babyPawn
        Possess(babyPawn);
        

        return babyPawn;
    }

    [ServerCallback]
    void Start()
    {
        pawn = SpawnPlayer();
    }

    [ClientCallback]
    void FixedUpdate()
    {
        if(isLocalPlayer && pawn != null)
        {
            Debug.Log("passing Inputs");
            action.AcceptInput(GetInput());
        }
    }

}
