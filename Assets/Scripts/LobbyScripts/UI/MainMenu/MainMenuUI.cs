using UnityEngine;

namespace Project.UI.MainMenu
{
    /// <summary>
    /// Só troca entre os painéis (Root / Criar Sala / Entrar em Sala).
    /// Mantém a lógica de UI separada da lógica de rede - esse script não sabe
    /// nada sobre Netcode, só liga/desliga GameObjects.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        [SerializeField] private GameObject _rootPanel;
        [SerializeField] private GameObject _createRoomPanel;
        [SerializeField] private GameObject _joinRoomPanel;

        private void Start()
        {
            ShowRoot();
        }

        public void ShowRoot()
        {
            SetActivePanel(_rootPanel);
        }

        public void ShowCreateRoom()
        {
            SetActivePanel(_createRoomPanel);
        }

        public void ShowJoinRoom()
        {
            SetActivePanel(_joinRoomPanel);
        }

        private void SetActivePanel(GameObject target)
        {
            _rootPanel.SetActive(target == _rootPanel);
            _createRoomPanel.SetActive(target == _createRoomPanel);
            _joinRoomPanel.SetActive(target == _joinRoomPanel);
        }
    }
}
