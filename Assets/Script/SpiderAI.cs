using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;

public class SpiderAI : NetworkBehaviour
{
    public Transform target; 
    private NavMeshAgent agent;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        if (IsServer && target != null)
        {
            agent.SetDestination(target.position);
        }
    }

    void Update()
    {
        if (!IsServer) return; 

        if (target != null)
        {
            agent.SetDestination(target.position);
        }
    }
}
