using UnityEngine;

namespace Project.Interaction
{
    /// <summary>
    /// Exemplo de arma corpo a corpo: golpe simples via OverlapSphere na frente do
    /// player. Substitua o TODO por uma interface real de dano quando tiver o
    /// sistema de vida/combate pronto - esse esqueleto só resolve a detecção do golpe.
    /// </summary>
    public class MeleeWeaponItem : GrabbableItem
    {
        [SerializeField] private float _attackRange = 1.5f;
        [SerializeField] private float _attackRadius = 0.6f;
        [SerializeField] private int _damage = 10;
        [SerializeField] private float _cooldownSeconds = 0.6f;
        [SerializeField] private LayerMask _hitMask = ~0;

        private float _nextAttackTime;

        protected override void ServerUse()
        {
            if (Time.time < _nextAttackTime) return;
            _nextAttackTime = Time.time + _cooldownSeconds;

            var origin = transform.position + transform.forward * (_attackRange * 0.5f);
            var hits = Physics.OverlapSphere(origin, _attackRadius, _hitMask);

            foreach (var hit in hits)
            {
                // TODO: troque por uma interface real, ex:
                // if (hit.TryGetComponent<IDamageable>(out var d)) d.ApplyDamage(_damage);
                Debug.Log($"[{name}] golpe acertou {hit.name} (dano placeholder: {_damage})");
            }
        }
    }
}
