using UnityEngine;
using System.Collections;
using UnityEngine.Networking;

public class BasicController : NetworkBehaviour {

    public float speed;
    public float jumpForce;
    public bool canMove;

    private Rigidbody thisRB;

    private float horInput = 0;
    private float verInput = 0;

    private Vector3 playerForce;

	// Use this for initialization
	void Start ()
    {
        speed = 30;
        jumpForce = 30;
        canMove = true;

        thisRB = this.GetComponent<Rigidbody>();
	}
	
	// Update is called once per frame
	void FixedUpdate ()
    {
        if(!isLocalPlayer)
        {
            return;
        }

        horInput = Input.GetAxisRaw("Horizontal");
        verInput = Input.GetAxisRaw("Vertical");

        if (canMove)
        {
            handleInput();
        }
    }

    void handleInput()
    {
        playerForce = new Vector3(horInput * speed, verInput * jumpForce, 0);

        thisRB.AddForce(playerForce);
    }
}
