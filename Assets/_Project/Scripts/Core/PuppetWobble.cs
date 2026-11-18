using UnityEngine;

namespace UnCuentoMas
{
    /// <summary>
    /// Efecto universal de balanceo, inclinación y elasticidad (Squash & Stretch) de marioneta sobre el Palito.
    /// - Oculta la punta del palito detrás del cuerpo del personaje, casa o torre.
    /// - Permite que el palito salga desde abajo (personajes/casas/dragón terrestre) o desde arriba (dragón aéreo).
    /// </summary>
    public class PuppetWobble : MonoBehaviour
    {
        [Header("=== 1. REFERENCIAS DEL TÍTERE Y PALITO ===")]
        [Tooltip("Transform visual que rota, rebota y se inclina.")]
        [SerializeField] private Transform visualPivot;

        [Tooltip("SpriteRenderer del Palito de madera (Frame 0 de Herramientas y Ataques).")]
        [SerializeField] private SpriteRenderer stickRenderer;

        [Tooltip("Sprite del Palito Roto (Frame 1 de Herramientas y Ataques) para el Final A.")]
        [SerializeField] private Sprite brokenStickSprite;

        [Header("=== 2. POSICIÓN DEL PALITO (PUNTA OCULTA DETRÁS) ===")]
        [Tooltip("Si es true, el palito sale hacia ARRIBA (usado por el Dragón en su Fase 1 Aérea). Si es false, sale hacia ABAJO.")]
        [SerializeField] private bool stickComesFromTop = false;

        [Tooltip("Altura local dentro del cuerpo donde queda oculta la punta del palito cuando sale desde abajo.")]
        [SerializeField] private float bottomStickInsertY = 1.85f;

        [Tooltip("Altura local detrás del cuerpo donde queda oculta la punta del palito cuando sale desde arriba.")]
        [SerializeField] private float topStickInsertY = 0.25f;

        [Header("=== 3. BALANCEO EN REPOSO (CASAS, TORRES, NPCS) ===")]
        [SerializeField] private float idleSwayFrequency = 2.2f;
        [SerializeField] private float idleSwayAngle = 1.6f;
        [SerializeField] private float idleBobHeight = 0.03f;

        [Header("=== 4. ANIMACIÓN SUAVE AL CAMINAR (MAGO Y ENEMIGOS) ===")]
        [SerializeField] private float walkSwayFrequency = 7.8f;
        [SerializeField] private float walkSwayAngle = 4.2f;
        [SerializeField] private float walkBobHeight = 0.065f;
        [SerializeField] private float moveTiltAngle = 3.2f;

        [Header("=== 5. MOVIMIENTO SUAVE DEL PALITO (MANO DEL TITIRITERO) ===")]
        [Tooltip("Ángulo suave con el que el palito se inclina y muñequea cuando el personaje camina.")]
        [SerializeField] private float stickWalkSwingAngle = 3.8f;
        [Tooltip("Desplazamiento horizontal sutil del palito al empujar al personaje.")]
        [SerializeField] private float stickHorizontalPush = 0.055f;
        [Tooltip("Pequeño bombeo vertical del palito como si una mano lo llevara desde abajo.")]
        [SerializeField] private float stickVerticalPump = 0.035f;
        [Tooltip("Escala en mundo que mantiene el palito para no achicarse aunque el personaje sea más pequeño.")]
        [SerializeField] private Vector2 stickWorldScaleBottom = new Vector2(0.52f, 1.05f);

        private Vector3 _initialLocalPos;
        private Vector3 _initialLocalScale;
        private Quaternion _initialLocalRot;
        private Vector3 _stickBaseLocalPos;
        private Vector3 _stickBaseLocalScale = new Vector3(0.95f, 1.45f, 1f);
        private float _randomPhase;
        private float _walkPhase;
        private float _walkBlend;
        private float _smoothedMoveDir;
        private bool _isMoving;
        private float _moveDirection;
        private float _shakeTimer;
        private float _shakeIntensity;
        private float _extraTiltAngle;
        private float _extraTiltTimer;
        private Vector2 _squashStretchOffset = Vector2.one;
        private bool _isStickBroken;
        private bool _isCutDown;

