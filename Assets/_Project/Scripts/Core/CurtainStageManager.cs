using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UnCuentoMas
{
    /// <summary>
    /// Controla el telón fijo del teatro usando las 3 imágenes exactas:
    /// - Cortinas Rojas Abiertas (se mantiene durante el gameplay)
    /// - Cortinas Rojas Entreabiertas (paso intermedio al cerrar o abrir)
    /// - Cortinas Rojas Cerradas (cierre total para cambiar de sección, de día o de escena)
    /// </summary>
    public class CurtainStageManager : MonoBehaviour
    {
        public enum CurtainVisualState
        {
            Open = 0,
            Half = 1,
            Closed = 2
        }

        public static bool IsVillageDay2 = false;
        public static bool IsReturningForEndingA = false;
        public static bool IsReturningForEndingB = false;
        public static bool EnterAtLastSectionFromRight = false;

        [Header("=== 1. LAS 3 IMÁGENES DE CORTINAS ROJAS ===")]
        [Tooltip("UI Image opcional que muestra el telón.")]
        [SerializeField] private Image curtainUIImage;

        [Tooltip("SpriteRenderer en primer plano que muestra el telón.")]
        [SerializeField] private SpriteRenderer curtainWorldRenderer;

        [Tooltip("Sprite: Cortinas Rojas Abiertas (se mantiene durante casi todo el juego).")]
        [SerializeField] private Sprite curtainsOpenSprite;

        [Tooltip("Sprite: Cortinas Rojas Entreabiertas (paso intermedio al cambiar de stage).")]
        [SerializeField] private Sprite curtainsHalfSprite;

        [Tooltip("Sprite: Cortinas Rojas Cerradas (finaliza cierre y oculta el cambio de stage).")]
        [SerializeField] private Sprite curtainsClosedSprite;

        [Tooltip("Duración de cada paso del telón (Abiertas -> Entreabiertas -> Cerradas).")]
        [SerializeField] private float curtainStepDuration = 0.28f;

        [Header("=== 2. CONFIGURACIÓN DEL STAGE ACTUAL (ADELANTE Y ATRÁS) ===")]
        [Tooltip("Nombre de la siguiente escena a cargar cuando el jugador avanza por la derecha en la última sección.")]
        [SerializeField] private string nextSceneName = "Stage2_Bosque";

        [Tooltip("Nombre de la escena anterior si el jugador retrocede por la izquierda desde la primera sección.")]
        [SerializeField] private string previousSceneName = "";

        [Tooltip("Posición X en el borde derecho donde el mago avanza a la siguiente sección o escena.")]
        [SerializeField] private float rightExitTriggerX = 6.2f;

        [Tooltip("Posición X en el borde izquierdo donde el mago retrocede a la sección o escena anterior.")]
        [SerializeField] private float leftExitTriggerX = -6.2f;

        [Tooltip("Posición X donde se coloca al mago a la izquierda cuando avanza hacia adelante.")]
        [SerializeField] private float playerStartX = -5.3f;

        [Tooltip("Posición X donde se coloca al mago a la derecha cuando retrocede o vuelve a casa en los finales.")]
        [SerializeField] private float playerReturnRightX = 5.3f;

        [Tooltip("Las secciones dentro del mismo Stage (Stage 1 Pueblo y Stage 2 Bosque).")]
        [SerializeField] private GameObject[] stageSubSections;

        [Tooltip("Si es true, avanzar a la siguiente sección o escena se realiza caminando hacia la IZQUIERDA (usado en Stage 4 Bosque de regreso).")]
        [SerializeField] private bool advanceGoingLeft = false;

        [Header("=== 3. REFERENCIAS ===")]
        [SerializeField] private PlayerMagoController playerMago;
        [SerializeField] private StoryAndEndingManager storyManager;

        [Header("=== 4. EVENTOS ===")]
        public UnityEvent<int> OnSubSectionChanged;

        private int _currentSubSectionIndex;
        private bool _isTransitioning;

        public bool IsTransitioning => _isTransitioning;
        public int CurrentSubSectionIndex => _currentSubSectionIndex;
        public GameObject[] StageSubSections => stageSubSections;

        /// <summary>
        /// Reinicia todas las variables estáticas persistentes del juego.
        /// Útil cuando el jugador pulsa la tecla 'R' tras terminar un final y desea iniciar una partida fresca desde el Acto 1.
        /// </summary>
        public static void ResetAllStaticProgress()
        {
            IsVillageDay2 = false;
            IsReturningForEndingA = false;
            IsReturningForEndingB = false;
            EnterAtLastSectionFromRight = false;
            PlayerMagoController.UseFinalWizardModelStatic = false;
            PlayerMagoController.DiagonalAttackUnlockedStatic = false;
            EnemyController.ResetStaticData();
            AudioManager.Instance?.ResetEndingState();
        }

        private void Awake()
        {
            if (playerMago == null) playerMago = FindFirstObjectByType<PlayerMagoController>();
            if (storyManager == null) storyManager = FindFirstObjectByType<StoryAndEndingManager>();
        }

        /// <summary>
        /// Start se ejecuta en el primer fotograma tras la inicialización.
        /// Determina por qué borde entra el Mago:
        /// - Si venimos de un final o caminamos de regreso hacia la izquierda: inicia en la derecha mirando a la izquierda.
        /// - Si avanzamos de forma regular: inicia en la izquierda (playerStartX) mirando a la derecha.
        /// Inicia la corrutina de apertura de telón para dar la bienvenida al público.
        /// </summary>
        private void Start()
        {
            bool isEndingReturn = IsReturningForEndingA || IsReturningForEndingB;

            if (isEndingReturn || advanceGoingLeft)
            {
                EnterAtLastSectionFromRight = false;
                _currentSubSectionIndex = 0;
                ActivateOnlySubSection(0);

                if (playerMago != null)
                {
                    Vector3 pos = playerMago.transform.position;
                    pos.x = playerReturnRightX;
                    playerMago.transform.position = pos;
                    playerMago.SetFacingDirection(false);
                }
            }
            else if (EnterAtLastSectionFromRight && stageSubSections != null && stageSubSections.Length > 0)
            {
                EnterAtLastSectionFromRight = false;
                _currentSubSectionIndex = stageSubSections.Length - 1;
                ActivateOnlySubSection(_currentSubSectionIndex);

                if (playerMago != null)
                {
                    Vector3 pos = playerMago.transform.position;
                    pos.x = playerReturnRightX;
                    playerMago.transform.position = pos;
                    playerMago.SetFacingDirection(false);
                }
            }
            else
            {
                EnterAtLastSectionFromRight = false;
                _currentSubSectionIndex = 0;
                ActivateOnlySubSection(0);

                if (playerMago != null)
                {
                    Vector3 pos = playerMago.transform.position;
                    pos.x = playerStartX;
                    playerMago.transform.position = pos;
                    playerMago.SetFacingDirection(true);
                }
            }

            // Iniciamos la animación de apertura del telón teatral al comenzar la escena
            StartCoroutine(OpeningCurtainSequenceRoutine());
        }

        /// <summary>
        /// Bucle de comprobación de bordes del escenario:
        /// Si el mago camina hasta el borde derecho (o izquierdo según el sentido de avance)
        /// y ha derrotado a todos los enemigos de la sección actual, el telón se cierra para cambiar de sección o escena.
        /// Regla de diseño: Solo se puede retroceder entre secciones del mismo stage, nunca volver a un stage previo.
        /// </summary>
        private void Update()
        {
            if (_isTransitioning || playerMago == null || !playerMago.CanControl) return;

            // En Stage 5 (final), el StoryAndEndingManager controla el final cuando el jugador llega a la zona requerida
            if (SceneManager.GetActiveScene().name == "Stage5_PuebloNoche") return;

            if (advanceGoingLeft)
            {
                // AVANZAR HACIA LA IZQUIERDA (Borde izquierdo -> siguiente Stage)
                if (playerMago.transform.position.x <= leftExitTriggerX && AreAllCurrentEnemiesDefeated())
                {
                    AdvanceStageOrSection();
                    return;
                }
            }
            else
            {
                // AVANZAR (Borde derecho -> siguiente imagen o siguiente Stage)
                if (playerMago.transform.position.x >= rightExitTriggerX && AreAllCurrentEnemiesDefeated())
                {
                    AdvanceStageOrSection();
                    return;
                }

                // RETROCEDER ENTRE IMÁGENES:
                // Regla estricta: NO se permite volver entre imágenes para mantener el ritmo del juego y evitar que el jugador huya del combate.
                // La única excepción narrativa es el tutorial del Acto 1 Día 1 (cuando el jugador visita el pueblo vacío y debe volver a casa).
                bool canReturnForDay1Tutorial = (SceneManager.GetActiveScene().name == "Stage1_Pueblo"
                    && !IsVillageDay2 && !IsReturningForEndingA && !IsReturningForEndingB && _currentSubSectionIndex == 1);

                if (canReturnForDay1Tutorial && stageSubSections != null && _currentSubSectionIndex > 0 && playerMago.transform.position.x <= leftExitTriggerX)
                {
                    GoBackStageOrSection();
                }
            }
        }

        /// <summary>
        /// Comprueba si todos los enemigos de la sección activa han sido derrotados.
        /// Impide que el jugador huya o avance dejando enemigos activos detrás.
        /// En Stage 2 Sección 2, también verifica que la Oleada 2 (refuerzos desde abajo) haya emergido y sido vencida.
        /// </summary>
        public bool AreAllCurrentEnemiesDefeated()
        {
            var stage2WaveMgr = FindFirstObjectByType<Stage2ForestWaveManager>();
            if (stage2WaveMgr != null && !stage2WaveMgr.IsSectionComplete(_currentSubSectionIndex))
            {
                return false;
            }

            EnemyController[] enemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
            foreach (var enemy in enemies)
            {
                if (enemy != null && enemy.gameObject.activeInHierarchy && !enemy.IsDefeated)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Avanza a la siguiente sección del mismo escenario o carga la siguiente escena si es la última sección.
        /// </summary>
        public void AdvanceStageOrSection()
        {
            if (_isTransitioning) return;

            if (stageSubSections != null && _currentSubSectionIndex + 1 < stageSubSections.Length)
            {
                StartCoroutine(SwitchSubSectionWithCurtainsRoutine(_currentSubSectionIndex + 1, spawnOnRightSide: advanceGoingLeft));
            }
            else if (!string.IsNullOrEmpty(nextSceneName))
            {
                if (storyManager != null && !storyManager.CanLeaveCurrentStageToNextScene())
                {
                    return;
                }

                EnterAtLastSectionFromRight = false;
                StartCoroutine(LoadSceneWithCurtainsRoutine(nextSceneName));
            }
        }

        /// <summary>
        /// Retrocede a la sección anterior dentro del mismo stage.
        /// </summary>
        public void GoBackStageOrSection()
        {
            if (_isTransitioning) return;

            // SOLO se puede volver entre secciones del mismo stage, NUNCA a una escena anterior
            if (stageSubSections != null && _currentSubSectionIndex > 0)
            {
                StartCoroutine(SwitchSubSectionWithCurtainsRoutine(_currentSubSectionIndex - 1, spawnOnRightSide: true));
            }
        }

        /// <summary>
        /// Carga una escena específica cerrando el telón teatral antes de llamar a SceneManager.LoadScene.
        /// </summary>
        public void LoadSpecificSceneWithCurtains(string targetSceneName)
        {
            if (_isTransitioning) return;
            EnterAtLastSectionFromRight = false;
            StartCoroutine(LoadSceneWithCurtainsRoutine(targetSceneName));
        }

        /// <summary>
        /// Secuencia de apertura del telón al iniciar:
        /// Cerradas -> Entreabiertas -> Abiertas.
        /// Desbloquea el movimiento del Mago una vez que el telón está completamente abierto.
        /// </summary>
        private IEnumerator OpeningCurtainSequenceRoutine()
        {
            _isTransitioning = true;
            if (playerMago != null) playerMago.SetControlEnabled(false);

            SetCurtainSprite(curtainsClosedSprite);
            yield return new WaitForSeconds(curtainStepDuration);

            SetCurtainSprite(curtainsHalfSprite);
            yield return new WaitForSeconds(curtainStepDuration);

            SetCurtainSprite(curtainsOpenSprite);

            if (storyManager != null)
            {
                storyManager.OnStageCurtainsOpened(_currentSubSectionIndex);
            }

            if (playerMago != null)
            {
                playerMago.SetControlEnabled(true);
            }

            _isTransitioning = false;
        }

        private IEnumerator SwitchSubSectionWithCurtainsRoutine(int nextIndex, bool spawnOnRightSide)
        {
            _isTransitioning = true;
            if (playerMago != null) playerMago.SetControlEnabled(false);

            HangingTextSignPuppet sign = FindFirstObjectByType<HangingTextSignPuppet>();
            if (sign != null) sign.HideSign();

            SetCurtainSprite(curtainsHalfSprite);
            yield return new WaitForSeconds(curtainStepDuration);

            SetCurtainSprite(curtainsClosedSprite);
            yield return new WaitForSeconds(curtainStepDuration * 1.15f);

            _currentSubSectionIndex = nextIndex;
            ActivateOnlySubSection(_currentSubSectionIndex);

            if (playerMago != null)
            {
                Vector3 pos = playerMago.transform.position;
                pos.x = spawnOnRightSide ? playerReturnRightX : playerStartX;
                playerMago.transform.position = pos;
                playerMago.SetFacingDirection(!spawnOnRightSide);
            }

            OnSubSectionChanged?.Invoke(_currentSubSectionIndex);
            yield return new WaitForSeconds(0.15f);

            SetCurtainSprite(curtainsHalfSprite);
            yield return new WaitForSeconds(curtainStepDuration);

            SetCurtainSprite(curtainsOpenSprite);

            // Temporizador de gracia de 0.5 segundos para los enemigos de la nueva sección al abrir el telón
            EnemyController[] sectionEnemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
            foreach (var enemy in sectionEnemies)
            {
                if (enemy != null && enemy.gameObject.activeInHierarchy && !enemy.IsDefeated)
                {
                    enemy.SetEntryWaitDelay(0.5f);
                }
            }

            if (storyManager != null)
            {
                storyManager.OnStageCurtainsOpened(_currentSubSectionIndex);
            }

            if (playerMago != null) playerMago.SetControlEnabled(true);
            _isTransitioning = false;
        }

        private IEnumerator LoadSceneWithCurtainsRoutine(string sceneToLoad)
        {
            _isTransitioning = true;
            if (playerMago != null) playerMago.SetControlEnabled(false);

            SetCurtainSprite(curtainsHalfSprite);
            yield return new WaitForSeconds(curtainStepDuration);

            SetCurtainSprite(curtainsClosedSprite);
            yield return new WaitForSeconds(curtainStepDuration * 1.25f);

            SceneManager.LoadScene(sceneToLoad);
        }

        public IEnumerator PlayCloseCurtainsOnlyRoutine(float customStepDuration = -1f)
        {
            float step = customStepDuration > 0f ? customStepDuration : curtainStepDuration;
            SetCurtainSprite(curtainsHalfSprite);
            yield return new WaitForSeconds(step);
            SetCurtainSprite(curtainsClosedSprite);
            yield return new WaitForSeconds(step);
        }

        public IEnumerator PlayOpenCurtainsOnlyRoutine(float customStepDuration = -1f)
        {
            float step = customStepDuration > 0f ? customStepDuration : curtainStepDuration;
            SetCurtainSprite(curtainsClosedSprite);
            yield return new WaitForSeconds(step * 0.5f);
            SetCurtainSprite(curtainsHalfSprite);
            yield return new WaitForSeconds(step);
            SetCurtainSprite(curtainsOpenSprite);
            yield return new WaitForSeconds(step * 0.4f);
        }

        public void SetCurtainVisualState(CurtainVisualState state)
        {
            switch (state)
            {
                case CurtainVisualState.Open:
                    SetCurtainSprite(curtainsOpenSprite);
                    break;
                case CurtainVisualState.Half:
                    SetCurtainSprite(curtainsHalfSprite);
                    break;
                case CurtainVisualState.Closed:
                    SetCurtainSprite(curtainsClosedSprite);
                    break;
            }
        }

        public void SetTransitionLock(bool locked)
        {
            _isTransitioning = locked;
        }

        public void ActivateOnlySubSection(int indexToActivate)
        {
            _currentSubSectionIndex = indexToActivate;
            if (stageSubSections == null || stageSubSections.Length == 0) return;
            for (int i = 0; i < stageSubSections.Length; i++)
            {
                if (stageSubSections[i] != null)
                {
                    stageSubSections[i].SetActive(i == indexToActivate);
                }
            }
        }

        public Vector3 GetPlayerStartLeftPosition(float currentY)
        {
            return new Vector3(playerStartX, currentY, 0f);
        }

        private void SetCurtainSprite(Sprite s)
        {
            if (s == null) return;
            if (curtainUIImage != null) curtainUIImage.sprite = s;
            if (curtainWorldRenderer != null) curtainWorldRenderer.sprite = s;
        }
    }
}
