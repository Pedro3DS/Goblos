using Project.Lobby;
using UnityEngine;

namespace Project.Interaction
{
    /// <summary>
    /// Fica no objeto da cama de palha. Só o dono daquela cama consegue deitar nela.
    /// A lógica de posicionamento fica no LobbyManager, aqui só validamos e delegamos.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BedInteractable : NetworkInteractable
    {
        [SerializeField] private LobbyBedSpawnPoint _bedSpawnPoint;

        public LobbyBedSpawnPoint BedSpawnPoint => _bedSpawnPoint;

        public override string InteractionPrompt => "Deitar / Levantar";

        public override bool CanInteract(ulong interactingClientId)
        {
            if (LobbyManager.Instance == null || _bedSpawnPoint == null) return false;
            return LobbyManager.Instance.IsBedOwner(_bedSpawnPoint.BedIndex, interactingClientId);
        }

        protected override void OnInteract(ulong interactingClientId)
        {
            LobbyManager.Instance.ToggleLyingDown(interactingClientId);
        }

        private void OnValidate()
        {
            if (_bedSpawnPoint == null)
                _bedSpawnPoint = GetComponent<LobbyBedSpawnPoint>();
        }
    }
}
