using UnityEngine;
using UnityEngine.Networking;
using System.Collections.Generic;

public struct PlayerActionState
{
    //Position/Orientation of Players pawn/entity
    public Vector3 pawnPosition;
    public Vector3 pawnForward;

    //Current Game Time as validated by the Server
    public float timeStamp;

    //Checks to see if the PlayerActionState instance is sent from Server (timeStamp is an actual value vs NegativeInfinity) ||Supposedly||
    public bool isFromServer { get { return float.IsNegativeInfinity(timeStamp); } }

    //Syncs the Client Transform with the Server Transform
    public PlayerActionState(Vector3 position, Vector3 forward)
    {
        this.pawnPosition = position;
        this.pawnForward = forward;
        timeStamp = float.NegativeInfinity;
    }

    //Applies the servers changes to the Pawns Rigidbody
    public void applyChanges(Rigidbody targetRB)
    {
        targetRB.position = pawnPosition;
        targetRB.rotation = Quaternion.LookRotation(pawnForward, Vector3.up);
    }

    //Interpolates between the previous state and the new state for smoothed movement
    public static PlayerActionState Lerp(PlayerActionState start, PlayerActionState end, float alpha)
    {
        PlayerActionState lerpState = new PlayerActionState(Vector3.Lerp(start.pawnPosition, end.pawnPosition, alpha), Vector3.Lerp(start.pawnForward, end.pawnForward, alpha));
        return lerpState;
    }
}

public class PlayerActions : NetworkBehaviour
{
    //Speed of the player
    private float speed = 10f;

    //Rigidbody that this acts upon
    [SerializeField]
    private Rigidbody attachedRigidboy;

    //Commands sent to server from client
    private Queue<PlayerInputState> serverCommandHistory = new Queue<PlayerInputState>();

    //Last Valid state reported by Server
    public PlayerActionState serverState;

    //The ID of the last command acknowledged by Server
    private int lastAcknowledgedCommandID = -1;

    //Number of state updates received from the server for this character
    private int clientStateCounter = 0;

    //Retains a circular buffer of states from the server so that actions can be undone etc.
    private PlayerActionState[] clientStateHistory;

    //Size of the clientStateHistory buffer
    private const int clientStateHistorySize = 40;

    //Number of commands issued by the client
    private int clientCommandCounter = 0;

    //List of Commands issued by this Client
    private PlayerInputState[] clientCommandHistory;

    //Size of clientCommandHistory
    private const int clientCommandHistorySize = 10;

    //Time to interpolate State Updates for this character in Seconds
    [SerializeField]
    private float clientStateInterpolationTime = 0.1f;

    //TODO: Find out what a DELEGATE Type is
    private delegate void ServerAcknowledgeDelegate(PlayerActionState newState, int newAckCmdID);

    //Invoked by the server and executed on the client whenever the server acknowledges a new State
    [SyncEvent(channel = 2)]
    private event ServerAcknowledgeDelegate EventServerAck;

    //Callback for when the Player has processed Commands and provided an Authoritative State
    private void OnServerAck(PlayerActionState newState, int newAckCmdID)
    {
        //Set current serverState to new and set the lastAcknowledgedCommandID to the current one
        serverState = newState;
        lastAcknowledgedCommandID = newAckCmdID;

        //Put the new state into our clientStateHistory and increment the size of the array
        clientStateHistory[clientStateCounter++ % clientStateHistorySize] = newState;
    }

    //Sends inputs to server for simulation in the authoratative game state
    [Command(channel = 0)]
    private void CmdUploadInput(PlayerInputState input)
    {
        serverCommandHistory.Enqueue(input);
    }

    //Give Input an ID and add to circular Buffer
    [Client]
    public void AcceptInput(PlayerInputState input)
    {
        //Determine position in circular buffer
        int index = clientCommandCounter % clientCommandHistorySize;

        //Assign an ID for the command
        input.id = clientCommandCounter++;
        clientCommandHistory[index] = input;
        CmdUploadInput(input);
    }

    public PlayerActionState Simulate(PlayerActionState startState, PlayerInputState input)
    {
        var newState = startState;

        newState.pawnPosition.x += input.rightInput * input.simDuration * speed;
        newState.pawnPosition.y += input.forwardInput * input.simDuration * speed;

        return newState;
    }