        public Transform VisualPivot => visualPivot;
        public SpriteRenderer StickRenderer => stickRenderer;
        public bool IsStickBroken => _isStickBroken;

        private void Awake()
        {
            // Buscamos o creamos el pivote visual hijo
            // CONCEPTO CLAVE DE ARQUITECTURA EN UNITY:
            // Separar el GameObject raíz (con Collider2D y Rigidbody2D) del Transform visual ("VisualPivot").
            // Esto permite rotar, inclinar y deformar el sprite libremente sin alterar el centro de masa de la física.
            if (visualPivot == null)
            {
                Transform childPivot = transform.Find("VisualPivot");
                visualPivot = childPivot != null ? childPivot : transform;
            }

            if (stickWorldScaleBottom.y < 1.05f)
            {
                stickWorldScaleBottom.y = 1.05f;
            }

            _initialLocalPos = visualPivot.localPosition;
            _initialLocalScale = visualPivot.localScale;
            _initialLocalRot = visualPivot.localRotation;
            
            // Fase aleatoria para que dos títeres en pantalla (ej. dos casas o NPCs) no oscilen exactamente al mismo tiempo
            _randomPhase = Random.Range(0f, 10f);
            _walkPhase = _randomPhase;

            ApplyStickOrientation(stickComesFromTop);
        }

        /// <summary>
        /// Bucle de animación procedimental en Update:
        /// Calcula en cada fotograma el balanceo senoidal (Mathf.Sin), el rebote de pasos,
        /// la vibración por impacto y la deformación elástica de papel (Squash & Stretch).
        /// </summary>
        private void Update()
        {
            if (visualPivot == null || _isCutDown) return;

            float idleTime = Time.time + _randomPhase;
            float facingSign = Mathf.Sign(transform.localScale.x);
            if (Mathf.Abs(facingSign) < 0.01f) facingSign = 1f;

            // 1. MEZCLA SUAVE (BLEND) ENTRE REPOSO Y CAMINATA:
            // Usamos Mathf.MoveTowards para pasar progresivamente de reposo (_walkBlend = 0) a caminata (_walkBlend = 1)
            // eliminando tirones bruscos cuando el personaje empieza a caminar o se detiene.
            float targetWalkBlend = (_isMoving && !_isStickBroken) ? 1f : 0f;
            _walkBlend = Mathf.MoveTowards(_walkBlend, targetWalkBlend, Time.deltaTime * 5.5f);

            // Suavizado de la dirección para inclinar el cuerpo hacia adelante según la marcha
            float targetLocalDir = _isMoving ? (_moveDirection * facingSign) : 0f;
            _smoothedMoveDir = Mathf.Lerp(_smoothedMoveDir, targetLocalDir, Time.deltaTime * 8.5f);

            // El ciclo de pasos avanza de forma continua y suave
            _walkPhase += Time.deltaTime * walkSwayFrequency * Mathf.Max(0.25f, _walkBlend);

            // 2. OSCILACIÓN EN REPOSO (IDLE):
            // Movimiento armónico simple con funciones trigonométricas senoidales
            float factor = _isStickBroken ? 0.25f : 1f;
            float idleZAngle = Mathf.Sin(idleTime * idleSwayFrequency) * (idleSwayAngle * factor);
            float idleBobY = Mathf.Sin(idleTime * idleSwayFrequency * 0.5f) * (idleBobHeight * factor);

            // 3. OSCILACIÓN AL CAMINAR (WALK):
            // Usamos una onda senoidal para el balanceo lateral y (1 - Cos(2x))/2 para el rebote vertical,
            // garantizando que cada paso produzca una subida y bajada suave como un títere en mano
            float walkWave = Mathf.Sin(_walkPhase);
            float smoothBobWave = (1f - Mathf.Cos(_walkPhase * 2f)) * 0.5f;
            float walkZAngle = (walkWave * walkSwayAngle) - (_smoothedMoveDir * moveTiltAngle);
            float walkBobY = smoothBobWave * walkBobHeight;

            // Interpolamos según el factor de mezcla (walkBlend)
            float targetZAngle = Mathf.Lerp(idleZAngle, walkZAngle, _walkBlend);
            float bobY = Mathf.Lerp(idleBobY, walkBobY, _walkBlend);

            // 4. ELASTICIDAD DE PAPEL AL PISAR (SQUASH & STRETCH):
            // Durante la caminata, el sprite se estira levemente al subir y se aplasta al bajar
            if (_walkBlend > 0.01f)
            {
                float stepStretch = 1f + smoothBobWave * 0.022f * _walkBlend;
                float stepSquash = 1f - smoothBobWave * 0.015f * _walkBlend;
                _squashStretchOffset = Vector2.Lerp(_squashStretchOffset, new Vector2(stepSquash, stepStretch), Time.deltaTime * 9f);
            }
            else
            {
                _squashStretchOffset = Vector2.Lerp(_squashStretchOffset, Vector2.one, Time.deltaTime * 8f);
            }

            // 5. SACUDIDA POR IMPACTO (HIT SHAKE):
            float shakeX = 0f;
            if (_shakeTimer > 0f)
            {
                _shakeTimer -= Time.deltaTime;
                targetZAngle += Random.Range(-_shakeIntensity, _shakeIntensity);
                shakeX = Random.Range(-0.045f, 0.045f);
            }

            // Inclinación adicional (por ejemplo para la telegrafía de ataque de los guerreros)
            if (_extraTiltTimer > 0f)
            {
                _extraTiltTimer -= Time.deltaTime;
                targetZAngle += _extraTiltAngle;
                if (_extraTiltTimer <= 0f) _extraTiltAngle = 0f;
            }

            // 6. APLICACIÓN FINAL DE TRANSFORMACIONES CON INTERPOLACIÓN ESFÉRICA (Slerp):
            Quaternion targetRot = _initialLocalRot * Quaternion.Euler(0f, 0f, targetZAngle);
            visualPivot.localRotation = Quaternion.Slerp(visualPivot.localRotation, targetRot, Time.deltaTime * 10f);

            Vector3 targetPos = _initialLocalPos + new Vector3(shakeX, bobY, 0f);
            visualPivot.localPosition = Vector3.Lerp(visualPivot.localPosition, targetPos, Time.deltaTime * 10f);

            visualPivot.localScale = new Vector3(
                _initialLocalScale.x * _squashStretchOffset.x,
                _initialLocalScale.y * _squashStretchOffset.y,
                _initialLocalScale.z
            );

            AnimatePuppeteerStick(idleTime);
        }

