using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
// adding namespaces
using Unity.Netcode;
// because we are using the NetworkBehaviour class
// NewtorkBehaviour class is a part of the Unity.Netcode namespace
// extension of MonoBehaviour that has functions related to multiplayer
public class PlayerMovement : NetworkBehaviour
{
    // headers are used for cleanliness and easy editing of script variable properties
    // https://docs.unity3d.com/6000.3/Documentation/ScriptReference/HeaderAttribute.html

    //header for adjusting player movement
    [Header("Movement Settings")]
    public float speed = 25.0f; //adjusting movement speed
    public float maxSpeed = 10.0f; //speed clamper max
    public float rotationSpeed = 90f; //turning speed

    //header for adjusting player ability configurations
    [Header("Dashing Settings")]
    public float dashForce = 40f; //power of the squasher's burst
    public float dashCooldown = 1.5f; //time between dashes

    //header for adjusting bullet configuration
    [Header("Bullet Settings")]
    public GameObject cannon; //bullet spawn point
    public GameObject bullet; //bullet prefab
    public float bulletForce = 1500;
    public float shootCooldown = 0.4f; //time between shots
    public float bulletLifetime = 3.0f; //time before bullet is destroyed

    //header for adjusting network configurations
    [Header("Network Settings")]
    public List<Color> colors = new List<Color>(); //list of colors for player identification
    public NetworkVariable<bool> isShooter = new NetworkVariable<bool>(false); //boolean to assign role of player
    private float lastAbilityTime; //time for tracking the last use of assigned ability
    
    //getting the reference to the prefab
    [SerializeField]
    private GameObject spawnedPrefab;
    //save the instantiated prefab
    private GameObject instantiatedPrefab;

    //references for visual
    [Header("References")]
    [SerializeField] private AudioListener audioListener; //camera
    [SerializeField] private Camera playerCamera; //listener

    //variables to get object's rigidbody and transform
    Rigidbody rb;
    Transform t;

    //expression-bodied variables to get device status for conciseness
    //https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/statements-expressions-operators/expression-bodied-members
    private Keyboard kb => Keyboard.current;
    private Mouse m => Mouse.current;

    //Start() - method called right before Update()
    void Start()
    {
        //getting rigidbody (rb) and transform (t) from object components
        rb = GetComponent<Rigidbody>();
        t = GetComponent<Transform>();

        //prevent physics rotation drift
        rb.freezeRotation = true;
    }

    //called when the object is spawned on the network
    public override void OnNetworkSpawn()
    {
        //role assignment
        isShooter.Value = (OwnerClientId % 2 == 0);

        //change color of the mesh based on client id
        GetComponent<MeshRenderer>().material.color = colors[(int)OwnerClientId];

        //check if the player is the owner of the object
        if (!IsOwner)
        {
            if (playerCamera) playerCamera.enabled = false;
            if (audioListener) audioListener.enabled = false;
            return;
        }

        //determine class name
        string myClass = isShooter.Value ? "SHOOTER" : "SQUASHER"; 
        
        //tells the manager to update for client
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.SetPlayerClassUI(myClass);
        }

        //find spawn point in the current game scene
        //prints out debug message if not
        GameObject spawnPoint = GameObject.Find("SpawnPoint");
        if (spawnPoint != null)
        {
            // move transformation to spawn point
            transform.position = spawnPoint.transform.position;
            transform.rotation = spawnPoint.transform.rotation;
        }

