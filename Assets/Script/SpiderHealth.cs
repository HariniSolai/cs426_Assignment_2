using UnityEngine;
using Unity.Netcode;

public class SpiderHealth : NetworkBehaviour
{
    [SerializeField] private int scoreValue = 10; //points given from spider after defeat

    private void OnTriggerEnter(Collider other)
    {
        //only server handles killing authority
        if (!IsServer) return;

        //bullet detection by name
        if (other.gameObject.name.Contains("Bullet")){
            DestroySpiderServerRpc();
            return;
        }

        //find player script on object or parent
        PlayerMovement player = other.gameObject.GetComponentInParent<PlayerMovement>();

        if (player != null){
            //check if squasher role
            if (!player.isShooter){
                Debug.Log($"spider squashed by {other.gameObject.name}");
                DestroySpiderServerRpc();
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void DestroySpiderServerRpc()
    {
        //check if already despawning
        if (!IsSpawned) return;

        //increase score based on spider's value
        ScoreManager.Instance.IncreaseScoreServerRpc(scoreValue);

        //remove from network and delete
        GetComponent<NetworkObject>().Despawn();
        Destroy(gameObject);
    }
}