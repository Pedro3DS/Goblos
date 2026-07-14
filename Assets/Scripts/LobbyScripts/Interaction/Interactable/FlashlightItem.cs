using Unity.Netcode;
using UnityEngine;

namespace Project.Interaction
{
    /// <summary>
    /// Exemplo de item com "uso" além de pegar/largar/arremessar: alterna a luz.
    /// O input de usar (clique) já está ligado no PlayerHandsController, que chama
    /// heldItem.RequestUseRpc() - esse método vem de GrabbableItem.
    /// </summary>
    public class FlashlightItem : GrabbableItem
    {
        [SerializeField] private Light _light;

        private readonly NetworkVariable<bool> _isOn =
            new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        protected override void ServerUse()
        {
            _isOn.Value = !_isOn.Value;
        }

        private void OnEnable()
        {
            _isOn.OnValueChanged += HandleIsOnChanged;
            if (_light != null) _light.enabled = _isOn.Value;
        }

        private void OnDisable()
        {
            _isOn.OnValueChanged -= HandleIsOnChanged;
        }

        private void HandleIsOnChanged(bool previous, bool current)
        {
            if (_light != null) _light.enabled = current;
        }
    }
}
