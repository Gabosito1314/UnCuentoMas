using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace UnCuentoMas
{
    /// <summary>
    /// Controlador del Jefe Dragón en Stage 3 (Cueva del Dragón):
    /// - Fase 1 Aérea ("Dragon Ataque Aereo"): Su Palito sale desde ARRIBA (ocultando la punta detrás del lomo)
    ///   y es vulnerable al Ataque Diagonal del Mago.
    /// - Fase 2 Terrestre ("Dragon Rojo Ataque y Final" frames 0 y 1): Su Palito cambia para salir desde ABAJO
    ///   y es vulnerable al Ataque Recto del Mago.
    /// - Fase 3 Derrotado ("Dragon Rojo Ataque y Final" frame 2): Revela la verdad y da paso a los 2 finales.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    [RequireComponent(typeof(PuppetWobble))]
    public class DragonBossController : MonoBehaviour
    {
        public enum BossPhase
        {
            Phase1_Aerial = 0,
            Phase2_Ground = 1,
            Phase3_DefeatedTruth = 2
        }

        [Header("=== 1. SPRITES DEL DRAGÓN ===")]
        [SerializeField] private SpriteRenderer dragonRenderer;
        [SerializeField] private Sprite[] aerialDragonFrames = new Sprite[3];
        [SerializeField] private Sprite[] groundAndFinalDragonFrames = new Sprite[3];
        [SerializeField] private Sprite[] dragonFlamesFrames = new Sprite[4];

        [Header("=== 2. SALUD Y POSICIONES DE FASE ===")]
        [SerializeField] private int maxHealth = 18;
        [SerializeField] private Vector3 aerialStagePosition = new Vector3(4.2f, 0.55f, 0f);
        [SerializeField] private Vector3 groundStagePosition = new Vector3(4.2f, -3.70f, 0f);
        [SerializeField] private Slider bossHealthSlider;

        [Header("=== 3. REFERENCIAS ===")]
        [SerializeField] private PlayerMagoController playerMago;
        [SerializeField] private StoryAndEndingManager storyManager;

        public UnityEvent OnPhase2GroundStarted;
        public UnityEvent OnDragonDefeatedForChoice;

        private BossPhase _currentPhase = BossPhase.Phase1_Aerial;
        private int _currentHealth;
        private bool _isCombatActive = true;
        private bool _isExecutingAttack;
        private bool _hasBeenAttackedByPlayer = false;
        private bool _groundAttackAlternate = false;
        private float _attackTimer = 2.0f;
        private float _wingFlapTimer;
        private int _wingFlapIndex;
        private PuppetWobble _wobble;
        private BoxCollider2D _collider;

        public bool IsCombatActive => _isCombatActive && _currentPhase != BossPhase.Phase3_DefeatedTruth;
        public BossPhase CurrentPhase => _currentPhase;

        /// <summary>
        /// Inicialización del Jefe Dragón:
        /// - En Fase 1 (Aérea), el dragón se suspende en el aire y su palito sale hacia ARRIBA hacia el techo del teatro.
        /// - Inicializa la barra de vida y asegura el desbloqueo del hechizo diagonal para el Mago.
        /// </summary>
        private void Awake()
        {
            _currentHealth = maxHealth;
            _wobble = GetComponent<PuppetWobble>();
            _collider = GetComponent<BoxCollider2D>();

            if (dragonRenderer == null)
            {
                dragonRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (playerMago == null) playerMago = FindFirstObjectByType<PlayerMagoController>();
            if (storyManager == null) storyManager = FindFirstObjectByType<StoryAndEndingManager>();

            if (playerMago != null)
            {
                playerMago.UnlockDiagonalAttack();
            }

            // En Fase 1 Aérea: El palito del dragón sale desde ARRIBA hacia el techo del teatro
            transform.position = aerialStagePosition;
            _wobble.ApplyStickOrientation(fromTop: true);
            SetAerialFrame(0);
            UpdateHealthBar();
        }

        /// <summary>
        /// Bucle principal del combate en Update:
        /// Ejecuta la lógica correspondiente a la fase activa (Fase 1 Aérea o Fase 2 Terrestre).
        /// </summary>
        private void Update()
        {
            if (!_isCombatActive || _currentPhase == BossPhase.Phase3_DefeatedTruth) return;

            if (_currentPhase == BossPhase.Phase1_Aerial)
            {
                UpdateAerialPhase();
            }
            else if (_currentPhase == BossPhase.Phase2_Ground)
            {
                UpdateGroundPhase();
            }
        }

        /// <summary>
        /// Comportamiento de Fase 1 (Aérea):
        /// 1. Desplazamiento horizontal continuo de derecha a izquierda entre X = 1.2f y X = 4.8f.
        /// 2. Orientación visual: Sigue al Player con la mirada (si el dragón va a la izquierda mira a la derecha hacia el Mago).
        /// 3. Regla de combate: El dragón NO empieza a atacar hasta que el Mago lo ataque primero.
        /// 4. Dispara bolas de fuego (Dragon Llamas_0) que se anulan si el jugador les dispara.
        /// </summary>
        private void UpdateAerialPhase()
        {
            // Desplazamiento horizontal de derecha a izquierda con mayor rango hacia la izquierda (de X = 4.8f hasta X = -2.4f)
            float patrolX = Mathf.PingPong(Time.time * 1.4f, 7.2f) - 2.4f;
            float hoverY = aerialStagePosition.y + Mathf.Sin(Time.time * 2.5f) * 0.35f;
            transform.position = Vector3.Lerp(
                transform.position,
                new Vector3(patrolX, hoverY, 0f),
                Time.deltaTime * 3.5f
            );

            // Orientación de la mirada siguiendo al Player:
            // Si el Mago está a la izquierda (dirX <= 0), scale.x es positivo (+0.76f)
            // Si el Mago está a la derecha (dirX > 0), scale.x es negativo (-0.76f) para mirarlo de frente
            if (playerMago != null)
            {
                float dirX = playerMago.transform.position.x - transform.position.x;
                Vector3 s = transform.localScale;
                s.x = Mathf.Abs(s.x) * (dirX <= 0f ? 1f : -1f);
                transform.localScale = s;
            }

            if (!_isExecutingAttack)
            {
                _wingFlapTimer += Time.deltaTime;
                if (_wingFlapTimer >= 0.32f)
                {
                    _wingFlapTimer = 0f;
                    _wingFlapIndex = (_wingFlapIndex == 0) ? 1 : 0;
                    SetAerialFrame(_wingFlapIndex);
                }
            }

            // REGLA CLAVE: El dragón NO empieza a atacar hasta que tú lo ataques primero
            if (!_hasBeenAttackedByPlayer) return;

            _attackTimer -= Time.deltaTime;
            if (_attackTimer <= 0f && !_isExecutingAttack)
            {
                _attackTimer = 2.4f;
                StartCoroutine(AerialAttackSequenceRoutine());
            }
        }

        /// <summary>
        /// Comportamiento de Fase 2 (Terrestre):
        /// - El dragón se apoya sobre el suelo y orienta su mirada hacia el jugador.
        /// - Ataques intercalados estrictamente entre:
        ///   1. Bola de fuego ("Dragon Llamas_0"): se anula si el Mago le dispara.
        ///   2. Ola de fuego terrestre ("Dragon Llamas_1"): no se puede destruir, solo se evita saltando.
        /// </summary>
        private void UpdateGroundPhase()
        {
            transform.position = Vector3.Lerp(transform.position, groundStagePosition, Time.deltaTime * 6f);

            if (playerMago != null)
            {
                float dirX = playerMago.transform.position.x - transform.position.x;
                Vector3 s = transform.localScale;
                s.x = Mathf.Abs(s.x) * (dirX <= 0f ? 1f : -1f);
                transform.localScale = s;
            }

            _attackTimer -= Time.deltaTime;
            if (_attackTimer <= 0f && !_isExecutingAttack)
            {
                _attackTimer = 2.3f;
                StartCoroutine(GroundAttackSequenceRoutine());
            }
        }

        private IEnumerator AerialAttackSequenceRoutine()
        {
            _isExecutingAttack = true;
            SetAerialFrame(2);
            _wobble.TriggerHitShake(0.2f, 7f);

            yield return new WaitForSeconds(0.28f);

            if (_currentPhase == BossPhase.Phase1_Aerial && playerMago != null)
            {
                // Lanza bola de fuego que se puede anular si el jugador le dispara
                SpawnFireballProjectile();

                // Puede lanzar a veces doble bola de fuego para que sea más peligroso
                if (Random.value < 0.65f)
                {
                    yield return new WaitForSeconds(0.38f);
                    if (_currentPhase == BossPhase.Phase1_Aerial && playerMago != null)
                    {
                        SetAerialFrame(2);
                        _wobble.TriggerHitShake(0.18f, 6.5f);
                        SpawnFireballProjectile();
                    }
                }
            }

            yield return new WaitForSeconds(0.45f);
            if (_currentPhase == BossPhase.Phase1_Aerial)
            {
                SetAerialFrame(0);
            }
            _isExecutingAttack = false;
        }

        private IEnumerator GroundAttackSequenceRoutine()
        {
            _isExecutingAttack = true;
            SetGroundFrame(1);
            _wobble.TriggerHitShake(0.22f, 8f);

            yield return new WaitForSeconds(0.28f);

            if (_currentPhase == BossPhase.Phase2_Ground)
            {
                // Alternar estrictamente entre los dos ataques terrestres:
                // Ataque A: Bola de fuego ("Dragon Llamas_0") -> anulable con hechizo.
                // Ataque B: Ola de fuego terrestre ("Dragon Llamas_1") -> indestructible, solo esquivable saltando.
                _groundAttackAlternate = !_groundAttackAlternate;

                if (_groundAttackAlternate)
                {
                    SpawnFireballProjectile();
                }
                else
                {
                    SpawnGroundFireWave();
                }
            }

            yield return new WaitForSeconds(0.5f);
            if (_currentPhase == BossPhase.Phase2_Ground)
            {
                SetGroundFrame(0);
            }
            _isExecutingAttack = false;
        }

        private void SpawnFireballProjectile()
        {
            if (dragonFlamesFrames == null || dragonFlamesFrames.Length < 1 || playerMago == null) return;

            AudioManager.Instance?.PlayDragonFireball();

            float faceDir = transform.localScale.x <= 0f ? 1.4f : -1.4f;
            Vector3 spawnPos = transform.position + new Vector3(faceDir, -0.2f, 0f);
            Vector2 target = playerMago.transform.position + new Vector3(0f, 0.9f, 0f);
            Vector2 dir = (target - (Vector2)spawnPos).normalized;

            GameObject go = new GameObject("DragonFlame_Fireball_Frame0");
            go.transform.position = spawnPos;
            go.transform.localScale = Vector3.one * 0.7f;

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            go.transform.rotation = Quaternion.Euler(0f, 0f, angle + 180f);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = dragonFlamesFrames[0];
            sr.sortingOrder = 22;

            Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;

            CircleCollider2D col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.7f;
            col.isTrigger = true;

            DragonFlameHazard hazard = go.AddComponent<DragonFlameHazard>();
            // Se puede anular si choca contra un hechizo del jugador (canBeDestroyed: true)
            hazard.InitializeMoving(dir, 5.8f, 3.5f, canBeDestroyed: true);
        }

        private void SpawnGroundFireWave()
        {
            if (dragonFlamesFrames == null || dragonFlamesFrames.Length < 2) return;

            AudioManager.Instance?.PlayDragonFireball();

            float faceDir = transform.localScale.x <= 0f ? 1.6f : -1.6f;
            Vector3 spawnPos = new Vector3(transform.position.x + faceDir, -3.95f, 0f);
            GameObject go = new GameObject("DragonFlame_GroundWave_Frame1");
            go.transform.position = spawnPos;
            go.transform.localScale = Vector3.one * 0.65f;

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = dragonFlamesFrames[1];
            sr.sortingOrder = 22;

            Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;

            BoxCollider2D col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(2.6f, 1.1f);
            col.offset = new Vector2(0f, 0.55f);
            col.isTrigger = true;

            DragonFlameHazard hazard = go.AddComponent<DragonFlameHazard>();
            Vector2 waveDir = transform.localScale.x <= 0f ? Vector2.right : Vector2.left;
            // Indestructible por hechizos: solo se puede esquivar saltando con 'W' / Espacio (canBeDestroyed: false)
            hazard.InitializeMoving(waveDir, 5.8f, 3.5f, canBeDestroyed: false);
        }

        private IEnumerator SpawnGroundFirePatchRoutine()
        {
            if (dragonFlamesFrames == null || dragonFlamesFrames.Length < 3 || playerMago == null) yield break;

            float targetX = Mathf.Clamp(playerMago.transform.position.x, -6.8f, 3.0f);
            Vector3 pos = new Vector3(targetX, -3.95f, 0f);

            GameObject go = new GameObject("DragonFlame_FloorFire_Frame2");
            go.transform.position = pos;
            go.transform.localScale = new Vector3(0.7f, 0.15f, 1f);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = dragonFlamesFrames[2];
            sr.color = new Color(1f, 1f, 1f, 0.5f);
            sr.sortingOrder = 22;

            yield return new WaitForSeconds(0.45f);
            if (go == null) yield break;

            go.transform.localScale = Vector3.one * 0.75f;
            sr.color = Color.white;

            Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;

            BoxCollider2D col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(2.0f, 1.8f);
            col.offset = new Vector2(0f, 0.9f);
            col.isTrigger = true;

            DragonFlameHazard hazard = go.AddComponent<DragonFlameHazard>();
            hazard.InitializeStationary(1.3f);
        }

        private IEnumerator SpawnFirePillarAtPlayerRoutine()
        {
            if (dragonFlamesFrames == null || dragonFlamesFrames.Length < 4 || playerMago == null) yield break;

            float targetX = Mathf.Clamp(playerMago.transform.position.x, -7.0f, 2.8f);
            Vector3 pos = new Vector3(targetX, -3.95f, 0f);

            GameObject go = new GameObject("DragonFlame_Pillar_Frame3");
            go.transform.position = pos;
            go.transform.localScale = new Vector3(0.65f, 0.15f, 1f);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = dragonFlamesFrames[3];
            sr.color = new Color(1f, 1f, 1f, 0.45f);
            sr.sortingOrder = 22;

            yield return new WaitForSeconds(0.5f);
            if (go == null) yield break;

            go.transform.localScale = Vector3.one * 0.8f;
            sr.color = Color.white;

            Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;

            BoxCollider2D col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1.8f, 3.2f);
            col.offset = new Vector2(0f, 1.6f);
            col.isTrigger = true;

            DragonFlameHazard hazard = go.AddComponent<DragonFlameHazard>();
            hazard.InitializeStationary(1.1f);
        }

        /// <summary>
        /// Procesa el impacto de un hechizo del Mago sobre el Dragón:
        /// - En Fase 1 (Aérea): solo recibe daño de hechizos Diagonales (aéreos).
        /// - En Fase 2 (Terrestre): solo recibe daño de hechizos Rectos (frontales).
        /// - Al llegar al 50% de vida (<= 6 HP): pasa a Fase 2 Terrestre.
        /// - Al llegar a 0 HP: pasa a Fase 3 (Derrotado) y activa la cinemática de la verdad.
        /// </summary>
        public bool TryHitDragon(bool isDiagonalShot, int damage = 1)
        {
            if (!IsCombatActive) return false;

            if (_currentPhase == BossPhase.Phase1_Aerial && !isDiagonalShot)
            {
                return false;
            }

            if (_currentPhase == BossPhase.Phase2_Ground && isDiagonalShot)
            {
                return false;
            }

            // Regla clave: el Mago debe atacarlo primero para que el dragón empiece el combate
            if (!_hasBeenAttackedByPlayer)
            {
                _hasBeenAttackedByPlayer = true;
                _attackTimer = 1.0f;
            }

            _currentHealth = Mathf.Max(0, _currentHealth - damage);
            AudioManager.Instance?.PlayDamageDragon();
            _wobble.TriggerPaperSquashStretch(0.88f, 1.12f);
            _wobble.TriggerHitShake(0.22f, 9f);
            StartCoroutine(DamageFlashRoutine());
            UpdateHealthBar();

            int phase2Threshold = Mathf.CeilToInt(maxHealth * 0.5f);

            // Transición a Fase 2 al 50% de salud
            if (_currentPhase == BossPhase.Phase1_Aerial && _currentHealth <= phase2Threshold)
            {
                EnterPhase2Ground();
            }
            // Derrota al llegar a 0 HP
            else if (_currentHealth <= 0)
            {
                EnterPhase3DefeatedAndRevealTruth();
            }

            return true;
        }

        /// <summary>
        /// Transición teatral a Fase 2 (Terrestre):
        /// - El dragón desciende al suelo.
        /// - El palito de madera cambia dinámicamente de orientación: antes salía hacia arriba,
        ///   ahora pasa a salir hacia abajo detrás de su panza.
        /// - Ajusta el colisionador 2D a la forma del cuerpo posado en el suelo.
        /// </summary>
        private void EnterPhase2Ground()
        {
            _currentPhase = BossPhase.Phase2_Ground;
            _isExecutingAttack = false;
            StopAllCoroutines();
            if (dragonRenderer != null) dragonRenderer.color = Color.white;

            transform.position = groundStagePosition;
            // En Fase 2 Terrestre: El palito cambia para salir desde ABAJO (ocultando la punta detrás de la panza del dragón)
            _wobble.ApplyStickOrientation(fromTop: false);

            if (_collider != null)
            {
                _collider.offset = new Vector2(0f, 1.4f);
                _collider.size = new Vector2(4.6f, 2.8f);
            }

            SetGroundFrame(0);
            _attackTimer = 1.5f;

            OnPhase2GroundStarted?.Invoke();
        }

        /// <summary>
        /// Transición a Fase 3 (Derrota y Revelación):
        /// Muestra el Frame 2 (dragón agonizante), detiene el combate y dispara la cinemática del Flashback
        /// en StoryAndEndingManager.
        /// </summary>
        private void EnterPhase3DefeatedAndRevealTruth()
        {
            _currentPhase = BossPhase.Phase3_DefeatedTruth;
            _isCombatActive = false;
            _isExecutingAttack = false;
            StopAllCoroutines();
            if (dragonRenderer != null) dragonRenderer.color = Color.white;

            transform.position = groundStagePosition;
            _wobble.ApplyStickOrientation(fromTop: false);

            // Frame 2 de "Dragon Rojo Ataque y Final": Dragón derrotado/agonizando en el suelo
            SetGroundFrame(2);

            OnDragonDefeatedForChoice?.Invoke();

            if (storyManager != null)
            {
                storyManager.StartDragonTruthAndChoices();
            }
        }

        private IEnumerator DamageFlashRoutine()
        {
            if (dragonRenderer == null) yield break;
            dragonRenderer.color = new Color(1f, 0.55f, 0.55f, 1f);
            yield return new WaitForSeconds(0.12f);
            dragonRenderer.color = Color.white;
        }

        private void SetAerialFrame(int index)
        {
            if (dragonRenderer != null && aerialDragonFrames != null && index >= 0 && index < aerialDragonFrames.Length && aerialDragonFrames[index] != null)
            {
                dragonRenderer.sprite = aerialDragonFrames[index];
            }
        }

        private void SetGroundFrame(int index)
        {
            if (dragonRenderer != null && groundAndFinalDragonFrames != null && index >= 0 && index < groundAndFinalDragonFrames.Length && groundAndFinalDragonFrames[index] != null)
            {
                dragonRenderer.sprite = groundAndFinalDragonFrames[index];
            }
        }

        private void UpdateHealthBar()
        {
            if (bossHealthSlider != null)
            {
                bossHealthSlider.maxValue = maxHealth;
                bossHealthSlider.value = _currentHealth;
            }
        }

        public void SetDragonVisible(bool visible)
        {
            if (dragonRenderer != null)
            {
                dragonRenderer.gameObject.SetActive(visible);
            }
        }

        /// <summary>
        /// En el Final A (Perdonar al Dragón): el Dragón vuelve a elevarse con su palito superior
        /// y sale volando hacia la izquierda rumbo al pueblo.
        /// </summary>
        public IEnumerator FlyAwayTowardsVillageRoutine(float duration = 1.9f)
        {
            SetDragonVisible(true);
            if (_wobble != null)
            {
                _wobble.ApplyStickOrientation(fromTop: true);
            }

            Vector3 startPos = transform.position;
            Vector3 endPos = new Vector3(-8.5f, 2.15f, 0f);
            float elapsed = 0f;
            float flapTimer = 0f;
            int flapFrame = 0;

            SetAerialFrame(0);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                flapTimer += Time.deltaTime;
                if (flapTimer >= 0.18f)
                {
                    flapTimer = 0f;
                    flapFrame = (flapFrame == 0) ? 1 : 0;
                    SetAerialFrame(flapFrame);
                }

                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                transform.position = Vector3.Lerp(startPos, endPos, t);
                yield return null;
            }
        }
    }

    public class DragonFlameHazard : MonoBehaviour
    {
        private Vector2 _moveDir;
        private float _speed;
        private bool _isMoving;
        private bool _canBeDestroyedBySpell = false;
        private bool _isDestroyed = false;

        public bool CanBeDestroyed => _canBeDestroyedBySpell;

        public void InitializeMoving(Vector2 dir, float speed, float lifeTime, bool canBeDestroyed = false)
        {
            _moveDir = dir.normalized;
            _speed = speed;
            _isMoving = true;
            _canBeDestroyedBySpell = canBeDestroyed;
            Destroy(gameObject, lifeTime);
        }

        public void InitializeStationary(float lifeTime)
        {
            _isMoving = false;
            _canBeDestroyedBySpell = false;
            Destroy(gameObject, lifeTime);
        }

        private void Update()
        {
            if (_isDestroyed) return;

            if (_isMoving)
            {
                transform.position += (Vector3)(_moveDir * _speed * Time.deltaTime);
            }

            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 0.65f);
            foreach (var h in hits)
            {
                if (h == null) continue;

                // 1. Intercepción con hechizos del Mago (si la llama es destructible, como Dragon Llamas_0)
                if (_canBeDestroyedBySpell)
                {
                    MagicProjectileBehaviour spell = h.GetComponent<MagicProjectileBehaviour>();
                    if (spell == null) spell = h.GetComponentInParent<MagicProjectileBehaviour>();
                    if (spell != null)
                    {
                        _isDestroyed = true;
                        Destroy(spell.gameObject);
                        Destroy(gameObject);
                        return;
                    }
                }

                // 2. Colisión con el Player
                PlayerMagoController player = h.GetComponentInParent<PlayerMagoController>();
                if (player != null)
                {
                    player.TakeDamage(1);
                    if (_isMoving)
                    {
                        _isDestroyed = true;
                        Destroy(gameObject);
                    }
                    return;
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_isDestroyed || other == null) return;

            if (_canBeDestroyedBySpell)
            {
                MagicProjectileBehaviour spell = other.GetComponent<MagicProjectileBehaviour>();
                if (spell == null) spell = other.GetComponentInParent<MagicProjectileBehaviour>();
                if (spell != null)
                {
                    _isDestroyed = true;
                    Destroy(spell.gameObject);
                    Destroy(gameObject);
                    return;
                }
            }

            PlayerMagoController player = other.GetComponentInParent<PlayerMagoController>();
            if (player != null)
            {
                player.TakeDamage(1);
                if (_isMoving)
                {
                    _isDestroyed = true;
                    Destroy(gameObject);
                }
            }
        }
    }
}
