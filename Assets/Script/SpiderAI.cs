using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;

public class SpiderAI : NetworkBehaviour
{
    private NavMeshAgent agent;

    void Awake()
    {
        //get reference to agent
        agent = GetComponent<NavMeshAgent>();
    }

    public override void OnNetworkSpawn()
    {
        //only server logic
        if (!IsServer) return;

        //find target and set destination once
        GameObject target = GameObject.Find("ServerRoomTarget");
        if (target != null)
        {
            agent.SetDestination(target.transform.position);
        }
    }
}