using UnityEngine;
using Unity.Netcode;

public class SpiderPenalty : NetworkBehaviour
{
    public int penaltyPerSecond = 1;

    private float timer = 0f;

    void Update()
    {
        if (!IsServer) return;

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
