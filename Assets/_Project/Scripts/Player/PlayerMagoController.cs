using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace UnCuentoMas
{
    /// <summary>
    /// Controlador mejorado del Mago (Player):
    /// - Animación fluida sobre el palito con Squash & Stretch al saltar, aterrizar y atacar.
    /// - Giro suave de recorte de cartón al cambiar entre izquierda y derecha.
    /// - Disparo con detección física doble (Rigidbody2D cinemático + OverlapCircleAll) para que los poderes NUNCA atraviesen a los enemigos sin dañarlos.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    [RequireComponent(typeof(PuppetWobble))]
    public class PlayerMagoController : MonoBehaviour
    {
        public static bool UseFinalWizardModelStatic = false;
        public static bool DiagonalAttackUnlockedStatic = false;

        [Header("=== 1. SPRITES DEL MAGO (4 FRAMES CADA UNO) ===")]
        [SerializeField] private SpriteRenderer wizardSpriteRenderer;
        [SerializeField] private Sprite[] normalWizardFrames = new Sprite[4];
        [SerializeField] private Sprite[] finalWizardFrames = new Sprite[4];
        [SerializeField] private bool useFinalWizardModel;

        [Header("=== 2. ATAQUES Y PROYECTIL (FRAME 2 DE HERRAMIENTAS) ===")]
        [SerializeField] private Sprite magicSpellSprite;
        [SerializeField] private Transform straightSpawnPoint;
        [SerializeField] private Transform diagonalSpawnPoint;
        [SerializeField] private bool diagonalAttackUnlocked = true;
        [SerializeField] private float spellSpeed = 7.5f;
        [SerializeField] private float attackCooldown = 0.32f;

        [Header("=== 3. MOVIMIENTO Y LÍMITES DEL TEATRO ===")]
        [SerializeField] private float moveSpeed = 5.8f;
        [Tooltip("Salto suave y dinámico de títere para esquivar ataques bajos sin revelar el palito debajo del telón.")]
        [SerializeField] private float jumpForce = 5.3f;
        [SerializeField] private float leftStageBound = -6.8f;
        [SerializeField] private float rightStageBound = 6.8f;
        [SerializeField] private Transform groundCheck;
        [SerializeField] private float groundCheckRadius = 0.3f;
        [SerializeField] private LayerMask groundLayer = ~0;

        [Header("=== 4. SALUD (3 CORAZONES CON HILO) Y MAGIA OXIDADA ===")]
        [SerializeField] private int maxHealth = 3;
        [SerializeField] private bool enableRustyOverheat = true;

        [Header("=== 5. GIRO DE PAPEL (DASH CON SHIFT) ===")]
        [SerializeField] private float dashDuration = 0.2f;
        [SerializeField] private float dashCooldown = 0.8f;
        [SerializeField] private float dashSpeed = 16.5f;

        [Header("=== 6. ACCIONES HABILITADAS (TUTORIAL) ===")]
        [SerializeField] private bool canAttack = true;
        [SerializeField] private bool canDash = true;

        public event System.Action<bool, bool> OnAttackFired;
        public event System.Action OnDashFired;

        public bool CanAttack => canAttack;
        public bool CanDash => canDash;
        public void SetAttacksEnabled(bool enabled) => canAttack = enabled;
        public void SetDashEnabled(bool enabled) => canDash = enabled;

        [Header("=== 7. EVENTOS ===")]
        public UnityEvent<int, int> OnHealthChanged;
        public UnityEvent OnDiagonalAttackUnlocked;

        private Rigidbody2D _rb;
        private PuppetWobble _wobble;
        private float _horizontalInput;
        private bool _jumpRequested;
        private bool _isGrounded;
        private bool _wasGroundedLastFrame = true;
        private bool _facingRight = true;
        private bool _canControl = true;
        private bool _isAnimatingAction;
        private bool _isInvulnerable;
        private float _lastAttackTime = -10f;
        private float _rustOverheatMeter;
        private float _baseScaleX = 0.50f;
        private float _baseScaleY = 0.45f;
        private float _currentFlipScaleX = 0.50f;
        private float _smoothedHorizontalSpeed;
        private int _currentHealth;

        // Giro de Papel (Dash)
        private bool _isDashing;
        private float _lastDashTime = -10f;
        private float _dashTimer;
        private float _dashDirection;

        // Sistema de Ataque (Clic rápido vs. cargado por ritmo)
        private bool _isChargingAttack;
        private bool _chargingDiagonal;
        private float _chargeHoldTime;
        private bool _chargeReadyNotified;

        public float CurrentHorizontalSpeed => _canControl ? _smoothedHorizontalSpeed : 0f;
        public bool CanControl => _canControl;
        public bool FacingRight => _facingRight;
        public bool IsDiagonalUnlocked => diagonalAttackUnlocked;
        public bool IsDashing => _isDashing;
        public bool IsInvulnerable => _isInvulnerable || _isDashing;

        /// <summary>
        /// Inicialización temprana de referencias y componentes de física.
        /// Se ejecuta antes de Start(), ideal para obtener referencias locales (GetComponent).
        /// </summary>
        private void Awake()
        {
            // Obtenemos el Rigidbody2D y congelamos su rotación Z para que el personaje no ruede ni se caiga de lado.
            // Activamos la interpolación para que los movimientos se rendericen suaves a altas tasas de refresco (Hz).
            _rb = GetComponent<Rigidbody2D>();
            _rb.freezeRotation = true;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            // Referencia al balanceo teatral de marioneta sobre el palito
            _wobble = GetComponent<PuppetWobble>();

            // Configuración de salud inicial (3 corazones)
            maxHealth = 3;
            _currentHealth = maxHealth;

            // Fuerza de salto calibrada para esquivar tajos y proyectiles bajos sin que el palito asome debajo del telón
            jumpForce = 8.5f;

            // Guardamos la escala base original para usarla al voltear el sprite hacia la izquierda o derecha
            _baseScaleX = Mathf.Abs(transform.localScale.x);
            _baseScaleY = Mathf.Abs(transform.localScale.y);
            if (_baseScaleX < 0.05f) _baseScaleX = 0.50f;
            if (_baseScaleY < 0.05f) _baseScaleY = 0.45f;
            _currentFlipScaleX = _baseScaleX;

            if (wizardSpriteRenderer == null)
            {
                wizardSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            // Si venimos de un final o desbloqueo persistente entre escenas (variables estáticas):
            if (UseFinalWizardModelStatic) useFinalWizardModel = true;
            if (DiagonalAttackUnlockedStatic) diagonalAttackUnlocked = true;

            // Iniciamos en Frame 0 (Pose de reposo / Idle)
            SetWizardFrame(0);
        }

        private void Start()
        {
            // Notificamos a la interfaz de usuario (HUD de Corazones) la salud inicial
            OnHealthChanged?.Invoke(_currentHealth, maxHealth);
        }

        /// <summary>
        /// Bucle principal de actualización por fotograma (se ejecuta en cada frame renderizado).
        /// Ideal para: lectura de teclas/mouse (Input), actualización de animaciones y comprobación visual.
        /// </summary>
        private void Update()
        {
            // Enfriamiento gradual del medidor de sobrecalentamiento por magia oxidada
            if (_rustOverheatMeter > 0f)
            {
                _rustOverheatMeter = Mathf.Max(0f, _rustOverheatMeter - 28f * Time.deltaTime);
            }

            // Comprobamos si el personaje está tocando el suelo del teatro
            CheckGrounded();

            // Animamos la transición suave del sprite como un recorte de papel girando en 2D
            AnimateSmoothPaperFlip();

            // Si el control está bloqueado (por ejemplo durante cinemáticas o cierre de telón):
            if (!_canControl)
            {
                _horizontalInput = 0f;
                _smoothedHorizontalSpeed = 0f;
                _wobble.SetMovingState(false, 0f);
                return;
            }

            // Leemos los botones de movimiento, salto, dash y ataques
            ReadMovementAndAttackInput();

            // Sincronizamos la oscilación teatral del palito con la dirección en la que camina el jugador
            _wobble.SetMovingState(Mathf.Abs(_horizontalInput) > 0.05f, _horizontalInput);
        }

        /// <summary>
        /// Bucle de física de Unity (se ejecuta a pasos de tiempo fijos y constantes: Time.fixedDeltaTime).
        /// Todo cálculo que modifique velocidades lineales o fuerzas del Rigidbody2D DEBE ir aquí.
        /// </summary>
        private void FixedUpdate()
        {
            if (!_canControl)
            {
                _smoothedHorizontalSpeed = 0f;
                _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
                return;
            }

            // 1. GIRO DE PAPEL (DASH CON SHIFT)
            // Durante el dash, el mago se desplaza velozmente en línea recta horizontal ignorando gravedad
            if (_isDashing)
            {
                _dashTimer -= Time.fixedDeltaTime;
                _rb.linearVelocity = new Vector2(_dashDirection * dashSpeed, 0f);

                // Mantenemos al personaje dentro de los límites visibles del telón
                Vector3 dashPos = transform.position;
                dashPos.x = Mathf.Clamp(dashPos.x, leftStageBound, rightStageBound);
                transform.position = dashPos;

                if (_dashTimer <= 0f)
                {
                    _isDashing = false;
                    _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
                }
                return;
            }

            // 2. ACELERACIÓN Y FRENADO SUAVE AL CAMINAR
            // Usamos Mathf.Lerp para simular la inercia manual de una marioneta sostenida con palito
            float targetSpeed = _horizontalInput * moveSpeed;
            _smoothedHorizontalSpeed = Mathf.Lerp(_smoothedHorizontalSpeed, targetSpeed, Time.fixedDeltaTime * 11.5f);
            if (Mathf.Abs(_smoothedHorizontalSpeed) < 0.02f && Mathf.Abs(targetSpeed) < 0.01f)
            {
                _smoothedHorizontalSpeed = 0f;
            }

            // Asignamos la velocidad horizontal en física manteniendo la velocidad vertical (gravedad/salto)
            _rb.linearVelocity = new Vector2(_smoothedHorizontalSpeed, _rb.linearVelocity.y);

            // Si hay un salto pendiente por ejecutar:
            if (_jumpRequested)
            {
                TryJump();
            }

            // Limitamos la posición en X para que el títere nunca salga fuera de la vista del público
            Vector3 pos = transform.position;
            pos.x = Mathf.Clamp(pos.x, leftStageBound, rightStageBound);
            transform.position = pos;
        }

        /// <summary>
        /// Establece la orientación visual del Mago (hacia la derecha o izquierda).
        /// Ajusta la escala en X para voltear el sprite respetando su tamaño base.
        /// </summary>
        public void SetFacingDirection(bool faceRight)
        {
            _facingRight = faceRight;
            _currentFlipScaleX = (_facingRight ? 1f : -1f) * _baseScaleX;
            transform.localScale = new Vector3(_currentFlipScaleX, _baseScaleY, _baseScaleY);
        }

        /// <summary>
        /// Efecto visual teatral: Giro de recorte de papel en 2D.
        /// En lugar de un volteo instantáneo brusco, interpolamos (Mathf.Lerp) el eje X de la escala.
        /// Si el personaje hace un Dash, la escala X se aplana hasta 0.15f para simular que el papel
        /// se pone "de canto", permitiéndole colarse entre los enemigos.
        /// </summary>
        private void AnimateSmoothPaperFlip()
        {
            if (_isDashing)
            {
                // Efecto de recorte de canto durante el Giro de Papel (Dash con Shift)
                float edgeTargetX = (_facingRight ? 1f : -1f) * 0.15f;
                _currentFlipScaleX = Mathf.Lerp(_currentFlipScaleX, edgeTargetX, Time.deltaTime * 24f);
                transform.localScale = new Vector3(_currentFlipScaleX, _baseScaleY, _baseScaleY);
                return;
            }

            // Interpolación suave hacia la escala normal (+baseScaleX a la derecha, -baseScaleX a la izquierda)
            float targetX = (_facingRight ? 1f : -1f) * _baseScaleX;
            _currentFlipScaleX = Mathf.Lerp(_currentFlipScaleX, targetX, Time.deltaTime * 14f);

            // Evitamos que la escala llegue a exactamente 0 absoluto para prevenir artefactos visuales de renderizado
            if (Mathf.Abs(_currentFlipScaleX) < 0.05f)
            {
                _currentFlipScaleX = Mathf.Sign(targetX) * 0.05f;
            }
            transform.localScale = new Vector3(_currentFlipScaleX, _baseScaleY, _baseScaleY);
        }

        /// <summary>
        /// Lee todas las entradas del jugador (Teclado y Ratón) utilizando el nuevo Input System de Unity.
        /// Gestiona: Dash (Shift), Movimiento lateral (A/D), Salto (W/Espacio) y Ataques (Clic rápido vs. cargado).
        /// </summary>
        private void ReadMovementAndAttackInput()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;

            // ---------------------------------------------------------
            // 1. GIRO DE PAPEL (DASH CON SHIFT)
            // ---------------------------------------------------------
            // Permite al mago esquivar proyectiles y atravesar enemigos volviéndose invulnerable temporalmente.
            bool dashKey = (kb != null && (kb.leftShiftKey.wasPressedThisFrame || kb.rightShiftKey.wasPressedThisFrame));
            if (dashKey && canDash && !_isDashing && Time.time >= _lastDashTime + dashCooldown)
            {
                _isDashing = true;
                _lastDashTime = Time.time;
                _dashTimer = dashDuration;
                // Si el jugador se está moviendo usamos esa dirección; si está quieto, usamos hacia donde mira
                _dashDirection = Mathf.Abs(_horizontalInput) > 0.05f ? Mathf.Sign(_horizontalInput) : (_facingRight ? 1f : -1f);
                
                // Efectos de squash (aplastamiento) y vibración teatral en el palito
                _wobble.TriggerPaperSquashStretch(0.35f, 1.15f);
                _wobble.TriggerHitShake(0.12f, 4.5f);
                
                // Cancelamos cualquier carga de hechizo en curso al hacer dash
                _isChargingAttack = false;
                _chargeHoldTime = 0f;
                _chargeReadyNotified = false;
                
                // Disparamos el evento para que el tutorial o el gestor narrativo sepan que se realizó el dash
                OnDashFired?.Invoke();
                return;
            }

            if (_isDashing) return;

            // ---------------------------------------------------------
            // 2. MOVIMIENTO LATERAL Y SALTO (TECLA "W" O ESPACIO)
            // ---------------------------------------------------------
            float move = 0f;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move += 1f;

                // Salto ágil y controlado para esquivar con W o Espacio
                // Se invoca TryJump() inmediatamente al presionar la tecla
                if (kb.spaceKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame)
                {
                    TryJump();
                }
            }

            _horizontalInput = Mathf.Clamp(move, -1f, 1f);

            // Voltear la mirada del personaje según la dirección de la marcha
            if (_horizontalInput > 0.05f && !_facingRight) _facingRight = true;
            else if (_horizontalInput < -0.05f && _facingRight) _facingRight = false;

            // ---------------------------------------------------------
            // 3. SISTEMA DE ATAQUE (CLIC RÁPIDO VS. CARGADO POR RITMO)
            // ---------------------------------------------------------
            // Mecánica clave: Clic rápido (<0.4s) dispara hechizo normal con empuje leve.
            // Mantener presionado (>=0.4s) acumula energía con vibración y dispara un proyectil 1.5x mayor
            // con daño masivo, rotura de guardia de escudos y gran knockback.
            if (!canAttack)
            {
                _isChargingAttack = false;
                _chargeHoldTime = 0f;
                return;
            }

            if (!_isChargingAttack)
            {
                // Verificamos que no esté en cooldown de ataque ni en medio de una animación de lanzamiento
                if (Time.time >= _lastAttackTime + attackCooldown && !_isAnimatingAction)
                {
                    if (CheckAttackPressed(out bool isDiag))
                    {
                        if (!CheckAttackStillHeld(isDiag))
                        {
                            // Fue solo un toque rápido: Disparo inmediato
                            ExecuteAttack(isDiag, isCharged: false);
                        }
                        else
                        {
                            // El jugador mantiene presionado el botón: Iniciamos acumulación de carga
                            _isChargingAttack = true;
                            _chargingDiagonal = isDiag;
                            _chargeHoldTime = 0f;
                            _chargeReadyNotified = false;
                        }
                    }
                }
            }
            else
            {
                // Ya estamos en modo de carga: medimos el tiempo transcurrido
                if (CheckAttackStillHeld(_chargingDiagonal))
                {
                    _chargeHoldTime += Time.deltaTime;
                    if (_chargeHoldTime >= 0.4f)
                    {
                        if (!_chargeReadyNotified)
                        {
                            // Retroalimentación visual y física al alcanzar la carga completa (0.4s)
                            _chargeReadyNotified = true;
                            _wobble.TriggerPaperSquashStretch(1.15f, 1.15f);
                            _wobble.TriggerHitShake(0.18f, 5.5f);
                        }
                        else if (Time.frameCount % 8 == 0)
                        {
                            // Vibración táctil continua periódica para indicar que la magia rebosa energía
                            _wobble.TriggerHitShake(0.06f, 3.5f);
                        }
                    }
                }
                else
                {
                    // El jugador soltó el botón: Determinamos si fue ataque cargado o normal según el tiempo acumulado
                    bool isCharged = _chargeHoldTime >= 0.4f;
                    ExecuteAttack(_chargingDiagonal, isCharged);
                    _isChargingAttack = false;
                    _chargeHoldTime = 0f;
                    _chargeReadyNotified = false;
                }
            }
        }

        /// <summary>
        /// Comprueba si se presionó el botón de ataque en este fotograma (wasPressedThisFrame).
        /// Clic Izquierdo / J / Z = Ataque Frontal Recto.
        /// Clic Derecho / K / X / E = Ataque Aéreo Diagonal (hacia arriba a 45°).
        /// </summary>
        private bool CheckAttackPressed(out bool isDiagonal)
        {
            isDiagonal = false;
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            bool holdingUp = kb != null && kb.upArrowKey.isPressed;

            bool straightPressed = (mouse != null && mouse.leftButton.wasPressedThisFrame) ||
                                   (kb != null && (kb.jKey.wasPressedThisFrame || kb.zKey.wasPressedThisFrame));

            bool diagonalPressed = (mouse != null && mouse.rightButton.wasPressedThisFrame) ||
                                   (kb != null && (kb.kKey.wasPressedThisFrame || kb.xKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame));

            if (diagonalPressed)
            {
                isDiagonal = diagonalAttackUnlocked;
                return true;
            }

            if (straightPressed)
            {
                if (holdingUp && diagonalAttackUnlocked) isDiagonal = true;
                else isDiagonal = false;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Comprueba si el botón de ataque sigue manteniéndose presionado continuamente (isPressed).
        /// Necesario para medir el tiempo de carga del hechizo potenciado.
        /// </summary>
        private bool CheckAttackStillHeld(bool forDiagonal)
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;

            if (forDiagonal)
            {
                bool mouseRight = mouse != null && mouse.rightButton.isPressed;
                bool kbDiag = kb != null && (kb.kKey.isPressed || kb.xKey.isPressed || kb.eKey.isPressed);
                bool straightWithUp = (kb != null && kb.upArrowKey.isPressed) &&
                                     ((mouse != null && mouse.leftButton.isPressed) || (kb != null && (kb.jKey.isPressed || kb.zKey.isPressed)));
                return mouseRight || kbDiag || straightWithUp;
            }
            else
            {
                bool mouseLeft = mouse != null && mouse.leftButton.isPressed;
                bool kbStraight = kb != null && (kb.jKey.isPressed || kb.zKey.isPressed);
                return mouseLeft || kbStraight;
            }
        }

        /// <summary>
        /// Ejecuta el ataque: gestiona el sobrecalentamiento del bastón viejo, notifica a los oyentes (Action)
        /// e inicia la corrutina de pose y generación del proyectil.
        /// </summary>
        private void ExecuteAttack(bool isDiagonal, bool isCharged)
        {
            _lastAttackTime = Time.time;
            OnAttackFired?.Invoke(isDiagonal, isCharged);

            if (isDiagonal)
            {
                AudioManager.Instance?.PlayPlayerAttackAir();
            }
            else
            {
                AudioManager.Instance?.PlayPlayerAttackNormal();
            }

            // Mecánica de "Magia Oxidada" (solo en el Acto 1 con el bastón viejo):
            // Si el jugador machaca los botones sin ritmo, el bastón se sobrecalienta y falla temporalmente.
            if (enableRustyOverheat && !useFinalWizardModel)
            {
                _rustOverheatMeter += isCharged ? 35f : 24f;
                if (_rustOverheatMeter >= 100f)
                {
                    _rustOverheatMeter = 25f;
                    StartCoroutine(RustyBackfireRoutine());
                    return;
                }
            }

            // Iniciamos la animación teatral de lanzamiento de hechizo
            StartCoroutine(AttackPoseAndFireRoutine(isDiagonal, isCharged));
        }

        /// <summary>
        /// Corrutina que anima el lanzamiento: cambia el sprite a la pose de ataque (Frame 1 o 2),
        /// aplica el retroceso físico (Squash/Shake) en el palito de la marioneta,
        /// genera el proyectil en el mundo y luego regresa a la pose de descanso (Frame 0).
        /// </summary>
        private IEnumerator AttackPoseAndFireRoutine(bool isDiagonal, bool isCharged)
        {
            _isAnimatingAction = true;

            // Frame 1 = Ataque Recto | Frame 2 = Ataque Aéreo/Diagonal
            SetWizardFrame(isDiagonal ? 2 : 1);
            if (isCharged)
            {
                _wobble.TriggerPaperSquashStretch(1.22f, 0.85f);
                _wobble.TriggerHitShake(0.20f, 7.5f);
            }
            else
            {
                _wobble.TriggerPaperSquashStretch(1.12f, 0.92f);
                _wobble.TriggerHitShake(0.12f, 4.5f);
            }

            // Instanciamos el proyectil mágico en el escenario
            SpawnMagicProjectile(isDiagonal, isCharged);

            // Pausa teatral que dura la animación antes de bajar el bastón
            yield return new WaitForSeconds(isCharged ? 0.26f : 0.20f);

            SetWizardFrame(0); // Volver a Frame 0 (Idle / Reposo)
            _isAnimatingAction = false;
        }

        /// <summary>
        /// Corrutina de fallo por sobrecalentamiento de magia oxidada:
        /// Hace que el mago muestre la pose de tropiezo/daño (Frame 3) y tiemble, mostrando un aviso pedagógico en pantalla.
        /// </summary>
        private IEnumerator RustyBackfireRoutine()
        {
            _isAnimatingAction = true;
            SetWizardFrame(3); // Frame 3: fallo / daño por magia oxidada
            _wobble.TriggerPaperSquashStretch(0.88f, 1.12f);
            _wobble.TriggerHitShake(0.32f, 12f);

            yield return new WaitForSeconds(0.32f);
            SetWizardFrame(0);
            _isAnimatingAction = false;
        }

        /// <summary>
        /// Crea e inicializa el GameObject del proyectil mágico en la escena.
        /// Configura su SpriteRenderer, capa de ordenamiento (sortingOrder = 28 para verse frente al fondo),
        /// un Rigidbody2D cinemático y un CircleCollider2D como Trigger para colisiones precisas.
        /// </summary>
        private void SpawnMagicProjectile(bool isDiagonal, bool isCharged)
        {
            float dirSign = _facingRight ? 1f : -1f;
            Vector3 spawnPos;

            // Punto de origen del proyectil: punta del bastón (recto o diagonal según corresponda)
            if (isDiagonal && diagonalSpawnPoint != null)
                spawnPos = diagonalSpawnPoint.position;
            else if (!isDiagonal && straightSpawnPoint != null)
                spawnPos = straightSpawnPoint.position;
            else
                spawnPos = transform.position + new Vector3(dirSign * 1.1f, isDiagonal ? 1.6f : 1.05f, 0f);

            // Dirección vectorial normalizada del disparo
            Vector2 moveDir = isDiagonal
                ? new Vector2(dirSign * 0.80f, 0.60f).normalized
                : new Vector2(dirSign, 0f);

            GameObject projGO = new GameObject(isCharged
                ? (isDiagonal ? "PlayerSpell_Diagonal_Charged" : "PlayerSpell_Straight_Charged")
                : (isDiagonal ? "PlayerSpell_Diagonal" : "PlayerSpell_Straight"));

            projGO.transform.position = spawnPos;
            
            // Escala visual del proyectil: los hechizos cargados son 1.55x mayores
            float baseProjScale = 0.38f;
            projGO.transform.localScale = Vector3.one * (isCharged ? baseProjScale * 1.55f : baseProjScale);

            // Orientamos el sprite rotándolo hacia la dirección del vector de vuelo
            float angle = Mathf.Atan2(moveDir.y, moveDir.x) * Mathf.Rad2Deg;
            projGO.transform.rotation = Quaternion.Euler(0f, 0f, angle + 180f);

            SpriteRenderer sr = projGO.AddComponent<SpriteRenderer>();
            sr.sprite = magicSpellSprite;
            sr.sortingOrder = 28;
            if (isCharged)
            {
                // Tinte dorado brillante para el hechizo cargado
                sr.color = new Color(1f, 0.96f, 0.72f, 1f);
            }

            // IMPORTANTE EN FÍSICAS DE UNITY 2D:
            // Al menos uno de los dos objetos que colisionan con Triggers DEBE tener un Rigidbody2D.
            // Usamos Kinematic con detección continua para evitar que proyectiles rápidos atraviesen paredes/enemigos.
            Rigidbody2D projRb = projGO.AddComponent<Rigidbody2D>();
            projRb.bodyType = RigidbodyType2D.Kinematic;
            projRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            CircleCollider2D col = projGO.AddComponent<CircleCollider2D>();
            col.radius = isCharged ? 0.65f : 0.55f;
            col.isTrigger = true;

            // Añadimos el script de comportamiento del proyectil y lo inicializamos
            MagicProjectileBehaviour behaviour = projGO.AddComponent<MagicProjectileBehaviour>();
            float currentSpeed = isCharged ? spellSpeed * 1.2f : spellSpeed;
            behaviour.Initialize(moveDir, currentSpeed, isDiagonal, isCharged);
        }

        /// <summary>
        /// Desbloquea permanentemente el ataque diagonal aéreo (por ejemplo al obtener la pluma del nido del dragón).
        /// Guarda el estado en la variable estática para preservarlo entre cambios de escena.
        /// </summary>
        public void UnlockDiagonalAttack()
        {
            if (diagonalAttackUnlocked) return;
            diagonalAttackUnlocked = true;
            DiagonalAttackUnlockedStatic = true;
            OnDiagonalAttackUnlocked?.Invoke();
        }

        /// <summary>
        /// Cambia el sprite sheet del Mago al modelo final (traje restaurado / alas de dragón)
        /// tras presenciar la cinemática de la verdad en la cueva.
        /// </summary>
        public void SwitchToFinalWizardModel(bool useFinal = true)
        {
            useFinalWizardModel = useFinal;
            UseFinalWizardModelStatic = useFinal;
            SetWizardFrame(0);
        }

        /// <summary>
        /// Aplica daño al jugador reduciendo corazones en el HUD.
        /// Si está en Dash o en periodo de invulnerabilidad (i-frames), el daño es ignorado.
        /// </summary>
        public void TakeDamage(int damage = 1)
        {
            if (_isInvulnerable || _isDashing || !_canControl) return;

            // Interrumpimos cualquier carga de ataque
            _isChargingAttack = false;
            _chargeHoldTime = 0f;
            _chargeReadyNotified = false;

            // Restamos salud y notificamos al HUD de Corazones (PuppetHeartHUD)
            _currentHealth = Mathf.Max(0, _currentHealth - damage);
            OnHealthChanged?.Invoke(_currentHealth, maxHealth);
            AudioManager.Instance?.PlayDamagePlayerOrEnemy();
            
            // Reacción teatral de impacto con parpadeo de invulnerabilidad
            StartCoroutine(DamageReactionRoutine());

            // Si el jugador pierde todas las vidas, se reinicia el escenario
            if (_currentHealth <= 0)
            {
                StartCoroutine(RestartStageOnDefeatRoutine());
            }
        }

        /// <summary>
        /// Reinicia el escenario actual cuando el jugador pierde todas sus vidas:
        /// Bloquea el control, cierra dramáticamente el telón rojo y recarga la escena activa.
        /// </summary>
        private IEnumerator RestartStageOnDefeatRoutine()
        {
            _canControl = false;
            _isDashing = false;
            _isChargingAttack = false;

            yield return new WaitForSeconds(0.45f);

            CurtainStageManager curtain = FindFirstObjectByType<CurtainStageManager>();
            if (curtain != null)
            {
                yield return StartCoroutine(curtain.PlayCloseCurtainsOnlyRoutine(0.45f));
            }
            else
            {
                yield return new WaitForSeconds(0.45f);
            }

            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        /// <summary>
        /// Corrutina de reacción a daño:
        /// Muestra la pose de dolor (Frame 3), sacude violentamente el palito de la marioneta,
        /// y activa un periodo de invulnerabilidad con parpadeo blanco/rojo para que el jugador pueda reubicarse.
        /// </summary>
        private IEnumerator DamageReactionRoutine()
        {
            _isInvulnerable = true;
            _isAnimatingAction = true;

            SetWizardFrame(3);
            _wobble.TriggerPaperSquashStretch(1.15f, 0.85f);
            _wobble.TriggerHitShake(0.35f, 14f);

            yield return new WaitForSeconds(0.32f);
            SetWizardFrame(0);
            _isAnimatingAction = false;

            // Parpadeo visual de invulnerabilidad (4 ciclos de 0.16s = ~0.64s de gracia)
            for (int i = 0; i < 4; i++)
            {
                if (wizardSpriteRenderer != null) wizardSpriteRenderer.color = new Color(1f, 0.7f, 0.7f, 0.6f);
                yield return new WaitForSeconds(0.08f);
                if (wizardSpriteRenderer != null) wizardSpriteRenderer.color = Color.white;
                yield return new WaitForSeconds(0.08f);
            }

            _isInvulnerable = false;
        }

        /// <summary>
        /// Cambia el sprite actual del Mago seleccionándolo del conjunto activo (Normal o Final).
        /// Índice 0 = Idle / Reposo
        /// Índice 1 = Hechizo Recto
        /// Índice 2 = Hechizo Diagonal
        /// Índice 3 = Reacción a Daño / Fallo
        /// </summary>
        public void SetWizardFrame(int frameIndex)
        {
            if (wizardSpriteRenderer == null) return;

            Sprite[] activeSet = (useFinalWizardModel && finalWizardFrames != null && finalWizardFrames.Length == 4 && finalWizardFrames[0] != null)
                ? finalWizardFrames
                : normalWizardFrames;

            if (activeSet != null && frameIndex >= 0 && frameIndex < activeSet.Length && activeSet[frameIndex] != null)
            {
                wizardSpriteRenderer.sprite = activeSet[frameIndex];
            }
        }

        /// <summary>
        /// Ejecuta el salto del títere:
        /// Comprueba si está en el suelo y aplica un impulso instantáneo hacia arriba en linearVelocity.y.
        /// La fuerza (jumpForce = 8.5f) está calibrada para un salto bajo y ágil que no despegue el palillo
        /// del fondo visible del telón.
        /// </summary>
        public void TryJump()
        {
            CheckGrounded();
            if (_isGrounded)
            {
                // Aplicamos la velocidad vertical conservando la horizontal actual
                _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpForce);
                _isGrounded = false;
                _jumpRequested = false;
                
                // Efecto de estiramiento teatral al impulsarse
                if (_wobble != null)
                {
                    _wobble.TriggerPaperSquashStretch(0.90f, 1.15f);
                    _wobble.TriggerHitShake(0.08f, 2.5f);
                }
            }
            else
            {
                _jumpRequested = false;
            }
        }

        /// <summary>
        /// Determina con precisión si el títere está apoyado sobre el suelo del teatro.
        /// Combina:
        /// 1. Coordenada Y del escenario (el tablón del teatro está en Y = -3.95f).
        /// 2. OverlapCircleAll con la capa del suelo (groundLayer), excluyendo al propio jugador y sus hijos.
        /// 3. Detección de aterrizaje para disparar una pequeña amortiguación (Squash de papel).
        /// </summary>
        private void CheckGrounded()
        {
            bool groundedNow = false;

            // 1. En el teatro de títeres, el escenario tiene su suelo plano fijo en Y = -3.95f
            if (transform.position.y <= -3.75f && _rb.linearVelocity.y <= 0.35f)
            {
                groundedNow = true;
            }
            else if (groundCheck != null)
            {
                // 2. Comprobación física con círculo en los pies del personaje
                Collider2D[] hits = Physics2D.OverlapCircleAll(groundCheck.position, groundCheckRadius, groundLayer);
                foreach (var h in hits)
                {
                    // Ignoramos los colisionadores propios del mago o sus hijos (como el palito o triggers)
                    if (h != null && h.gameObject != gameObject && !h.transform.IsChildOf(transform) && !h.isTrigger)
                    {
                        groundedNow = true;
                        break;
                    }
                }
            }
            else
            {
                // Respaldo de seguridad por posición y velocidad vertical casi nula
                groundedNow = (transform.position.y <= -3.65f) && Mathf.Abs(_rb.linearVelocity.y) < 0.15f;
            }

            // Si acabamos de tocar suelo tras estar en el aire, reproducimos la amortiguación de papel
            if (groundedNow && !_wasGroundedLastFrame)
            {
                _wobble.TriggerPaperSquashStretch(1.12f, 0.88f);
            }

            _isGrounded = groundedNow;
            _wasGroundedLastFrame = groundedNow;
        }

        /// <summary>
        /// Activa o bloquea el control del jugador (usado durante cinemáticas, diálogos o cambios de sección).
        /// Al deshabilitar el control, detiene en seco la inercia horizontal.
        /// </summary>
        public void SetControlEnabled(bool enabledControl)
        {
            _canControl = enabledControl;
            if (!enabledControl && _rb != null)
            {
                _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
                _horizontalInput = 0f;
            }
        }
    }

    /// <summary>
    /// Comportamiento de vuelo y colisión del Proyectil Mágico.
    /// Diseñado con doble verificación de impacto (OnTriggerEnter2D + OverlapCircleAll activo en Update)
    /// para asegurar que ningún proyectil atraviese a los muñecos de entrenamiento, flechas, guerreros o al dragón.
    /// </summary>
    public class MagicProjectileBehaviour : MonoBehaviour
    {
        private Vector2 _direction;
        private float _speed;
        private bool _isDiagonalShot;
        private bool _isCharged;
        private bool _hasHit;

        /// <summary>
        /// Inicializa los parámetros de vuelo del proyectil y programa su autodestrucción tras 2.5 segundos.
        /// </summary>
        public void Initialize(Vector2 direction, float speed, bool isDiagonalShot, bool isCharged = false)
        {
            _direction = direction.normalized;
            _speed = speed;
            _isDiagonalShot = isDiagonalShot;
            _isCharged = isCharged;
            // Destrucción automática tras salir de la pantalla para evitar fugas de memoria
            Destroy(gameObject, 2.5f);
        }

        /// <summary>
        /// Movimiento cuadro a cuadro y escaneo proactivo de colisiones.
        /// </summary>
        private void Update()
        {
            if (_hasHit) return;

            // Desplazamiento lineal en la dirección calculada
            transform.position += (Vector3)(_direction * _speed * Time.deltaTime);

            // Verificación activa por superposición física (OverlapCircleAll) en cada fotograma.
            // Esto complementa a OnTriggerEnter2D para proyectiles de alta velocidad en 60fps.
            float scanRadius = _isCharged ? 0.60f : 0.45f;
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, scanRadius);
            foreach (var hit in hits)
            {
                if (TryProcessHit(hit)) return;
            }
        }

        /// <summary>
        /// Detección de colisión estándar de Unity 2D cuando este Trigger entra en contacto con otro Collider2D.
        /// </summary>
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_hasHit) return;
            TryProcessHit(other);
        }

        /// <summary>
        /// Procesa el impacto contra las distintas entidades del juego:
        /// 1. Flechas enemigas: Las destruye en el aire (mecánica de contraataque).
        /// 2. Muñecos de entrenamiento (TrainingDummyPuppet): Registra el impacto recto o aéreo.
        /// 3. Guerreros enemigos (EnemyController): Aplica daño, knockback o rompe guardia si es cargado.
        /// 4. Jefe Dragón (DragonBossController): Aplica daño aéreo.
        /// </summary>
        private bool TryProcessHit(Collider2D other)
        {
            if (other == null) return false;
            // No impactar contra el propio mago que disparó
            if (other.GetComponentInParent<PlayerMagoController>() != null) return false;

            // 1. Intercepción de flechas en vuelo: el hechizo puede destruir flechas enemigas en el aire
            EnemyArrowBehaviour arrow = other.GetComponent<EnemyArrowBehaviour>();
            if (arrow == null) arrow = other.GetComponentInParent<EnemyArrowBehaviour>();
            if (arrow != null)
            {
                _hasHit = true;
                Destroy(arrow.gameObject);
                Destroy(gameObject);
                return true;
            }

            // 2. Muñecos de práctica del tutorial
            TrainingDummyPuppet dummy = other.GetComponentInParent<TrainingDummyPuppet>();
            if (dummy != null && dummy.gameObject.activeInHierarchy)
            {
                if (dummy.TryHitWithSpell(_isDiagonalShot, _isCharged))
                {
                    _hasHit = true;
                    Destroy(gameObject);
                    return true;
                }
            }

            // 3. Guerreros y arqueros enemigos
            EnemyController enemy = other.GetComponentInParent<EnemyController>();
            if (enemy != null && !enemy.IsDefeated)
            {
                if (enemy.TryHitWithPlayerSpell(_isDiagonalShot, _isCharged, _direction))
                {
                    _hasHit = true;
                    Destroy(gameObject);
                    return true;
                }
            }

            // 4. Jefe Dragón en la cueva
            DragonBossController dragon = other.GetComponentInParent<DragonBossController>();
            if (dragon != null && dragon.IsCombatActive)
            {
                if (dragon.TryHitDragon(_isDiagonalShot, _isCharged ? 2 : 1))
                {
                    _hasHit = true;
                    Destroy(gameObject);
                    return true;
                }
            }

            // 5. Intercepción de llamas / bolas de fuego del Dragón:
            // Al igual que con las flechas, si se dispara a la bola de fuego destructible, chocan y se anulan
            DragonFlameHazard flame = other.GetComponent<DragonFlameHazard>();
            if (flame == null) flame = other.GetComponentInParent<DragonFlameHazard>();
            if (flame != null && flame.CanBeDestroyed)
            {
                _hasHit = true;
                Destroy(flame.gameObject);
                Destroy(gameObject);
                return true;
            }

            return false;
        }
    }
}

