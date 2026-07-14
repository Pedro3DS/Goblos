using Project.Connection;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace Project.UI.MainMenu
{
    /// <summary>
    /// Painel "Entrar em Sala": campo de código + nome, chama ConnectionManager.StartClient.
    /// A troca pra cena de Lobby acontece automaticamente via NetworkSceneManager
    /// quando o host der o LoadLobbyScene() do lado dele.
    /// </summary>
    public class JoinRoomPanelUI : MonoBehaviour
    {
        [SerializeField] private TMP_InputField _joinCodeInput;
        [SerializeField] private TMP_InputField _playerNameInput;
        [SerializeField] private GameObject _loadingIndicator;
        [SerializeField] private TMP_Text _errorText;

        public void OnJoinButtonPressed()
        {
            var code = _joinCodeInput.text.Trim().ToUpperInvariant();

            if (string.IsNullOrEmpty(code))
            {
                if (_errorText != null) _errorText.text = "Digite um código válido.";
                return;
            }

            if (_errorText != null) _errorText.text = string.Empty;
            if (_loadingIndicator != null) _loadingIndicator.SetActive(true);

            NetworkManager.Singleton.OnClientConnectedCallback += HandleConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleDisconnected;
            ConnectionManager.Instance.OnConnectionFailed += HandleFailed;

            var playerName = string.IsNullOrWhiteSpace(_playerNameInput.text)
                ? "Player"
                : _playerNameInput.text.Trim();

            ConnectionManager.Instance.StartClient(code, playerName);
        }

        private void HandleConnected(ulong clientId)
        {
            if (clientId != NetworkManager.Singleton.LocalClientId) return;

            Unsubscribe();
            if (_loadingIndicator != null) _loadingIndicator.SetActive(false);
            // A cena muda sozinha quando o servidor chamar LoadLobbyScene().
        }

        private void HandleDisconnected(ulong clientId)
        {
            if (clientId != NetworkManager.Singleton.LocalClientId) return;

            Unsubscribe();
            if (_loadingIndicator != null) _loadingIndicator.SetActive(false);
            if (_errorText != null) _errorText.text = "Não foi possível entrar na sala. Verifique o código.";
        }

        private void HandleFailed(string reason)
        {
            Unsubscribe();
            if (_loadingIndicator != null) _loadingIndicator.SetActive(false);
            if (_errorText != null) _errorText.text = reason;
        }

        private void Unsubscribe()
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleDisconnected;
            ConnectionManager.Instance.OnConnectionFailed -= HandleFailed;
        }
    }
}
