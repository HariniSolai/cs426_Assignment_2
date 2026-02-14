using UnityEngine;
using Unity.Netcode;
using TMPro;

public class ScoreManager : NetworkBehaviour
{
    public static ScoreManager Instance;

    //header for adjustments to UI
    [Header("UI References")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI statusText; 
    public TextMeshProUGUI spiderTrackerText;

    private NetworkVariable<int> score = new NetworkVariable<int>(100);
    private NetworkVariable<float> timeRemaining = new NetworkVariable<float>(90f);
    public bool gameEnded = false;
    private float nextCheckTime;

    //Awake() - set singleton instance
    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    //OnNetworkSpawn() - find refs and force them to be visible
    public override void OnNetworkSpawn()
    {
        // try to find them if slots are empty
        scoreText = scoreText ?? FindText("ScoreText");
        timerText = timerText ?? FindText("TimerText");
        statusText = statusText ?? FindText("StatusText");
        spiderTrackerText = spiderTrackerText ?? FindText("SpiderTrackerText");

        // force the gameobjects to be active so we can see the text
        EnableUI(scoreText, true);
        EnableUI(timerText, true);
        EnableUI(spiderTrackerText, true);
        EnableUI(statusText, false); // keep status hidden at start

        if (spiderTrackerText) spiderTrackerText.color = Color.red;

        UpdateUI();
        score.OnValueChanged += (oldVal, newVal) => UpdateUI();
    }

    //FindText() - helper to find tmp components by object name
    private TextMeshProUGUI FindText(string n) 
    {
        GameObject o = GameObject.Find(n); 
        return o ? o.GetComponent<TextMeshProUGUI>() : null; 
    }

    //EnableUI() - helper to activate both the object and the component
    private void EnableUI(TextMeshProUGUI text, bool state)
    {
        if (text != null)
        {
            text.gameObject.SetActive(state);
            text.enabled = state;
        }
    }

    //UpdateUI() - refresh score text value
    private void UpdateUI() { if (scoreText) scoreText.text = "score: " + score.Value; }

    //Update() - handle local spider counting and server timer logic
    void Update()
    {
        if (!gameEnded && Time.time >= nextCheckTime)
        {
            UpdateSpiderCount();
            nextCheckTime = Time.time + 0.5f;
        }

        if (!IsServer || gameEnded) return;

        if (score.Value > 0 && timeRemaining.Value > 0)
        {
            timeRemaining.Value -= Time.deltaTime;
            if (timeRemaining.Value <= 0)
            {
                timeRemaining.Value = 0;
                EndGame(true);
            }
        }
        UpdateTimerDisplay();
    }

    //UpdateSpiderCount() - count objects by name and update tracker ui
    private void UpdateSpiderCount()
    {
        if (!spiderTrackerText) return;
        int count = 0;
        foreach (var obj in GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            if (obj.name.Contains("spider_brown_AI")) count++;
        
        spiderTrackerText.text = "B_spider: " + count;
    }

    //EndGame() - clear spiders and trigger win or loss rpc
    private void EndGame(bool win)
    {
        ClearAllSpiders();
        if (win) TriggerWinClientRpc(); 
        else TriggerGameOverClientRpc();
    }

    //ClearAllSpiders() - find and despawn all network spider objects
    private void ClearAllSpiders()
    {
        foreach (var obj in GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (obj.name.Contains("spider_brown_AI"))
            {
                var netObj = obj.GetComponent<NetworkObject>();
                if (netObj && netObj.IsSpawned) netObj.Despawn();
                else Destroy(obj);
            }
        }
    }

    //UpdateTimerDisplay() - format remaining seconds into 0:00 string
    private void UpdateTimerDisplay()
    {
        if (!timerText) return;
        timerText.text = string.Format("{0}:{1:00}", Mathf.FloorToInt(timeRemaining.Value / 60), Mathf.FloorToInt(timeRemaining.Value % 60));
    }

    //IncreaseScoreServerRpc() - add to score if game is active
    [ServerRpc(RequireOwnership = false)]
    public void IncreaseScoreServerRpc(int amount) { if (!gameEnded) score.Value += amount; }

    //DecreaseScoreServerRpc() - subtract from score and check for loss
    [ServerRpc(RequireOwnership = false)]
    public void DecreaseScoreServerRpc(int amount)
    {
        if (gameEnded) return;
        score.Value -= amount;
        if (score.Value <= 0)
        {
            score.Value = 0;
            EndGame(false);
        }
    }

    //TriggerWinClientRpc() - show win message and ensure reference exists
    [ClientRpc]
    private void TriggerWinClientRpc()
    {
        gameEnded = true;

        //ensure statusText is found before proceeding
        statusText = statusText ?? FindText("StatusText");
        if (statusText != null)
        {
            statusText.gameObject.SetActive(true);
            statusText.enabled = true;
            statusText.text = "YOU WIN!";
            statusText.color = Color.green;
            
            Invoke("HideStatus", 3f);
        }

        if (timerText) timerText.color = Color.green;
    }

    //TriggerGameOverClientRpc() - show loss message and ensure reference exists
    [ClientRpc]
    private void TriggerGameOverClientRpc()
    {
        gameEnded = true;

        //ensure statusText is found before proceeding
        statusText = statusText ?? FindText("StatusText");
        if (statusText != null)
        {
            statusText.gameObject.SetActive(true);
            statusText.enabled = true;
            statusText.text = "YOU LOSE!";
            statusText.color = Color.red;
            
            Invoke("HideStatus", 3f);
        }
        
        if (scoreText) scoreText.color = Color.red;
    }
}