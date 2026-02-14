using UnityEngine;
using Unity.Netcode;
using TMPro;

public class ScoreManager : NetworkBehaviour
{
    public static ScoreManager Instance;

    //header for adjustments to UI
    [Header("UI References")]
    public TextMeshProUGUI classText;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI statusText; 
    public TextMeshProUGUI brownSpiderText;
    public TextMeshProUGUI greenSpiderText; // new reference for green spiders

    private NetworkVariable<int> score = new NetworkVariable<int>(100);
    private NetworkVariable<float> timeRemaining = new NetworkVariable<float>(90f);

    public bool isGameStarted = false;
    public bool gameEnded = false;
    private float nextCheckTime;

    //Awake() - set singleton instance
    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    //OnNetworkSpawn() - find refs, force visibility, and sync initial state for late joiners
    public override void OnNetworkSpawn()
    {
        //failsafe lookup for all text components
        classText = classText ?? FindText("ClassText");
        scoreText = scoreText ?? FindText("ScoreText");
        timerText = timerText ?? FindText("TimerText");
        statusText = statusText ?? FindText("StatusText");
        brownSpiderText = brownSpiderText ?? FindText("BrownSpiderText");
        greenSpiderText = greenSpiderText ?? FindText("GreenSpiderText");

        //force enable ui elements
        EnableUI(classText, true);
        EnableUI(scoreText, true);
        EnableUI(timerText, true);
        EnableUI(brownSpiderText, true);
        EnableUI(greenSpiderText, true);
        EnableUI(statusText, false); // start hidden

        //set distinct colors for trackers
        if (brownSpiderText) brownSpiderText.color = Color.brown;
        if (greenSpiderText) greenSpiderText.color = Color.green;

        //immediate sync so late joiners see correct score/time instantly
        UpdateScoreDisplay();
        UpdateTimerDisplay();

        //subscribe to changes for real-time updates
        score.OnValueChanged += (oldVal, newVal) => UpdateScoreDisplay();
        timeRemaining.OnValueChanged += (oldVal, newVal) => UpdateTimerDisplay();
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

    //SetPlayerClassUI() - method that sets player's assigned classes
    public void SetPlayerClassUI(string className)
    {
        if (classText != null)
        {
            classText.text = "you are: " + className.ToUpper();
        }
    }

    //UpdateUI() - refresh score text value
    private void UpdateScoreDisplay() { if (scoreText) scoreText.text = "Security: " + score.Value; }

    //UpdateTimerDisplay() - format remaining seconds into 0:00 string
    private void UpdateTimerDisplay()
    {
        if (!timerText) return;
        timerText.text = string.Format("{0}:{1:00}", Mathf.FloorToInt(timeRemaining.Value / 60), Mathf.FloorToInt(timeRemaining.Value % 60));
    }

    //Update() - handle local spider counting and server timer logic
    void Update()
    {
        //safety check: prevent errors when exiting the game
        if (!IsSpawned || NetworkManager.Singleton == null) return;

        //check for at least 2 players to flip the start switch
        if (IsServer && !isGameStarted)
        {
            if (NetworkManager.Singleton.ConnectedClientsList.Count >= 2)
            {
                isGameStarted = true;
            }
        }

        //show placeholder and block logic if match hasn't started
        if (!isGameStarted)
        {
            if (timerText) timerText.text = "??:??";
            return;
        }

        //update total amount of spiders withing scene
        if (!gameEnded && Time.time >= nextCheckTime)
        {
            UpdateSpiderCount();
            nextCheckTime = Time.time + 0.5f;
        }

        //return from loop if no longer updating
        if (!IsServer || gameEnded) return;

        //endgame check, while score is above 0 or time remaining is above 0
        if (score.Value > 0 && timeRemaining.Value > 0)
        {
            timeRemaining.Value -= Time.deltaTime;
            if (timeRemaining.Value <= 0)
            {
                timeRemaining.Value = 0;
                EndGame(true);
            }
        }
    }

    //UpdateSpiderCount() - count both spider types and update trackers
    private void UpdateSpiderCount()
    {
        int brownCount = 0;
        int greenCount = 0;

        //scan all objects once to count both types
        foreach (var obj in GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (obj.name.Contains("spider_brown_AI")) brownCount++;
            else if (obj.name.Contains("spider_green_AI")) greenCount++;
        }
        
        if (brownSpiderText) brownSpiderText.text = "B_spider: " + brownCount;
        if (greenSpiderText) greenSpiderText.text = "G_spider: " + greenCount;
    }

    //EndGame() - clear spiders and trigger win or loss rpc
    private void EndGame(bool win)
    {
        ClearAllSpiders();
        if (win) TriggerWinClientRpc(); 
        else TriggerGameOverClientRpc();
    }

    //ClearAllSpiders() - find and despawn all network spider objects (brown and green)
    private void ClearAllSpiders()
    {
        foreach (var obj in GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            //check for either spider name
            if (obj.name.Contains("spider_brown_AI") || obj.name.Contains("spider_green_AI"))
            {
                var netObj = obj.GetComponent<NetworkObject>();
                if (netObj && netObj.IsSpawned) netObj.Despawn();
                else Destroy(obj);
            }
        }
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

        statusText = statusText ?? FindText("StatusText");
        if (statusText != null)
        {
            statusText.gameObject.SetActive(true);
            statusText.enabled = true;
            statusText.text = "YOU WIN!";
            statusText.color = Color.green;
            
            Invoke("HideStatus", 3f);
        }

        if (scoreText) scoreText.color = Color.green;
        if (timerText) timerText.color = Color.green;
    }

    //TriggerGameOverClientRpc() - show loss message and ensure reference exists
    [ClientRpc]
    private void TriggerGameOverClientRpc()
    {
        gameEnded = true;

        statusText = statusText ?? FindText("StatusText");
        if (statusText != null)
        {
            statusText.gameObject.SetActive(true);
            statusText.enabled = true;
            statusText.text = "YOU LOSE!";
            statusText.color = Color.red;
            
            Invoke("HideStatus", 3f);
        }
        
        // turn all UI red to indicate a loss state
        if (scoreText) scoreText.color = Color.red;
        if (timerText) timerText.color = Color.red;
    }

    //HideStatus() - must be public for invoke to work properly
    public void HideStatus() 
    { 
        if (statusText != null) statusText.gameObject.SetActive(false); 
    }
}