using UnityEngine;
using System.Collections;

public class PlayerController : MonoBehaviour {

    public Rigidbody playerRigidbody;

    private float horizontalInput;
    private Vector3 playerAcceleration;
    private Vector3 playerVelocity;
    private Vector3 gravity;

    private float Speed;
    public float speed { get { return Speed; } set { Speed = value; } }

	// Use this for initialization
	void Start ()
    {
        playerRigidbody = this.GetComponent<Rigidbody>();
        speed = 10;
	}

    void FixedUpdate ()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");

        playerAcceleration = new Vector3(horizontalInput * speed / playerRigidbody.mass, 0, 0);
        playerAcceleration.y -= 9.8f;
        playerVelocity = playerAcceleration;

        moveHorizontal();
    }

    void moveHorizontal()
    {
        playerRigidbody.velocity = playerVelocity;
    }
}
