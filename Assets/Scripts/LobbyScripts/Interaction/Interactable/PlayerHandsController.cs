using Project.Interaction;
using Project.Items;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.Player
{
    // ===================== SINGLEPLAYER =====================
    // "NetworkBehaviour" -> "MonoBehaviour"; NetworkVariable<NetworkObjectReference>
    // -> uma referência direta GrabbableItem; apague "IsOwner" (trate sempre como
    // dono); as chamadas heldItem.RequestDropRpc()/RequestThrowRpc()/RequestUseRpc()
    // viram heldItem.Drop()/Throw(force, direction)/Use() direto (ver nota em
    // GrabbableItem.cs). ServerTryGrab/ServerClearHandSlot perdem o "Server" do nome.
    // ==========================================================

    /// <summary>
    /// Dono do estado "o que está nas mãos do player agora". Cuida de pegar (chamado
    /// pelo GrabbableItem via NetworkInteractable), largar/arremessar (Q) e usar
    /// (clique) o item da mão principal, além de calcular o peso carregado pra
    /// afetar a velocidade de movimento (ver FirstPersonController.SetCarryWeightMultiplier).
    ///
    /// _mainHandSocket/_offHandSocket devem ser os mesmos transforms usados pelo IK
    /// do Animator (ex: TwoBoneIKConstraint "Hand Target") - já que a mão é redonda/
    /// sem dedos, o item só encosta no socket, não precisa de pose de dedo.
    ///
    /// Itens de UmaMao só ocupam a mão principal (ou a secundária se a principal
    /// estiver ocupada); a mão que sobra fica no seu pose de descanso normal do IK.
    /// Itens de DuasMaos são fisicamente parentados só na mão principal, mas o
    /// socket/IK-target da mão secundária é reposicionado a cada frame pra coincidir
    /// com o _secondaryGripPoint do item (ver UpdateOffHandGripFollow) - isso roda
    /// em TODOS os clientes, dono ou não, porque é pose visual, não input.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class PlayerHandsController : NetworkBehaviour
    {
        [Header("Sockets (IK Targets)")]
        [SerializeField] private Transform _mainHandSocket;
        [SerializeField] private Transform _offHandSocket;

        [Header("Referências")]
        [SerializeField] private FirstPersonController _bodyController;
        [Tooltip("Setado automaticamente pelo PlayerSetup (transform da câmera FP do dono). Só o .forward é usado, pra direção de arremesso.")]
        [SerializeField] private Transform _aimTransform;

        [Header("Input")]
        [SerializeField] private InputActionReference _dropThrowKey;
        [SerializeField] private InputActionReference _useKey;
        [Tooltip("Abaixo desse tempo segurando Q, conta como 'largar' em vez de 'arremessar'.")]
        [SerializeField] private float _tapThreshold = 0.15f;

        [Header("Peso")]
        [Tooltip("Peso total (kg) nas mãos a partir do qual a velocidade chega no mínimo.")]
        [SerializeField] private float _weightForMinSpeed = 20f;
        [SerializeField, Range(0.1f, 1f)] private float _minSpeedMultiplier = 0.5f;

        private readonly NetworkVariable<NetworkObjectReference> _mainHandItemRef =
            new NetworkVariable<NetworkObjectReference>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<NetworkObjectReference> _offHandItemRef =
            new NetworkVariable<NetworkObjectReference>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private float _keyHeldTime;
        private bool _isCharging;

        private Vector3 _offHandRestLocalPosition;
        private Quaternion _offHandRestLocalRotation;

        public bool MainHandFree => !TryGetMainHandItem(out _);
        public bool OffHandFree => !TryGetOffHandItem(out _);

        private void Awake()
        {
            // Guarda o pose de descanso original do IK target da mão secundária, pra
            // conseguir voltar pra ele quando não tiver item de duas mãos ocupando-a.
            if (_offHandSocket != null)
            {
                _offHandRestLocalPosition = _offHandSocket.localPosition;
                _offHandRestLocalRotation = _offHandSocket.localRotation;
            }
        }

        /// <summary>Chamado pelo PlayerSetup assim que a câmera do dono é criada.</summary>
        public void SetAimTransform(Transform aimTransform)
        {
            _aimTransform = aimTransform;
        }

        private void OnEnable()
        {
            _mainHandItemRef.OnValueChanged += HandleHandItemChanged;
            _offHandItemRef.OnValueChanged += HandleHandItemChanged;
        }

        private void OnDisable()
        {
            _mainHandItemRef.OnValueChanged -= HandleHandItemChanged;
            _offHandItemRef.OnValueChanged -= HandleHandItemChanged;
        }

        private void Update()
        {
            // Pose visual do IK: precisa rodar em todo mundo, não só no dono, senão
            // os outros clientes veem a mão secundária fora do lugar num item de duas mãos.
            UpdateOffHandGripFollow();

            if (!IsOwner) return;
            HandleDropThrowInput();
            HandleUseInput();
        }

        /// <summary>
        /// Quando as duas mãos seguram o MESMO item (item de duas mãos) e ele expõe
        /// um _secondaryGripPoint, o socket/IK-target da mão secundária se move até
        /// lá. Caso contrário, volta pro pose de descanso original do rig.
        /// </summary>
        private void UpdateOffHandGripFollow()
        {
            if (_offHandSocket == null) return;

            bool isTwoHandedGrip = TryGetMainHandItem(out var main) &&
                                   TryGetOffHandItem(out var off) &&
                                   main == off &&
                                   main.TryGetSecondaryGripPoint(out var secondaryGrip);

            if (isTwoHandedGrip)
            {
                // _offHandSocket.SetPositionAndRotation(secondaryGrip.position, secondaryGrip.rotation);
            }
            else
            {
                _offHandSocket.localPosition = _offHandRestLocalPosition;
                _offHandSocket.localRotation = _offHandRestLocalRotation;
            }
        }

        private void HandleDropThrowInput()
        {
            if (!TryGetMainHandItem(out var heldItem))
            {
                _isCharging = false;
                _keyHeldTime = 0f;
                return;
            }

            if (_dropThrowKey.action.WasPerformedThisFrame())
            {
                _isCharging = true;
                _keyHeldTime = 0f;
            }
            else if (_isCharging && _dropThrowKey.action.IsPressed())
            {
                _keyHeldTime += Time.deltaTime;
            }
            else if (_isCharging && _dropThrowKey.action.WasReleasedThisFrame())
            {
                _isCharging = false;

                if (_keyHeldTime < _tapThreshold || !heldItem.Definition.IsThrowable)
                {
                    heldItem.RequestDropRpc();
                }
                else
                {
                    float chargeFraction = Mathf.Clamp01(_keyHeldTime / heldItem.Definition.MaxChargeTimeSeconds);
                    Vector3 direction = _aimTransform != null ? _aimTransform.forward : transform.forward;
                    heldItem.RequestThrowRpc(chargeFraction, direction);
                }

                _keyHeldTime = 0f;
            }
        }

        void OnDrawGizmos()
        {
            if (_aimTransform != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(_aimTransform.position, _aimTransform.forward * 2f);
            }
        }

        private void HandleUseInput()
        {
            if (!_useKey.action.WasPerformedThisFrame()) return;
            if (TryGetMainHandItem(out var item))
            {
                item.RequestUseRpc();
            }
        }

        // ---------------------------------------------------------------
        // Servidor apenas - chamado por GrabbableItem
        // ---------------------------------------------------------------

        /// <summary>Chamado por GrabbableItem.OnInteract quando o player tenta pegar algo.</summary>
        public bool ServerTryGrab(GrabbableItem item)
        {
            if (item.Definition.HandsRequired == HandRequirement.DuasMaos)
            {
                if (!MainHandFree || !OffHandFree) return false;

                item.ServerGrab(_mainHandSocket, OwnerClientId);
                _mainHandItemRef.Value = item.NetworkObject;
                _offHandItemRef.Value = item.NetworkObject; // mesmo item ocupa as duas mãos
                return true;
            }

            if (MainHandFree)
            {
                item.ServerGrab(_mainHandSocket, OwnerClientId);
                _mainHandItemRef.Value = item.NetworkObject;
                return true;
            }

            if (OffHandFree)
            {
                item.ServerGrab(_offHandSocket, OwnerClientId);
                _offHandItemRef.Value = item.NetworkObject;
                return true;
            }

            return false;
        }

        /// <summary>Chamado por GrabbableItem quando o item é largado/arremessado, pra liberar o slot de mão.</summary>
        public void ServerClearHandSlot(GrabbableItem item)
        {
            if (_mainHandItemRef.Value.TryGet(out var mainObj) && mainObj == item.NetworkObject)
                _mainHandItemRef.Value = default;

            if (_offHandItemRef.Value.TryGet(out var offObj) && offObj == item.NetworkObject)
                _offHandItemRef.Value = default;
        }

        private bool TryGetMainHandItem(out GrabbableItem item)
        {
            item = null;
            if (_mainHandItemRef.Value.TryGet(out var netObj)) item = netObj.GetComponent<GrabbableItem>();
            return item != null;
        }

        private bool TryGetOffHandItem(out GrabbableItem item)
        {
            item = null;
            if (_offHandItemRef.Value.TryGet(out var netObj)) item = netObj.GetComponent<GrabbableItem>();
            return item != null;
        }

        private void HandleHandItemChanged(NetworkObjectReference previous, NetworkObjectReference current)
        {
            RecomputeCarriedWeight();
        }

        private void RecomputeCarriedWeight()
        {
            float weight = 0f;
            bool hasMain = TryGetMainHandItem(out var main);
            bool hasOff = TryGetOffHandItem(out var off);

            if (hasMain) weight += main.Definition.WeightKg;
            if (hasOff && (!hasMain || main != off)) weight += off.Definition.WeightKg;

            float t = _weightForMinSpeed > 0f ? Mathf.Clamp01(weight / _weightForMinSpeed) : 0f;
            float multiplier = Mathf.Lerp(1f, _minSpeedMultiplier, t);

            if (_bodyController != null) _bodyController.SetCarryWeightMultiplier(multiplier);
        }
    }
}