        //if the player is the owner of the object
        //enable the camera and the audio listener
        if (playerCamera) playerCamera.enabled = true;
        if (audioListener) audioListener.enabled = true;
    }
    

    //Update() - method called once per frame
    void Update()
    {
        //check if the player is the owner of the object
        //makes sure the script is only executed on the owners and not on other prefabs
        if (!IsOwner) return;

        //grouped core logic
        HandleMovement();
        HandleRotation();
        HandlePrimaryAction();
    }

    //HandleMovement() - handles character movement with speed capping
    void HandleMovement()
    {
        //moveDirection modified to only be a float (this prevents jitter from holding both keys)
        float moveDirection = 0;

        //checking inputs (w - forward, s - backward)
        if (kb != null && kb.wKey.isPressed == true) moveDirection += 1;
        if (kb != null && kb.sKey.isPressed == true) moveDirection -= 1;

        //applying velocity based on facing direction (t.forward)
        rb.linearVelocity += t.forward * moveDirection * speed * Time.deltaTime;

        //speed clamper, prevents character from perpetual acceleration of magnitude within x and z velocity
        Vector3 flatVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        if (flatVelocity.magnitude > maxSpeed)
        {
            //calculates capped velocity to keep at maxSpeed
            //normalized helps with keeping direction while setting magnitude to 1, and multiplying by maxSpeed
            Vector3 clampedVel = flatVelocity.normalized * maxSpeed;
            //re-applying clamped speed, maintains vertical velocity (fall)
            rb.linearVelocity = new Vector3(clampedVel.x, rb.linearVelocity.y, clampedVel.z);
        }
    }

    //HandleRotation() - handles character rotation from player input
    void HandleRotation()
    {
        //initialize local rotation variable 
        float rotDir = 0;

        //checking inputs (a - turn left, d - turn right)
        if (kb != null && kb.dKey.isPressed == true) rotDir = 1;
        if (kb != null && kb.aKey.isPressed == true) rotDir = -1;

        //update rotation by the y axis (Vector3.up)
        t.Rotate(Vector3.up, rotDir * rotationSpeed * Time.deltaTime);
    }

    //HandlePrimaryAction() - handles abilities assigned to player (lmb - shoot or dash)
    void HandlePrimaryAction()
    {
        if (m == null || !m.leftButton.wasPressedThisFrame) return;

        if (isShooter.Value)
        {   //shooter logic, shoots bullets within cooldown set
            if (Time.time > lastAbilityTime + shootCooldown)
            {
                BulletSpawningServerRpc(cannon.transform.position, cannon.transform.rotation);
                lastAbilityTime = Time.time;
            }
        }
        else
        {   //squasher logic, dashes within cooldown set
            if (Time.time > lastAbilityTime + dashCooldown)
            {
                rb.AddForce(t.forward * dashForce, ForceMode.VelocityChange);
                lastAbilityTime = Time.time;
            }
        }
    }

    [ServerRpc]
    private void BulletSpawningServerRpc(Vector3 position, Quaternion rotation)
    {
        //call the BulletSpawningClientRpc method to locally create the bullet on all clients
        BulletSpawningClientRpc(position, rotation);
    }

    [ClientRpc]
    private void BulletSpawningClientRpc(Vector3 position, Quaternion rotation)
    {
        //creates a new bullet object locally on clients
        GameObject newBullet = Instantiate(bullet, position, rotation);
        
        //getting the new bullet's rigidbody
        Rigidbody bRb = newBullet.GetComponent<Rigidbody>();

        //runs only if a rigidbody is present for bullet
        if (bRb != null)
        {
            //this adjusts bullet velocity to shoot upwards
            bRb.linearVelocity += Vector3.up * 2;
            //this adds force to the bullet forwards
            bRb.AddForce(newBullet.transform.forward * bulletForce);
        }

        //add a collision handler to the bullet dynamically
        //this detects any surface and destroys it immediately
        var collider = newBullet.AddComponent<BulletImpact>();

        //destroys the bullet after a certain amount of time has passed from bulletLifeTime (in seconds)
        //this is cruicial and beneficial for performance (no excess objects in scene)
        Destroy(newBullet, bulletLifetime);
    }

    //nested class to handle the impact logic
    //this stays inside playermovement for easy management of bullet collission
    private class BulletImpact : MonoBehaviour 
    {
        private void OnTriggerEnter(Collider other)
        {
            //destroy the local bullet instance on impact
            Destroy(gameObject);
        }
    }
}