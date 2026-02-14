using UnityEngine;
using Unity.Netcode;
using UnityEngine.AI;

public class SpiderSpawner : NetworkBehaviour
{
    [SerializeField] private GameObject spiderPrefab;  //spider prefab to spawn
    
    //headers for retrieving boxcolider reference on spawning area
    [Header("Area References")]
    [SerializeField] private BoxCollider spawnArea; //box used to define the total spawnable area
    [SerializeField] private BoxCollider safeZone; //box used to block spiders from spawning (server room)

    [Header("Settings")]
    [SerializeField] private float spawnDelay = 3f; //cooldown between spawns
    
    private Bounds spawnBounds;
    private Bounds safeBounds;
    private float lastSpawn;

    public override void OnNetworkSpawn()
    {
        //get the area data then kill the collider so bullets pass through
        spawnBounds = spawnArea.bounds;
        spawnArea.enabled = false; 

        //do the same for the safe zone
        safeBounds = safeZone.bounds;
        safeZone.enabled = false; 
    }

    void Update()
    {
        //only server handles spawning
        if (!IsServer) return;


        //check if the network manager still exists (prevents error on exit)
        if (NetworkManager.Singleton == null) return;

        //check for players and timer
        if (NetworkManager.Singleton.ConnectedClientsList.Count >= 1)
        {
            if (ScoreManager.Instance.gameEnded) return;
            if (Time.time > lastSpawn + spawnDelay)
            {
                Spawn();
                lastSpawn = Time.time;
            }
        }
    }

    //Spawn() - function to spawn spiders based on a random valid point within the spawnBounds
    void Spawn()
    {

        //pick a random spot inside the captured box boundaries
        Vector3 randomPoint = new Vector3(
            Random.Range(spawnBounds.min.x, spawnBounds.max.x),
            spawnBounds.center.y,
            Random.Range(spawnBounds.min.z, spawnBounds.max.z)
        );

        //try to find the ground on the navmesh
        if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            //if the spot is inside the safe room box, cancel
            if (safeZone != null && safeBounds.Contains(hit.position)) return;

            //instantiate and sync across network
            GameObject spider = Instantiate(spiderPrefab, hit.position, Quaternion.identity);
            spider.GetComponent<NetworkObject>().Spawn();
        }
    }

    //OnDrawGizmos() - this helps "outline" the properties for the spawn/safe zones
    //https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Gizmos.html
    private void OnDrawGizmos()
    {
        //draw the red area for spawning to occur
        if (spawnArea != null)
        {
            Gizmos.color = new Color(1, 0, 0, 0.25f);
            Gizmos.DrawCube(spawnArea.bounds.center, spawnArea.bounds.size);
        }

        //draw the green area for no spawning to occur
        if (safeZone != null)
        {
            Gizmos.color = new Color(0, 1, 0, 0.25f);
            Gizmos.DrawCube(safeZone.bounds.center, safeZone.bounds.size);
        }
    }
}