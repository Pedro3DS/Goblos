using Project.Items;
using Project.Player;
using Unity.Netcode;
using UnityEngine;

namespace Project.Interaction
{
    // ===================== SINGLEPLAYER =====================
    // Pra usar isso num projeto SEM Netcode:
    //   1. "NetworkInteractable" -> "MonoBehaviour" + implemente IInteractable direto
    //      (CanInteract/InteractionPrompt continuam iguais).
    //   2. Apague os atributos [Rpc(SendTo...)] e o "Request" dos nomes:
    //      RequestDropRpc() vira Drop(), RequestThrowRpc(...) vira Throw(...),
    //      RequestUseRpc() vira Use() - chamados direto pelo input, sem passar por servidor.
    //   3. NetworkObject.ChangeOwnership/TrySetParent -> transform.SetParent comum.
    //   4. NetworkVariable<T> -> campo T comum. As *ClientRpc viram só chamadas
    //      normais dos métodos OnGrabbed/OnDropped/OnThrown.
    // ==========================================================

    /// <summary>
    /// Item que pode ser pego, largado, arremessado e "usado" (lanterna, feitiço,
    /// golpe corpo a corpo - ver ServerUse e as subclasses de exemplo). Reaproveita
    /// o fluxo de interação que já existe (RequestInteractRpc) só pra PEGAR; drop,
    /// throw e use têm RPCs próprios porque não dependem de mirar+E, dependem de já
    /// estar segurando o item.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class GrabbableItem : NetworkInteractable, IGrabbable
    {
        [SerializeField] private ItemDefinition _definition;
        public ItemDefinition Definition => _definition;

        private readonly NetworkVariable<bool> _isHeld =
            new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<ulong> _holderClientId =
            new NetworkVariable<ulong>(ulong.MaxValue, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public bool IsHeld => _isHeld.Value;
        public ulong HolderClientId => _holderClientId.Value;

        private Rigidbody _rb;
        private Collider[] _colliders;

        public override string InteractionPrompt => IsHeld ? string.Empty : $"Pegar {_definition.DisplayName}";

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _colliders = GetComponentsInChildren<Collider>();
        }

        public override bool CanInteract(ulong interactingClientId) => !IsHeld;

        protected override void OnInteract(ulong interactingClientId)
        {
            if (!NetworkManager.ConnectedClients.TryGetValue(interactingClientId, out var client)) return;
            if (client.PlayerObject == null) return;

            var hands = client.PlayerObject.GetComponent<PlayerHandsController>();
            if (hands == null) return;

            if (!hands.ServerTryGrab(this))
            {
                Debug.Log($"[{name}] Client {interactingClientId} tentou pegar mas as mãos estão cheias.");
            }
        }

        /// <summary>Servidor apenas. Chamado pelo PlayerHandsController depois de validar que há slot de mão livre.</summary>
        public void ServerGrab(Transform handSocket, ulong holderClientId)
        {
            _isHeld.Value = true;
            _holderClientId.Value = holderClientId;

            NetworkObject.ChangeOwnership(holderClientId);
            NetworkObject.TrySetParent(handSocket, worldPositionStays: false);

            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            _rb.isKinematic = true;
            SetCollidersEnabled(false);

            NotifyGrabbedClientRpc();
        }

        [Rpc(SendTo.Everyone)]
        private void NotifyGrabbedClientRpc() => OnGrabbed(transform.parent);

        [Rpc(SendTo.Server)]
        public void RequestDropRpc()
        {
            if (!IsHeld) return;
            ServerRelease(null);
        }

        [Rpc(SendTo.Server)]
        public void RequestThrowRpc(float chargeFraction01, Vector3 direction)
        {
            if (!IsHeld || !_definition.IsThrowable) return;

            float t = Mathf.Clamp01(chargeFraction01);
            float force = Mathf.Lerp(_definition.ThrowForceMin, _definition.ThrowForceMax, t);
            ServerRelease(direction.normalized * force);
        }

        [Rpc(SendTo.Server)]
        public void RequestUseRpc()
        {
            if (!IsHeld) return;
            ServerUse();
        }

        /// <summary>
        /// Servidor apenas. Sobrescreva numa subclasse pra dar efeito ao "uso" do item -
        /// ver FlashlightItem (liga/desliga luz), SpellObjectItem (lança feitiço) e
        /// MeleeWeaponItem (golpe) como exemplos.
        /// </summary>
        protected virtual void ServerUse() { }

        /// <summary>Servidor apenas. throwImpulse == null significa "largar no chão", com valor significa arremesso.</summary>
        private void ServerRelease(Vector3? throwImpulse)
        {
            ulong previousHolder = _holderClientId.Value;
            var dropPosition = transform.position;
            var dropRotation = transform.rotation;

            NetworkObject.TrySetParent((Transform)null, worldPositionStays: true);

            _rb.isKinematic = false;
            SetCollidersEnabled(true);

            if (throwImpulse.HasValue)
            {
                _rb.AddForce(throwImpulse.Value, ForceMode.VelocityChange);
            }

            _isHeld.Value = false;
            _holderClientId.Value = ulong.MaxValue;
            NetworkObject.RemoveOwnership();

            // Avisa o PlayerHandsController de quem estava segurando pra ele liberar o slot de mão.
            if (NetworkManager.ConnectedClients.TryGetValue(previousHolder, out var client) && client.PlayerObject != null)
            {
                var hands = client.PlayerObject.GetComponent<PlayerHandsController>();
                hands?.ServerClearHandSlot(this);
            }

            NotifyReleasedClientRpc(dropPosition, dropRotation, throwImpulse ?? Vector3.zero, throwImpulse.HasValue);
        }

        [Rpc(SendTo.Everyone)]
        private void NotifyReleasedClientRpc(Vector3 position, Quaternion rotation, Vector3 impulse, bool wasThrown)
        {
            if (wasThrown) OnThrown(impulse);
            else OnDropped(position, rotation);
        }

        private void SetCollidersEnabled(bool state)
        {
            foreach (var c in _colliders) c.enabled = state;
        }

        // ---- IGrabbable: sobrescreva numa subclasse se quiser som/VFX/animação extra ----
        public virtual void OnGrabbed(Transform handSocket) { }
        public virtual void OnDropped(Vector3 position, Quaternion rotation) { }
        public virtual void OnThrown(Vector3 impulse) { }
    }
}
