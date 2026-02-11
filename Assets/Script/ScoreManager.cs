using UnityEngine;
using Unity.Netcode;
using TMPro;

public class ScoreManager : NetworkBehaviour
{
    public static ScoreManager Instance;

    public TextMeshProUGUI scoreText;

    private NetworkVariable<int> score = new NetworkVariable<int>(100);

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        UpdateUI();
        score.OnValueChanged += (oldVal, newVal) => UpdateUI();
    }

    [ServerRpc(RequireOwnership = false)]
    public void DecreaseScoreServerRpc(int amount)
    {
        score.Value -= amount;
    }

    void UpdateUI()
    {
        if (scoreText != null)
            scoreText.text = "Score: " + score.Value;
    }
}
