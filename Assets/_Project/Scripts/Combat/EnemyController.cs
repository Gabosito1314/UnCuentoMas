using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace UnCuentoMas
{
    /// <summary>
    /// Controlador de los Enemigos del Bosque (Stage 2):
    /// - Guerrero Enemigo y Guerrero Enemigo Alternativo (Suelo, vulnerables a Ataque Recto).
    /// - Arquero Enemigo (Sobre Torres Medievales, vulnerable a Ataque Diagonal).
    /// Al morir pasan a su Frame 2 (sin máscara, revelando el escudo de la Corona Real).
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    [RequireComponent(typeof(PuppetWobble))]
    public class EnemyController : MonoBehaviour
    {
        public enum EnemyRole
        {
            GroundWarrior_Standard = 0,
            GroundWarrior_Alternative = 1,
            TowerArcher = 2
        }

        [Header("=== 1. TIPO DE ENEMIGO Y FRAMES (3 FRAMES + POSE DE DEFENSA) ===")]
        [SerializeField] private EnemyRole enemyRole = EnemyRole.GroundWarrior_Standard;
        [SerializeField] private SpriteRenderer enemySpriteRenderer;
        [SerializeField] private Sprite[] enemyFrames = new Sprite[3];
        [Tooltip("Segundo frame de 'Guerrero Enemigo Defensa' (pose de defensa con espada en alto).")]
        [SerializeField] private Sprite defensePoseSprite;

        [Header("=== 2. COMBATE Y VULNERABILIDAD ===")]
        [SerializeField] private int maxHealth = 2;
        [SerializeField] private float moveSpeed = 2.1f;
        [SerializeField] private float detectionRange = 11.5f;
        [SerializeField] private float attackRange = 1.65f;
        [SerializeField] private float attackCooldown = 1.85f;

        [Header("=== 3. FLECHA DEL ARQUERO (FRAME 3 DE HERRAMIENTAS) ===")]
        [SerializeField] private Sprite archerArrowSprite;
        [SerializeField] private Transform arrowSpawnPoint;

        [Header("=== 4. PISTA 'SOMETHING'S OFF' AL SER DERROTADO ===")]
        [SerializeField]
        private string defeatedTruthComment = "¡Se le cayó la máscara! Lleva la armadura con la Corona del Rey debajo del disfraz...";

        [Header("=== PAUSA INICIAL TEATRAL AL ENTRAR AL ESCENARIO ===")]
        [Tooltip("Segundos de pausa inicial para que el jugador evalúe el entorno antes de iniciar el combate.")]
        [SerializeField] private float entryWaitDelay = 0.5f;

        public UnityEvent OnEnemyDefeated;

        private int _currentHealth;
        private bool _isDefeated;
        private bool _isAttacking;
        private bool _isTelegraphing;
        private bool _isRecovering;
        private float _stunTimer;
        private float _lastAttackTime;
        private float _entryTimer = 0.5f;
        private bool _promptedDiagonalUnlock;

        private PuppetWobble _wobble;
        private PlayerMagoController _player;
        private StoryAndEndingManager _storyManager;

        public bool IsDefeated => _isDefeated;
        public EnemyRole Role => enemyRole;
        public bool IsInGuard => !_isDefeated && !_isRecovering && !_isTelegraphing && defensePoseSprite != null && enemySpriteRenderer != null && enemySpriteRenderer.sprite == defensePoseSprite;

        public void SetEntryWaitDelay(float delay)
        {
            entryWaitDelay = delay;
            _entryTimer = delay;
            _lastAttackTime = Time.time + delay;
        }

        private void OnEnable()
        {
            _entryTimer = entryWaitDelay;
            _lastAttackTime = Time.time + entryWaitDelay;
            if (_wobble != null) _wobble.SetMovingState(false);
        }

        private void Awake()
        {
            _currentHealth = maxHealth;
            _wobble = GetComponent<PuppetWobble>();
            if (enemySpriteRenderer == null)
            {
                enemySpriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (enemyRole == EnemyRole.TowerArcher)
            {
                if (enemySpriteRenderer != null)
                {
                    enemySpriteRenderer.sortingOrder = 8;
                }
                if (_wobble != null && _wobble.StickRenderer != null)
                {
                    _wobble.StickRenderer.sortingOrder = 7;
                }

                // Asegurar que las estructuras de torres cercanas queden por delante del arquero (orden 10 y palito 9)
                Transform parent = transform.parent;
                if (parent != null)
                {
                    var renderers = parent.GetComponentsInChildren<SpriteRenderer>(true);
                    foreach (var r in renderers)
                    {
                        if (r.gameObject.name.Contains("Torre") || (r.transform.parent != null && r.transform.parent.name.Contains("Torre")))
                        {
                            if (Mathf.Abs(r.transform.position.x - transform.position.x) < 2.0f)
                            {
                                if (r.gameObject.name == "Palito_Stick") r.sortingOrder = 9;
                                else r.sortingOrder = 10;
                            }
                        }
                    }
                }
            }

            _player = FindFirstObjectByType<PlayerMagoController>();
            _storyManager = FindFirstObjectByType<StoryAndEndingManager>();

            SetFrame(0);
        }

        /// <summary>
        /// Bucle de comportamiento de Inteligencia Artificial (IA) en Update:
        /// 1. Si está aturdido (Stun) o en periodo de recuperación (Recovery) tras atacar: no se mueve.
        /// 2. Orienta su mirada hacia el jugador.
        /// 3. Si es Arquero en Torre: ataca a distancia disparando flechas si el jugador está en rango.
        /// 4. Si es Guerrero de Tierra:
        ///    - Avanza en pose defensiva (escudo en alto). IMPORTANTE: NO hace daño por mero contacto al caminar.
        ///    - Al llegar a 1.5 metros, se detiene en seco e inicia su ciclo de telegrafía y ataque.
        /// </summary>
        private void Update()
        {
            if (_isDefeated || _player == null || !_player.CanControl)
            {
                _wobble.SetMovingState(false);
                return;
            }

            // Pausa inicial teatral al entrar al escenario: el enemigo observa al jugador y no ataca de inmediato
            if (_entryTimer > 0f)
            {
                _entryTimer -= Time.deltaTime;
                _wobble.SetMovingState(false);
                float dir = Mathf.Sign(_player.transform.position.x - transform.position.x);
                OrientTowardsPlayer(dir);
                return;
            }

            // Si está aturdido (micro-freno de 0.2s o stun de 0.45s) o en recuperación de 0.6s tras un ataque
            if (_stunTimer > 0f)
            {
                _stunTimer -= Time.deltaTime;
                _wobble.SetMovingState(false);
                return;
            }

            if (_isRecovering)
            {
                _wobble.SetMovingState(false);
                return;
            }

            float distToPlayer = Vector2.Distance(transform.position, _player.transform.position);
            float dirX = Mathf.Sign(_player.transform.position.x - transform.position.x);

            OrientTowardsPlayer(dirX);

            if (distToPlayer > detectionRange)
            {
                _wobble.SetMovingState(false);
                return;
            }

            // IA DEL ARQUERO EN TORRE:
            if (enemyRole == EnemyRole.TowerArcher)
            {
                if (!_promptedDiagonalUnlock)
                {
                    _promptedDiagonalUnlock = true;
                    _player.UnlockDiagonalAttack();
                }

                _wobble.SetMovingState(false);
                if (Time.time >= _lastAttackTime + attackCooldown && !_isAttacking)
                {
                    _lastAttackTime = Time.time;
                    StartCoroutine(ArcherShootRoutine());
                }
            }
            // IA DE GUERREROS TERRESTRES:
            else
            {
                float horizontalDist = Mathf.Abs(_player.transform.position.x - transform.position.x);

                // REGLA DE COMBATE: Los guerreros NO hacen daño al player por contacto al caminar
                if (horizontalDist > 1.5f && !_isAttacking)
                {
                    // Se aproxima al Mago
                    transform.position += new Vector3(dirX * moveSpeed * Time.deltaTime, 0f, 0f);
                    _wobble.SetMovingState(true, dirX);

                    // Avanza con escudo en pose de defensa si está a la vista
                    if (defensePoseSprite != null && enemySpriteRenderer != null)
                    {
                        enemySpriteRenderer.sprite = defensePoseSprite;
                    }
                    else
                    {
                        SetFrame(0);
                    }
                }
                else
                {
                    // Al llegar a 1.5m, se detiene en seco e inicia su telegrafía
                    _wobble.SetMovingState(false);
                    if (Time.time >= _lastAttackTime + attackCooldown && !_isAttacking && !_isRecovering)
                    {
                        _lastAttackTime = Time.time;
                        StartCoroutine(WarriorTelegraphedAttackRoutine(dirX));
                    }
                    else if (!_isAttacking && !_isRecovering && defensePoseSprite != null && enemySpriteRenderer != null)
                    {
                        enemySpriteRenderer.sprite = defensePoseSprite;
                    }
                }
            }
        }

        /// <summary>
        /// Corrutina de Ataque Telegrafiado del Guerrero:
        /// Fase 1: Telegrafía (0.5s): El enemigo se detiene, se inclina hacia atrás y tiembla.
        ///         Da tiempo al jugador para reaccionar (saltar con W/Espacio o deslizarse con Shift).
        /// Fase 2: Tajo de espada (Frame 1): Verifica si el jugador sigue enfrente dentro del rango.
        ///         Si el jugador esquivó detrás o saltó por encima, no recibe daño.
        /// Fase 3: Recuperación (0.6s): Ventana de vulnerabilidad para que el jugador contraataque.
        /// </summary>
        private IEnumerator WarriorTelegraphedAttackRoutine(float attackFacingDir)
        {
            _isAttacking = true;
            _isTelegraphing = true;

            // 1. Pausa de telegrafía de 0.5s:
            // Se detiene en seco, se inclina levemente hacia atrás y vibra indicando que va a atacar
            if (defensePoseSprite != null && enemySpriteRenderer != null)
            {
                enemySpriteRenderer.sprite = defensePoseSprite;
            }
            else
            {
                SetFrame(0);
            }

            _wobble.TriggerVisualTilt(-attackFacingDir * 12f, 0.5f);
            _wobble.TriggerHitShake(0.5f, 4.5f);

            yield return new WaitForSeconds(0.5f);

            _isTelegraphing = false;

            if (_isDefeated)
            {
                _isAttacking = false;
                yield break;
            }

            // 2. Tajo de espada
            SetFrame(1); // Pose de tajo
            _wobble.TriggerPaperSquashStretch(1.20f, 0.86f);
            _wobble.TriggerHitShake(0.15f, 9f);

            yield return new WaitForSeconds(0.12f);

            // Hace daño SOLO si el Mago sigue enfrente dentro del alcance en ese instante exacto
            // Si el jugador usó el dash para pasar a su espalda, o saltó alto, no recibe daño
            if (!_isDefeated && _player != null)
            {
                float currentDirToPlayer = Mathf.Sign(_player.transform.position.x - transform.position.x);
                float currentHorizontalDist = Mathf.Abs(_player.transform.position.x - transform.position.x);
                float currentVerticalDist = Mathf.Abs(_player.transform.position.y - transform.position.y);

                bool playerIsInFront = Mathf.Approximately(currentDirToPlayer, attackFacingDir) || (currentHorizontalDist < 0.25f);
                bool playerInRange = currentHorizontalDist <= 1.6f && currentVerticalDist <= 1.2f;

                if (playerIsInFront && playerInRange && !_player.IsInvulnerable && !_player.IsDashing)
                {
                    _player.TakeDamage(1);
                }
            }

            yield return new WaitForSeconds(0.18f);

            // 3. Pausa de recuperación de 0.6s: vulnerable sin defensa para que el jugador contraataque o salte
            _isRecovering = true;
            SetFrame(0);
            _wobble.TriggerPaperSquashStretch(0.92f, 1.05f);

            yield return new WaitForSeconds(0.6f);

            _isRecovering = false;
            _isAttacking = false;
        }

        private IEnumerator ArcherShootRoutine()
        {
            _isAttacking = true;
            SetFrame(1);
            _wobble.TriggerPaperSquashStretch(1.08f, 0.94f);
            _wobble.TriggerHitShake(0.15f, 6f);

            yield return new WaitForSeconds(0.28f);

            if (!_isDefeated && _player != null)
            {
                SpawnArrowTowardsPlayer();
            }

            yield return new WaitForSeconds(0.35f);
            if (!_isDefeated) SetFrame(0);
            _isAttacking = false;
        }

        private void SpawnArrowTowardsPlayer()
        {
            float sideSign = transform.localScale.x <= 0f ? 0.6f : -0.6f;
            Vector3 defaultSpawnOffset = new Vector3(sideSign, 1.1f, 0f);

            Vector3 spawnPos = arrowSpawnPoint != null
                ? arrowSpawnPoint.position
                : transform.position + defaultSpawnOffset;

            Vector2 targetPos = _player.transform.position + new Vector3(0f, 0.9f, 0f);
            Vector2 dir = (targetPos - (Vector2)spawnPos).normalized;

            GameObject arrowGO = new GameObject("EnemyArrow_Projectile");
            arrowGO.transform.position = spawnPos;
            arrowGO.transform.localScale = Vector3.one * 0.36f;

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            arrowGO.transform.rotation = Quaternion.Euler(0f, 0f, angle + 180f);

            SpriteRenderer sr = arrowGO.AddComponent<SpriteRenderer>();
            sr.sprite = archerArrowSprite;
            sr.sortingOrder = 26;

            Rigidbody2D rb = arrowGO.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;

            BoxCollider2D col = arrowGO.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.95f, 0.3f);
            col.isTrigger = true;

            EnemyArrowBehaviour arrow = arrowGO.AddComponent<EnemyArrowBehaviour>();
            arrow.Initialize(dir, 4.6f);
        }

        /// <summary>
        /// Procesa el impacto de un hechizo lanzado por el Mago:
        /// 1. Arquero en Torre: Requiere obligatoriamente un hechizo diagonal (aéreo). Los disparos rectos no lo alcanzan.
        /// 2. Guerrero Terrestre en Guardia (IsInGuard):
        ///    - Disparo Normal: el escudo bloquea el daño, pero sufre knockback leve y micro-freno de 0.2s.
        ///    - Disparo Cargado (Hechizo Potenciado): ROMPE LA GUARDIA, inflige 2 de daño (daño masivo) y gran knockback.
        /// 3. Guerrero fuera de guardia (durante telegrafía o recuperación): recibe daño directo.
        /// </summary>
        public bool TryHitWithPlayerSpell(bool isDiagonalSpell, bool isCharged = false, Vector2 hitDirection = default)
        {
            if (_isDefeated) return false;

            // 1. COMPROBACIÓN DE VULNERABILIDAD DEL ARQUERO EN TORRE
            if (enemyRole == EnemyRole.TowerArcher && !isDiagonalSpell)
            {
                return false;
            }

            if (enemyRole == EnemyRole.TowerArcher)
            {
                int archerDmg = isCharged ? 2 : 1;
                _currentHealth -= archerDmg;
                AudioManager.Instance?.PlayDamagePlayerOrEnemy();
                _wobble.TriggerPaperSquashStretch(0.85f, 1.15f);
                _wobble.TriggerHitShake(0.25f, 12f);
                StartCoroutine(DamageFlashRoutine());

                if (_currentHealth <= 0)
                {
                    DefeatAndRevealCrown();
                }
                return true;
            }

            // 2. GUERREROS TERRESTRES (GROUND WARRIOR)
            float knockbackDir = hitDirection != Vector2.zero
                ? Mathf.Sign(hitDirection.x)
                : (_player != null ? Mathf.Sign(transform.position.x - _player.transform.position.x) : 1f);

            // A. Si el guerrero está en guardia defensiva (escudo en alto)
            if (IsInGuard)
            {
                if (!isCharged)
                {
                    // Hechizo Normal: el escudo bloquea el daño, pero sufre knockback leve y micro-freno de 0.2s
                    _stunTimer = 0.2f;
                    ApplyKnockback(knockbackDir, 0.45f);
                    _wobble.TriggerHitShake(0.18f, 6.5f);
                    _wobble.TriggerPaperSquashStretch(0.92f, 1.08f);
                    return true;
                }
                else
                {
                    // Hechizo Potenciado: rompe la guardia, daño masivo (2) y knockback fuerte
                    _stunTimer = 0.45f;
                    ApplyKnockback(knockbackDir, 1.45f);
                    _wobble.TriggerHitShake(0.35f, 16f);
                    _wobble.TriggerPaperSquashStretch(1.28f, 0.78f);
                    _currentHealth -= 2;
                    AudioManager.Instance?.PlayDamagePlayerOrEnemy();

                    if (_currentHealth <= 0)
                    {
                        DefeatAndRevealCrown();
                    }
                    else
                    {
                        StartCoroutine(DamageFlashRoutine());
                    }
                    return true;
                }
            }

            // B. Si el guerrero NO está en guardia (durante telegrafía, recuperación de 0.6s o sin escudo)
            int dmg = isCharged ? 2 : 1;
            float knockDist = isCharged ? 1.45f : 0.50f;
            _stunTimer = isCharged ? 0.45f : 0.2f;

            ApplyKnockback(knockbackDir, knockDist);
            _wobble.TriggerHitShake(isCharged ? 0.35f : 0.22f, isCharged ? 15f : 10f);
            _wobble.TriggerPaperSquashStretch(isCharged ? 1.25f : 0.88f, isCharged ? 0.8f : 1.12f);
            _currentHealth -= dmg;
            AudioManager.Instance?.PlayDamagePlayerOrEnemy();

            if (_currentHealth <= 0)
            {
                DefeatAndRevealCrown();
            }
            else
            {
                StartCoroutine(DamageFlashRoutine());
            }

            return true;
        }

        /// <summary>
        /// Aplica desplazamiento por retroceso (Knockback) manteniendo al personaje dentro de los límites del escenario.
        /// </summary>
        private void ApplyKnockback(float dirX, float distance)
        {
            float sign = Mathf.Sign(dirX);
            Vector3 newPos = transform.position + new Vector3(sign * distance, 0f, 0f);
            newPos.x = Mathf.Clamp(newPos.x, -7.5f, 7.5f);
            transform.position = newPos;
        }

        /// <summary>
        /// Parpadeo rojizo visual al recibir daño.
        /// </summary>
        private IEnumerator DamageFlashRoutine()
        {
            if (enemySpriteRenderer == null) yield break;
            enemySpriteRenderer.color = new Color(1f, 0.55f, 0.55f, 1f);
            yield return new WaitForSeconds(0.12f);
            enemySpriteRenderer.color = Color.white;
        }

        /// <summary>
        /// Derrota del enemigo:
        /// 1. Desactiva su colisionador para que el Mago pueda pasar caminando a través de él.
        /// 2. Cambia al Frame 2: revela que bajo la capucha llevan la armadura con el sello real de la Corona.
        /// 3. Si es un soldado, se queda 2 segundos derrotado, muestra la revelación del uniforme y el titiritero lo retira hacia abajo.
        /// </summary>
        private void DefeatAndRevealCrown()
        {
            _isDefeated = true;
            _isAttacking = false;
            StopAllCoroutines();

            if (enemySpriteRenderer != null) enemySpriteRenderer.color = Color.white;

            // Frame 2: sentado/tumbado sin la máscara blanca, revelando el escudo de la Corona Real
            SetFrame(2);
            _wobble.SetMovingState(false);

            // Al quedar sentado/caído en su Frame 2, bajamos un poco la inserción del palito para que siga oculto detrás
            _wobble.ApplyStickOrientation(fromTop: false);

            BoxCollider2D col = GetComponent<BoxCollider2D>();
            if (col != null) col.enabled = false;

            OnEnemyDefeated?.Invoke();

            // Si es un guerrero terrestre o arquero en torre:
            // Su frame de derrotado se queda unos 2 segundos, luego alguien lo retira hacia abajo y baja el texto de sospecha
            StartCoroutine(EnemyDefeatAndRetireRoutine());
        }

        public static void ResetStaticData()
        {
        }

        /// <summary>
        /// Aplica un aturdimiento temporal (útil durante la emergencia teatral de enemigos desde abajo).
        /// </summary>
        public void ApplyStun(float duration)
        {
            _stunTimer = Mathf.Max(_stunTimer, duration);
        }

        /// <summary>
        /// Corrutina de retirada teatral del enemigo derrotado (soldados y arqueros):
        /// 1. Se mantiene visible en su Frame 2 durante 2 segundos exactos.
        /// 2. Baja el cartel colgante ("Caja de Texto") con la sospecha: "Esos uniformes se me hacen conocidos".
        /// 3. Simula que el titiritero tira del palillo hacia abajo por debajo del telón/escenario (Mathf.SmoothStep).
        ///    Los arqueros en torre (que están en Y superior) bajan 7.7f para llegar a la misma profundidad que los soldados normales.
        /// 4. Desactiva el GameObject al quedar fuera de pantalla.
        /// </summary>
        private IEnumerator EnemyDefeatAndRetireRoutine()
        {
            // Espera exacta de 2 segundos en pose de derrotado (Frame 2)
            yield return new WaitForSeconds(2.0f);

            // Baja el cuadro de texto teatral con el diálogo exacto solicitado
            if (_storyManager == null) _storyManager = FindFirstObjectByType<StoryAndEndingManager>();
            if (_storyManager != null)
            {
                _storyManager.ShowSignText("Esos uniformes se me hacen conocidos", ominous: false, autoHideSeconds: 4.0f);
            }

            // Animación suave de descenso hacia abajo por debajo del suelo del retablo
            Vector3 startPos = transform.position;
            // Para soldados desciende 4.5f (desde -3.95f hasta -8.45f).
            // Para arqueros desciende 7.7f (desde -0.75f hasta -8.45f) para llegar exactamente a la misma cota de fondo que los soldados normales.
            float descendDistance = (enemyRole == EnemyRole.TowerArcher) ? 7.7f : 4.5f;
            Vector3 targetPos = startPos + new Vector3(0f, -descendDistance, 0f);
            float duration = 0.95f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                transform.position = Vector3.Lerp(startPos, targetPos, t);
                yield return null;
            }

            gameObject.SetActive(false);
        }

        private void OrientTowardsPlayer(float dirToPlayerX)
        {
            Vector3 s = transform.localScale;
            s.x = Mathf.Abs(s.x) * (dirToPlayerX <= 0f ? 1f : -1f);
            transform.localScale = s;
        }

        private void SetFrame(int index)
        {
            if (enemySpriteRenderer != null && enemyFrames != null && index >= 0 && index < enemyFrames.Length && enemyFrames[index] != null)
            {
                enemySpriteRenderer.sprite = enemyFrames[index];
            }
        }
    }

    /// <summary>
    /// Comportamiento de las Flechas disparadas por los Arqueros de las torres:
    /// - Se desplaza en línea recta hacia donde estaba el jugador.
    /// - Puede ser destruida en el aire si es golpeada por un hechizo del Mago (mecánica de intercepción).
    /// </summary>
    public class EnemyArrowBehaviour : MonoBehaviour
    {
        private Vector2 _dir;
        private float _speed;

        public void Initialize(Vector2 dir, float speed)
        {
            _dir = dir.normalized;
            _speed = speed;
            Destroy(gameObject, 3.0f);
        }

        private void Update()
        {
            transform.position += (Vector3)(_dir * _speed * Time.deltaTime);

            // Escaneo activo por proximidad en cada frame para interceptar proyectiles o impactar al jugador
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 0.35f);
            foreach (var h in hits)
            {
                if (h == null) continue;

                // Intercepción: si choca contra un hechizo del Mago, ambos se destruyen mutuamente
                MagicProjectileBehaviour spell = h.GetComponent<MagicProjectileBehaviour>();
                if (spell == null) spell = h.GetComponentInParent<MagicProjectileBehaviour>();
                if (spell != null)
                {
                    Destroy(spell.gameObject);
                    Destroy(gameObject);
                    return;
                }

                // Daño al jugador si impacta en su cuerpo
                PlayerMagoController player = h.GetComponentInParent<PlayerMagoController>();
                if (player != null)
                {
                    player.TakeDamage(1);
                    Destroy(gameObject);
                    return;
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null) return;

            MagicProjectileBehaviour spell = other.GetComponent<MagicProjectileBehaviour>();
            if (spell == null) spell = other.GetComponentInParent<MagicProjectileBehaviour>();
            if (spell != null)
            {
                Destroy(spell.gameObject);
                Destroy(gameObject);
                return;
            }

            PlayerMagoController player = other.GetComponentInParent<PlayerMagoController>();
            if (player != null)
            {
                player.TakeDamage(1);
                Destroy(gameObject);
            }
        }
    }
}