        /// <summary>
        /// Mueve suavemente el palito junto con el personaje cuando se desplaza,
        /// manteniendo además su tamaño en mundo constante para que el palito NO se haga más pequeño.
        /// </summary>
        private void AnimatePuppeteerStick(float idleTime)
        {
            if (stickRenderer == null) return;
            Transform st = stickRenderer.transform;

            UpdateConstantStickScale();

            Vector3 targetStickPos = _stickBaseLocalPos;
            float idleWave = Mathf.Sin(idleTime * idleSwayFrequency + 0.5f);
            Vector3 idleStickOffset = new Vector3(idleWave * 0.015f, Mathf.Cos(idleTime * idleSwayFrequency) * 0.012f, 0f);
            float idleStickAngle = idleWave * 1.1f;

            float handWave = Mathf.Sin(_walkPhase + 0.55f);
            float handCos = Mathf.Cos(_walkPhase);

            Vector3 walkStickOffset = new Vector3(
                (_smoothedMoveDir * stickHorizontalPush * 0.6f) + (handCos * stickHorizontalPush * 0.45f),
                Mathf.Sin(_walkPhase * 2f) * stickVerticalPump,
                0f
            );
            float walkStickAngle = (handWave * stickWalkSwingAngle) - (_smoothedMoveDir * 2.8f);

            targetStickPos += Vector3.Lerp(idleStickOffset, walkStickOffset, _walkBlend);
            float targetStickAngle = Mathf.Lerp(idleStickAngle, walkStickAngle, _walkBlend);

            if (_shakeTimer > 0f)
            {
                targetStickAngle += Random.Range(-4f, 4f);
            }

            st.localPosition = Vector3.Lerp(st.localPosition, targetStickPos, Time.deltaTime * 10f);
            st.localRotation = Quaternion.Slerp(st.localRotation, Quaternion.Euler(0f, 0f, targetStickAngle), Time.deltaTime * 10f);
            st.localScale = _stickBaseLocalScale;
        }

