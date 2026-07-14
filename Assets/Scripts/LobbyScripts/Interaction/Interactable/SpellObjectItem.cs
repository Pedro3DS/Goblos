using Unity.Netcode;
using UnityEngine;

namespace Project.Interaction
{
    /// <summary>
    /// Exemplo de item que "usa" pra lançar um feitiço: spawna um prefab de
    /// efeito/projétil na frente do player. Troque o corpo de ServerUse pela sua
    /// lógica real de magia (custo de mana, tipo de feitiço, efeito de área, etc).
    /// </summary>
    public class SpellObjectItem : GrabbableItem
    {
        [SerializeField] private NetworkObject _spellEffectPrefab;
        [SerializeField] private float _launchForce = 12f;
        [SerializeField] private float _cooldownSeconds = 1.5f;

        private float _nextUseTime;

        protected override void ServerUse()
        {
            if (Time.time < _nextUseTime) return;
            if (_spellEffectPrefab == null) return;

            _nextUseTime = Time.time + _cooldownSeconds;

            var origin = transform.position + transform.forward * 0.5f;
            var instance = Instantiate(_spellEffectPrefab, origin, transform.rotation);
            instance.Spawn();

            if (instance.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.AddForce(transform.forward * _launchForce, ForceMode.VelocityChange);
            }

            // TODO: ligue aqui seu sistema de mana/cooldown visual e o efeito do feitiço em si
            // (dano em área, debuff, etc). Esse esqueleto só cuida do spawn + impulso físico.
        }
    }
}