    [Server]
    public PlayerActionState GetSimulatedState()
    {
        PlayerInputState workingCmd;
        PlayerActionState workingState = serverState;

        while (serverCommandHistory.Count > 0)
        {
            workingCmd = serverCommandHistory.Peek();
            workingState = Simulate(workingState, workingCmd);

            lastAcknowledgedCommandID = workingCmd.id;
            workingState.timeStamp = GameMode.singleton.serverGameTime;

            //Clear Queue for next loop
            serverCommandHistory.Dequeue();
        }

        return workingState;
    }

    [Client]
    public PlayerActionState GetPredictedState()
    {
        if(!hasAuthority) { Debug.LogWarning("A non-authoratative client is trying to perform Prediction on this character"); }

        //Last server authoratative state
        var workingState = serverState;

        //First command the server hasn't processed
        var workingCmdIndex = (lastAcknowledgedCommandID + 1);

        //RTC: Ensure sufficient number of commands have been buffered
        if(clientCommandCounter - workingCmdIndex > clientCommandHistorySize)
        {
            Debug.LogWarning("Number of commands needed for local predicition exceed the number of commands kept in buffer");
        }

        //iterate through and simulate each command locally
        while(workingCmdIndex < clientCommandCounter)
        {
            var curCmd = clientCommandHistory[workingCmdIndex % clientCommandHistorySize];
            var newState = Simulate(workingState, curCmd);

            curCmd.clientReceived = true;

            //prep for next command
            workingCmdIndex++;
            workingState = newState;
        }

        return workingState;
    }

    [Client]
    public PlayerActionState GetInterpolatedState()
    {
        if(hasAuthority) { Debug.LogWarning("A Client is trying to interpolate a character it has authority to predict"); }

        //Start the second to last update received
        int workingStateIndex = clientStateCounter - 1;
        float targetTime = GameMode.singleton.serverGameTime - clientStateInterpolationTime;

        //Search recent commands for a pair of stat updates that encompass' target time
        while (workingStateIndex > clientStateCounter - clientStateHistorySize - 1 && workingStateIndex > -1)
        {
            //Setup the "Old" state
            var oldState = clientStateHistory[workingStateIndex % clientStateHistorySize];

            //Ignore if this is newer than target time
            if(oldState.timeStamp > targetTime)
            {
                workingStateIndex--;
                continue;
            }

            //Prep the new state
            var newState = clientStateHistory[(workingStateIndex + 1) % clientStateHistorySize];

            //Confirm the new state is newer than the old one
            Debug.Assert(newState.timeStamp > oldState.timeStamp, "Newer game state is tagged at a later time than the old state");

            //Generate an Alpha Time for Interpolation
            float alpha = (targetTime - oldState.timeStamp) / (newState.timeStamp - oldState.timeStamp);

            return PlayerActionState.Lerp(oldState, newState, alpha);
        }

        return serverState;
    }

    public PlayerActionState GetFinalState()
    {
        return (isServer     ? GetSimulatedState() :
                hasAuthority ? GetPredictedState() :
                               GetInterpolatedState());
    }

    //setup Server State on start
    public override void OnStartServer()
    {
        base.OnStartServer();
        serverState = new PlayerActionState(transform.position, transform.forward);
    }
    public override void OnStartClient()
    {
        clientCommandHistory = new PlayerInputState[clientCommandHistorySize];
        clientStateHistory = new PlayerActionState[clientCommandHistorySize];
        EventServerAck += OnServerAck;
    }

    //Initializes bookkeeping structures
    [ClientCallback]
    void Start()
    {
        if(isClient)
        {
            // lol no see OnStartClient();
        }
    }

    //Update the state of player object
    void FixedUpdate()
    {
        var state = GetFinalState();

        if(isServer) { serverState = state;  EventServerAck(state, lastAcknowledgedCommandID); }

        state.applyChanges(attachedRigidboy);
    }

    void Reset()
    {
        attachedRigidboy = GetComponent<Rigidbody>();
    }

    void OnValidate()
    {
        clientStateInterpolationTime = Mathf.Clamp(clientStateInterpolationTime, 0.1f, Mathf.Infinity);
    }
}
