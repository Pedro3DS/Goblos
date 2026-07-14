using StarterAssets;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Player
{
    /// <summary>
    /// Movimento em primeira pessoa estilo Content Warning: o CORPO sempre gira pra
    /// acompanhar pra onde a câmera está olhando (yaw), a câmera cuida do próprio
    /// pitch sozinha via Cinemachine (CinemachinePanTilt no FP_Camera.prefab).
    /// Esse script NUNCA lê mouse/look diretamente - só copia o yaw de _lookReference.
    ///
    /// Só fica ativo no dono (ver PlayerCameraRig, que desliga isso nos outros clientes).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        [Header("Movimento")]
        public float MoveSpeed = 3.0f;
        public float SprintSpeed = 5.5f;
        public float SprintCoolDown;
        public float SprintTime;
        public float SprintTimeMax;
        public Slider SprintTimeSlider;
        public float SpeedChangeRate = 10.0f;

        [Header("Peso Carregado")]
        [Tooltip("Setado por PlayerHandsController conforme o peso dos itens nas mãos. 1 = sem peso, valores menores = mais devagar.")]
        [Range(0.1f, 1f)] public float CarryWeightSpeedMultiplier = 1f;

        [Header("Pulo / Gravidade")]
        public float JumpHeight = 1.2f;
        public float Gravity = -15.0f;
        public float JumpTimeout = 0.5f;
        public float FallTimeout = 0.15f;

        [Header("Chão")]
        public bool Grounded = true;
        public float GroundedOffset = -0.14f;
        public float GroundedRadius = 0.28f;
        public LayerMask GroundLayers;

        [Header("Animação (opcional, visível pros outros players)")]
        [SerializeField] private Animator _animator;

        private Transform _lookReference;
        private CharacterController _controller;
        private StarterAssetsInputs _input;

        private float _speed;
        private float _verticalVelocity;
        private float _jumpTimeoutDelta;
        private float _fallTimeoutDelta;
        private const float _terminalVelocity = 53.0f;
        private float _animationBlend;


        private int _animIDSpeed;
        private int _animIDGrounded;
        private int _animIDJump;
        private int _animIDFreeFall;
        private bool _hasAnimator;
        private int _animIDMotionSpeed;

        /// <summary>Chamado pelo PlayerCameraRig assim que a câmera do dono é criada.</summary>
        public void Initialize(Transform lookReference)
        {
            _lookReference = lookReference;
        }

        /// <summary>Chamado pelo PlayerHandsController sempre que o peso nas mãos muda.</summary>
        public void SetCarryWeightMultiplier(float multiplier)
        {
            CarryWeightSpeedMultiplier = Mathf.Clamp(multiplier, 0.1f, 1f);
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<StarterAssetsInputs>();
            _hasAnimator = _animator != null;

            if (_hasAnimator)
            {
                _animIDSpeed = Animator.StringToHash("Speed");
                _animIDGrounded = Animator.StringToHash("Grounded");
                _animIDJump = Animator.StringToHash("Jump");
                _animIDFreeFall = Animator.StringToHash("FreeFall");
                _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
            }
        }

        private void Start()
        {
            _jumpTimeoutDelta = JumpTimeout;
            _fallTimeoutDelta = FallTimeout;
        }

        private void Update()
        {
            if (_lookReference == null) return; // câmera ainda não foi criada

            GroundedCheck();
            FaceLookDirection();
            JumpAndGravity();
            Move();
        }

        private void FaceLookDirection()
        {
            // O corpo copia só o eixo Y da câmera. O pitch (olhar pra cima/baixo)
            // fica isolado na câmera e nunca afeta o corpo.
            var euler = transform.eulerAngles;
            euler.y = _lookReference.eulerAngles.y;
            transform.eulerAngles = euler;
        }

        private void SetUpSprintSlider()
        {
            SprintTimeSlider.maxValue = SprintTimeMax;
            SprintTimeSlider.value = SprintTimeMax;
        }
        private void UpdateSprintSlider(float value)
        {
            SprintTimeSlider.value = value;
        }

        private void GroundedCheck()
        {
            Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z);
            Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);

            if (_hasAnimator) _animator.SetBool(_animIDGrounded, Grounded);
        }

        void OnDrawGizmos()
        {
            
            Gizmos.color = Grounded ? Color.green : Color.red;
            Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z);
            Gizmos.DrawSphere(spherePosition, GroundedRadius);
        }

        private void Move()
        {
            float targetSpeed = (_input.sprint ? SprintSpeed : MoveSpeed) * CarryWeightSpeedMultiplier;
            if (_input.move == Vector2.zero) targetSpeed = 0f;

            float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0f, _controller.velocity.z).magnitude;
            const float speedOffset = 0.1f;
            float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

            if (currentHorizontalSpeed < targetSpeed - speedOffset || currentHorizontalSpeed > targetSpeed + speedOffset)
            {
                _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude, Time.deltaTime * SpeedChangeRate);
                _speed = Mathf.Round(_speed * 1000f) / 1000f;
            }
            else
            {
                _speed = targetSpeed;
            }
            _animationBlend = Mathf.Lerp(_animationBlend, targetSpeed, Time.deltaTime * SpeedChangeRate);
            if (_animationBlend < 0.01f) _animationBlend = 0f;

            // Já que o corpo está sempre alinhado com a câmera, forward/right do próprio
            // transform já são a direção de olhar - não precisa recalcular com a câmera.
            Vector3 moveDirection = (transform.right * _input.move.x + transform.forward * _input.move.y).normalized;

            _controller.Move(moveDirection * (_speed * Time.deltaTime) + new Vector3(0f, _verticalVelocity, 0f) * Time.deltaTime);

            if (_hasAnimator)
            {
                _animator.SetFloat(_animIDSpeed, _animationBlend);
                _animator.SetFloat(_animIDMotionSpeed, inputMagnitude);
            }
        }

        private void JumpAndGravity()
        {
            if (Grounded)
            {
                _fallTimeoutDelta = FallTimeout;

                if (_hasAnimator)
                {
                    _animator.SetBool(_animIDJump, false);
                    _animator.SetBool(_animIDFreeFall, false);
                }

                if (_verticalVelocity < 0f) _verticalVelocity = -2f;

                if (_input.jump && _jumpTimeoutDelta <= 0f)
                {
                    _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
                    if (_hasAnimator) _animator.SetBool(_animIDJump, true);
                }

                if (_jumpTimeoutDelta >= 0f) _jumpTimeoutDelta -= Time.deltaTime;
            }
            else
            {
                _jumpTimeoutDelta = JumpTimeout;

                if (_fallTimeoutDelta >= 0f)
                {
                    _fallTimeoutDelta -= Time.deltaTime;
                }
                else if (_hasAnimator)
                {
                    _animator.SetBool(_animIDFreeFall, true);
                }

                _input.jump = false;
            }

            if (_verticalVelocity < _terminalVelocity)
                _verticalVelocity += Gravity * Time.deltaTime;
        }
    }
}
