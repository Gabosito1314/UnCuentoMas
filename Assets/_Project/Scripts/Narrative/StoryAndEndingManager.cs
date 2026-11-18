using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UnCuentoMas
{
    /// <summary>
    /// Orquesta la secuencia narrativa completa de 5 pasos de "Un cuento más":
    /// 1) Acto 1 - Día 1: Tutorial con Muñeco ("Maniqui Intro" 0 -> 1) y Cartel ("Caja de Texto" con palito),
    ///    descubrimiento del pueblo misteriosamente vacío y regreso a la casa del Mago para cerrar el telón.
    /// 2) Acto 1 - Día 2: "Aldeanos y Rey" (Frame 0) y "Campesinos" (Frame 0) piden ayuda y siguen al Mago
    ///    con la mirada en todo momento (flipX automático).
    /// 3) Acto 2 - El Bosque: Combate contra Guerreros (con pose de defensa) y Arqueros en torres.
    /// 4) Acto 3 - La Cueva y Flashback de los Huevos: Al derrotar al Dragón, se cierra el telón y se muestra
    ///    el flashback con "Nido Dragon" en la parte derecha (Frame 0 intactos), los pueblerinos/Rey entran,
    ///    se acercan y se sobreponen a los huevos, y cuando se retiran ya se ven los huevos destruidos (Frame 1).
    ///    SOLO DESPUÉS de ver esta cinemática del dragón cambia el diseño del Player a "Player Mago Final".
    /// 5) Los 2 Finales Jugables de regreso en Stage 1:
    ///    - Final A (Perdonar al Dragón): El dragón vuela al pueblo; el Mago vuelve por la derecha, todas las casas
    ///      y pueblerinos desaparecieron salvo la casa del Mago, y al llegar a ella el telón se cierra lentamente.
    ///    - Final B (Matar al Dragón): El Mago vuelve con "Player Mago Final"; al llegar al pueblo aparecen
    ///      "Aldeanos y Rey" y "Campesinos" en Frame 1 (Traición) por ambos lados acorralándolo mientras el telón
    ///      se va cerrando paso a paso hasta cerrarse por completo cuando lo alcanzan.
    /// </summary>
    public class StoryAndEndingManager : MonoBehaviour
    {
        public enum StageNarrativeType
        {
            Stage1_Village = 0,
            Stage2_Forest = 1,
            Stage3_DragonCave = 2,
            Stage4_ForestNight = 3,
            Stage5_VillageNight = 4
        }

        [Header("=== 1. CONFIGURACIÓN DEL STAGE ACTUAL ===")]
        [SerializeField] private StageNarrativeType stageType = StageNarrativeType.Stage1_Village;

        [Header("=== 2. CARTEL DE TEXTO COLGANTE ('CAJA DE TEXTO' + PALITO) Y BOTONES FINALES ===")]
        [SerializeField] private HangingTextSignPuppet hangingTextSign;
        [SerializeField] private HangingTextSignPuppet choiceSignSpare;
        [SerializeField] private HangingTextSignPuppet choiceSignKill;
        [SerializeField] private TMP_Text objectiveText;
        [SerializeField] private TMP_Text subtitleText;
        [SerializeField] private GameObject endingChoicePanel;
        [SerializeField] private Button btnEndingA_SpareDragon;
        [SerializeField] private Button btnEndingB_KillDragon;

        public enum Stage1TutorialStep
        {
            Step0_MovementPrompt = 0,
            Step1_WaitingBothMoveKeys = 1,
            Step2_PromptGoToVillage = 2,
            Step3_WalkingToVillage = 3,
            Step4_InVillageEmptyPrompt = 4,
            Step5_ReturnedHomePractice = 5,
            Step6_WaitingStraightAttack = 6,
            Step7_PromptDiagonalAttack = 7,
            Step8_WaitingDiagonalAttack = 8,
            Step9_PromptDash = 9,
            Step10_WaitingDash = 10,
            Step11_PromptGoHome = 11,
            Step12_TransitioningToDay2 = 12,
            Step13_Day2Active = 13
        }

        [Header("=== 3. ELEMENTOS DE STAGE 1 (CASA DEL MAGO, MANIQUÍ, ALDEANOS Y REY, CAMPESINOS) ===")]
        [SerializeField] private TrainingDummyPuppet trainingDummy;
        [SerializeField] private GameObject practiceTowerRoot;
        [SerializeField] private TrainingDummyPuppet elevatedTrainingDummy;
        [SerializeField] private Sprite tower2_1Sprite;
        [SerializeField] private Sprite intactDummySprite;
        [SerializeField] private Sprite damagedDummySprite;
        [SerializeField] private Sprite puppetStickSprite;
        [SerializeField] private GameObject wizardHouseLeftObject;
        [SerializeField] private GameObject[] otherVillageHousesToHideInEndingA;
        [SerializeField] private StageNPCPuppet aldeanosYReyPuppet;
        [SerializeField] private StageNPCPuppet campesinosPuppet;
        [SerializeField] private StageNPCPuppet campesinosSec1Puppet;
        [SerializeField] private StageNPCPuppet aldeanosSec2Puppet;

        [Header("=== 4. ELEMENTOS DE STAGE 3 (NIDO DEL DRAGÓN Y FLASHBACK EN LA CUEVA) ===")]
        [SerializeField] private DragonBossController dragonBoss;
        [SerializeField] private SpriteRenderer dragonNestRenderer;
        [SerializeField] private Sprite[] dragonNestFrames = new Sprite[2];
        [SerializeField] private StageNPCPuppet flashbackAldeanosYReyPuppet;
        [SerializeField] private StageNPCPuppet flashbackCampesinosPuppet;

        [Header("=== 5. ELEMENTOS ADICIONALES DE FINALES ===")]
        [SerializeField] private Sprite brokenStickSprite;
        [SerializeField] private Transform giantScissorsTransform;
        [SerializeField] private GameObject endingAFlamesContainer;
        [SerializeField] private EpilogueSignPuppet epilogueSign;

        [Header("=== 6. REFERENCIAS PRINCIPALES ===")]
        [SerializeField] private PlayerMagoController playerMago;
        [SerializeField] private CurtainStageManager curtainManager;

        private Stage1TutorialStep _stage1TutorialStep;
        private bool _hasMovedLeft;
        private bool _hasMovedRight;
        private bool _hasHitTrainingDummyDay1;
        private bool _hasVisitedEmptyVillageDay1;
        private bool _isTransitioningDay1ToDay2;
        private bool _waitingForEndingChoice;
        private bool _endingSequenceStarted;
        private bool _endingFinished;

        /// <summary>
        /// OnEnable se ejecuta cada vez que el GameObject se activa o la escena inicia.
        /// Patrón de Suscripción a Eventos (Observer Pattern):
        /// Nos suscribimos a los eventos del Mago (OnAttackFired, OnDashFired) para que el gestor narrativo
        /// sepa cuándo el jugador disparó o esquivó, sin necesidad de consultar el teclado constantemente en Update.
        /// Desuscribirse antes de suscribirse (-= seguido de +=) evita suscripciones duplicadas.
        /// </summary>
        private void OnEnable()
        {
            if (playerMago == null) playerMago = FindFirstObjectByType<PlayerMagoController>();
            if (playerMago != null)
            {
                playerMago.OnAttackFired -= HandlePlayerAttackFired;
                playerMago.OnAttackFired += HandlePlayerAttackFired;
                playerMago.OnDashFired -= HandlePlayerDashFired;
                playerMago.OnDashFired += HandlePlayerDashFired;
            }
        }

        /// <summary>
        /// OnDisable se ejecuta cuando el GameObject se desactiva o se destruye al cambiar de escena.
        /// Buena práctica de arquitectura en Unity: Siempre desuscribirse de eventos para evitar
        /// fugas de memoria (memory leaks) o referencias a objetos destruidos (NullReferenceException).
        /// </summary>
        private void OnDisable()
        {
            if (playerMago != null)
            {
                playerMago.OnAttackFired -= HandlePlayerAttackFired;
                playerMago.OnDashFired -= HandlePlayerDashFired;
            }
        }

        /// <summary>
        /// Awake es el primer método del ciclo de vida de Unity.
        /// Se utiliza para auto-enlazar componentes de la escena si no fueron asignados en el Inspector,
        /// registrar callbacks de botones de la interfaz (UI Buttons) y preparar el estado del escenario.
        /// </summary>
        private void Awake()
        {
            if (playerMago == null) playerMago = FindFirstObjectByType<PlayerMagoController>();
            if (curtainManager == null) curtainManager = FindFirstObjectByType<CurtainStageManager>();
            if (hangingTextSign == null) hangingTextSign = FindFirstObjectByType<HangingTextSignPuppet>();
            if (choiceSignSpare == null) choiceSignSpare = GameObject.Find("Prop_Cartel_PerdonarDragon")?.GetComponent<HangingTextSignPuppet>();
            if (choiceSignKill == null) choiceSignKill = GameObject.Find("Prop_Cartel_MatarDragon")?.GetComponent<HangingTextSignPuppet>();
            if (dragonBoss == null) dragonBoss = FindFirstObjectByType<DragonBossController>();

            AutoFindTutorialSprites();

            if (aldeanosYReyPuppet == null) aldeanosYReyPuppet = GameObject.Find("NPC_Aldeanos_Y_Rey")?.GetComponent<StageNPCPuppet>();
            if (campesinosSec1Puppet == null) campesinosSec1Puppet = GameObject.Find("NPC_Campesinos_Sec1")?.GetComponent<StageNPCPuppet>();
            if (campesinosPuppet == null) campesinosPuppet = GameObject.Find("NPC_Campesinos")?.GetComponent<StageNPCPuppet>();
            if (aldeanosSec2Puppet == null) aldeanosSec2Puppet = GameObject.Find("NPC_Aldeanos_Seccion2")?.GetComponent<StageNPCPuppet>();

            // Ocultamos elementos de UI que solo se activan al llegar a los finales
            if (endingChoicePanel != null) endingChoicePanel.SetActive(false);
            if (endingAFlamesContainer != null) endingAFlamesContainer.SetActive(false);
            if (giantScissorsTransform != null) giantScissorsTransform.gameObject.SetActive(false);

            EnsureTopRightHUDExists();

            // Vinculamos los botones de decisión del Acto 3 (Perdonar vs. Matar al Dragón)
            if (btnEndingA_SpareDragon != null)
            {
                btnEndingA_SpareDragon.onClick.RemoveListener(SelectEndingA_SpareDragon);
                btnEndingA_SpareDragon.onClick.AddListener(SelectEndingA_SpareDragon);
            }

            if (btnEndingB_KillDragon != null)
            {
                btnEndingB_KillDragon.onClick.RemoveListener(SelectEndingB_KillDragon);
                btnEndingB_KillDragon.onClick.AddListener(SelectEndingB_KillDragon);
            }

            ConfigureInitialStageState();
        }

        /// <summary>
        /// Configura el escenario según el Acto narrativo actual y el progreso persistente entre escenas.
        /// Evalúa las banderas estáticas (IsReturningForEndingA, IsReturningForEndingB, IsVillageDay2)
        /// para saber si el jugador está en su primer día o regresando triunfal/trágicamente de la cueva.
        /// </summary>
        private void ConfigureInitialStageState()
        {
            if (stageType == StageNarrativeType.Stage1_Village)
            {
                if (CurtainStageManager.IsReturningForEndingA)
                {
                    ConfigureStage1ForEndingA();
                }
                else if (CurtainStageManager.IsReturningForEndingB)
                {
                    ConfigureStage1ForEndingB();
                }
                else if (CurtainStageManager.IsVillageDay2)
                {
                    ConfigureStage1ForDay2();
                }
                else
                {
                    ConfigureStage1ForDay1();
                }
            }
            else if (stageType == StageNarrativeType.Stage3_DragonCave)
            {
                // Regla narrativa: El diseño del Player Mago NO cambia hasta después de ver la cinemática de la verdad
                if (playerMago != null)
                {
                    playerMago.SwitchToFinalWizardModel(false);
                }

                // Asegura la existencia de los dos carteles teatrales colgantes
                EnsureStage3ChoiceSignsExist();

                // Ocultamos el nido, los carteles de decisión y los aldeanos del flashback durante el combate
                SetDragonNestActive(false);
                if (choiceSignSpare != null) choiceSignSpare.HideSign();
                if (choiceSignKill != null) choiceSignKill.HideSign();
                if (flashbackAldeanosYReyPuppet != null) flashbackAldeanosYReyPuppet.gameObject.SetActive(false);
                if (flashbackCampesinosPuppet != null) flashbackCampesinosPuppet.gameObject.SetActive(false);
            }
            else if (stageType == StageNarrativeType.Stage4_ForestNight)
            {
                // De regreso por el bosque de noche: el Mago ya viste como "Player Mago Final"
                if (playerMago != null)
                {
                    playerMago.SwitchToFinalWizardModel(true);
                }
            }
            else if (stageType == StageNarrativeType.Stage5_VillageNight)
            {
                if (playerMago != null)
                {
                    playerMago.SwitchToFinalWizardModel(true);
                }

                if (CurtainStageManager.IsReturningForEndingA)
                {
                    ConfigureStage5ForEndingA();
                }
                else
                {
                    ConfigureStage5ForEndingB();
                }
            }
        }

        /// <summary>
        /// Configuración del Día 1:
        /// El mago inicia sin ataques ni dash, solo pudiendo moverse y saltar.
        /// No hay enemigos ni pueblerinos; solo la casa del mago para permitir el tutorial de movimiento.
        /// </summary>
        private void ConfigureStage1ForDay1()
        {
            if (playerMago != null)
            {
                playerMago.SwitchToFinalWizardModel(false);
                playerMago.SetAttacksEnabled(false);
                playerMago.SetDashEnabled(false);
            }

            // Día 1 Inicial: Solo la casa del Mago; sin muñeco de práctica ni habitantes
            if (trainingDummy != null) trainingDummy.gameObject.SetActive(false);
            if (practiceTowerRoot != null) practiceTowerRoot.SetActive(false);
            if (elevatedTrainingDummy != null) elevatedTrainingDummy.gameObject.SetActive(false);
            if (aldeanosYReyPuppet != null) aldeanosYReyPuppet.gameObject.SetActive(false);
            if (campesinosSec1Puppet != null) campesinosSec1Puppet.gameObject.SetActive(false);
            if (campesinosPuppet != null) campesinosPuppet.gameObject.SetActive(false);
            if (aldeanosSec2Puppet != null) aldeanosSec2Puppet.gameObject.SetActive(false);

            if (otherVillageHousesToHideInEndingA != null)
            {
                foreach (var house in otherVillageHousesToHideInEndingA)
                {
                    if (house != null) house.SetActive(true);
                }
            }

            _stage1TutorialStep = Stage1TutorialStep.Step0_MovementPrompt;
            _hasMovedLeft = false;
            _hasMovedRight = false;
            _hasVisitedEmptyVillageDay1 = false;

            StartCoroutine(Stage1MovementTutorialRoutine());
        }

        /// <summary>
        /// Configuración del Día 2:
        /// Tras la elipsis nocturna, el pueblo despierta alarmado.
        /// Los habitantes y el Rey aparecen frente a la casa del Mago (Sección 1 con 2 grupos) para rogarle que derrote al dragón.
        /// Y en la Sección 2 (la imagen de sus casas), los pueblerinos están en sus puestos frente a sus hogares a nivel del suelo (-3.95f).
        /// El Mago recupera todas sus facultades de combate.
        /// </summary>
        private void ConfigureStage1ForDay2()
        {
            if (playerMago != null)
            {
                playerMago.SwitchToFinalWizardModel(false);
                playerMago.SetAttacksEnabled(true);
                playerMago.SetDashEnabled(true);
            }

            if (trainingDummy != null) trainingDummy.gameObject.SetActive(false);
            if (practiceTowerRoot != null) practiceTowerRoot.SetActive(false);
            if (elevatedTrainingDummy != null) elevatedTrainingDummy.gameObject.SetActive(false);

            Transform section0 = null;
            Transform section1 = null;
            if (curtainManager != null && curtainManager.StageSubSections != null)
            {
                if (curtainManager.StageSubSections.Length > 0 && curtainManager.StageSubSections[0] != null)
                    section0 = curtainManager.StageSubSections[0].transform;
                if (curtainManager.StageSubSections.Length > 1 && curtainManager.StageSubSections[1] != null)
                    section1 = curtainManager.StageSubSections[1].transform;
            }
            if (section0 == null)
            {
                GameObject secGO = GameObject.Find("Pueblo_Seccion_1_Casas1_Y_Aldeanos");
                if (secGO != null) section0 = secGO.transform;
            }
            if (section1 == null)
            {
                GameObject secGO = GameObject.Find("Pueblo_Seccion_2_Casas2_Y_Campesinos");
                if (secGO != null) section1 = secGO.transform;
            }

            // Día 2 - Sección 1: 2 grupos de habitantes frente a la casa del Mago (a nivel de suelo -3.95f)
            // Grupo 1: Aldeanos en el que está el Rey (Frame 0)
            if (aldeanosYReyPuppet != null)
            {
                aldeanosYReyPuppet.gameObject.SetActive(true);
                if (section0 != null) aldeanosYReyPuppet.transform.SetParent(section0, true);
                aldeanosYReyPuppet.transform.position = new Vector3(-0.95f, -3.95f, 0f);
                aldeanosYReyPuppet.SetFrame(0);
                aldeanosYReyPuppet.SetFollowPlayerGaze(true);
            }

            // Grupo 2: Solo el grupo de pueblerinos (SIN EL REY, Frame 0)
            if (campesinosSec1Puppet != null)
            {
                campesinosSec1Puppet.gameObject.SetActive(true);
                if (section0 != null) campesinosSec1Puppet.transform.SetParent(section0, true);
                campesinosSec1Puppet.transform.position = new Vector3(2.35f, -3.95f, 0f);
                campesinosSec1Puppet.SetFrame(0);
                campesinosSec1Puppet.SetFollowPlayerGaze(true);
            }
            else if (campesinosPuppet != null && section0 != null)
            {
                // Fallback clonando a los campesinos (que NO tienen rey) para sección 1
                GameObject cloneSec1 = Instantiate(campesinosPuppet.gameObject, section0);
                cloneSec1.name = "NPC_Campesinos_Sec1";
                cloneSec1.transform.position = new Vector3(2.35f, -3.95f, 0f);
                cloneSec1.SetActive(true);
                campesinosSec1Puppet = cloneSec1.GetComponent<StageNPCPuppet>();
                if (campesinosSec1Puppet != null)
                {
                    campesinosSec1Puppet.SetFrame(0);
                    campesinosSec1Puppet.SetFollowPlayerGaze(true);
                }
            }

            // Día 2 - Sección 2: 2 grupos de pueblerinos en la imagen de sus casas (AMBOS SIN EL REY) a nivel de suelo (-3.95f)
            // Grupo 1 de pueblerinos: Campesinos (sin rey)
            if (campesinosPuppet != null)
            {
                campesinosPuppet.gameObject.SetActive(true);
                if (section1 != null && campesinosPuppet.transform.parent != section1)
                {
                    campesinosPuppet.transform.SetParent(section1, true);
                }
                campesinosPuppet.transform.position = new Vector3(2.35f, -3.95f, 0f);
                campesinosPuppet.SetFrame(0);
                campesinosPuppet.SetFollowPlayerGaze(true);
            }

            // Grupo 2 de pueblerinos: Aldeanos (sin rey)
            if (aldeanosSec2Puppet != null)
            {
                aldeanosSec2Puppet.gameObject.SetActive(true);
                if (section1 != null && aldeanosSec2Puppet.transform.parent != section1)
                {
                    aldeanosSec2Puppet.transform.SetParent(section1, true);
                }
                aldeanosSec2Puppet.transform.position = new Vector3(-1.8f, -3.95f, 0f);
                aldeanosSec2Puppet.SetFrame(0);
                aldeanosSec2Puppet.SetFollowPlayerGaze(true);
            }
            else if (campesinosPuppet != null && section1 != null)
            {
                // Fallback asegurando pueblerinos (sin rey) en Sección 2
                GameObject cloneSec2 = Instantiate(campesinosPuppet.gameObject, section1);
                cloneSec2.name = "NPC_Aldeanos_Seccion2";
                cloneSec2.transform.position = new Vector3(-1.8f, -3.95f, 0f);
                cloneSec2.SetActive(true);
                aldeanosSec2Puppet = cloneSec2.GetComponent<StageNPCPuppet>();
                if (aldeanosSec2Puppet != null)
                {
                    aldeanosSec2Puppet.SetFrame(0);
                    aldeanosSec2Puppet.SetFollowPlayerGaze(true);
                }
            }
        }

        private void EnsurePracticeTowerCreated()
        {
            if (practiceTowerRoot != null) return;

            Transform section0 = null;
            if (curtainManager != null && curtainManager.StageSubSections != null && curtainManager.StageSubSections.Length > 0 && curtainManager.StageSubSections[0] != null)
            {
                section0 = curtainManager.StageSubSections[0].transform;
            }
            if (section0 == null)
            {
                GameObject secGO = GameObject.Find("Pueblo_Seccion_1_Casas1_Y_Aldeanos");
                if (secGO != null) section0 = secGO.transform;
            }

            AutoFindTutorialSprites();

            practiceTowerRoot = new GameObject("Torre_Practica_Tutorial");
            if (section0 != null) practiceTowerRoot.transform.SetParent(section0, true);
            practiceTowerRoot.transform.position = new Vector3(4.5f, -3.95f, 0f);
            practiceTowerRoot.transform.localScale = new Vector3(0.48f, 0.48f, 1f);

            // 1. VisualPivot de la Torre
            GameObject towerVisual = new GameObject("VisualPivot");
            towerVisual.transform.SetParent(practiceTowerRoot.transform, false);

            SpriteRenderer towerSr = towerVisual.AddComponent<SpriteRenderer>();
            towerSr.sprite = tower2_1Sprite;
            towerSr.sortingOrder = 10; // Frente de la torre

            // 2. Palito de madera para la Torre (¡con palito como todas las demás marionetas!)
            GameObject towerStickGO = new GameObject("Palito_Stick");
            towerStickGO.transform.SetParent(towerVisual.transform, false);
            towerStickGO.transform.localPosition = new Vector3(0f, 2.8f, 0f);
            towerStickGO.transform.localScale = new Vector3(0.52f / 0.48f, 1.05f / 0.48f, 1f);

            SpriteRenderer towerStickSr = towerStickGO.AddComponent<SpriteRenderer>();
            towerStickSr.sprite = puppetStickSprite;
            towerStickSr.sortingOrder = 9; // Justo detrás de la torre

            PuppetWobble towerWobble = practiceTowerRoot.AddComponent<PuppetWobble>();
            towerWobble.ConfigureStick(towerVisual.transform, towerStickSr, 2.8f);

            // 3. Muñeco de prueba parado sobre la torre (en la cima, de pie) con su palito detrás de la torre
            GameObject dummyGO = new GameObject("Maniqui_Elevado_Torre");
            dummyGO.transform.SetParent(towerVisual.transform, false);
            dummyGO.transform.localPosition = new Vector3(0f, 6.45f, 0f); // Parado sobre la cima de la torre (no hundido)
            dummyGO.transform.localScale = Vector3.one * 0.85f;

            GameObject dummyVisual = new GameObject("VisualPivot");
            dummyVisual.transform.SetParent(dummyGO.transform, false);

            SpriteRenderer dummySr = dummyVisual.AddComponent<SpriteRenderer>();
            dummySr.sprite = intactDummySprite;
            dummySr.sortingOrder = 11; // Parado sobre la torre, completamente visible de pie

            GameObject dummyStickGO = new GameObject("Palito_Stick");
            dummyStickGO.transform.SetParent(dummyVisual.transform, false);
            dummyStickGO.transform.localPosition = new Vector3(0f, 1.85f, 0f);
            dummyStickGO.transform.localScale = new Vector3(0.52f / 0.85f, 0.78f / 0.85f, 1f);

            SpriteRenderer dummyStickSr = dummyStickGO.AddComponent<SpriteRenderer>();
            dummyStickSr.sprite = puppetStickSprite;
            dummyStickSr.sortingOrder = 7; // Detrás de la torre (10), justo como los demás

            BoxCollider2D dummyCol = dummyGO.AddComponent<BoxCollider2D>();
            dummyCol.size = new Vector2(2.4f, 4.8f);
            dummyCol.offset = new Vector2(0f, 2.4f);
            dummyCol.isTrigger = true;

            PuppetWobble dummyWobble = dummyGO.AddComponent<PuppetWobble>();
            dummyWobble.ConfigureStick(dummyVisual.transform, dummyStickSr, 1.85f);

            elevatedTrainingDummy = dummyGO.AddComponent<TrainingDummyPuppet>();
            elevatedTrainingDummy.ConfigureSprites(intactDummySprite, damagedDummySprite);
            elevatedTrainingDummy.SetElevated(true);
        }

        private void AutoFindTutorialSprites()
        {
            if (puppetStickSprite == null)
            {
                puppetStickSprite = TheaterSpriteLibrary.Get("Herramientas y Ataques_0");
                if (puppetStickSprite == null)
                {
                    Transform stickTransform = GameObject.Find("Palito_Stick")?.transform;
                    if (stickTransform != null)
                    {
                        SpriteRenderer sr = stickTransform.GetComponent<SpriteRenderer>();
                        if (sr != null && sr.sprite != null) puppetStickSprite = sr.sprite;
                    }
                }
            }
            if (tower2_1Sprite == null) tower2_1Sprite = TheaterSpriteLibrary.Get("Torres Medievales 2_1");
            if (intactDummySprite == null) intactDummySprite = TheaterSpriteLibrary.Get("Maniqui Intro_0");
            if (damagedDummySprite == null) damagedDummySprite = TheaterSpriteLibrary.Get("Maniqui Intro_1");

#if UNITY_EDITOR
            if (puppetStickSprite == null)
            {
                var sprites = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Sprites/Herramientas y Ataques.png");
                foreach (var s in sprites)
                {
                    if (s is Sprite sp && sp.name == "Herramientas y Ataques_0") { puppetStickSprite = sp; break; }
                }
            }
            if (tower2_1Sprite == null)
            {
                var sprites = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Sprites/Torres Medievales 2.png");
                foreach (var s in sprites)
                {
                    if (s is Sprite sp && sp.name == "Torres Medievales 2_1") { tower2_1Sprite = sp; break; }
                }
            }
            if (intactDummySprite == null || damagedDummySprite == null)
            {
                var sprites = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Sprites/Maniqui Intro.png");
                foreach (var s in sprites)
                {
                    if (s is Sprite sp)
                    {
                        if (sp.name == "Maniqui Intro_0") intactDummySprite = sp;
                        if (sp.name == "Maniqui Intro_1") damagedDummySprite = sp;
                    }
                }
            }
#endif
        }

        private void ConfigureStage1ForEndingA()
        {
            // Final A (Perdonar al Dragón): Todas las casas del pueblo y sus habitantes desaparecieron;
            // solo queda en pie la casa del Mago a la izquierda. Su diseño ya es "Player Mago Final" tras la cinemática de la cueva.
            if (playerMago != null)
            {
                playerMago.SwitchToFinalWizardModel(true);
            }

            if (trainingDummy != null) trainingDummy.gameObject.SetActive(false);
            if (aldeanosYReyPuppet != null) aldeanosYReyPuppet.gameObject.SetActive(false);
            if (campesinosPuppet != null) campesinosPuppet.gameObject.SetActive(false);

            if (otherVillageHousesToHideInEndingA != null)
            {
                foreach (var house in otherVillageHousesToHideInEndingA)
                {
                    if (house != null) house.SetActive(false);
                }
            }

            if (wizardHouseLeftObject != null)
            {
                wizardHouseLeftObject.SetActive(true);
            }

            // Agrega 2 llamas (Dragon Llamas_2) para mostrar que el dragón devastó todo el pueblo
            EnsureEndingAFlamesExist();
        }

        private void ConfigureStage1ForEndingB()
        {
            // Final B (Matar al Dragón): El Mago regresa con el sprite "Player Mago Final" desde la derecha.
            if (playerMago != null)
            {
                playerMago.SwitchToFinalWizardModel(true);
            }

            if (trainingDummy != null) trainingDummy.gameObject.SetActive(false);
            if (aldeanosYReyPuppet != null) aldeanosYReyPuppet.gameObject.SetActive(false);
            if (campesinosPuppet != null) campesinosPuppet.gameObject.SetActive(false);

            var flames = GameObject.Find("EndingA_DevastationFlames");
            if (flames != null) flames.SetActive(false);
        }

        private void ConfigureStage5ForEndingA()
        {
            if (playerMago != null)
            {
                playerMago.SwitchToFinalWizardModel(true);
            }

            if (trainingDummy != null) trainingDummy.gameObject.SetActive(false);
            if (aldeanosYReyPuppet != null) aldeanosYReyPuppet.gameObject.SetActive(false);
            if (campesinosPuppet != null) campesinosPuppet.gameObject.SetActive(false);

            if (otherVillageHousesToHideInEndingA != null)
            {
                foreach (var house in otherVillageHousesToHideInEndingA)
                {
                    if (house != null) house.SetActive(false);
                }
            }

            if (wizardHouseLeftObject != null)
            {
                wizardHouseLeftObject.SetActive(true);
            }

            // Agrega 2 llamas (Dragon Llamas_2) para mostrar que el dragón devastó todo el pueblo
            EnsureEndingAFlamesExist();
        }

        private void ConfigureStage5ForEndingB()
        {
            if (playerMago != null)
            {
                playerMago.SwitchToFinalWizardModel(true);
            }

            if (trainingDummy != null) trainingDummy.gameObject.SetActive(false);
            if (aldeanosYReyPuppet != null) aldeanosYReyPuppet.gameObject.SetActive(false);
            if (campesinosPuppet != null) campesinosPuppet.gameObject.SetActive(false);

            var flames = GameObject.Find("EndingA_DevastationFlames");
            if (flames != null) flames.SetActive(false);

            if (otherVillageHousesToHideInEndingA != null)
            {
                foreach (var house in otherVillageHousesToHideInEndingA)
                {
                    if (house != null) house.SetActive(true);
                }
            }

            if (wizardHouseLeftObject != null)
            {
                wizardHouseLeftObject.SetActive(true);
            }
        }

        /// <summary>
        /// Bucle de comprobación por fotograma:
        /// 1. Lee las teclas de decisión final (1 o 2 en el teclado/numpad) cuando la caja de elección está activa.
        /// 2. Permite reiniciar el juego completo con la tecla 'R' cuando un final ha concluido.
        /// 3. Ejecuta los disparadores espaciales (Narrative Triggers) según la posición X del Mago.
        /// </summary>
        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                // Elección en la Cueva del Dragón (Acto 3):
                // 1 = Perdonar al dragón (Final A) | 2 = Matar al dragón (Final B)
                if (_waitingForEndingChoice)
                {
                    if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame)
                    {
                        SelectEndingA_SpareDragon();
                    }
                    else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame)
                    {
                        SelectEndingB_KillDragon();
                    }
                }

                // Reinicio o regreso rápido tras ver cualquiera de los dos finales
                if (_endingFinished && (kb.rKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
                {
                    CurtainStageManager.ResetAllStaticProgress();
                    SceneManager.LoadScene("Stage0_MainMenu");
                    return;
                }
            }

            // Permite hacer clic con el mouse directamente sobre los carteles colgantes
            var mouse = Mouse.current;
            if (_waitingForEndingChoice && mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    Vector3 mWorld = cam.ScreenToWorldPoint(mouse.position.ReadValue());
                    if (mWorld.x < -0.8f && mWorld.y > 0.2f)
                    {
                        SelectEndingA_SpareDragon();
                    }
                    else if (mWorld.x > 0.8f && mWorld.y > 0.2f)
                    {
                        SelectEndingB_KillDragon();
                    }
                }
            }

            // Disparadores espaciales narrativos por posición del Mago en el escenario
            if (stageType == StageNarrativeType.Stage1_Village && playerMago != null)
            {
                UpdateStage1NarrativeTriggers();
            }
            else if (stageType == StageNarrativeType.Stage5_VillageNight && playerMago != null)
            {
                UpdateStage5NarrativeTriggers();
            }
        }

        /// <summary>
        /// Evalúa la posición del Mago en el Stage 5 (Pueblo de Noche) para activar las secuencias de clímax.
        /// </summary>
        private void UpdateStage5NarrativeTriggers()
        {
            float px = playerMago.transform.position.x;

            // 1) FINAL A: El Mago llega a su casa a la izquierda (X <= -3.9f)
            if (CurtainStageManager.IsReturningForEndingA && !_endingSequenceStarted)
            {
                if (px <= -3.9f)
                {
                    _endingSequenceStarted = true;
                    StartCoroutine(CompleteEndingA_AtWizardHouseRoutine());
                }
                return;
            }

            // 2) FINAL B: El Mago llega hacia el centro del pueblo (X <= 1.15f) donde es emboscado y acorralado
            if (CurtainStageManager.IsReturningForEndingB && !_endingSequenceStarted)
            {
                if (px <= 1.15f)
                {
                    _endingSequenceStarted = true;
                    StartCoroutine(ExecuteEndingB_SurroundingBetrayalRoutine());
                }
            }
        }

        /// <summary>
        /// Máquina de estados tutorial del Stage 1:
        /// Paso 1: Espera a que el jugador pruebe ambas teclas de movimiento ('A' y 'D').
        /// Paso 11: Si ya practicó el combate y vuelve a su casa (X <= -3.9f), cierra el telón e inicia el Día 2.
        /// También gestiona los disparadores de los finales si se juegan en la misma escena.
        /// </summary>
        private void UpdateStage1NarrativeTriggers()
        {
            float px = playerMago.transform.position.x;
            int currentSection = curtainManager != null ? curtainManager.CurrentSubSectionIndex : 0;

            // 1) ACTO 1 - DÍA 1: Tutorial y Secuencia Narrativa
            if (!CurtainStageManager.IsVillageDay2 && !CurtainStageManager.IsReturningForEndingA && !CurtainStageManager.IsReturningForEndingB)
            {
                if (_stage1TutorialStep == Stage1TutorialStep.Step1_WaitingBothMoveKeys)
                {
                    var kb = Keyboard.current;
                    if (kb != null)
                    {
                        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) _hasMovedLeft = true;
                        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) _hasMovedRight = true;
                    }

                    // Cuando el jugador se haya desplazado a ambos lados, avanzamos al siguiente paso tutorial
                    if (_hasMovedLeft && _hasMovedRight)
                    {
                        _stage1TutorialStep = Stage1TutorialStep.Step2_PromptGoToVillage;
                        StartCoroutine(Stage1PromptGoToVillageRoutine());
                    }
                    return;
                }

                // Al volver a la casa del Mago (X <= -3.9f en la sección 0) tras completar la práctica con el maniquí:
                if (_stage1TutorialStep == Stage1TutorialStep.Step11_PromptGoHome && !_isTransitioningDay1ToDay2 && currentSection == 0 && px <= -3.9f)
                {
                    _stage1TutorialStep = Stage1TutorialStep.Step12_TransitioningToDay2;
                    StartCoroutine(TransitionFromDay1ToDay2Routine());
                    return;
                }

                return;
            }

            // 2) FINAL A JUGABLE: Caminar de regreso hasta la casa del Mago (X <= -4.2f) en el pueblo arrasado
            if (CurtainStageManager.IsReturningForEndingA && !_endingSequenceStarted)
            {
                if (px <= -4.2f)
                {
                    _endingSequenceStarted = true;
                    StartCoroutine(CompleteEndingA_AtWizardHouseRoutine());
                }
                return;
            }

            // 3) FINAL B JUGABLE: Esperar a que el jugador se posicione en el medio de la escena (X aprox 0)
            if (CurtainStageManager.IsReturningForEndingB && !_endingSequenceStarted)
            {
                if (Mathf.Abs(px) <= 0.85f)
                {
                    _endingSequenceStarted = true;
                    StartCoroutine(ExecuteEndingB_SurroundingBetrayalRoutine());
                }
            }
        }

        /// <summary>
        /// Callback del evento OnAttackFired del PlayerMagoController.
        /// Permite al tutorial avanzar en la secuencia de práctica de hechizos (Recto -> Diagonal).
        /// </summary>
        private void HandlePlayerAttackFired(bool isDiagonal, bool isCharged)
        {
            if (stageType != StageNarrativeType.Stage1_Village || CurtainStageManager.IsVillageDay2) return;

            if (_stage1TutorialStep == Stage1TutorialStep.Step6_WaitingStraightAttack && !isDiagonal)
            {
                AdvanceFromStraightAttackToDiagonal();
            }
            else if (_stage1TutorialStep == Stage1TutorialStep.Step8_WaitingDiagonalAttack && isDiagonal)
            {
                AdvanceFromDiagonalAttackToDash();
            }
        }

        /// <summary>
        /// Callback del evento OnDashFired del PlayerMagoController.
        /// Avanza el tutorial tras ejecutar con éxito el Giro de Papel (Dash con Shift).
        /// </summary>
        private void HandlePlayerDashFired()
        {
            if (stageType != StageNarrativeType.Stage1_Village || CurtainStageManager.IsVillageDay2) return;

            if (_stage1TutorialStep == Stage1TutorialStep.Step10_WaitingDash)
            {
                StartCoroutine(Stage1PromptGoHomeRoutine());
            }
        }

        public void OnTrainingDummyHit()
        {
            OnTrainingDummyHit(null, false, false);
        }

        /// <summary>
        /// Se llama cuando un proyectil del jugador golpea el muñeco de entrenamiento (suelo o torre).
        /// Valida el tipo de impacto para avanzar los pasos del tutorial de combate.
        /// </summary>
        public void OnTrainingDummyHit(TrainingDummyPuppet dummy, bool isDiagonalShot, bool isCharged)
        {
            _hasHitTrainingDummyDay1 = true;
            if (!CurtainStageManager.IsVillageDay2 && !CurtainStageManager.IsReturningForEndingA && !CurtainStageManager.IsReturningForEndingB)
            {
                if (_stage1TutorialStep == Stage1TutorialStep.Step6_WaitingStraightAttack && !isDiagonalShot)
                {
                    AdvanceFromStraightAttackToDiagonal();
                }
                else if (_stage1TutorialStep == Stage1TutorialStep.Step8_WaitingDiagonalAttack && isDiagonalShot)
                {
                    AdvanceFromDiagonalAttackToDash();
                }
            }
        }

        private void AdvanceFromStraightAttackToDiagonal()
        {
            if (_stage1TutorialStep != Stage1TutorialStep.Step6_WaitingStraightAttack) return;
            _stage1TutorialStep = Stage1TutorialStep.Step7_PromptDiagonalAttack;
            StartCoroutine(Stage1PromptDiagonalAttackRoutine());
        }

        private void AdvanceFromDiagonalAttackToDash()
        {
            if (_stage1TutorialStep != Stage1TutorialStep.Step8_WaitingDiagonalAttack) return;
            _stage1TutorialStep = Stage1TutorialStep.Step9_PromptDash;
            StartCoroutine(Stage1PromptDashRoutine());
        }

        /// <summary>
        /// Corrutina del tutorial de movimiento básico:
        /// Espera a que el telón termine de abrirse y baja la caja de texto colgante con el mensaje.
        /// </summary>
        private IEnumerator Stage1MovementTutorialRoutine()
        {
            while (curtainManager != null && curtainManager.IsTransitioning)
            {
                yield return null;
            }
            yield return new WaitForSeconds(0.35f);

            ShowSignText("Muevete a la izquierda con la tecla \"A\", a la derecha con \"D\" y salta con \"W\"", ominous: false);
            _stage1TutorialStep = Stage1TutorialStep.Step1_WaitingBothMoveKeys;
        }

        /// <summary>
        /// Corrutina de instrucción para ir al pueblo:
        /// Sube el cartel, espera 2 segundos, lo vuelve a bajar con la nueva consigna y luego lo oculta.
        /// </summary>
        private IEnumerator Stage1PromptGoToVillageRoutine()
        {
            if (hangingTextSign != null) hangingTextSign.HideSign();
            yield return new WaitForSeconds(2.0f);

            ShowSignText("Ve al pueblo a hablar con sus habitantes", ominous: false);
            yield return new WaitForSeconds(3.5f);

            if (hangingTextSign != null) hangingTextSign.HideSign();
            _stage1TutorialStep = Stage1TutorialStep.Step3_WalkingToVillage;
        }

        /// <summary>
        /// Corrutina de práctica de combate:
        /// Se activa cuando el jugador regresa a su casa tras ver el pueblo vacío.
        /// Aparece el muñeco de paja en el suelo y la torre medieval con el muñeco elevado sobre el almenar.
        /// Habilita los ataques del mago y baja el cartel indicando el ataque frontal (débil y fuerte).
        /// </summary>
        private IEnumerator Stage1CombatPracticeRoutine()
        {
            _stage1TutorialStep = Stage1TutorialStep.Step5_ReturnedHomePractice;
            EnsurePracticeTowerCreated();

            if (trainingDummy != null)
            {
                trainingDummy.gameObject.SetActive(true);
                trainingDummy.transform.position = new Vector3(1.8f, -3.95f, 0f);
                trainingDummy.ResetDummy();
            }

            if (practiceTowerRoot != null)
            {
                practiceTowerRoot.SetActive(true);
            }

            if (elevatedTrainingDummy != null)
            {
                elevatedTrainingDummy.gameObject.SetActive(true);
                elevatedTrainingDummy.ResetDummy();
            }

            if (playerMago != null)
            {
                playerMago.SetAttacksEnabled(true);
                playerMago.UnlockDiagonalAttack();
                playerMago.SetDashEnabled(false);
            }

            yield return new WaitForSeconds(0.4f);
            ShowSignText("Pulsa Click Izquierdo para ataque debil, Manten pulsado el click para ataque fuerte", ominous: false);
            _stage1TutorialStep = Stage1TutorialStep.Step6_WaitingStraightAttack;
        }

        /// <summary>
        /// Instrucción del ataque aéreo diagonal (Clic derecho).
        /// </summary>
        private IEnumerator Stage1PromptDiagonalAttackRoutine()
        {
            yield return new WaitForSeconds(0.3f);
            ShowSignText("Pulsa Click Derecho para ataque aereo debil, Manten pulsado para ataque aereo Fuerte", ominous: false);
            _stage1TutorialStep = Stage1TutorialStep.Step8_WaitingDiagonalAttack;
        }

        /// <summary>
        /// Instrucción del Giro de Papel / Dash (Tecla Shift).
        /// </summary>
        private IEnumerator Stage1PromptDashRoutine()
        {
            yield return new WaitForSeconds(0.3f);
            if (playerMago != null) playerMago.SetDashEnabled(true);
            ShowSignText("A veces estaras acorralado para poder reubicarte Pulsa la tecla \"Shift\"", ominous: false);
            _stage1TutorialStep = Stage1TutorialStep.Step10_WaitingDash;
        }

        /// <summary>
        /// Instrucción final del tutorial: "Dirigete a tu casa" para descansar y pasar al Día 2.
        /// </summary>
        private IEnumerator Stage1PromptGoHomeRoutine()
        {
            _stage1TutorialStep = Stage1TutorialStep.Step11_PromptGoHome;
            yield return new WaitForSeconds(0.8f);
            ShowSignText("Dirigete a tu casa", ominous: false);
        }

        public bool CanLeaveCurrentStageToNextScene()
        {
            if (stageType == StageNarrativeType.Stage1_Village && !CurtainStageManager.IsVillageDay2)
            {
                _hasVisitedEmptyVillageDay1 = true;
                ShowSignText("Que raro parece que no hay nadie mejor vuelvo a casa", ominous: false);
                return false;
            }
            return true;
        }

        public void OnStageCurtainsOpened(int subSectionIndex)
        {
            if (CurtainStageManager.IsReturningForEndingA)
            {
                UpdateEndingAFlamesVisibility();
            }

            switch (stageType)
            {
                case StageNarrativeType.Stage1_Village:
                    if (!CurtainStageManager.IsReturningForEndingA && !CurtainStageManager.IsReturningForEndingB)
                    {
                        if (!CurtainStageManager.IsVillageDay2)
                        {
                            if (subSectionIndex == 0)
                            {
                                if (_hasVisitedEmptyVillageDay1)
                                {
                                    StartCoroutine(Stage1CombatPracticeRoutine());
                                }
                            }
                            else
                            {
                                _hasVisitedEmptyVillageDay1 = true;
                                if (campesinosPuppet != null) campesinosPuppet.gameObject.SetActive(false);
                                if (aldeanosSec2Puppet != null) aldeanosSec2Puppet.gameObject.SetActive(false);
                                ShowSignText("Que raro parece que no hay nadie mejor vuelvo a casa", ominous: false);
                                _stage1TutorialStep = Stage1TutorialStep.Step4_InVillageEmptyPrompt;
                            }
                        }
                        else
                        {
                            if (subSectionIndex == 0)
                            {
                                ShowSignText(
                                    "Señor Mago un dragon amenaza al pueblo eres el unico que puede derrotarlo, nuestra vida y la del pueblo estan en tus manos",
                                    ominous: false
                                );
                            }
                            else
                            {
                                // Al ir a la siguiente imagen desaparece el texto
                                if (hangingTextSign != null) hangingTextSign.HideSign();
                            }
                        }
                    }
                    break;

                case StageNarrativeType.Stage2_Forest:
                    break;

                case StageNarrativeType.Stage3_DragonCave:
                    break;

                case StageNarrativeType.Stage4_ForestNight:
                    break;

                case StageNarrativeType.Stage5_VillageNight:
                    break;
            }
        }

        // =========================================================================================
        // TRANSICIÓN DÍA 1 -> DÍA 2 EN STAGE 1 (CIERRE Y APERTURA DE TELÓN EN LA CASA DEL MAGO)
        // =========================================================================================
        /// <summary>
        /// Corrutina de transición temporal (Elipsis teatral de Día 1 a Día 2):
        /// 1. Bloquea el control del jugador y el telón.
        /// 2. Cierra las cortinas rojas del teatro (PlayCloseCurtainsOnlyRoutine).
        /// 3. Reubica al Mago durmiendo frente a su casa y activa a los aldeanos pidiendo socorro.
        /// 4. Abre las cortinas (PlayOpenCurtainsOnlyRoutine) revelando el nuevo día teatral.
        /// </summary>
        private IEnumerator TransitionFromDay1ToDay2Routine()
        {
            _isTransitioningDay1ToDay2 = true;
            if (curtainManager != null) curtainManager.SetTransitionLock(true);
            if (playerMago != null) playerMago.SetControlEnabled(false);

            if (hangingTextSign != null) hangingTextSign.HideSign();
            yield return new WaitForSeconds(0.35f);

            // Cierre completo del telón teatral
            if (curtainManager != null)
            {
                yield return StartCoroutine(curtainManager.PlayCloseCurtainsOnlyRoutine(0.40f));
            }

            // Cambiamos el estado estático del mundo al Día 2
            CurtainStageManager.IsVillageDay2 = true;
            ConfigureStage1ForDay2();

            // Posicionamos al mago frente a la puerta de su casa a nivel del suelo (-3.95f)
            if (playerMago != null)
            {
                Vector3 pos = playerMago.transform.position;
                pos.x = -5.1f;
                pos.y = -3.95f;
                playerMago.transform.position = pos;
                playerMago.SetFacingDirection(true);
            }

            yield return new WaitForSeconds(0.45f);

            // Apertura del telón para el Acto 1 - Día 2
            if (curtainManager != null)
            {
                yield return StartCoroutine(curtainManager.PlayOpenCurtainsOnlyRoutine(0.40f));
                curtainManager.SetTransitionLock(false);
            }

            ShowSignText(
                "Señor Mago un dragon amenaza al pueblo eres el unico que puede derrotarlo, nuestra vida y la del pueblo estan en tus manos",
                ominous: false
            );

            if (playerMago != null) playerMago.SetControlEnabled(true);
            _stage1TutorialStep = Stage1TutorialStep.Step13_Day2Active;
            _isTransitioningDay1ToDay2 = false;
        }

        // =========================================================================================
        // ACTO 3: DERROTA DEL DRAGÓN -> TELÓN -> FLASHBACK DE LOS HUEVOS EN LA DERECHA -> 2 OPCIONES
        // =========================================================================================
        /// <summary>
        /// Invocado por DragonBossController cuando el dragón agota su vida en la Cueva (Acto 3).
        /// Inicia la cinemática de revelación de la verdad.
        /// </summary>
        public void StartDragonTruthAndChoices()
        {
            StartCoroutine(DragonCaveFlashbackAndChoicesRoutine());
        }

        /// <summary>
        /// Coreografía completa del Flashback:
        /// 1. Cierra el telón y oculta al dragón y al mago.
        /// 2. Configura el Nido con huevos intactos (Frame 0) en la derecha (X = 3.6f).
        /// 3. Abre el telón y hace entrar a los pueblerinos/Rey desde la izquierda usando interpolación matemática
        ///    suave (Mathf.SmoothStep) con rebote oscilatorio de pasos teatrales (Mathf.Sin).
        /// 4. Los pueblerinos se sobreponen a los huevos, sacan sus armas (Frame 1) y los destruyen (Frame 1 huevos rotos).
        /// 5. Se retiran hacia la izquierda dejando al descubierto el nido roto.
        /// 6. Cierra el telón, restaura la escena y AHORA SÍ cambia el modelo del Mago a "Player Mago Final".
        /// 7. Abre el telón y presenta la bifurcación moral: [1] Perdonar al Dragón o [2] Matar al Dragón.
        /// </summary>
        private IEnumerator DragonCaveFlashbackAndChoicesRoutine()
        {
            if (curtainManager != null) curtainManager.SetTransitionLock(true);
            if (playerMago != null) playerMago.SetControlEnabled(false);

            yield return new WaitForSeconds(0.4f);

            if (hangingTextSign != null) hangingTextSign.HideSign();

            // 1. Detenemos la música y se cierra el telón para preparar el Flashback en la misma cueva
            AudioManager.Instance?.StopBgm(0.35f);

            if (curtainManager != null)
            {
                yield return StartCoroutine(curtainManager.PlayCloseCurtainsOnlyRoutine(0.40f));
            }

            // El telón se mantiene cerrado más tiempo y desciende la caja de texto con "El día de ayer"
            yield return new WaitForSeconds(0.5f);
            if (hangingTextSign != null)
            {
                hangingTextSign.SetSortingOrder(55); // Por delante del telón cerrado (50)
                hangingTextSign.SetLoweredY(2.75f);
                hangingTextSign.SetMoveSpeed(6.0f);
                hangingTextSign.ShowSign("El día de ayer", ominousInk: false);
            }

            yield return new WaitForSeconds(3.0f);

            if (hangingTextSign != null)
            {
                hangingTextSign.HideSign();
                yield return new WaitForSeconds(0.6f);
                hangingTextSign.SetSortingOrder(45); // Restaurar orden normal
            }

            yield return new WaitForSeconds(0.8f);

            // Ocultamos temporalmente al Mago y al Dragón para escenificar el Flashback del Día 1
            if (playerMago != null) playerMago.gameObject.SetActive(false);
            if (dragonBoss != null) dragonBoss.SetDragonVisible(false);

            // Mostramos los Huevos del Dragón ("Nido Dragon") en la PARTE DERECHA (X = 3.6f) en Frame 0 (Huevos Intactos)
            Vector3 nestRightPos = new Vector3(3.6f, -3.95f, 0f);
            SetDragonNestActive(true);
            SetDragonNestPosition(nestRightPos);
            SetDragonNestFrame(0);

            // Colocamos a los pueblerinos (Aldeanos y Rey + Campesinos) en la entrada izquierda de la cueva
            Vector3 aldStart = new Vector3(-4.8f, -3.95f, 0f);
            Vector3 campStart = new Vector3(-6.1f, -3.95f, 0f);

            if (flashbackAldeanosYReyPuppet != null)
            {
                flashbackAldeanosYReyPuppet.gameObject.SetActive(true);
                flashbackAldeanosYReyPuppet.SetFollowPlayerGaze(false);
                flashbackAldeanosYReyPuppet.SetFrame(0);
                flashbackAldeanosYReyPuppet.transform.position = aldStart;
                flashbackAldeanosYReyPuppet.FaceDirection(1f); // Mirando hacia la derecha (hacia los huevos)
            }

            if (flashbackCampesinosPuppet != null)
            {
                flashbackCampesinosPuppet.gameObject.SetActive(true);
                flashbackCampesinosPuppet.SetFollowPlayerGaze(false);
                flashbackCampesinosPuppet.SetFrame(0);
                flashbackCampesinosPuppet.transform.position = campStart;
                flashbackCampesinosPuppet.FaceDirection(1f); // Mirando hacia la derecha (hacia los huevos)
            }

            // 2. Se abre el telón: se ven los huevos intactos a la derecha y a los pueblerinos entrando por la izquierda
            if (curtainManager != null)
            {
                yield return StartCoroutine(curtainManager.PlayOpenCurtainsOnlyRoutine(0.38f));
            }

            // Los pueblerinos caminan LENTAMENTE hacia la derecha y SE SOBREPONEN a los huevos
            Vector3 aldOverlapEnd = new Vector3(nestRightPos.x + 0.25f, -3.95f, 0f);
            Vector3 campOverlapEnd = new Vector3(nestRightPos.x - 0.25f, -3.95f, 0f);

            float walkDuration = 7.5f;
            float elapsed = 0f;
            while (elapsed < walkDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / walkDuration);
                float stepBob = Mathf.Abs(Mathf.Sin(elapsed * 3.8f)) * 0.08f;

                if (flashbackAldeanosYReyPuppet != null)
                {
                    flashbackAldeanosYReyPuppet.transform.position = Vector3.Lerp(aldStart, aldOverlapEnd, t) + new Vector3(0f, stepBob, 0f);
                }
                if (flashbackCampesinosPuppet != null)
                {
                    flashbackCampesinosPuppet.transform.position = Vector3.Lerp(campStart, campOverlapEnd, t) + new Vector3(0f, stepBob, 0f);
                }
                yield return null;
            }

            // Estando sobrepuestos encima de los huevos, sacan sus armas (Frame 1) y los destruyen
            if (flashbackAldeanosYReyPuppet != null)
            {
                flashbackAldeanosYReyPuppet.SetFrame(1);
                if (flashbackAldeanosYReyPuppet.Wobble != null) flashbackAldeanosYReyPuppet.Wobble.TriggerHitShake(0.45f, 13f);
            }
            if (flashbackCampesinosPuppet != null)
            {
                flashbackCampesinosPuppet.SetFrame(1);
                if (flashbackCampesinosPuppet.Wobble != null) flashbackCampesinosPuppet.Wobble.TriggerHitShake(0.45f, 13f);
            }

            // Mientras están sobrepuestos cubriendo el nido, los huevos cambian a Frame 1 (Huevos Rotos)
            yield return new WaitForSeconds(0.7f);
            SetDragonNestFrame(1);
            yield return new WaitForSeconds(0.9f);

            // Se dan la vuelta hacia la izquierda y se retiran lentamente, dejando al descubierto los huevos ya destruidos
            if (flashbackAldeanosYReyPuppet != null) flashbackAldeanosYReyPuppet.FaceDirection(-1f);
            if (flashbackCampesinosPuppet != null) flashbackCampesinosPuppet.FaceDirection(-1f);

            elapsed = 0f;
            float exitDuration = 6.8f;
            while (elapsed < exitDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / exitDuration);
                float stepBob = Mathf.Abs(Mathf.Sin(elapsed * 4.0f)) * 0.08f;

                if (flashbackAldeanosYReyPuppet != null)
                {
                    flashbackAldeanosYReyPuppet.transform.position = Vector3.Lerp(aldOverlapEnd, aldStart, t) + new Vector3(0f, stepBob, 0f);
                }
                if (flashbackCampesinosPuppet != null)
                {
                    flashbackCampesinosPuppet.transform.position = Vector3.Lerp(campOverlapEnd, campStart, t) + new Vector3(0f, stepBob, 0f);
                }
                yield return null;
            }

            yield return new WaitForSeconds(0.8f);

            // 3. Se cierra el telón tras terminar la cinemática del dragón y vuelve al presente
            if (curtainManager != null)
            {
                yield return StartCoroutine(curtainManager.PlayCloseCurtainsOnlyRoutine(0.32f));
            }

            if (flashbackAldeanosYReyPuppet != null) flashbackAldeanosYReyPuppet.gameObject.SetActive(false);
            if (flashbackCampesinosPuppet != null) flashbackCampesinosPuppet.gameObject.SetActive(false);

            // Dejamos el nido roto visible junto al dragón derrotado (tamaño reducido de los huevos)
            SetDragonNestPosition(new Vector3(1.25f, -3.95f, 0f));

            // AHORA SÍ: Después de que se vio la cinemática del dragón, cambia el diseño del Player a "Player Mago Final"
            if (playerMago != null)
            {
                playerMago.gameObject.SetActive(true);
                playerMago.SwitchToFinalWizardModel(true);
            }

            if (dragonBoss != null)
            {
                dragonBoss.SetDragonVisible(true);
            }

            if (curtainManager != null)
            {
                yield return StartCoroutine(curtainManager.PlayOpenCurtainsOnlyRoutine(0.32f));
            }

            _waitingForEndingChoice = true;
            // Solo deben aparecer los carteles colgantes teatrales (sin paneles de prueba)
            if (endingChoicePanel != null)
            {
                endingChoicePanel.SetActive(false);
            }

            // Descenso teatral de los dos cuadros de decisión:
            // 1. "MATAR AL DRAGÓN" (Derecha) sale más rápido primero incitando a la venganza
            if (choiceSignKill != null)
            {
                choiceSignKill.SetMoveSpeed(12.0f);
                choiceSignKill.SetLoweredY(2.15f);
                choiceSignKill.ShowSign("MATAR AL DRAGÓN\n(Pulsa 2)", ominousInk: true);
            }

            // 2. "PERDONAR AL DRAGÓN" (Izquierda) tarda un poco más en bajar y lo hace con un ritmo más pausado
            yield return new WaitForSeconds(1.1f);
            if (_waitingForEndingChoice && choiceSignSpare != null)
            {
                choiceSignSpare.SetMoveSpeed(3.8f);
                choiceSignSpare.SetLoweredY(2.15f);
                choiceSignSpare.ShowSign("PERDONAR AL DRAGÓN\n(Pulsa 1)", ominousInk: false);
            }
        }

        /// <summary>
        /// Selección de la decisión moral: FINAL A (Perdonar al Dragón).
        /// </summary>
        public void SelectEndingA_SpareDragon()
        {
            if (!_waitingForEndingChoice) return;
            _waitingForEndingChoice = false;

            AudioManager.Instance?.PlayBgmFinalSpare();

            if (choiceSignSpare != null) choiceSignSpare.HideSign();
            if (choiceSignKill != null) choiceSignKill.HideSign();
            if (endingChoicePanel != null) endingChoicePanel.SetActive(false);

            StartCoroutine(EndingA_DragonFliesToVillageRoutine());
        }

        /// <summary>
        /// Selección de la decisión moral: FINAL B (Matar al Dragón).
        /// </summary>
        public void SelectEndingB_KillDragon()
        {
            if (!_waitingForEndingChoice) return;
            _waitingForEndingChoice = false;

            AudioManager.Instance?.PlayBgmFinalKill();

            if (choiceSignSpare != null) choiceSignSpare.HideSign();
            if (choiceSignKill != null) choiceSignKill.HideSign();
            if (endingChoicePanel != null) endingChoicePanel.SetActive(false);

            StartCoroutine(EndingB_KillDragonAndReturnRoutine());
        }

        // =========================================================================================
        // OPCIÓN A: PERDONAR AL DRAGÓN -> VUELA AL PUEBLO -> STAGE 1 SIN CASAS NI GENTE -> CASA DEL MAGO
        // =========================================================================================
        /// <summary>
        /// Final A: El dragón bate sus alas y vuela hacia el pueblo para vengarse de los traidores.
        /// El mago emprende la vuelta a través del Stage 4 (Bosque de noche) hacia el Stage 5 (Pueblo en cenizas).
        /// </summary>
        private IEnumerator EndingA_DragonFliesToVillageRoutine()
        {
            if (dragonBoss != null)
            {
                yield return StartCoroutine(dragonBoss.FlyAwayTowardsVillageRoutine(1.9f));
            }
            else
            {
                yield return new WaitForSeconds(1.8f);
            }

            CurtainStageManager.IsReturningForEndingA = true;
            CurtainStageManager.IsReturningForEndingB = false;
            PlayerMagoController.UseFinalWizardModelStatic = true;

            if (curtainManager != null)
            {
                yield return StartCoroutine(curtainManager.PlayCloseCurtainsOnlyRoutine(0.35f));
            }

            SceneManager.LoadScene("Stage4_BosqueNoche");
        }

        /// <summary>
        /// Conclusión del Final A: Al llegar a la casa del mago en el pueblo destruido,
        /// el telón se cierra solemnemente y se muestra la opción de reiniciar ('R').
        /// </summary>
        private IEnumerator CompleteEndingA_AtWizardHouseRoutine()
        {
            if (playerMago != null)
            {
                playerMago.SetControlEnabled(false);
            }

            yield return new WaitForSeconds(0.6f);

            if (curtainManager != null)
            {
                yield return StartCoroutine(curtainManager.PlayCloseCurtainsOnlyRoutine(0.85f));
            }

            _endingFinished = true;
            yield return StartCoroutine(ExecuteEpilogueSequenceRoutine(isEndingA: true));
        }

        // =========================================================================================
        // OPCIÓN B: MATAR AL DRAGÓN -> REGRESAR CON PLAYER MAGO FINAL -> EMBOSCADA EN FRAME 1
        // =========================================================================================
        /// <summary>
        /// Final B: El mago cumple la orden del rey y mata al dragón.
        /// Emprende el camino de regreso sin sospechar la emboscada que le espera en el pueblo.
        /// </summary>
        private IEnumerator EndingB_KillDragonAndReturnRoutine()
        {
            if (playerMago != null)
            {
                playerMago.SwitchToFinalWizardModel(true);
            }

            if (dragonBoss != null)
            {
                dragonBoss.SetDragonVisible(false);
            }

            yield return new WaitForSeconds(0.6f);

            CurtainStageManager.IsReturningForEndingA = false;
            CurtainStageManager.IsReturningForEndingB = true;
            PlayerMagoController.UseFinalWizardModelStatic = true;

            if (curtainManager != null)
            {
                yield return StartCoroutine(curtainManager.PlayCloseCurtainsOnlyRoutine(0.35f));
            }

            SceneManager.LoadScene("Stage4_BosqueNoche");
        }

        /// <summary>
        /// Clímax dramático del Final B:
        /// El Mago se ubica al centro de la escena.
        /// Aparece el cartel con la revelación:
        /// "Mataste al monstruo, sí... pero escuchaste su historia. Y en este reino, nadie puede saber la verdad."
        /// Los pueblerinos y el Rey avanzan LENTAMENTE acorralando al Mago mientras el telón se cierra poco a poco.
        /// Al cerrar, se muestra el Botón de Menú Final.
        /// </summary>
        private IEnumerator ExecuteEndingB_SurroundingBetrayalRoutine()
        {
            if (playerMago != null)
            {
                playerMago.SetControlEnabled(false);
                playerMago.SwitchToFinalWizardModel(true);
            }

            float wizardX = playerMago != null ? playerMago.transform.position.x : 0f;

            // Aparecen Aldeanos y Rey (Frame 1 Traición) por la izquierda y Campesinos (Frame 1 Traición) por la derecha
            if (aldeanosYReyPuppet != null)
            {
                aldeanosYReyPuppet.gameObject.SetActive(true);
                aldeanosYReyPuppet.SetFrame(1);
                aldeanosYReyPuppet.SetFollowPlayerGaze(true);
                aldeanosYReyPuppet.transform.position = new Vector3(wizardX - 5.5f, -3.95f, 0f);
            }

            if (campesinosPuppet != null)
            {
                campesinosPuppet.gameObject.SetActive(true);
                campesinosPuppet.transform.SetParent(aldeanosYReyPuppet != null ? aldeanosYReyPuppet.transform.parent : null, true);
                campesinosPuppet.SetFrame(1);
                campesinosPuppet.SetFollowPlayerGaze(true);
                campesinosPuppet.transform.position = new Vector3(wizardX + 5.5f, -3.95f, 0f);
            }

            // Baja el cuadro de texto teatral con el diálogo exacto solicitado
            if (hangingTextSign != null)
            {
                hangingTextSign.SetMoveSpeed(6.5f);
                hangingTextSign.SetLoweredY(2.65f);
                hangingTextSign.ShowSign("Mataste al monstruo, sí... pero escuchaste su historia. Y en este reino, nadie puede saber la verdad.", ominousInk: true);
            }

            // Los pueblerinos se quedan quietos e inmóviles para que el jugador tenga tiempo de leer la revelación
            yield return new WaitForSeconds(5.2f);

            Vector3 leftStart = new Vector3(wizardX - 5.5f, -3.95f, 0f);
            Vector3 rightStart = new Vector3(wizardX + 5.5f, -3.95f, 0f);
            Vector3 leftEnd = new Vector3(wizardX - 1.15f, -3.95f, 0f);
            Vector3 rightEnd = new Vector3(wizardX + 1.15f, -3.95f, 0f);

            // Avance muy lento ("ojo lentamente se acercan hacia el personaje y se va cerrando el telon")
            float duration = 9.5f;
            float elapsed = 0f;
            bool halfCurtainSet = false;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                float bob = Mathf.Abs(Mathf.Sin(elapsed * 4.5f)) * 0.10f;

                if (aldeanosYReyPuppet != null)
                {
                    aldeanosYReyPuppet.transform.position = Vector3.Lerp(leftStart, leftEnd, t) + new Vector3(0f, bob, 0f);
                }

                if (campesinosPuppet != null)
                {
                    campesinosPuppet.transform.position = Vector3.Lerp(rightStart, rightEnd, t) + new Vector3(0f, bob, 0f);
                }

                // Cierre paulatino del telón
                if (!halfCurtainSet && t >= 0.45f)
                {
                    halfCurtainSet = true;
                    if (curtainManager != null)
                    {
                        curtainManager.SetCurtainVisualState(CurtainStageManager.CurtainVisualState.Half);
                    }
                }

                yield return null;
            }

            // Cierre total definitivo del telón sobre la tragedia
            if (curtainManager != null)
            {
                curtainManager.SetCurtainVisualState(CurtainStageManager.CurtainVisualState.Closed);
            }

            if (hangingTextSign != null)
            {
                hangingTextSign.HideSign();
            }

            _endingFinished = true;
            yield return StartCoroutine(ExecuteEpilogueSequenceRoutine(isEndingA: false));
        }

        /// <summary>
        /// Secuencia Solemne de Epílogo Teatral:
        /// 1. Pausa dramática: Cuando el telón termina de cerrarse (Cortinas Rojas Cerradas),
        ///    espera 1.0 segundos en silencio antes de mostrar nada.
        /// 2. Bajada del Cartel de Epílogo:
        ///    Baja desde arriba del telón un Cartel de Texto ancho con cuerdas dobles,
        ///    usando la fuente clásica IM Fell English con tono tinta #1E1E24.
        ///    - Final A (Perdonar al Dragón):
        ///      "ACTO I: CONCLUIDO\n\nLas cenizas del reino se apagaron, pero los hilos del engaño se cortaron para siempre.\nA veces, la verdadera justicia destruye el escenario entero.\n\n— Fin del Cuento del Mago —"
        ///    - Final B (Matar al Dragón):
        ///      "ACTO I: CONCLUIDO\n\nEl monstruo ha caído y la farsa de la corona sigue intacta.\nCumpliste tu papel a la perfección... pero en este teatro, los héroes que saben demasiado nunca llegan a ver los aplausos.\n\n— Un Cuento Más para dormir al pueblo —"
        /// 3. Pausa de lectura cómoda (5.5s) para no romper la emoción con un botón abrupto.
        /// 4. Despliegue suave del botón inferior de volver al menú (Y = -3.20f) y activación del cursor.
        /// </summary>
        private IEnumerator ExecuteEpilogueSequenceRoutine(bool isEndingA)
        {
            // 1. Pausa dramática de 1.0 segundo exacto en silencio tras cerrarse el telón
            yield return new WaitForSeconds(1.0f);

            // 2. Bajada del Cartel de Epílogo ancho con cuerdas
            EnsureEpilogueSignExists();

            string epilogueText = isEndingA
                ? "ACTO I: CONCLUIDO\n\nLas cenizas del reino se apagaron, pero los hilos del engaño se cortaron para siempre.\nA veces, la verdadera justicia destruye el escenario entero.\n\n— Fin del Cuento del Mago —"
                : "ACTO I: CONCLUIDO\n\nEl monstruo ha caído y la farsa de la corona sigue intacta.\nCumpliste tu papel a la perfección... pero en este teatro, los héroes que saben demasiado nunca llegan a ver los aplausos.\n\n— Un Cuento Más para dormir al pueblo —";

            if (epilogueSign != null)
            {
                yield return StartCoroutine(epilogueSign.ShowEpilogueRoutine(epilogueText));
            }

            // 3. Pausa de lectura para asimilar la conclusión sin prisas (5.5s)
            yield return new WaitForSeconds(5.5f);

            // 4. Muestra suavemente el botón de menú final en la parte inferior para volver al menú
            StageTopRightHUD hud = FindFirstObjectByType<StageTopRightHUD>();
            if (hud != null)
            {
                hud.ShowFinalMenuButton(new Vector3(0f, -3.20f, 0f));
            }
            else
            {
                TheaterCursorManager.SetCursorVisible(true);
            }
        }

        private void EnsureEpilogueSignExists()
        {
            if (epilogueSign != null) return;

            epilogueSign = FindFirstObjectByType<EpilogueSignPuppet>();
            if (epilogueSign == null)
            {
                GameObject epilogueGO = new GameObject("EpilogueSignPuppet");
                epilogueGO.transform.position = Vector3.zero;
                epilogueSign = epilogueGO.AddComponent<EpilogueSignPuppet>();
            }
            epilogueSign.EnsureVisualsAndRopesExist();
        }

        // =========================================================================================
        // MÉTODOS AUXILIARES DEL NIDO Y DE LOS CARTELES DE DECISIÓN TEATRALES ("CAJA DE TEXTO")
        // =========================================================================================
        private void EnsureStage3ChoiceSignsExist()
        {
            if (stageType != StageNarrativeType.Stage3_DragonCave) return;

            if (choiceSignSpare == null)
            {
                var go = GameObject.Find("Prop_Cartel_PerdonarDragon");
                if (go != null) choiceSignSpare = go.GetComponent<HangingTextSignPuppet>();
            }
            if (choiceSignKill == null)
            {
                var go = GameObject.Find("Prop_Cartel_MatarDragon");
                if (go != null) choiceSignKill = go.GetComponent<HangingTextSignPuppet>();
            }

            if (hangingTextSign != null)
            {
                if (choiceSignSpare == null)
                {
                    GameObject spareGO = Instantiate(hangingTextSign.gameObject, hangingTextSign.transform.parent);
                    spareGO.name = "Prop_Cartel_PerdonarDragon";
                    Transform visualRoot = spareGO.transform.Find("SignVisualRoot");
                    if (visualRoot != null)
                    {
                        visualRoot.localPosition = new Vector3(-3.2f, 2.15f, 0f);
                        visualRoot.localScale = Vector3.one * 0.38f;
                    }
                    choiceSignSpare = spareGO.GetComponent<HangingTextSignPuppet>();
                    if (choiceSignSpare != null)
                    {
                        choiceSignSpare.SetLoweredY(2.15f);
                        choiceSignSpare.HideSign();
                    }
                }

                if (choiceSignKill == null)
                {
                    GameObject killGO = Instantiate(hangingTextSign.gameObject, hangingTextSign.transform.parent);
                    killGO.name = "Prop_Cartel_MatarDragon";
                    Transform visualRoot = killGO.transform.Find("SignVisualRoot");
                    if (visualRoot != null)
                    {
                        visualRoot.localPosition = new Vector3(3.2f, 2.15f, 0f);
                        visualRoot.localScale = Vector3.one * 0.38f;
                    }
                    choiceSignKill = killGO.GetComponent<HangingTextSignPuppet>();
                    if (choiceSignKill != null)
                    {
                        choiceSignKill.SetLoweredY(2.15f);
                        choiceSignKill.HideSign();
                    }
                }
            }
        }

        private void SetDragonNestActive(bool active)
        {
            if (dragonNestRenderer == null) return;
            Transform root = dragonNestRenderer.transform.parent != null ? dragonNestRenderer.transform.parent : dragonNestRenderer.transform;
            root.localScale = Vector3.one * 0.38f;
            root.gameObject.SetActive(active);
        }

        private void SetDragonNestPosition(Vector3 worldPos)
        {
            if (dragonNestRenderer == null) return;
            Transform root = dragonNestRenderer.transform.parent != null ? dragonNestRenderer.transform.parent : dragonNestRenderer.transform;
            root.localScale = Vector3.one * 0.38f;
            root.position = worldPos;
        }

        private void SetDragonNestFrame(int frameIndex)
        {
            if (dragonNestRenderer == null || dragonNestFrames == null || dragonNestFrames.Length == 0) return;
            int idx = Mathf.Clamp(frameIndex, 0, dragonNestFrames.Length - 1);
            if (dragonNestFrames[idx] != null)
            {
                dragonNestRenderer.sprite = dragonNestFrames[idx];
            }
        }

        /// <summary>
        /// Muestra el texto en el cartel colgante ("Caja de Texto" que baja con palito)
        /// y actualiza los elementos auxiliares de UI.
        /// </summary>
        public void ShowSignText(string message, bool ominous = false, float autoHideSeconds = 0f)
        {
            if (hangingTextSign != null)
            {
                hangingTextSign.ShowSign(message, ominous, autoHideSeconds);
            }

            if (objectiveText != null)
            {
                objectiveText.text = message;
            }

            if (subtitleText != null)
            {
                subtitleText.text = message;
            }
        }

        public void SetObjective(string text)
        {
            // Sin textos adicionales
        }

        public void ShowDualMessage(string officialText, string hiddenTruthText, float duration = 4.8f)
        {
            // Suprimido según instrucciones del usuario: solo mantener los diálogos explicados
        }

        public void ShowQuickHint(string hintText)
        {
            // Suprimido según instrucciones del usuario: solo mantener los diálogos explicados
        }

        public void ShowDirectLine(string line, bool isTruthTone, float duration)
        {
            // Suprimido según instrucciones del usuario: solo mantener los diálogos explicados
        }

        private void EnsureEndingAFlamesExist()
        {
            var existing = GameObject.Find("EndingA_DevastationFlames");
            if (existing != null)
            {
                UpdateEndingAFlamesVisibility();
                return;
            }

            Sprite flameSprite = TheaterSpriteLibrary.Get("Dragon Llamas_2");
#if UNITY_EDITOR
            if (flameSprite == null)
            {
                var sprites = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Sprites/Dragon Llamas.png");
                foreach (var s in sprites)
                {
                    if (s is Sprite sp && sp.name == "Dragon Llamas_2") { flameSprite = sp; break; }
                }
            }
#endif
            if (flameSprite == null) return;

            Transform targetParent = null;
            GameObject sec2 = GameObject.Find("Pueblo_Seccion_2_Casas2_Y_Campesinos");
            if (sec2 == null) sec2 = GameObject.Find("Pueblo_Noche_Seccion_2");
            if (sec2 != null) targetParent = sec2.transform;

            GameObject container = new GameObject("EndingA_DevastationFlames");
            if (targetParent != null)
            {
                container.transform.SetParent(targetParent, false);
            }
            CreateDevastationFlame("Flame_Village_1", container.transform, flameSprite, new Vector3(-1.2f, -3.85f, 0f), 0.85f);
            CreateDevastationFlame("Flame_Village_2", container.transform, flameSprite, new Vector3(2.4f, -3.85f, 0f), 0.85f);

            UpdateEndingAFlamesVisibility();
        }

        private void UpdateEndingAFlamesVisibility()
        {
            var flames = GameObject.Find("EndingA_DevastationFlames");
            if (flames == null) return;

            // REGLA CLAVE: En el Final A las llamas NO deben verse en la imagen donde está la casa del mago (Sección 0),
            // sino en la imagen anterior donde tendrían que estar las casas de los pueblerinos (Sección 1).
            if (curtainManager != null && curtainManager.StageSubSections != null && curtainManager.StageSubSections.Length > 1)
            {
                flames.SetActive(curtainManager.CurrentSubSectionIndex == 1);
            }
            else if (flames.transform.parent != null)
            {
                flames.SetActive(flames.transform.parent.gameObject.activeSelf);
            }
        }

        private void CreateDevastationFlame(string name, Transform parent, Sprite sprite, Vector3 pos, float scale)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * scale;

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 25;
        }

        private void EnsureTopRightHUDExists()
        {
            if (FindFirstObjectByType<StageTopRightHUD>() != null) return;

            GameObject hudGO = new GameObject("HUD_TopRight_Options");
            hudGO.transform.position = Vector3.zero;

            StageTopRightHUD hud = hudGO.AddComponent<StageTopRightHUD>();

            Sprite soundOn = TheaterSpriteLibrary.Get("Hud Opciones UnCuentoMas_2");
            Sprite soundOff = TheaterSpriteLibrary.Get("Hud Opciones UnCuentoMas_3");
            Sprite exitBtn = TheaterSpriteLibrary.Get("Hud Opciones UnCuentoMas_4");
            Sprite finalBtn = TheaterSpriteLibrary.Get("Boton Menu Final_0");
            Sprite threadSprite = TheaterSpriteLibrary.Get("Corazones y etiqueta de papel_3");

#if UNITY_EDITOR
            if (soundOn == null || soundOff == null || exitBtn == null)
            {
                var hudSprites = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Sprites/Hud Opciones UnCuentoMas.png");
                foreach (var s in hudSprites)
                {
                    if (s is Sprite sp)
                    {
                        if (sp.name == "Hud Opciones UnCuentoMas_2") soundOn = sp;
                        if (sp.name == "Hud Opciones UnCuentoMas_3") soundOff = sp;
                        if (sp.name == "Hud Opciones UnCuentoMas_4") exitBtn = sp;
                    }
                }
            }

            if (finalBtn == null)
            {
                var finalSprites = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Sprites/Boton Menu Final.png");
                foreach (var s in finalSprites)
                {
                    if (s is Sprite sp && sp.name == "Boton Menu Final_0") { finalBtn = sp; break; }
                }
            }

            if (threadSprite == null)
            {
                var heartSprites = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Sprites/Corazones y etiqueta de papel.png");
                foreach (var s in heartSprites)
                {
                    if (s is Sprite sp && sp.name == "Corazones y etiqueta de papel_3") { threadSprite = sp; break; }
                }
            }
#endif

            // Botón de Sonido (desplazado a la izquierda para evitar corte con el telón)
            GameObject soundGO = new GameObject("Btn_SoundToggle");
            soundGO.transform.SetParent(hudGO.transform, false);
            soundGO.transform.position = new Vector3(4.25f, 3.65f, 0f);
            soundGO.transform.localScale = Vector3.one * 0.45f;
            SpriteRenderer soundSR = soundGO.AddComponent<SpriteRenderer>();
            soundSR.sprite = StageTopRightHUD.IsSoundMuted ? soundOff : soundOn;
            soundSR.sortingOrder = 48;
            BoxCollider2D soundCol = soundGO.AddComponent<BoxCollider2D>();
            soundCol.size = new Vector2(1.6f, 1.6f);
            soundCol.isTrigger = true;

            // Hilo de soporte para el Botón de Sonido
            GameObject soundThreadGO = new GameObject("Hilo_Soporte");
            soundThreadGO.transform.SetParent(soundGO.transform, false);
            soundThreadGO.transform.localPosition = new Vector3(0f, 0.20f, 0f);
            soundThreadGO.transform.localScale = new Vector3(1.1f, 3.2f, 1f);
            SpriteRenderer soundThreadSR = soundThreadGO.AddComponent<SpriteRenderer>();
            soundThreadSR.sprite = threadSprite;
            soundThreadSR.sortingOrder = 47;

            // Botón de Salir (desplazado a la izquierda para evitar corte con el telón)
            GameObject exitGO = new GameObject("Btn_ExitToMenu");
            exitGO.transform.SetParent(hudGO.transform, false);
            exitGO.transform.position = new Vector3(5.05f, 3.65f, 0f);
            exitGO.transform.localScale = Vector3.one * 0.45f;
            SpriteRenderer exitSR = exitGO.AddComponent<SpriteRenderer>();
            exitSR.sprite = exitBtn;
            exitSR.sortingOrder = 48;
            BoxCollider2D exitCol = exitGO.AddComponent<BoxCollider2D>();
            exitCol.size = new Vector2(1.6f, 1.6f);
            exitCol.isTrigger = true;

            // Hilo de soporte para el Botón de Salir
            GameObject exitThreadGO = new GameObject("Hilo_Soporte");
            exitThreadGO.transform.SetParent(exitGO.transform, false);
            exitThreadGO.transform.localPosition = new Vector3(0f, 0.20f, 0f);
            exitThreadGO.transform.localScale = new Vector3(1.1f, 3.2f, 1f);
            SpriteRenderer exitThreadSR = exitThreadGO.AddComponent<SpriteRenderer>();
            exitThreadSR.sprite = threadSprite;
            exitThreadSR.sortingOrder = 47;

            // Botón Menú Final
            GameObject finalGO = new GameObject("Btn_MenuFinal");
            finalGO.transform.SetParent(hudGO.transform, false);
            finalGO.transform.position = new Vector3(0f, -1.25f, 0f);
            finalGO.transform.localScale = Vector3.one * 0.85f;
            SpriteRenderer finalSR = finalGO.AddComponent<SpriteRenderer>();
            finalSR.sprite = finalBtn;
            finalSR.sortingOrder = 55;
            BoxCollider2D finalCol = finalGO.AddComponent<BoxCollider2D>();
            finalCol.size = new Vector2(2.8f, 1.5f);
            finalCol.isTrigger = true;
            finalGO.SetActive(false);

            SetPrivateField(hud, "soundOnSprite", soundOn);
            SetPrivateField(hud, "soundOffSprite", soundOff);
            SetPrivateField(hud, "exitButtonSprite", exitBtn);
            SetPrivateField(hud, "finalMenuButtonSprite", finalBtn);
            SetPrivateField(hud, "threadSprite", threadSprite);
            SetPrivateField(hud, "soundRenderer", soundSR);
            SetPrivateField(hud, "exitRenderer", exitSR);
            SetPrivateField(hud, "finalMenuRenderer", finalSR);
            SetPrivateField(hud, "soundThreadRenderer", soundThreadSR);
            SetPrivateField(hud, "exitThreadRenderer", exitThreadSR);
            SetPrivateField(hud, "soundCollider", soundCol);
            SetPrivateField(hud, "exitCollider", exitCol);
            SetPrivateField(hud, "finalMenuCollider", finalCol);
        }

        private static void SetPrivateField(object target, string fieldName, object val)
        {
            if (target == null) return;
            var f = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            if (f != null) f.SetValue(target, val);
        }
    }
}

