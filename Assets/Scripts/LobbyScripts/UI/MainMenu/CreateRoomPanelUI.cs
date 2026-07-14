using Project.Connection;
using Project.Save;
using TMPro;
using UnityEngine;

namespace Project.UI.MainMenu
{
    /// <summary>
    /// Painel "Criar Sala": lista os 3 slots, e ao escolher um, inicia o host
    /// (via ConnectionManager) usando aquele slot como sala ativa.
    /// </summary>
    public class CreateRoomPanelUI : MonoBehaviour
    {
        [SerializeField] private SaveSlotButtonUI[] _slotButtons; // arraste os 3 na ordem 0,1,2
        [SerializeField] private TMP_InputField _playerNameInput;
        [SerializeField] private GameObject _loadingIndicator;
        [SerializeField] private TMP_Text _errorText;

        private void OnEnable()
        {
            RefreshSlots();
            if (_errorText != null) _errorText.text = string.Empty;
        }

        private void RefreshSlots()
        {
            var slots = SaveSlotManager.LoadAllSlots();

            for (int i = 0; i < _slotButtons.Length && i < slots.Length; i++)
                _slotButtons[i].Bind(slots[i], OnSlotChosen);
        }

        private void OnSlotChosen(int slotIndex)
        {
            if (_errorText != null) _errorText.text = string.Empty;
            if (_loadingIndicator != null) _loadingIndicator.SetActive(true);

            ConnectionManager.Instance.OnHostReady += HandleHostReady;
            ConnectionManager.Instance.OnConnectionFailed += HandleFailed;

            var playerName = string.IsNullOrWhiteSpace(_playerNameInput.text)
                ? "Host"
                : _playerNameInput.text.Trim();

            ConnectionManager.Instance.StartHost(playerName, slotIndex);
        }

        private void HandleHostReady(string joinCode)
        {
            Unsubscribe();
            if (_loadingIndicator != null) _loadingIndicator.SetActive(false);

            Debug.Log($"[CreateRoomPanelUI] Sala criada. Código: {joinCode}");
            // TODO: exibir o código numa tela de "sala criada" antes de trocar de cena,
            // pra o host poder copiar/mostrar pros amigos.

            ConnectionManager.Instance.LoadLobbyScene();
        }

        private void HandleFailed(string reason)
        {
            Unsubscribe();
            if (_loadingIndicator != null) _loadingIndicator.SetActive(false);
            if (_errorText != null) _errorText.text = reason;
        }

        private void Unsubscribe()
        {
            ConnectionManager.Instance.OnHostReady -= HandleHostReady;
            ConnectionManager.Instance.OnConnectionFailed -= HandleFailed;
        }

        private void OnDisable()
        {
            if (ConnectionManager.Instance != null)
                Unsubscribe();
        }
    }
}
