using Mirror;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NetworkLobbyUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private Button beginButton;
    [SerializeField] private Button disconnectButton;
    [SerializeField] private TMP_InputField addressInput;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text playerCountText;
    [SerializeField] private TMP_Dropdown gameModeDropdown;

    private NetworkGameManager networkManager;

    private void Start()
    {
        networkManager = NetworkGameManager.Instance;
        if (networkManager == null)
        {
            networkManager = FindObjectOfType<NetworkGameManager>();
        }

        if (hostButton != null)
            hostButton.onClick.AddListener(OnHostClicked);

        if (joinButton != null)
            joinButton.onClick.AddListener(OnJoinClicked);

        if (disconnectButton != null)
            disconnectButton.onClick.AddListener(OnDisconnectClicked);

        if (beginButton != null)
            beginButton.onClick.AddListener(OnBeginClicked);

        if (addressInput != null)
            addressInput.text = "localhost";

        // Hide begin button initially - only host should see it
        if (beginButton != null)
            beginButton.gameObject.SetActive(false);

        UpdateUI();
    }

    private void Update()
    {
        UpdateUI();
    }

    private void UpdateUI()
    {
        bool isConnected = NetworkClient.isConnected || NetworkServer.active;

        if (lobbyPanel != null)
            lobbyPanel.SetActive(isConnected);

        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(!isConnected);

        if (disconnectButton != null)
            disconnectButton.gameObject.SetActive(isConnected);

        // Only show begin button to host
        if (beginButton != null)
        {
            beginButton.gameObject.SetActive(NetworkServer.active && NetworkClient.isConnected);
        }

        if (statusText != null)
        {
            if (NetworkServer.active && NetworkClient.isConnected)
                statusText.text = "Hosting (Connected)";
            else if (NetworkServer.active)
                statusText.text = "Hosting";
            else if (NetworkClient.isConnected)
                statusText.text = "Connected";
            else
                statusText.text = "Not Connected";
        }

        if (playerCountText != null && NetworkServer.active)
        {
            playerCountText.text = $"Players: {NetworkServer.connections.Count} / {networkManager?.maxPlayers ?? 4}";
        }
    }

    private void OnHostClicked()
    {
        if (networkManager != null)
        {
            networkManager.StartHostGame();
        }
        else
        {
            Debug.LogError("NetworkGameManager not found!");
        }
    }

    private void OnJoinClicked()
    {
        if (networkManager != null)
        {
            string address = addressInput != null ? addressInput.text : "localhost";
            if (string.IsNullOrEmpty(address))
                address = "localhost";

            networkManager.StartClientGame(address);
        }
        else
        {
            Debug.LogError("NetworkGameManager not found!");
        }
    }

    private void OnDisconnectClicked()
    {
        if (NetworkServer.active)
        {
            networkManager?.StopHostGame();
        }
        else if (NetworkClient.isConnected)
        {
            networkManager?.StopClientGame();
        }
    }

    private void OnBeginClicked()
    {
        if (networkManager != null && NetworkServer.active)
        {
            int selectedGameMode = 0;
            if (gameModeDropdown != null)
            {
                selectedGameMode = gameModeDropdown.value;
            }
            networkManager.BeginGame(selectedGameMode);
        }
    }
}