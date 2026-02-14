using UnityEngine;
using Unity.Netcode;

public class SpiderPenalty : NetworkBehaviour
{
    public bool ignoreRoomBounds = false; //true for green, false for brown
    public int penaltyPerSecond = 1;

    private float timer = 0f;

    void Update()
    {
        if (!IsServer) return;

        // check if we should apply penalty
        if (CanDeductPoints())
        {
            timer += Time.deltaTime;

            if (timer >= 1f)
            {
                timer = 0f;

                if (ScoreManager.Instance != null)
                {
                    ScoreManager.Instance.DecreaseScoreServerRpc(penaltyPerSecond);
                }
            }
        }
    }

    //CanDeductPoints() - logic to determine if the spider is "active"
    private bool CanDeductPoints()
    {
        //green spiders always deduct points
        if (ignoreRoomBounds) return true;

        //brown spiders only deduct if they are inside the safe bounds defined in spawner
        if (SpiderSpawner.Instance != null)
        {
            return SpiderSpawner.Instance.GetSafeBounds().Contains(transform.position);
        }

        return false;
    }
}