        private void UpdateConstantStickScale()
        {
            if (stickComesFromTop)
            {
                _stickBaseLocalScale = new Vector3(1.1f, -1.75f, 1f);
                return;
            }

            // Compensamos la escala del personaje para que el palito mantenga siempre su tamaño original en pantalla
            float parentScaleY = Mathf.Max(0.25f, Mathf.Abs(transform.localScale.y));
            _stickBaseLocalScale = new Vector3(
                stickWorldScaleBottom.x / parentScaleY,
                stickWorldScaleBottom.y / parentScaleY,
                1f
            );
        }

        public void ConfigureStick(Transform pivot, SpriteRenderer stick, float insertY)
        {
            visualPivot = pivot;
            stickRenderer = stick;
            bottomStickInsertY = insertY;
            ApplyStickOrientation(stickComesFromTop);
        }

        /// <summary>
        /// Configura si el palito sale hacia arriba (ej. Dragón volando) o hacia abajo (personajes en tierra),
        /// metiendo siempre la punta redondeada detrás del dibujo para que parezca pegado con cola/cinta.
        /// </summary>
        public void ApplyStickOrientation(bool fromTop)
        {
            stickComesFromTop = fromTop;
            if (stickRenderer == null) return;

            Transform st = stickRenderer.transform;
            if (fromTop)
            {
                // El pivote del Palito (TopCenter) se coloca detrás del lomo y se invierte en Y (-1.75) para que suba hacia el techo
                _stickBaseLocalPos = new Vector3(0.15f, topStickInsertY, 0f);
            }
            else
            {
                // El pivote del Palito (TopCenter) se mete dentro del torso (bottomStickInsertY) ocultando la punta detrás del sprite
                _stickBaseLocalPos = new Vector3(0f, bottomStickInsertY, 0f);
            }

            UpdateConstantStickScale();
            st.localPosition = _stickBaseLocalPos;
            st.localRotation = Quaternion.identity;
            st.localScale = _stickBaseLocalScale;
        }

        public void SetMovingState(bool isMoving, float horizontalDirection = 0f)
        {
            _isMoving = isMoving;
            _moveDirection = Mathf.Clamp(horizontalDirection, -1f, 1f);
        }

        /// <summary>
        /// Aplica un impulso elástico de papel (por ejemplo al saltar, aterrizar o lanzar un hechizo).
        /// </summary>
        public void TriggerPaperSquashStretch(float scaleX, float scaleY)
        {
            _squashStretchOffset = new Vector2(scaleX, scaleY);
        }

        public void TriggerHitShake(float duration = 0.22f, float angleIntensity = 10f)
        {
            _shakeTimer = Mathf.Max(_shakeTimer, duration);
            _shakeIntensity = angleIntensity;
        }

        /// <summary>
        /// Inclina visualmente el títere un ángulo específico durante cierto tiempo (por ejemplo en la telegrafía de ataque).
        /// </summary>
        public void TriggerVisualTilt(float angle, float duration = 0.5f)
        {
            _extraTiltAngle = angle;
            _extraTiltTimer = duration;
        }

        /// <summary>
        /// FINAL A: Cambia el Palito (Frame 0) por el Palito Roto (Frame 1 de Herramientas y Ataques).
        /// </summary>
        public void SwapToBrokenStick(Sprite customBrokenSprite = null)
        {
            _isStickBroken = true;
            TriggerHitShake(0.45f, 15f);

            Sprite targetSprite = customBrokenSprite != null ? customBrokenSprite : brokenStickSprite;
            if (stickRenderer != null && targetSprite != null)
            {
                stickRenderer.sprite = targetSprite;
            }
        }

        /// <summary>
        /// FINAL B: Cuando la Tijera corta el títere del mago.
        /// </summary>
        public void CollapseCutPuppet()
        {
            _isCutDown = true;
            if (visualPivot != null)
            {
                visualPivot.localRotation = Quaternion.Euler(0f, 0f, -85f);
                visualPivot.localPosition = _initialLocalPos + new Vector3(0f, -0.55f, 0f);
            }
        }
    }
}
