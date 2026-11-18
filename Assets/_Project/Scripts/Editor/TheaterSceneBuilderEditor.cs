#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace UnCuentoMas.Editor
{
    /// <summary>
    /// Constructor automático v6 de las 3 Escenas de "Un cuento más":
    /// - Replica exactamente los ajustes manuales de Stage 1 del usuario en las 3 escenas:
    ///   * Theater_RedCurtains_Foreground: pos (0, -0.1, 0), scale (1.07, 1.1, 1)
    ///   * Background_Stage*: pos (0, -0.25, 0), scale (1.01, 1, 1)
    ///   * Player_Mago: pos (-5.3, -3.95, 0), scale (0.5, 0.45, 0.45)
    /// - Integra los 6 nuevos sprites:
    ///   * Corazones y etiqueta de papel (Frame 0 corazón + Frame 3 hilo en esquina superior izquierda)
    ///   * Caja de Texto (Cartel colgante con palito superior para tutoriales y narrativa)
    ///   * Maniqui Intro (Frame 0 intacto -> Frame 1 dañado al lado de la casa del Mago)
    ///   * Aldeanos y Rey Inocencia y Traicion (Frame 0 amistosos -> Frame 1 traición, siguen al Player con la mirada)
    ///   * Guerrero Enemigo Defensa (Frame 1 pose de defensa en los guerreros terrestres del Bosque)
    ///   * Nido Dragon (Frame 0 huevos intactos -> Frame 1 huevos rotos en la Cueva del Dragón + Flashback)
    /// </summary>
    [InitializeOnLoad]
    public static class TheaterSceneBuilderEditor
    {
        private const string ScenesFolder = "Assets/_Project/Scenes";
        private const string Stage0Path = ScenesFolder + "/Stage0_MainMenu.unity";
        private const string Stage1Path = ScenesFolder + "/Stage1_Pueblo.unity";
        private const string Stage2Path = ScenesFolder + "/Stage2_Bosque.unity";
        private const string Stage3Path = ScenesFolder + "/Stage3_CuevaDragon.unity";
        private const string Stage4Path = ScenesFolder + "/Stage4_BosqueNoche.unity";
        private const string Stage5Path = ScenesFolder + "/Stage5_PuebloNoche.unity";

        static TheaterSceneBuilderEditor()
        {
            EditorApplication.update += CheckAutoBuild;
        }

        private static void CheckAutoBuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.update -= CheckAutoBuild;

            if (!File.Exists("Assets/_Project/Resources/Fonts/IMFellEnglish-Regular SDF.asset"))
            {
                GetOrCreateIMFellFontAsset("IMFellEnglish-Regular");
                GetOrCreateIMFellFontAsset("IMFellDWPica-Regular");
            }

            if (!File.Exists("Assets/_Project/Resources/TheaterSpriteLibrary.asset"))
            {
                GenerateSpriteLibrary();
            }

            if (!File.Exists(Stage0Path) || EditorPrefs.GetInt("UnCuentoMas_MainMenu_Version", 0) < 4)
            {
                EditorPrefs.SetInt("UnCuentoMas_MainMenu_Version", 4);
                BuildMainMenuScene();
                RegisterAllFiveScenesInBuildSettings();
            }

            if (!File.Exists(Stage4Path) || !File.Exists(Stage5Path))
            {
                BuildStages4And5();
                SyncSortingOrdersInExistingScenes();
            }

            if (EditorPrefs.GetInt("UnCuentoMas_Stage2_WaveVersion", 0) < 1)
            {
                EditorPrefs.SetInt("UnCuentoMas_Stage2_WaveVersion", 1);
                BuildStage2Only();
            }
        }

        [MenuItem("Un Cuento Más/🌙 1. Generar Nuevas Escenas: Stage 4 Bosque Noche y Stage 5 Pueblo Noche", false, 1)]
        public static void BuildStages4And5()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Sal del modo Play antes de generar escenas.");
                return;
            }

            if (!Directory.Exists(ScenesFolder))
            {
                Directory.CreateDirectory(ScenesFolder);
            }

            AssetDatabase.Refresh();

            BuildStage4ForestNightScene();
            BuildStage5VillageNightScene();

            RegisterAllFiveScenesInBuildSettings();
            Debug.Log("✅ [Un Cuento Más] ¡Stage 4 (Bosque Noche) y Stage 5 (Pueblo Noche) creados y registrados correctamente en Build Settings!");
        }

        [MenuItem("Un Cuento Más/🎨 2. Sincronizar Orden de Capas (Corazones tras telón y Arqueros tras torres)", false, 2)]
        public static void SyncSortingOrdersInExistingScenes()
        {
            if (EditorApplication.isPlaying) return;

            AdjustSceneSortingOrders(Stage1Path);
            AdjustSceneSortingOrders(Stage2Path);
            AdjustSceneSortingOrders(Stage3Path);
            AdjustSceneSortingOrders(Stage4Path);
            AdjustSceneSortingOrders(Stage5Path);

            RegisterAllFiveScenesInBuildSettings();
            Debug.Log("✅ [Un Cuento Más] ¡Orden de capas sincronizado en todas las escenas respetando todos los cambios manuales!");
        }

        [MenuItem("Un Cuento Más/🌲 3. Actualizar Stage 2 (Solo Torres Medievales 2_0 y Oleadas)", false, 3)]
        public static void BuildStage2Only()
        {
            if (EditorApplication.isPlaying) return;
            BuildStage2ForestScene();
            SyncSortingOrdersInExistingScenes();
            Debug.Log("✅ [Un Cuento Más] ¡Stage 2 (Bosque) actualizado con éxito: Torres Medievales 2_0, Sección 1 y Sección 2 con Oleadas!");
        }

        [MenuItem("Un Cuento Más/🎭 4. Regenerar 3 Escenas Iniciales (Cuidado: Solo si deseas resetear)", false, 10)]
        public static void BuildAllThreeStagesAndSlices()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Sal del modo Play antes de regenerar escenas.");
                return;
            }

            if (!Directory.Exists(ScenesFolder))
            {
                Directory.CreateDirectory(ScenesFolder);
            }

            AssetDatabase.Refresh();

            BuildStage1VillageScene();
            BuildStage2ForestScene();
            BuildStage3DragonCaveScene();

            RegisterAllFiveScenesInBuildSettings();
            EditorSceneManager.OpenScene(Stage1Path, OpenSceneMode.Single);
            CurtainStageManager.ResetAllStaticProgress();

            Debug.Log("✅ [Un Cuento Más] ¡3 Escenas iniciales actualizadas!");
        }

        [MenuItem("Un Cuento Más/🎭 0. Reconstruir Menú Principal (Stage 0)", false, 0)]
        public static void BuildMainMenuOnly()
        {
            if (EditorApplication.isPlaying) return;
            BuildMainMenuScene();
            RegisterAllFiveScenesInBuildSettings();
            EditorSceneManager.OpenScene(Stage0Path, OpenSceneMode.Single);
            Debug.Log("✅ [Un Cuento Más] ¡Menú Principal (Stage 0) construido exitosamente!");
        }

        [MenuItem("Un Cuento Más/🐉 5. Reconstruir solo Stage 3 (Cueva del Dragón)", false, 4)]
        public static void BuildStage3Only()
        {
            if (EditorApplication.isPlaying) return;
            BuildStage3DragonCaveScene();
            EditorSceneManager.OpenScene(Stage3Path, OpenSceneMode.Single);
            Debug.Log("✅ [Un Cuento Más] ¡Stage 3 (Cueva del Dragón) reconstruido exitosamente sin tocar Stage 1!");
        }

        [MenuItem("Un Cuento Más/🔤 Generar Fuentes IM Fell (Libro Clásico)", false, 18)]
        public static void GenerateIMFellFontsMenu()
        {
            GetOrCreateIMFellFontAsset("IMFellEnglish-Regular");
            GetOrCreateIMFellFontAsset("IMFellDWPica-Regular");
            Debug.Log("✅ [Un Cuento Más] ¡Fuentes IM Fell English y IM Fell DW Pica generadas exitosamente!");
        }

        [MenuItem("Un Cuento Más/📦 Generar Biblioteca Universal de Sprites (TheaterSpriteLibrary)", false, 19)]
        public static void GenerateSpriteLibraryMenu()
        {
            GenerateSpriteLibrary();
        }

        public static void GenerateSpriteLibrary()
        {
            string resourcesDir = "Assets/_Project/Resources";
            if (!Directory.Exists(resourcesDir)) Directory.CreateDirectory(resourcesDir);

            string assetPath = resourcesDir + "/TheaterSpriteLibrary.asset";
            TheaterSpriteLibrary lib = AssetDatabase.LoadAssetAtPath<TheaterSpriteLibrary>(assetPath);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<TheaterSpriteLibrary>();
                AssetDatabase.CreateAsset(lib, assetPath);
            }

            var allSprites = new List<Sprite>();
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Project/Sprites" });
            foreach (var guid in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(p);
                foreach (var a in assets)
                {
                    if (a is Sprite sp)
                    {
                        allSprites.Add(sp);
                    }
                }
            }

            lib.RegisterSprites(allSprites);
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"✅ [TheaterSpriteLibrary] Registrados {allSprites.Count} sprites universales en {assetPath} para PC Standalone y Editor.");
        }

        [MenuItem("Un Cuento Más/🎬 Ir a Menú Principal (Stage 0)", false, 19)]
        public static void OpenStage0() => EditorSceneManager.OpenScene(Stage0Path, OpenSceneMode.Single);

        [MenuItem("Un Cuento Más/🎬 Ir a Stage 1: Pueblo (Día 1, Día 2)", false, 20)]
        public static void OpenStage1() => EditorSceneManager.OpenScene(Stage1Path, OpenSceneMode.Single);

        [MenuItem("Un Cuento Más/🌲 Ir a Stage 2: Bosque (Guerreros y Arqueros)", false, 21)]
        public static void OpenStage2() => EditorSceneManager.OpenScene(Stage2Path, OpenSceneMode.Single);

        [MenuItem("Un Cuento Más/🐉 Ir a Stage 3: Cueva del Dragón (Flashback y Decisión)", false, 22)]
        public static void OpenStage3() => EditorSceneManager.OpenScene(Stage3Path, OpenSceneMode.Single);

        [MenuItem("Un Cuento Más/🌲🌙 Ir a Stage 4: Bosque de Noche (Regreso)", false, 23)]
        public static void OpenStage4() => EditorSceneManager.OpenScene(Stage4Path, OpenSceneMode.Single);

        [MenuItem("Un Cuento Más/🏠🌙 Ir a Stage 5: Pueblo de Noche (Finales A y B)", false, 24)]
        public static void OpenStage5() => EditorSceneManager.OpenScene(Stage5Path, OpenSceneMode.Single);

        // =========================================================================================
        // ESCENA 1: STAGE 1 PUEBLO (DÍA 1 VACÍO + MANIQUÍ -> DÍA 2 REY Y ALDEANOS -> FINALES A Y B)
        // =========================================================================================
        private static void BuildStage1VillageScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupStaticTheaterCameraAndFloor();

            Sprite[] tools = LoadSprites("Herramientas y Ataques");
            Sprite stickSprite = GetAt(tools, 0);
            Sprite brokenStickSprite = GetAt(tools, 1);
            Sprite scissorsSprite = GetAt(tools, 4);

            // Fondo con los valores manuales exactos definidos por el usuario en Stage 1: pos (0, -0.25, 0), scale (1.01, 1, 1)
            CreateFullScreenSprite(
                "Background_Stage1_Paisaje",
                LoadSingleSprite("Stage 1 Paisaje"),
                -20,
                new Vector3(0f, -0.25f, 0f),
                new Vector3(1.01f, 1f, 1f)
            );

            Sprite[] casas1 = LoadSprites("Casas Medievales");
            Sprite[] casas2 = LoadSprites("Casas Medievales 2");
            Sprite[] aldeanosYReySprites = LoadSprites("Aldeanos y Rey Inocencia y Traicion");
            Sprite[] aldeanosSinReySprites = LoadSprites("Aldeanos Inocencia y Traicion");
            Sprite[] campesinosSprites = LoadSprites("Campesinos Inocencia y Traicion");
            Sprite[] maniquiSprites = LoadSprites("Maniqui Intro");

            // SECCIÓN 1 DEL PUEBLO (en Y = -1.2): Casa del Mago a la izquierda + Muñeco de Entrenamiento + Casas y Aldeanos/Rey
            GameObject villageSec1 = new GameObject("Pueblo_Seccion_1_Casas1_Y_Aldeanos");
            villageSec1.transform.position = new Vector3(0f, -1.2f, 0f);

            SpriteRenderer wizardHouseSR = CreatePuppetStructure("Casa_Mago_Izquierda", villageSec1.transform, GetAt(casas1, 0), stickSprite,
                new Vector3(-5.1f, -2.75f, 0f), 0.54f, -8, 1.0f, stickInsertY: 2.4f);

            // Muñeco de Entrenamiento ("Maniqui Intro" Frame 0 intacto -> Frame 1 dañado) al lado de la casa del Mago
            TrainingDummyPuppet trainingDummy = CreateTrainingDummyPuppet(
                "Maniqui_Entrenamiento_Intro",
                villageSec1.transform,
                maniquiSprites,
                stickSprite,
                new Vector3(-2.65f, -2.75f, 0f),
                0.42f
            );

            SpriteRenderer houseCenter1SR = CreatePuppetStructure("Casa_Medieval_Centro", villageSec1.transform, GetAt(casas1, 1), stickSprite,
                new Vector3(0.5f, -2.75f, 0f), 0.52f, -9, 0.9f, stickInsertY: 2.4f);
            SpriteRenderer houseRight1SR = CreatePuppetStructure("Casa_Medieval_Derecha", villageSec1.transform, GetAt(casas1, 2), stickSprite,
                new Vector3(4.8f, -2.75f, 0f), 0.53f, -8, 1.1f, stickInsertY: 2.6f);

            // "Aldeanos y Rey Inocencia y Traicion" (Grupo 1 de Sección 1)
            StageNPCPuppet aldeanosYReyPuppet = CreateStageNPCPuppet(
                "NPC_Aldeanos_Y_Rey",
                villageSec1.transform,
                aldeanosYReySprites,
                stickSprite,
                new Vector3(-0.95f, -2.75f, 0f),
                0.38f,
                sortingOrder: 6
            );

            // "Campesinos" (Grupo 2 de Sección 1 frente a la casa del Mago en Día 2)
            StageNPCPuppet campesinosSec1Puppet = CreateStageNPCPuppet(
                "NPC_Campesinos_Sec1",
                villageSec1.transform,
                campesinosSprites,
                stickSprite,
                new Vector3(2.35f, -2.75f, 0f),
                0.38f,
                sortingOrder: 6
            );

            // SECCIÓN 2 DEL PUEBLO (en Y = -1.2): Casas Medievales 2 + Pueblerinos en la imagen de sus casas
            GameObject villageSec2 = new GameObject("Pueblo_Seccion_2_Casas2_Y_Campesinos");
            villageSec2.transform.position = new Vector3(0f, -1.2f, 0f);

            SpriteRenderer house2LeftSR = CreatePuppetStructure("Casa2_Modelo1_Izquierda", villageSec2.transform, GetAt(casas2, 0), stickSprite,
                new Vector3(-4.6f, -2.75f, 0f), 0.54f, -8, 1.0f, stickInsertY: 2.4f);
            SpriteRenderer house2CenterSR = CreatePuppetStructure("Casa2_Modelo2_Centro", villageSec2.transform, GetAt(casas2, 1), stickSprite,
                new Vector3(0.2f, -2.75f, 0f), 0.52f, -9, 0.9f, stickInsertY: 2.4f);
            SpriteRenderer house2RightSR = CreatePuppetStructure("Casa2_Modelo3_Derecha", villageSec2.transform, GetAt(casas2, 2), stickSprite,
                new Vector3(4.8f, -2.75f, 0f), 0.53f, -8, 1.1f, stickInsertY: 2.6f);

            StageNPCPuppet campesinosPuppet = CreateStageNPCPuppet(
                "NPC_Campesinos",
                villageSec2.transform,
                campesinosSprites,
                stickSprite,
                new Vector3(2.35f, -2.75f, 0f),
                0.38f,
                sortingOrder: 6
            );

            StageNPCPuppet aldeanosSec2Puppet = CreateStageNPCPuppet(
                "NPC_Aldeanos_Seccion2",
                villageSec2.transform,
                aldeanosSinReySprites,
                stickSprite,
                new Vector3(-1.8f, -2.75f, 0f),
                0.38f,
                sortingOrder: 6
            );

            // Tijera Gigante opcional
            GameObject scissorsGO = new GameObject("Prop_Tijera_FinalB");
            scissorsGO.transform.position = new Vector3(-5.2f, 3.3f, 0f);
            scissorsGO.transform.localScale = Vector3.one * 0.85f;
            SpriteRenderer scissorsSR = scissorsGO.AddComponent<SpriteRenderer>();
            scissorsSR.sprite = scissorsSprite;
            scissorsSR.sortingOrder = 40;
            scissorsGO.SetActive(false);

            // Player Mago con posición (-5.3, -3.95, 0) y escala (0.5, 0.45, 0.45) igual a los ajustes manuales de Stage 1
            PlayerMagoController player = CreatePlayerMago(new Vector3(-5.3f, -3.95f, 0f), tools, allowDiagonalFromStart: false);

            StoryAndEndingManager storyMgr = BuildTheaterCurtainsHUDAndStoryUI(
                stageType: StoryAndEndingManager.StageNarrativeType.Stage1_Village,
                nextSceneName: "Stage2_Bosque",
                previousSceneName: "",
                player: player,
                stickSprite: stickSprite,
                subSections: new[] { villageSec1, villageSec2 },
                brokenStickSprite: brokenStickSprite,
                scissorsTransform: scissorsGO.transform,
                endingAFlames: null
            );

            // Cableamos las referencias específicas de Stage 1
            SetPrivateField(storyMgr, "trainingDummy", trainingDummy);
            SetPrivateField(storyMgr, "wizardHouseLeftObject", wizardHouseSR.transform.parent.gameObject);
            SetPrivateField(storyMgr, "otherVillageHousesToHideInEndingA", new GameObject[]
            {
                houseCenter1SR.transform.parent.gameObject,
                houseRight1SR.transform.parent.gameObject,
                house2LeftSR.transform.parent.gameObject,
                house2CenterSR.transform.parent.gameObject,
                house2RightSR.transform.parent.gameObject
            });
            SetPrivateField(storyMgr, "aldeanosYReyPuppet", aldeanosYReyPuppet);
            SetPrivateField(storyMgr, "campesinosPuppet", campesinosPuppet);
            SetPrivateField(storyMgr, "campesinosSec1Puppet", campesinosSec1Puppet);
            SetPrivateField(storyMgr, "aldeanosSec2Puppet", aldeanosSec2Puppet);

            EditorSceneManager.SaveScene(scene, Stage1Path);
        }

        // =========================================================================================
        // ESCENA 2: STAGE 2 BOSQUE (GUERREROS CON POSE DE DEFENSA Y ARQUEROS EN TORRES)
        // =========================================================================================
        private static void BuildStage2ForestScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupStaticTheaterCameraAndFloor();

            Sprite[] tools = LoadSprites("Herramientas y Ataques");
            Sprite stickSprite = GetAt(tools, 0);
            Sprite brokenStickSprite = GetAt(tools, 1);
            Sprite arrowSprite = GetAt(tools, 3);

            // Fondo con los mismos valores manuales de Stage 1: pos (0, -0.25, 0), scale (1.01, 1, 1)
            CreateFullScreenSprite(
                "Background_Stage2_Bosque",
                LoadSingleSprite("Stage 2 Bosque"),
                -20,
                new Vector3(0f, -0.25f, 0f),
                new Vector3(1.01f, 1f, 1f)
            );

            Sprite[] guerreroFrames = LoadSprites("Guerrero Enemigo");
            Sprite[] guerreroAltFrames = LoadSprites("Guerrero Enemigo Alt");
            Sprite[] guerreroDefensaFrames = LoadSprites("Guerrero Enemigo Defensa");
            Sprite defensePoseSprite = GetAt(guerreroDefensaFrames, 1); // Segundo frame: pose de defensa
            Sprite[] arqueroFrames = LoadSprites("Arquero Enemigo");

            // Exclusivamente el modelo solicitado: "Torres Medievales 2_0"
            Sprite[] torres2 = LoadSprites("Torres Medievales 2");
            Sprite torreModel = GetAt(torres2, 0);

            // =========================================================================
            // SECCIÓN 1 DEL BOSQUE:
            // "solo debe haber una torre y un soldado enemigo al lado derecho"
            // =========================================================================
            GameObject section1 = new GameObject("Bosque_Seccion_1_GuerreroYTorre");
            section1.transform.position = new Vector3(0f, -1.2f, 0f);

            // 1 sola torre con su arquero al lado derecho
            CreatePuppetStructure("Torre_Madera_Sec1", section1.transform, torreModel, stickSprite,
                new Vector3(4.3f, -2.75f, 0f), 0.46f, 10, 1.1f, stickInsertY: 2.8f);
            CreateEnemyPuppet("Arquero_En_Torre_Sec1", section1.transform, EnemyController.EnemyRole.TowerArcher,
                arqueroFrames, null, stickSprite, arrowSprite, new Vector3(4.3f, 0.45f, 0f), 0.38f);

            // 1 solo soldado enemigo al lado derecho
            CreateEnemyPuppet("Guerrero_Enemigo_Sec1", section1.transform, EnemyController.EnemyRole.GroundWarrior_Standard,
                guerreroFrames, defensePoseSprite, stickSprite, arrowSprite, new Vector3(1.8f, -2.75f, 0f), 0.40f);

            // =========================================================================
            // SECCIÓN 2 DEL BOSQUE:
            // "1 torre con su arquero en el costado derecho y 1 soldado" (Oleada 1)
            // "cuando los derrotamos aparece otro soldado de la parte de abajo y otra torre con su arquero pero esta vez del costado izquierdo" (Oleada 2)
            // =========================================================================
            GameObject section2 = new GameObject("Bosque_Seccion_2_TorresYGuardiaReal");
            section2.transform.position = new Vector3(0f, -1.2f, 0f);

            // Oleada 1: Torre derecha con arquero y soldado
            CreatePuppetStructure("Torre_Derecha_Sec2", section2.transform, torreModel, stickSprite,
                new Vector3(4.3f, -2.75f, 0f), 0.46f, 10, 1.1f, stickInsertY: 2.8f);
            EnemyController sec2Archer1 = CreateEnemyPuppet("Arquero_En_Torre_Derecha_Sec2", section2.transform, EnemyController.EnemyRole.TowerArcher,
                arqueroFrames, null, stickSprite, arrowSprite, new Vector3(4.3f, 0.45f, 0f), 0.38f);
            EnemyController sec2Soldier1 = CreateEnemyPuppet("Guerrero_Enemigo_Derecha_Sec2", section2.transform, EnemyController.EnemyRole.GroundWarrior_Standard,
                guerreroFrames, defensePoseSprite, stickSprite, arrowSprite, new Vector3(1.8f, -2.75f, 0f), 0.40f);

            // Oleada 2: Torre izquierda ("Torres Medievales 2_0") con arquero y segundo soldado (emergen desde abajo)
            SpriteRenderer sec2Tower2SR = CreatePuppetStructure("Torre_Izquierda_Oleada2", section2.transform, torreModel, stickSprite,
                new Vector3(-4.3f, -2.75f, 0f), 0.46f, 10, 1.1f, stickInsertY: 2.8f);
            EnemyController sec2Archer2 = CreateEnemyPuppet("Arquero_En_Torre_Izquierda_Oleada2", section2.transform, EnemyController.EnemyRole.TowerArcher,
                arqueroFrames, null, stickSprite, arrowSprite, new Vector3(-4.3f, 0.45f, 0f), 0.38f);
            EnemyController sec2Soldier2 = CreateEnemyPuppet("Guerrero_Enemigo_Oleada2", section2.transform, EnemyController.EnemyRole.GroundWarrior_Alternative,
                guerreroAltFrames, defensePoseSprite, stickSprite, arrowSprite, new Vector3(0.0f, -2.75f, 0f), 0.40f);

            // Conectar el gestor de oleadas teatrales en la Sección 2
            Stage2ForestWaveManager waveMgr = section2.AddComponent<Stage2ForestWaveManager>();
            SetPrivateField(waveMgr, "initialArcher", sec2Archer1);
            SetPrivateField(waveMgr, "initialSoldier", sec2Soldier1);
            SetPrivateField(waveMgr, "wave2Soldier", sec2Soldier2);
            SetPrivateField(waveMgr, "wave2TowerRoot", sec2Tower2SR.transform.parent.gameObject);
            SetPrivateField(waveMgr, "wave2Archer", sec2Archer2);
            SetPrivateField(waveMgr, "wave2SoldierTargetPos", new Vector3(0.0f, -2.75f, 0f));
            SetPrivateField(waveMgr, "wave2TowerTargetPos", new Vector3(-4.3f, -2.75f, 0f));
            SetPrivateField(waveMgr, "wave2ArcherTargetPos", new Vector3(-4.3f, 0.45f, 0f));

            // Player Mago con posición (-5.3, -3.95, 0) y escala (0.5, 0.45, 0.45)
            PlayerMagoController player = CreatePlayerMago(new Vector3(-5.3f, -3.95f, 0f), tools, allowDiagonalFromStart: true);

            BuildTheaterCurtainsHUDAndStoryUI(
                stageType: StoryAndEndingManager.StageNarrativeType.Stage2_Forest,
                nextSceneName: "Stage3_CuevaDragon",
                previousSceneName: "",
                player: player,
                stickSprite: stickSprite,
                subSections: new[] { section1, section2 },
                brokenStickSprite: brokenStickSprite,
                scissorsTransform: null,
                endingAFlames: null
            );

            EditorSceneManager.SaveScene(scene, Stage2Path);
        }

        // =========================================================================================
        // ESCENA 3: STAGE 3 CUEVA DEL DRAGÓN (JEFE + NIDO DEL DRAGÓN + FLASHBACK DE LOS HUEVOS)
        // =========================================================================================
        private static void BuildStage3DragonCaveScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupStaticTheaterCameraAndFloor();

            Sprite[] tools = LoadSprites("Herramientas y Ataques");
            Sprite stickSprite = GetAt(tools, 0);
            Sprite brokenStickSprite = GetAt(tools, 1);

            // Fondo con los mismos valores manuales de Stage 1: pos (0, -0.25, 0), scale (1.01, 1, 1)
            CreateFullScreenSprite(
                "Background_Stage3_CuevaDragon",
                LoadSingleSprite("Stage 3 Cueva Dragon"),
                -20,
                new Vector3(0f, -0.25f, 0f),
                new Vector3(1.01f, 1f, 1f)
            );

            // Nido del Dragón ("Nido Dragon": Frame 0 huevos intactos, Frame 1 huevos rotos) en la PARTE DERECHA (diseño más pequeño)
            Sprite[] nidoFrames = LoadSprites("Nido Dragon");
            SpriteRenderer nidoSR = CreatePuppetStructure(
                "Prop_Nido_Dragon",
                null,
                GetAt(nidoFrames, 0),
                stickSprite,
                new Vector3(3.6f, -3.95f, 0f),
                0.38f,
                sortingOrder: 8,
                swayAngle: 0.8f,
                stickInsertY: 1.35f
            );

            // Títeres para el Flashback de la Cueva (Aldeanos y Rey + Campesinos entrando a sobreponerse y romper los huevos)
            Sprite[] aldeanosYReySprites = LoadSprites("Aldeanos y Rey Inocencia y Traicion");
            Sprite[] campesinosSprites = LoadSprites("Campesinos Inocencia y Traicion");

            StageNPCPuppet flashbackAldeanos = CreateStageNPCPuppet(
                "Flashback_Aldeanos_Y_Rey",
                null,
                aldeanosYReySprites,
                stickSprite,
                new Vector3(-4.8f, -3.95f, 0f),
                0.38f,
                sortingOrder: 16
            );
            flashbackAldeanos.gameObject.SetActive(false);

            StageNPCPuppet flashbackCampesinos = CreateStageNPCPuppet(
                "Flashback_Campesinos",
                null,
                campesinosSprites,
                stickSprite,
                new Vector3(-6.1f, -3.95f, 0f),
                0.38f,
                sortingOrder: 14
            );
            flashbackCampesinos.gameObject.SetActive(false);

            // Player Mago con posición (-5.3, -3.95, 0) y escala (0.5, 0.45, 0.45)
            PlayerMagoController player = CreatePlayerMago(new Vector3(-5.3f, -3.95f, 0f), tools, allowDiagonalFromStart: true);

            Sprite[] aerialDragon = LoadSprites("Dragon Ataque Aereo");
            Sprite[] groundDragon = LoadSprites("Dragon Rojo Ataque y Final");
            Sprite[] dragonFlames = LoadSprites("Dragon Llamas");

            GameObject dragonGO = new GameObject("Boss_Dragon");
            dragonGO.transform.position = new Vector3(4.2f, 0.55f, 0f);
            dragonGO.transform.localScale = Vector3.one * 0.76f;

            BoxCollider2D dragonCol = dragonGO.AddComponent<BoxCollider2D>();
            dragonCol.size = new Vector2(4.4f, 3.8f);
            dragonCol.isTrigger = true;

            GameObject dragonVisual = new GameObject("VisualPivot");
            dragonVisual.transform.SetParent(dragonGO.transform, false);

            SpriteRenderer dragonSR = dragonVisual.AddComponent<SpriteRenderer>();
            dragonSR.sprite = GetAt(aerialDragon, 0);
            dragonSR.sortingOrder = 12;

            // Palito del Dragón: en Fase 1 Aérea sale hacia ARRIBA y en Fase 2 Terrestre sale hacia ABAJO
            GameObject dragonStickGO = new GameObject("Dragon_Palito");
            dragonStickGO.transform.SetParent(dragonVisual.transform, false);
            dragonStickGO.transform.localPosition = new Vector3(0.15f, 0.25f, 0f);
            dragonStickGO.transform.localScale = new Vector3(1.1f, -1.75f, 1f);
            SpriteRenderer dragonStickSR = dragonStickGO.AddComponent<SpriteRenderer>();
            dragonStickSR.sprite = stickSprite;
            dragonStickSR.sortingOrder = 11;

            PuppetWobble dragonWobble = dragonGO.AddComponent<PuppetWobble>();
            SetPrivateField(dragonWobble, "visualPivot", dragonVisual.transform);
            SetPrivateField(dragonWobble, "stickRenderer", dragonStickSR);
            SetPrivateField(dragonWobble, "stickComesFromTop", true);
            SetPrivateField(dragonWobble, "topStickInsertY", 0.25f);
            SetPrivateField(dragonWobble, "bottomStickInsertY", 1.55f);
            SetPrivateField(dragonWobble, "idleSwayAngle", 2.5f);

            DragonBossController dragonBoss = dragonGO.AddComponent<DragonBossController>();
            SetPrivateField(dragonBoss, "dragonRenderer", dragonSR);
            SetPrivateField(dragonBoss, "aerialDragonFrames", aerialDragon);
            SetPrivateField(dragonBoss, "groundAndFinalDragonFrames", groundDragon);
            SetPrivateField(dragonBoss, "dragonFlamesFrames", dragonFlames);
            SetPrivateField(dragonBoss, "aerialStagePosition", new Vector3(4.2f, 0.55f, 0f));
            SetPrivateField(dragonBoss, "groundStagePosition", new Vector3(4.2f, -3.70f, 0f));
            SetPrivateField(dragonBoss, "playerMago", player);

            GameObject endingAFlames = new GameObject("EndingA_FireVengeance_Group");
            CreateFlameProp("Flame_A", endingAFlames.transform, GetAt(dragonFlames, 2), new Vector3(-3.5f, -3.95f, 0f), 1.1f);
            CreateFlameProp("Flame_B", endingAFlames.transform, GetAt(dragonFlames, 3), new Vector3(0.5f, -3.95f, 0f), 1.2f);
            CreateFlameProp("Flame_C", endingAFlames.transform, GetAt(dragonFlames, 2), new Vector3(3.8f, -3.95f, 0f), 1.1f);
            endingAFlames.SetActive(false);

            StoryAndEndingManager storyMgr = BuildTheaterCurtainsHUDAndStoryUI(
                stageType: StoryAndEndingManager.StageNarrativeType.Stage3_DragonCave,
                nextSceneName: "",
                previousSceneName: "",
                player: player,
                stickSprite: stickSprite,
                subSections: null,
                brokenStickSprite: brokenStickSprite,
                scissorsTransform: null,
                endingAFlames: endingAFlames
            );

            // 2 Carteles teatrales colgantes para las opciones tras derrotar al Dragón:
            // Izquierda: PERDONAR AL DRAGÓN (X = -3.2f)
            HangingTextSignPuppet choiceSignSpare = CreateHangingTextSignPuppet(
                stickSprite,
                "Prop_Cartel_PerdonarDragon",
                posX: -3.2f,
                loweredY: 2.15f,
                scale: 0.42f,
                defaultText: "PERDONAR AL DRAGÓN\n(Pulsa [1])"
            );

            // Derecha: MATAR AL DRAGÓN (X = 3.2f)
            HangingTextSignPuppet choiceSignKill = CreateHangingTextSignPuppet(
                stickSprite,
                "Prop_Cartel_MatarDragon",
                posX: 3.2f,
                loweredY: 2.15f,
                scale: 0.42f,
                defaultText: "MATAR AL DRAGÓN\n(Pulsa [2])"
            );

            SetPrivateField(storyMgr, "dragonBoss", dragonBoss);
            SetPrivateField(storyMgr, "dragonNestRenderer", nidoSR);
            SetPrivateField(storyMgr, "dragonNestFrames", nidoFrames);
            SetPrivateField(storyMgr, "flashbackAldeanosYReyPuppet", flashbackAldeanos);
            SetPrivateField(storyMgr, "flashbackCampesinosPuppet", flashbackCampesinos);
            SetPrivateField(storyMgr, "choiceSignSpare", choiceSignSpare);
            SetPrivateField(storyMgr, "choiceSignKill", choiceSignKill);
            SetPrivateField(dragonBoss, "storyManager", storyMgr);

            EditorSceneManager.SaveScene(scene, Stage3Path);
        }

        // =========================================================================================
        // HELPERS DE CONSTRUCCIÓN DE OBJETOS Y TÍTERES
        // =========================================================================================
        private static void SetupStaticTheaterCameraAndFloor()
        {
            GameObject camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            camGO.transform.position = new Vector3(0f, 0f, -10f);
            Camera cam = camGO.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.06f, 0.05f, 1f);
            camGO.AddComponent<AudioListener>();

            System.Type light2DType = System.Type.GetType("UnityEngine.Rendering.Universal.Light2D, Unity.RenderPipelines.Universal.Runtime");
            if (light2DType != null)
            {
                GameObject lightGO = new GameObject("Global Light 2D");
                Component lightComp = lightGO.AddComponent(light2DType);
                FieldInfo lightTypeField = light2DType.GetField("m_LightType", BindingFlags.Instance | BindingFlags.NonPublic);
                if (lightTypeField != null) lightTypeField.SetValue(lightComp, 4);
            }

            GameObject floorGO = new GameObject("Stage_GroundCollider");
            floorGO.transform.position = new Vector3(0f, -4.75f, 0f);
            BoxCollider2D floorCol = floorGO.AddComponent<BoxCollider2D>();
            floorCol.size = new Vector2(30f, 1.6f);
        }

        private static GameObject CreateFullScreenSprite(string name, Sprite sprite, int sortingOrder, Vector3 position, Vector3 scale)
        {
            GameObject go = new GameObject(name);
            go.transform.position = position;
            go.transform.localScale = scale;
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;
            return go;
        }

        private static SpriteRenderer CreatePuppetStructure(string name, Transform parent, Sprite mainSprite, Sprite stickSprite,
            Vector3 localPosition, float scale, int sortingOrder, float swayAngle, float stickInsertY = 2.0f)
        {
            GameObject root = new GameObject(name);
            if (parent != null) root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;
            root.transform.localScale = Vector3.one * scale;

            GameObject visualPivot = new GameObject("VisualPivot");
            visualPivot.transform.SetParent(root.transform, false);

            SpriteRenderer mainSR = visualPivot.AddComponent<SpriteRenderer>();
            mainSR.sprite = mainSprite;
            mainSR.sortingOrder = sortingOrder;

            GameObject stickGO = new GameObject("Palito_Stick");
            stickGO.transform.SetParent(visualPivot.transform, false);
            stickGO.transform.localPosition = new Vector3(0f, stickInsertY, 0f);
            stickGO.transform.localScale = new Vector3(0.52f / scale, 0.78f / scale, 1f);
            SpriteRenderer stickSR = stickGO.AddComponent<SpriteRenderer>();
            stickSR.sprite = stickSprite;
            stickSR.sortingOrder = sortingOrder - 1;

            PuppetWobble wobble = root.AddComponent<PuppetWobble>();
            SetPrivateField(wobble, "visualPivot", visualPivot.transform);
            SetPrivateField(wobble, "stickRenderer", stickSR);
            SetPrivateField(wobble, "bottomStickInsertY", stickInsertY);
            SetPrivateField(wobble, "idleSwayAngle", swayAngle);

            return mainSR;
        }

        private static TrainingDummyPuppet CreateTrainingDummyPuppet(string name, Transform parent, Sprite[] maniquiSprites,
            Sprite stickSprite, Vector3 localPos, float scale)
        {
            Sprite intactSprite = GetAt(maniquiSprites, 0);
            Sprite damagedSprite = GetAt(maniquiSprites, 1);

            SpriteRenderer sr = CreatePuppetStructure(name, parent, intactSprite, stickSprite, localPos, scale, sortingOrder: 7, swayAngle: 1.2f, stickInsertY: 1.85f);
            GameObject root = sr.transform.parent.gameObject;

            BoxCollider2D col = root.AddComponent<BoxCollider2D>();
            col.size = new Vector2(2.1f, 3.8f);
            col.offset = new Vector2(0f, 1.9f);
            col.isTrigger = true;

            TrainingDummyPuppet dummy = root.AddComponent<TrainingDummyPuppet>();
            SetPrivateField(dummy, "dummyRenderer", sr);
            SetPrivateField(dummy, "intactDummySprite", intactSprite);
            SetPrivateField(dummy, "damagedDummySprite", damagedSprite);

            return dummy;
        }

        private static StageNPCPuppet CreateStageNPCPuppet(string name, Transform parent, Sprite[] npcSprites,
            Sprite stickSprite, Vector3 localPos, float scale, int sortingOrder)
        {
            SpriteRenderer sr = CreatePuppetStructure(name, parent, GetAt(npcSprites, 0), stickSprite, localPos, scale, sortingOrder, swayAngle: 1.5f, stickInsertY: 1.9f);
            GameObject root = sr.transform.parent.gameObject;

            StageNPCPuppet npc = root.AddComponent<StageNPCPuppet>();
            SetPrivateField(npc, "npcRenderer", sr);
            SetPrivateField(npc, "npcFrames", npcSprites);
            SetPrivateField(npc, "followPlayerGaze", true);
            SetPrivateField(npc, "rawSpriteFacesLeft", true);

            return npc;
        }

        private static PlayerMagoController CreatePlayerMago(Vector3 position, Sprite[] tools, bool allowDiagonalFromStart)
        {
            Sprite[] normalFrames = LoadSprites("Player Mago");
            Sprite[] finalFrames = LoadSprites("Player Mago Final");
            Sprite stickSprite = GetAt(tools, 0);
            Sprite brokenStickSprite = GetAt(tools, 1);
            Sprite spellSprite = GetAt(tools, 2);

            GameObject playerGO = new GameObject("Player_Mago");
            playerGO.transform.position = position;
            // Escala exacta ajustada manualmente por el usuario en Stage 1: (0.5f, 0.45f, 0.45f)
            playerGO.transform.localScale = new Vector3(0.5f, 0.45f, 0.45f);

            Rigidbody2D rb = playerGO.AddComponent<Rigidbody2D>();
            rb.gravityScale = 2.6f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            BoxCollider2D col = playerGO.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1.8f, 4.2f);
            col.offset = new Vector2(0f, 2.1f);

            GameObject groundCheck = new GameObject("GroundCheck");
            groundCheck.transform.SetParent(playerGO.transform, false);
            groundCheck.transform.localPosition = new Vector3(0f, 0.02f, 0f);

            GameObject straightSpawn = new GameObject("StraightSpawnPoint");
            straightSpawn.transform.SetParent(playerGO.transform, false);
            straightSpawn.transform.localPosition = new Vector3(2.2f, 2.2f, 0f);

            GameObject diagonalSpawn = new GameObject("DiagonalSpawnPoint");
            diagonalSpawn.transform.SetParent(playerGO.transform, false);
            diagonalSpawn.transform.localPosition = new Vector3(2.0f, 3.1f, 0f);

            GameObject visualPivot = new GameObject("VisualPivot");
            visualPivot.transform.SetParent(playerGO.transform, false);

            SpriteRenderer wizardSR = visualPivot.AddComponent<SpriteRenderer>();
            wizardSR.sprite = GetAt(normalFrames, 0);
            wizardSR.sortingOrder = 15;

            GameObject stickGO = new GameObject("Palito_Stick");
            stickGO.transform.SetParent(visualPivot.transform, false);
            stickGO.transform.localPosition = new Vector3(0f, 1.95f, 0f);
            stickGO.transform.localScale = new Vector3(0.52f / 0.45f, 0.78f / 0.45f, 1f);
            SpriteRenderer stickSR = stickGO.AddComponent<SpriteRenderer>();
            stickSR.sprite = stickSprite;
            stickSR.sortingOrder = 14;

            PuppetWobble wobble = playerGO.AddComponent<PuppetWobble>();
            SetPrivateField(wobble, "visualPivot", visualPivot.transform);
            SetPrivateField(wobble, "stickRenderer", stickSR);
            SetPrivateField(wobble, "brokenStickSprite", brokenStickSprite);
            SetPrivateField(wobble, "bottomStickInsertY", 1.95f);

            PlayerMagoController controller = playerGO.AddComponent<PlayerMagoController>();
            SetPrivateField(controller, "wizardSpriteRenderer", wizardSR);
            SetPrivateField(controller, "normalWizardFrames", normalFrames);
            SetPrivateField(controller, "finalWizardFrames", finalFrames);
            SetPrivateField(controller, "magicSpellSprite", spellSprite);
            SetPrivateField(controller, "straightSpawnPoint", straightSpawn.transform);
            SetPrivateField(controller, "diagonalSpawnPoint", diagonalSpawn.transform);
            SetPrivateField(controller, "groundCheck", groundCheck.transform);
            SetPrivateField(controller, "diagonalAttackUnlocked", allowDiagonalFromStart);
            SetPrivateField(controller, "leftStageBound", -6.8f);
            SetPrivateField(controller, "rightStageBound", 6.8f);
            SetPrivateField(controller, "maxHealth", 3);

            return controller;
        }

        private static EnemyController CreateEnemyPuppet(string name, Transform parent, EnemyController.EnemyRole role,
            Sprite[] frames, Sprite defensePoseSprite, Sprite stickSprite, Sprite arrowSprite, Vector3 localPos, float scale)
        {
            GameObject enemyGO = new GameObject(name);
            enemyGO.transform.SetParent(parent, false);
            enemyGO.transform.localPosition = localPos;
            enemyGO.transform.localScale = Vector3.one * scale;

            BoxCollider2D col = enemyGO.AddComponent<BoxCollider2D>();
            col.size = new Vector2(2.6f, 4.4f);
            col.offset = new Vector2(0f, 2.2f);
            col.isTrigger = true;

            GameObject visualPivot = new GameObject("VisualPivot");
            visualPivot.transform.SetParent(enemyGO.transform, false);

            SpriteRenderer sr = visualPivot.AddComponent<SpriteRenderer>();
            sr.sprite = GetAt(frames, 0);
            int enemySortingOrder = role == EnemyController.EnemyRole.TowerArcher ? 8 : 12;
            sr.sortingOrder = enemySortingOrder;

            GameObject stickGO = new GameObject("Palito_Stick");
            stickGO.transform.SetParent(visualPivot.transform, false);
            stickGO.transform.localPosition = new Vector3(0f, 1.85f, 0f);
            stickGO.transform.localScale = new Vector3(0.52f / scale, 0.78f / scale, 1f);
            SpriteRenderer stickSR = stickGO.AddComponent<SpriteRenderer>();
            stickSR.sprite = stickSprite;
            stickSR.sortingOrder = enemySortingOrder - 1;

            PuppetWobble wobble = enemyGO.AddComponent<PuppetWobble>();
            SetPrivateField(wobble, "visualPivot", visualPivot.transform);
            SetPrivateField(wobble, "stickRenderer", stickSR);
            SetPrivateField(wobble, "bottomStickInsertY", 1.85f);

            EnemyController enemy = enemyGO.AddComponent<EnemyController>();
            SetPrivateField(enemy, "enemyRole", role);
            SetPrivateField(enemy, "enemySpriteRenderer", sr);
            SetPrivateField(enemy, "enemyFrames", frames);
            SetPrivateField(enemy, "defensePoseSprite", defensePoseSprite);
            SetPrivateField(enemy, "archerArrowSprite", arrowSprite);

            return enemy;
        }

        private static void CreateFlameProp(string name, Transform parent, Sprite flameSprite, Vector3 pos, float scale)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * scale;
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = flameSprite;
            sr.sortingOrder = 30;
        }

        // =========================================================================================
        // HUD TEATRAL: 3 CORAZONES CON HILO + CARTEL COLGANTE ("CAJA DE TEXTO") + CORTINAS ROJAS
        // =========================================================================================
        private static StoryAndEndingManager BuildTheaterCurtainsHUDAndStoryUI(
            StoryAndEndingManager.StageNarrativeType stageType,
            string nextSceneName,
            string previousSceneName,
            PlayerMagoController player,
            Sprite stickSprite,
            GameObject[] subSections,
            Sprite brokenStickSprite,
            Transform scissorsTransform,
            GameObject endingAFlames)
        {
            Sprite curtainsOpen = LoadSingleSprite("Cortinas Rojas Abiertas");
            Sprite curtainsHalf = LoadSingleSprite("Cortinas Rojas Entreabiertas");
            Sprite curtainsClosed = LoadSingleSprite("Cortinas Rojas Cerradas");

            // Telón con los valores manuales exactos definidos por el usuario en Stage 1: pos (0, -0.1, 0), scale (1.07, 1.1, 1)
            GameObject curtainWorldGO = CreateFullScreenSprite(
                "Theater_RedCurtains_Foreground",
                curtainsOpen,
                50,
                new Vector3(0f, -0.1f, 0f),
                new Vector3(1.07f, 1.1f, 1f)
            );
            SpriteRenderer curtainWorldSR = curtainWorldGO.GetComponent<SpriteRenderer>();

            // 1) HUD DE 3 CORAZONES SOSTENIDOS POR HILO EN LA PARTE SUPERIOR IZQUIERDA
            CreateHeartsHUDTopLeft(player);

            // 2) HUD DE OPCIONES (SONIDO Y SALIR) SOSTENIDOS POR HILO EN LA PARTE SUPERIOR DERECHA
            CreateTopRightOptionsHUD();

            // 3) CARTEL DE TEXTO COLGANTE ("Caja de Texto" + Palito desde arriba)
            HangingTextSignPuppet hangingSign = CreateHangingTextSignPuppet(stickSprite);

            // 3) CANVAS PARA BOTONES DE DECISIÓN FINAL EN LA CUEVA
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
            }

            GameObject canvasGO = new GameObject("Canvas_StoryUI");
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGO.AddComponent<GraphicRaycaster>();

            RectTransform choicePanel = CreateUIPanel("EndingChoice_Panel", canvasGO.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), new Vector2(1140f, 195f), new Color(0.10f, 0.06f, 0.05f, 0.94f));
            Button btnA = CreateUIButton("Btn_EndingA_Spare", choicePanel, new Vector2(-275f, 0f), new Vector2(500f, 135f),
                "[1] PERDONAR AL DRAGÓN (Opción A)\nDejar que vuele al pueblo y regresar a casa", new Color(0.65f, 0.24f, 0.12f, 1f));
            Button btnB = CreateUIButton("Btn_EndingB_Kill", choicePanel, new Vector2(275f, 0f), new Vector2(500f, 135f),
                "[2] MATAR AL DRAGÓN (Opción B)\nTerminar el encargo y volver al pueblo", new Color(0.20f, 0.30f, 0.48f, 1f));
            choicePanel.gameObject.SetActive(false);

            GameObject managersGO = new GameObject("Theater_Managers");
            CurtainStageManager curtainMgr = managersGO.AddComponent<CurtainStageManager>();
            StoryAndEndingManager storyMgr = managersGO.AddComponent<StoryAndEndingManager>();

            SetPrivateField(curtainMgr, "curtainWorldRenderer", curtainWorldSR);
            SetPrivateField(curtainMgr, "curtainsOpenSprite", curtainsOpen);
            SetPrivateField(curtainMgr, "curtainsHalfSprite", curtainsHalf);
            SetPrivateField(curtainMgr, "curtainsClosedSprite", curtainsClosed);
            SetPrivateField(curtainMgr, "nextSceneName", nextSceneName);
            SetPrivateField(curtainMgr, "previousSceneName", previousSceneName);
            SetPrivateField(curtainMgr, "rightExitTriggerX", 6.2f);
            SetPrivateField(curtainMgr, "leftExitTriggerX", -6.2f);
            SetPrivateField(curtainMgr, "playerStartX", -5.3f);
            SetPrivateField(curtainMgr, "playerReturnRightX", 5.3f);
            SetPrivateField(curtainMgr, "stageSubSections", subSections);
            SetPrivateField(curtainMgr, "playerMago", player);
            SetPrivateField(curtainMgr, "storyManager", storyMgr);

            SetPrivateField(storyMgr, "stageType", stageType);
            SetPrivateField(storyMgr, "hangingTextSign", hangingSign);
            SetPrivateField(storyMgr, "endingChoicePanel", choicePanel.gameObject);
            SetPrivateField(storyMgr, "btnEndingA_SpareDragon", btnA);
            SetPrivateField(storyMgr, "btnEndingB_KillDragon", btnB);
            SetPrivateField(storyMgr, "brokenStickSprite", brokenStickSprite);
            SetPrivateField(storyMgr, "giantScissorsTransform", scissorsTransform);
            SetPrivateField(storyMgr, "endingAFlamesContainer", endingAFlames);
            SetPrivateField(storyMgr, "playerMago", player);
            SetPrivateField(storyMgr, "curtainManager", curtainMgr);

            return storyMgr;
        }

        private static PuppetHeartHUD CreateHeartsHUDTopLeft(PlayerMagoController player)
        {
            Sprite[] heartSheet = LoadSprites("Corazones y etiqueta de papel");
            Sprite heartSprite = GetAt(heartSheet, 0);  // Frame 0: Corazón intacto
            Sprite threadSprite = GetAt(heartSheet, 3); // Frame 3: Hilo que sostiene al corazón

            GameObject hudRoot = new GameObject("HUD_Hearts_TopLeft");
            hudRoot.transform.position = Vector3.zero;

            PuppetHeartHUD heartHUD = hudRoot.AddComponent<PuppetHeartHUD>();
            PuppetHeartHUD.HeartSlot[] slots = new PuppetHeartHUD.HeartSlot[3];

            // Posicionados en la esquina superior izquierda dentro del marco del telón
            float startX = -5.65f;
            float spacingX = 0.90f;
            float topY = 3.55f;

            for (int i = 0; i < 3; i++)
            {
                GameObject slotGO = new GameObject($"HeartSlot_{i + 1}");
                slotGO.transform.SetParent(hudRoot.transform, false);
                slotGO.transform.localPosition = new Vector3(startX + i * spacingX, topY, 0f);

                // Corazón (Frame 0)
                GameObject heartGO = new GameObject("Corazon_Sprite");
                heartGO.transform.SetParent(slotGO.transform, false);
                heartGO.transform.localPosition = Vector3.zero;
                heartGO.transform.localScale = Vector3.one * 0.24f;
                SpriteRenderer heartSR = heartGO.AddComponent<SpriteRenderer>();
                heartSR.sprite = heartSprite;
                heartSR.sortingOrder = 48; // Detrás de las cortinas rojas (50)

                // Hilo (Frame 3) sosteniendo al corazón desde arriba (con su punta inferior oculta detrás del corazón)
                GameObject threadGO = new GameObject("Hilo_Thread");
                threadGO.transform.SetParent(slotGO.transform, false);
                threadGO.transform.localPosition = new Vector3(0f, 0.08f, 0f);
                threadGO.transform.localScale = new Vector3(0.42f, 0.55f, 1f);
                SpriteRenderer threadSR = threadGO.AddComponent<SpriteRenderer>();
                threadSR.sprite = threadSprite;
                threadSR.sortingOrder = 47; // Detrás del corazón y del telón

                slots[i] = new PuppetHeartHUD.HeartSlot
                {
                    slotRoot = slotGO.transform,
                    heartRenderer = heartSR,
                    threadRenderer = threadSR,
                    heartInitialLocalPos = heartGO.transform.localPosition,
                    threadInitialLocalPos = threadGO.transform.localPosition
                };
            }

            SetPrivateField(heartHUD, "heartSlots", slots);
            SetPrivateField(heartHUD, "playerMago", player);
            return heartHUD;
        }

        private static StageTopRightHUD CreateTopRightOptionsHUD()
        {
            Sprite[] hudSprites = LoadSprites("Hud Opciones UnCuentoMas");
            Sprite soundOnSprite = GetAt(hudSprites, 2);
            Sprite soundOffSprite = GetAt(hudSprites, 3);
            Sprite exitButtonSprite = GetAt(hudSprites, 4);

            Sprite[] finalSprites = LoadSprites("Boton Menu Final");
            Sprite finalMenuButtonSprite = GetAt(finalSprites, 0);

            Sprite[] heartSprites = LoadSprites("Corazones y etiqueta de papel");
            Sprite threadSprite = GetAt(heartSprites, 3);

            GameObject hudRoot = new GameObject("HUD_TopRight_Options");
            hudRoot.transform.position = Vector3.zero;

            // 1. Botón de Sonido (desplazado a la izquierda para evitar corte con el telón)
            GameObject soundGO = new GameObject("Btn_SoundToggle");
            soundGO.transform.SetParent(hudRoot.transform, false);
            soundGO.transform.position = new Vector3(4.25f, 3.65f, 0f);
            soundGO.transform.localScale = Vector3.one * 0.45f;
            SpriteRenderer soundSR = soundGO.AddComponent<SpriteRenderer>();
            soundSR.sprite = StageTopRightHUD.IsSoundMuted ? soundOffSprite : soundOnSprite;
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

            // 2. Botón de Salir (volver al Menú Principal)
            GameObject exitGO = new GameObject("Btn_ExitToMenu");
            exitGO.transform.SetParent(hudRoot.transform, false);
            exitGO.transform.position = new Vector3(5.05f, 3.65f, 0f);
            exitGO.transform.localScale = Vector3.one * 0.45f;
            SpriteRenderer exitSR = exitGO.AddComponent<SpriteRenderer>();
            exitSR.sprite = exitButtonSprite;
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

            // 3. Botón de Menú Final
            GameObject finalGO = new GameObject("Btn_MenuFinal");
            finalGO.transform.SetParent(hudRoot.transform, false);
            finalGO.transform.position = new Vector3(0f, -1.25f, 0f);
            finalGO.transform.localScale = Vector3.one * 0.85f;
            SpriteRenderer finalSR = finalGO.AddComponent<SpriteRenderer>();
            finalSR.sprite = finalMenuButtonSprite;
            finalSR.sortingOrder = 55;
            BoxCollider2D finalCol = finalGO.AddComponent<BoxCollider2D>();
            finalCol.size = new Vector2(2.8f, 1.5f);
            finalCol.isTrigger = true;
            finalGO.SetActive(false);

            StageTopRightHUD hud = hudRoot.AddComponent<StageTopRightHUD>();
            SetPrivateField(hud, "soundOnSprite", soundOnSprite);
            SetPrivateField(hud, "soundOffSprite", soundOffSprite);
            SetPrivateField(hud, "exitButtonSprite", exitButtonSprite);
            SetPrivateField(hud, "finalMenuButtonSprite", finalMenuButtonSprite);
            SetPrivateField(hud, "threadSprite", threadSprite);
            SetPrivateField(hud, "soundRenderer", soundSR);
            SetPrivateField(hud, "exitRenderer", exitSR);
            SetPrivateField(hud, "finalMenuRenderer", finalSR);
            SetPrivateField(hud, "soundThreadRenderer", soundThreadSR);
            SetPrivateField(hud, "exitThreadRenderer", exitThreadSR);
            SetPrivateField(hud, "soundCollider", soundCol);
            SetPrivateField(hud, "exitCollider", exitCol);
            SetPrivateField(hud, "finalMenuCollider", finalCol);

            return hud;
        }

        private static HangingTextSignPuppet CreateHangingTextSignPuppet(
            Sprite stickSprite,
            string holderName = "Prop_CartelTexto_Colgante",
            float posX = 0.35f,
            float loweredY = 2.75f,
            float scale = 0.54f,
            string defaultText = "")
        {
            Sprite signBoxSprite = LoadSingleSprite("Caja de Texto");

            GameObject signHolderGO = new GameObject(holderName);
            signHolderGO.transform.position = Vector3.zero;

            GameObject visualRoot = new GameObject("SignVisualRoot");
            visualRoot.transform.SetParent(signHolderGO.transform, false);
            visualRoot.transform.localPosition = new Vector3(posX, loweredY, 0f);
            visualRoot.transform.localScale = Vector3.one * scale;

            // Cuadro de pergamino grande ("Caja de Texto")
            SpriteRenderer boxSR = visualRoot.AddComponent<SpriteRenderer>();
            boxSR.sprite = signBoxSprite;
            boxSR.sortingOrder = 45;

            // Palito que sostiene a la Caja de Texto desde arriba (punta redondeada oculta detrás del pergamino)
            GameObject stickGO = new GameObject("Palito_Superior_Cartel");
            stickGO.transform.SetParent(visualRoot.transform, false);
            stickGO.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            stickGO.transform.localScale = new Vector3(1.05f, -1.65f, 1f);
            SpriteRenderer stickSR = stickGO.AddComponent<SpriteRenderer>();
            stickSR.sprite = stickSprite;
            stickSR.sortingOrder = 44;

            // Texto TMP en mundo directamente sobre la Caja de Texto (sortingOrder = 46, por delante de Caja de Texto = 45)
            GameObject tmpGO = new GameObject("SignText_TMP");
            tmpGO.transform.SetParent(visualRoot.transform, false);
            tmpGO.transform.localPosition = new Vector3(0f, 0f, -0.05f);
            tmpGO.transform.localScale = Vector3.one;

            TextMeshPro signTMP = tmpGO.AddComponent<TextMeshPro>();
            RectTransform tmpRT = signTMP.rectTransform;
            tmpRT.sizeDelta = new Vector2(11.8f, 3.5f);

            TMP_FontAsset fellFont = GetOrCreateIMFellFontAsset("IMFellEnglish-Regular");
            if (fellFont != null)
            {
                signTMP.font = fellFont;
            }
            signTMP.margin = new Vector4(0.35f, 0.15f, 0.35f, 0.15f);
            signTMP.lineSpacing = 2f;
            signTMP.fontSize = 4.8f;
            signTMP.enableAutoSizing = true;
            signTMP.fontSizeMin = 3.4f;
            signTMP.fontSizeMax = 5.4f;
            signTMP.fontStyle = FontStyles.Bold;
            signTMP.alignment = TextAlignmentOptions.Center;
            signTMP.color = new Color(0.18f, 0.10f, 0.05f, 1f);
            signTMP.sortingOrder = 46;
            signTMP.textWrappingMode = TextWrappingModes.Normal;
            signTMP.text = defaultText;

            MeshRenderer tmpMeshRenderer = tmpGO.GetComponent<MeshRenderer>();
            if (tmpMeshRenderer != null)
            {
                tmpMeshRenderer.sortingOrder = 46;
            }

            HangingTextSignPuppet signPuppet = signHolderGO.AddComponent<HangingTextSignPuppet>();
            SetPrivateField(signPuppet, "signVisualRoot", visualRoot.transform);
            SetPrivateField(signPuppet, "signBoxRenderer", boxSR);
            SetPrivateField(signPuppet, "topStickRenderer", stickSR);
            SetPrivateField(signPuppet, "signTextTMP", signTMP);
            SetPrivateField(signPuppet, "imFellFontAsset", fellFont);
            SetPrivateField(signPuppet, "loweredY", loweredY);
            SetPrivateField(signPuppet, "raisedHiddenY", 7.15f);

            return signPuppet;
        }

        public static TMP_FontAsset GetOrCreateIMFellFontAsset(string fontName = "IMFellEnglish-Regular")
        {
            string assetPath = $"Assets/_Project/Resources/Fonts/{fontName} SDF.asset";
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null) return existing;

            string ttfPath = $"Assets/_Project/Art/Fonts/{fontName}.ttf";
            Font font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (font == null)
            {
                Debug.LogWarning($"[Un Cuento Más] No se encontró el archivo TTF en: {ttfPath}");
                return null;
            }

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(font);
            if (fontAsset != null)
            {
                if (!Directory.Exists("Assets/_Project/Resources/Fonts"))
                {
                    Directory.CreateDirectory("Assets/_Project/Resources/Fonts");
                }
                AssetDatabase.CreateAsset(fontAsset, assetPath);
                if (fontAsset.material != null)
                {
                    fontAsset.material.name = fontAsset.name + " Material";
                    AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
                }
                if (fontAsset.atlasTextures != null)
                {
                    for (int i = 0; i < fontAsset.atlasTextures.Length; i++)
                    {
                        var tex = fontAsset.atlasTextures[i];
                        if (tex != null)
                        {
                            tex.name = fontAsset.name + " Atlas " + i;
                            AssetDatabase.AddObjectToAsset(tex, fontAsset);
                        }
                    }
                }
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"✅ [Un Cuento Más] Fuente '{fontName}' SDF creada con éxito en: {assetPath}");
            }
            return fontAsset;
        }

        private static RectTransform CreateUIPanel(string name, Transform parent, Vector2 anchor, Vector2 anchoredPos, Vector2 size, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = color;
            return rt;
        }

        private static TMP_Text CreateUIText(string name, Transform parent, Vector2 anchoredPos, Vector2 size, float fontSize)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.98f, 0.93f, 0.78f, 1f);
            return tmp;
        }

        private static Button CreateUIButton(string name, Transform parent, Vector2 anchoredPos, Vector2 size, string label, Color bgColor)
        {
            RectTransform rt = CreateUIPanel(name, parent, new Vector2(0.5f, 0.5f), anchoredPos, size, bgColor);
            Button btn = rt.gameObject.AddComponent<Button>();
            TMP_Text txt = CreateUIText("Label_TMP", rt, Vector2.zero, size - new Vector2(20f, 16f), 21);
            txt.text = label;
            return btn;
        }

        private static Sprite LoadSingleSprite(string baseFileName)
        {
            Sprite[] all = LoadSprites(baseFileName);
            return all.Length > 0 ? all[0] : null;
        }

        private static Sprite[] LoadSprites(string baseFileName)
        {
            string assetPath = $"Assets/_Project/Sprites/{baseFileName}.png";
            Object[] objs = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            List<Sprite> list = new List<Sprite>();
            foreach (Object o in objs)
            {
                if (o is Sprite s) list.Add(s);
            }
            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return list.ToArray();
        }

        private static Sprite GetAt(Sprite[] arr, int index)
        {
            if (arr == null || arr.Length == 0) return null;
            return arr[Mathf.Clamp(index, 0, arr.Length - 1)];
        }

        // =========================================================================================
        // ESCENA 4: STAGE 4 BOSQUE NOCHE (REGRESO DE LA CUEVA HACIA EL PUEBLO)
        // =========================================================================================
        private static void BuildStage4ForestNightScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupStaticTheaterCameraAndFloor();

            Sprite[] tools = LoadSprites("Herramientas y Ataques");
            Sprite stickSprite = GetAt(tools, 0);
            Sprite brokenStickSprite = GetAt(tools, 1);

            // Fondo noche: "Stage 2 Bosque (Final)" con pos (0, -0.25, 0), scale (1.01, 1, 1), sortingOrder -20
            CreateFullScreenSprite(
                "Background_Stage4_BosqueNoche",
                LoadSingleSprite("Stage 2 Bosque (Final)"),
                -20,
                new Vector3(0f, -0.25f, 0f),
                new Vector3(1.01f, 1f, 1f)
            );

            // Escenografía del bosque nocturno (torre vacía en orden 10 usando Torres Medievales 2_0)
            Sprite[] torres2 = LoadSprites("Torres Medievales 2");
            GameObject forestProps = new GameObject("Bosque_Noche_Escenografia");
            forestProps.transform.position = new Vector3(0f, -1.2f, 0f);

            CreatePuppetStructure("Torre_Madera_Vacia", forestProps.transform, GetAt(torres2, 0), stickSprite,
                new Vector3(0.5f, -2.75f, 0f), 0.46f, 10, 0.8f, stickInsertY: 2.8f);

            // Player Mago Final apareciendo a la derecha (X = 5.3) y caminando hacia la izquierda
            PlayerMagoController player = CreatePlayerMago(new Vector3(5.3f, -3.95f, 0f), tools, allowDiagonalFromStart: true);
            player.SetFacingDirection(false);

            StoryAndEndingManager storyMgr = BuildTheaterCurtainsHUDAndStoryUI(
                stageType: StoryAndEndingManager.StageNarrativeType.Stage4_ForestNight,
                nextSceneName: "Stage5_PuebloNoche",
                previousSceneName: "",
                player: player,
                stickSprite: stickSprite,
                subSections: null,
                brokenStickSprite: brokenStickSprite,
                scissorsTransform: null,
                endingAFlames: null
            );

            CurtainStageManager curtainMgr = storyMgr.GetComponent<CurtainStageManager>();
            if (curtainMgr != null)
            {
                SetPrivateField(curtainMgr, "advanceGoingLeft", true);
                SetPrivateField(curtainMgr, "playerStartX", 5.3f);
                SetPrivateField(curtainMgr, "playerReturnRightX", 5.3f);
                SetPrivateField(curtainMgr, "leftExitTriggerX", -6.2f);
            }

            EditorSceneManager.SaveScene(scene, Stage4Path);
        }

        // =========================================================================================
        // ESCENA 5: STAGE 5 PUEBLO NOCHE (FINALES A Y B JUGABLES)
        // =========================================================================================
        private static void BuildStage5VillageNightScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupStaticTheaterCameraAndFloor();

            Sprite[] tools = LoadSprites("Herramientas y Ataques");
            Sprite stickSprite = GetAt(tools, 0);
            Sprite brokenStickSprite = GetAt(tools, 1);

            // Fondo noche: "Stage 1 Paisaje Noche (Final)" con pos (0, -0.25, 0), scale (1.01, 1, 1), sortingOrder -20
            CreateFullScreenSprite(
                "Background_Stage5_PuebloNoche",
                LoadSingleSprite("Stage 1 Paisaje Noche (Final)"),
                -20,
                new Vector3(0f, -0.25f, 0f),
                new Vector3(1.01f, 1f, 1f)
            );

            Sprite[] casas1 = LoadSprites("Casas Medievales");
            Sprite[] aldeanosYReySprites = LoadSprites("Aldeanos y Rey Inocencia y Traicion");
            Sprite[] campesinosSprites = LoadSprites("Campesinos Inocencia y Traicion");

            GameObject villageProps = new GameObject("Pueblo_Noche_Estructuras");
            villageProps.transform.position = new Vector3(0f, -1.2f, 0f);

            // Casa del Mago a la izquierda (queda intacta en el Final A)
            SpriteRenderer wizardHouseSR = CreatePuppetStructure("Casa_Mago_Izquierda", villageProps.transform, GetAt(casas1, 0), stickSprite,
                new Vector3(-5.1f, -2.75f, 0f), 0.54f, -8, 0.9f, stickInsertY: 2.4f);

            // Otras casas del pueblo (se ocultan en el Final A, permanecen en el Final B)
            SpriteRenderer houseCenterSR = CreatePuppetStructure("Casa_Medieval_Centro", villageProps.transform, GetAt(casas1, 1), stickSprite,
                new Vector3(0.5f, -2.75f, 0f), 0.52f, -9, 0.9f, stickInsertY: 2.4f);
            SpriteRenderer houseRightSR = CreatePuppetStructure("Casa_Medieval_Derecha", villageProps.transform, GetAt(casas1, 2), stickSprite,
                new Vector3(4.8f, -2.75f, 0f), 0.53f, -8, 1.0f, stickInsertY: 2.6f);

            // Pueblerinos y Rey para la emboscada del Final B (inicialmente ocultos)
            StageNPCPuppet aldeanosYReyPuppet = CreateStageNPCPuppet(
                "Aldeanos_Y_Rey_Final",
                null,
                aldeanosYReySprites,
                stickSprite,
                new Vector3(-3.5f, -3.95f, 0f),
                0.38f,
                sortingOrder: 16
            );
            aldeanosYReyPuppet.gameObject.SetActive(false);

            StageNPCPuppet campesinosPuppet = CreateStageNPCPuppet(
                "Campesinos_Final",
                null,
                campesinosSprites,
                stickSprite,
                new Vector3(3.5f, -3.95f, 0f),
                0.38f,
                sortingOrder: 14
            );
            campesinosPuppet.gameObject.SetActive(false);

            // Player Mago Final apareciendo a la derecha (X = 5.3) y caminando hacia la izquierda
            PlayerMagoController player = CreatePlayerMago(new Vector3(5.3f, -3.95f, 0f), tools, allowDiagonalFromStart: true);
            player.SetFacingDirection(false);

            StoryAndEndingManager storyMgr = BuildTheaterCurtainsHUDAndStoryUI(
                stageType: StoryAndEndingManager.StageNarrativeType.Stage5_VillageNight,
                nextSceneName: "",
                previousSceneName: "",
                player: player,
                stickSprite: stickSprite,
                subSections: null,
                brokenStickSprite: brokenStickSprite,
                scissorsTransform: null,
                endingAFlames: null
            );

            CurtainStageManager curtainMgr = storyMgr.GetComponent<CurtainStageManager>();
            if (curtainMgr != null)
            {
                SetPrivateField(curtainMgr, "advanceGoingLeft", true);
                SetPrivateField(curtainMgr, "playerStartX", 5.3f);
                SetPrivateField(curtainMgr, "playerReturnRightX", 5.3f);
            }

            SetPrivateField(storyMgr, "wizardHouseLeftObject", wizardHouseSR.transform.parent.gameObject);
            SetPrivateField(storyMgr, "otherVillageHousesToHideInEndingA", new GameObject[]
            {
                houseCenterSR.transform.parent.gameObject,
                houseRightSR.transform.parent.gameObject
            });
            SetPrivateField(storyMgr, "aldeanosYReyPuppet", aldeanosYReyPuppet);
            SetPrivateField(storyMgr, "campesinosPuppet", campesinosPuppet);

            GameObject epilogueGO = new GameObject("EpilogueSignPuppet");
            EpilogueSignPuppet epilogueSign = epilogueGO.AddComponent<EpilogueSignPuppet>();
            epilogueSign.EnsureVisualsAndRopesExist();
            SetPrivateField(storyMgr, "epilogueSign", epilogueSign);

            EditorSceneManager.SaveScene(scene, Stage5Path);
        }

        // =========================================================================================
        // ESCENA 0: MENÚ PRINCIPAL (STAGE 0 MAIN MENU)
        // =========================================================================================
        private static void BuildMainMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupStaticTheaterCameraAndFloor();

            // Fondo negro detrás del telón para cuando se abra a la selección de niveles
            GameObject blackBgGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
            blackBgGO.name = "Background_Black";
            blackBgGO.transform.position = new Vector3(0f, 0f, 0f);
            blackBgGO.transform.localScale = new Vector3(22f, 13f, 1f);
            Object.DestroyImmediate(blackBgGO.GetComponent<Collider>());
            MeshRenderer bgMr = blackBgGO.GetComponent<MeshRenderer>();
            bgMr.sortingOrder = -20;
            bgMr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            bgMr.sharedMaterial.color = Color.black;

            // Sprites necesarios
            Sprite curtainsOpen = LoadSingleSprite("Cortinas Rojas Abiertas");
            Sprite curtainsHalf = LoadSingleSprite("Cortinas Rojas Entreabiertas");
            Sprite curtainsClosed = LoadSingleSprite("Cortinas Rojas Cerradas");

            Sprite logoSprite = LoadSingleSprite("Logo UnCuentoMas");
            Sprite[] toolSprites = LoadSprites("Herramientas y Ataques");
            Sprite[] heartSprites = LoadSprites("Corazones y etiqueta de papel");
            Sprite threadSprite = GetAt(heartSprites, 3); // Frame 3: Hilo

            Sprite[] hudSprites = LoadSprites("Hud Opciones UnCuentoMas");
            Sprite playBtnSprite = GetAt(hudSprites, 1);
            Sprite soundOnSprite = GetAt(hudSprites, 2);
            Sprite soundOffSprite = GetAt(hudSprites, 3);
            Sprite exitBtnSprite = GetAt(hudSprites, 4);

            Sprite[] levelSprites = LoadSprites("Niveles UnCuentoMas");

            // 1. Telón en primer plano (Inicia CERRADO)
            GameObject curtainWorldGO = CreateFullScreenSprite(
                "Theater_RedCurtains_Foreground",
                curtainsClosed,
                50,
                new Vector3(0f, -0.1f, 0f),
                new Vector3(1.07f, 1.1f, 1f)
            );
            SpriteRenderer curtainWorldSR = curtainWorldGO.GetComponent<SpriteRenderer>();

            // 2. Título (Logo) sujeto por 2 hilos desde el techo
            GameObject logoRootGO = new GameObject("Logo_Root");
            logoRootGO.transform.position = new Vector3(0f, 1.35f, 0f);

            GameObject logoVisualGO = new GameObject("Logo_Visual");
            logoVisualGO.transform.SetParent(logoRootGO.transform, false);
            logoVisualGO.transform.localPosition = Vector3.zero;
            logoVisualGO.transform.localScale = Vector3.one * 0.62f;
            SpriteRenderer logoSR = logoVisualGO.AddComponent<SpriteRenderer>();
            logoSR.sprite = logoSprite;
            logoSR.sortingOrder = 52; // Por delante de las cortinas cerradas (50)

            // Hilo Izquierdo del Logo
            GameObject leftThreadGO = new GameObject("Hilo_Izquierdo");
            leftThreadGO.transform.SetParent(logoVisualGO.transform, false);
            leftThreadGO.transform.localPosition = new Vector3(-3.25f, 1.95f, 0f);
            leftThreadGO.transform.localScale = new Vector3(1.1f, 3.2f, 1f);
            SpriteRenderer leftThreadSR = leftThreadGO.AddComponent<SpriteRenderer>();
            leftThreadSR.sprite = threadSprite;
            leftThreadSR.sortingOrder = 51;

            // Hilo Derecho del Logo
            GameObject rightThreadGO = new GameObject("Hilo_Derecho");
            rightThreadGO.transform.SetParent(logoVisualGO.transform, false);
            rightThreadGO.transform.localPosition = new Vector3(3.25f, 1.95f, 0f);
            rightThreadGO.transform.localScale = new Vector3(1.1f, 3.2f, 1f);
            SpriteRenderer rightThreadSR = rightThreadGO.AddComponent<SpriteRenderer>();
            rightThreadSR.sprite = threadSprite;
            rightThreadSR.sortingOrder = 51;

            // Botón de Jugar 1 (debajo del logo, al centro en X = 0f, con su palito)
            Sprite stickSprite = GetAt(toolSprites, 0);
            GameObject firstPlayBtnGO = new GameObject("Btn_Jugar_Menu1");
            firstPlayBtnGO.transform.position = new Vector3(0f, -3.20f, 0f);
            firstPlayBtnGO.transform.localScale = Vector3.one * 0.65f;
            SpriteRenderer firstPlayBtnSR = firstPlayBtnGO.AddComponent<SpriteRenderer>();
            firstPlayBtnSR.sprite = playBtnSprite;
            firstPlayBtnSR.sortingOrder = 52;
            BoxCollider2D firstPlayBtnCol = firstPlayBtnGO.AddComponent<BoxCollider2D>();
            firstPlayBtnCol.size = new Vector2(3.4f, 1.8f);
            firstPlayBtnCol.isTrigger = true;

            // Palito de madera del botón de jugar (insertado detrás del botón, extendiéndose hacia abajo)
            GameObject firstPlayStickGO = new GameObject("Palito_Stick");
            firstPlayStickGO.transform.SetParent(firstPlayBtnGO.transform, false);
            firstPlayStickGO.transform.localPosition = new Vector3(0f, 0.20f, 0f);
            firstPlayStickGO.transform.localScale = new Vector3(1.1f, 3.5f, 1f);
            SpriteRenderer firstPlayStickSR = firstPlayStickGO.AddComponent<SpriteRenderer>();
            firstPlayStickSR.sprite = stickSprite;
            firstPlayStickSR.sortingOrder = 51;

            // Botón de Salir del Menú (arriba a la derecha al lado del botón de sonido en X = 5.05f con hilo)
            GameObject exitBtnGO = new GameObject("Btn_Salir_Menu");
            exitBtnGO.transform.position = new Vector3(5.05f, 3.65f, 0f);
            exitBtnGO.transform.localScale = Vector3.one * 0.45f;
            SpriteRenderer exitBtnSR = exitBtnGO.AddComponent<SpriteRenderer>();
            exitBtnSR.sprite = exitBtnSprite;
            exitBtnSR.sortingOrder = 52;
            BoxCollider2D exitBtnCol = exitBtnGO.AddComponent<BoxCollider2D>();
            exitBtnCol.size = new Vector2(1.6f, 1.6f);
            exitBtnCol.isTrigger = true;

            // Hilo de soporte del botón de salir (igual que el botón de sonido)
            GameObject exitThreadGO = new GameObject("Hilo_Soporte");
            exitThreadGO.transform.SetParent(exitBtnGO.transform, false);
            exitThreadGO.transform.localPosition = new Vector3(0f, 0.20f, 0f);
            exitThreadGO.transform.localScale = new Vector3(1.1f, 3.2f, 1f);
            SpriteRenderer exitThreadSR = exitThreadGO.AddComponent<SpriteRenderer>();
            exitThreadSR.sprite = threadSprite;
            exitThreadSR.sortingOrder = 51;

            // Botón de Sonido en el Menú Principal (arriba a la derecha en X = 4.25f, al lado de Salir)
            GameObject soundGO = new GameObject("Btn_SoundToggle_Menu");
            soundGO.transform.position = new Vector3(4.25f, 3.65f, 0f);
            soundGO.transform.localScale = Vector3.one * 0.45f;
            SpriteRenderer soundSR = soundGO.AddComponent<SpriteRenderer>();
            soundSR.sprite = StageTopRightHUD.IsSoundMuted ? soundOffSprite : soundOnSprite;
            soundSR.sortingOrder = 52;
            BoxCollider2D soundCol = soundGO.AddComponent<BoxCollider2D>();
            soundCol.size = new Vector2(1.6f, 1.6f);
            soundCol.isTrigger = true;

            // Hilo del botón de sonido en el Menú Principal
            GameObject soundThreadGO = new GameObject("Hilo_Soporte");
            soundThreadGO.transform.SetParent(soundGO.transform, false);
            soundThreadGO.transform.localPosition = new Vector3(0f, 0.20f, 0f);
            soundThreadGO.transform.localScale = new Vector3(1.1f, 3.2f, 1f);
            SpriteRenderer soundThreadSR = soundThreadGO.AddComponent<SpriteRenderer>();
            soundThreadSR.sprite = threadSprite;
            soundThreadSR.sortingOrder = 51;

            // 3. Grupo de Selección de Niveles (3 portadas con fondo negro)
            GameObject levelGroupGO = new GameObject("Levels_Selection_Group");
            levelGroupGO.transform.position = Vector3.zero;

            // Nivel 1 (Cuento Jugable)
            GameObject level1GO = new GameObject("Level_1_Cover");
            level1GO.transform.SetParent(levelGroupGO.transform, false);
            level1GO.transform.localPosition = new Vector3(-3.75f, 0.45f, 0f);
            level1GO.transform.localScale = Vector3.one * 0.62f;
            SpriteRenderer level1SR = level1GO.AddComponent<SpriteRenderer>();
            level1SR.sprite = GetAt(levelSprites, 0);
            level1SR.sortingOrder = 30;

            // Botón de Jugar debajo del Nivel 1
            GameObject level1PlayBtnGO = new GameObject("Btn_Jugar_Level1");
            level1PlayBtnGO.transform.SetParent(levelGroupGO.transform, false);
            level1PlayBtnGO.transform.localPosition = new Vector3(-3.75f, -2.35f, 0f);
            level1PlayBtnGO.transform.localScale = Vector3.one * 0.50f;
            SpriteRenderer level1PlayBtnSR = level1PlayBtnGO.AddComponent<SpriteRenderer>();
            level1PlayBtnSR.sprite = playBtnSprite;
            level1PlayBtnSR.sortingOrder = 32;
            BoxCollider2D level1PlayBtnCol = level1PlayBtnGO.AddComponent<BoxCollider2D>();
            level1PlayBtnCol.size = new Vector2(3.4f, 1.8f);
            level1PlayBtnCol.isTrigger = true;

            // Palito de madera del botón de jugar del nivel 1
            GameObject level1StickGO = new GameObject("Palito_Stick");
            level1StickGO.transform.SetParent(level1PlayBtnGO.transform, false);
            level1StickGO.transform.localPosition = new Vector3(0f, 0.20f, 0f);
            level1StickGO.transform.localScale = new Vector3(1.1f, 3.5f, 1f);
            SpriteRenderer level1StickSR = level1StickGO.AddComponent<SpriteRenderer>();
            level1StickSR.sprite = stickSprite;
            level1StickSR.sortingOrder = 31;

            // Nivel 2 (Decorativo)
            GameObject level2GO = new GameObject("Level_2_Cover");
            level2GO.transform.SetParent(levelGroupGO.transform, false);
            level2GO.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            level2GO.transform.localScale = Vector3.one * 0.62f;
            SpriteRenderer level2SR = level2GO.AddComponent<SpriteRenderer>();
            level2SR.sprite = GetAt(levelSprites, 1);
            level2SR.sortingOrder = 30;

            // Nivel 3 (Decorativo)
            GameObject level3GO = new GameObject("Level_3_Cover");
            level3GO.transform.SetParent(levelGroupGO.transform, false);
            level3GO.transform.localPosition = new Vector3(3.75f, 0.45f, 0f);
            level3GO.transform.localScale = Vector3.one * 0.62f;
            SpriteRenderer level3SR = level3GO.AddComponent<SpriteRenderer>();
            level3SR.sprite = GetAt(levelSprites, 2);
            level3SR.sortingOrder = 30;

            levelGroupGO.SetActive(false);

            // 4. Componente Administrador del Menú Principal
            GameObject managerGO = new GameObject("MainMenu_Manager");
            MainMenuController menuCtrl = managerGO.AddComponent<MainMenuController>();

            SetPrivateField(menuCtrl, "curtainRenderer", curtainWorldSR);
            SetPrivateField(menuCtrl, "curtainClosedSprite", curtainsClosed);
            SetPrivateField(menuCtrl, "curtainHalfSprite", curtainsHalf);
            SetPrivateField(menuCtrl, "curtainOpenSprite", curtainsOpen);

            SetPrivateField(menuCtrl, "logoRoot", logoRootGO.transform);
            SetPrivateField(menuCtrl, "logoRenderer", logoSR);
            SetPrivateField(menuCtrl, "leftThreadRenderer", leftThreadSR);
            SetPrivateField(menuCtrl, "rightThreadRenderer", rightThreadSR);
            SetPrivateField(menuCtrl, "firstPlayButtonRoot", firstPlayBtnGO.transform);
            SetPrivateField(menuCtrl, "firstPlayButtonRenderer", firstPlayBtnSR);
            SetPrivateField(menuCtrl, "firstPlayButtonStickRenderer", firstPlayStickSR);
            SetPrivateField(menuCtrl, "firstPlayButtonCollider", firstPlayBtnCol);
            SetPrivateField(menuCtrl, "exitButtonRoot", exitBtnGO.transform);
            SetPrivateField(menuCtrl, "exitButtonRenderer", exitBtnSR);
            SetPrivateField(menuCtrl, "exitButtonThreadRenderer", exitThreadSR);
            SetPrivateField(menuCtrl, "exitButtonCollider", exitBtnCol);
            SetPrivateField(menuCtrl, "exitButtonSprite", exitBtnSprite);
            SetPrivateField(menuCtrl, "stickSprite", stickSprite);

            SetPrivateField(menuCtrl, "levelSelectionGroup", levelGroupGO);
            SetPrivateField(menuCtrl, "level1CoverRenderer", level1SR);
            SetPrivateField(menuCtrl, "level2CoverRenderer", level2SR);
            SetPrivateField(menuCtrl, "level3CoverRenderer", level3SR);
            SetPrivateField(menuCtrl, "level1PlayButtonRoot", level1PlayBtnGO.transform);
            SetPrivateField(menuCtrl, "level1PlayButtonRenderer", level1PlayBtnSR);
            SetPrivateField(menuCtrl, "level1PlayButtonCollider", level1PlayBtnCol);

            SetPrivateField(menuCtrl, "soundRenderer", soundSR);
            SetPrivateField(menuCtrl, "soundThreadRenderer", soundThreadSR);
            SetPrivateField(menuCtrl, "soundCollider", soundCol);
            SetPrivateField(menuCtrl, "soundOnSprite", soundOnSprite);
            SetPrivateField(menuCtrl, "soundOffSprite", soundOffSprite);
            SetPrivateField(menuCtrl, "threadSprite", threadSprite);

            EditorSceneManager.SaveScene(scene, Stage0Path);
        }

        public static void RegisterAllFiveScenesInBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>();
            if (File.Exists(Stage0Path)) scenes.Add(new EditorBuildSettingsScene(Stage0Path, true));
            if (File.Exists(Stage1Path)) scenes.Add(new EditorBuildSettingsScene(Stage1Path, true));
            if (File.Exists(Stage2Path)) scenes.Add(new EditorBuildSettingsScene(Stage2Path, true));
            if (File.Exists(Stage3Path)) scenes.Add(new EditorBuildSettingsScene(Stage3Path, true));
            if (File.Exists(Stage4Path)) scenes.Add(new EditorBuildSettingsScene(Stage4Path, true));
            if (File.Exists(Stage5Path)) scenes.Add(new EditorBuildSettingsScene(Stage5Path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void AdjustSceneSortingOrders(string scenePath)
        {
            if (!File.Exists(scenePath)) return;

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            bool modified = false;

            // 1. Ajustar corazones (48) e hilos (47) para que queden DETRÁS del telón rojo (50)
            PuppetHeartHUD heartHUD = Object.FindFirstObjectByType<PuppetHeartHUD>();
            if (heartHUD != null)
            {
                SpriteRenderer[] srs = heartHUD.GetComponentsInChildren<SpriteRenderer>(true);
                foreach (var sr in srs)
                {
                    if (sr.gameObject.name.Contains("Corazon"))
                    {
                        sr.sortingOrder = 48;
                        modified = true;
                    }
                    else if (sr.gameObject.name.Contains("Hilo"))
                    {
                        sr.sortingOrder = 47;
                        modified = true;
                    }
                }
            }

            // 2. Ajustar torres (10 y 9) y arqueros (8 y 7) para que los arqueros queden DETRÁS de las torres
            SpriteRenderer[] allRenderers = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
            foreach (var sr in allRenderers)
            {
                if (sr == null) continue;
                string goName = sr.gameObject.name;
                string parentName = sr.transform.parent != null ? sr.transform.parent.name : "";

                if (goName.Contains("Torre_") || parentName.Contains("Torre_"))
                {
                    if (goName == "Palito_Stick") sr.sortingOrder = 9;
                    else sr.sortingOrder = 10;
                    modified = true;
                }
                else if (goName.Contains("Arquero_") || parentName.Contains("Arquero_"))
                {
                    if (goName == "Palito_Stick") sr.sortingOrder = 7;
                    else sr.sortingOrder = 8;
                    modified = true;
                }
            }

            // 3. Asegurar que CurtainStageManager no permita retroceder entre escenas
            CurtainStageManager curtainMgr = Object.FindFirstObjectByType<CurtainStageManager>();
            if (curtainMgr != null)
            {
                SetPrivateField(curtainMgr, "previousSceneName", "");
                modified = true;
            }

            if (modified)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            if (target == null) return;
            FieldInfo fi = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (fi != null) fi.SetValue(target, value);
        }
    }
}
#endif
