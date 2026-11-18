using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace UnCuentoMas
{
    /// <summary>
    /// Controlador del Menú Principal teatral:
    /// 1. Inicia con las cortinas rojas cerradas.
    /// 2. Desciende el 'Logo UnCuentoMas' sujeto por 2 hilos desde el techo, con balanceo teatral suave,
    ///    y debajo el botón de 'Jugar'.
    /// 3. Al pulsar 'Jugar', el logo se retira hacia arriba, el telón se abre y revela un fondo negro con
    ///    las 3 portadas de niveles ('Niveles UnCuentoMas').
    /// 4. Únicamente el primer nivel (Cuento 1) posee el botón de 'Jugar' debajo; al pulsarlo, el telón se
    ///    cierra solemnemente e inicia el Stage 1.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("=== 1. TELÓN ROJO TEATRAL ===")]
        [SerializeField] private SpriteRenderer curtainRenderer;
        [SerializeField] private Sprite curtainClosedSprite;
        [SerializeField] private Sprite curtainHalfSprite;
        [SerializeField] private Sprite curtainOpenSprite;

        [Header("=== 2. TÍTULO, BOTÓN DE JUGAR Y BOTÓN DE SALIR (PANTALLA 1) ===")]
        [SerializeField] private Transform logoRoot;
        [SerializeField] private SpriteRenderer logoRenderer;
        [SerializeField] private SpriteRenderer leftThreadRenderer;
        [SerializeField] private SpriteRenderer rightThreadRenderer;
        [SerializeField] private Transform firstPlayButtonRoot;
        [SerializeField] private SpriteRenderer firstPlayButtonRenderer;
        [SerializeField] private SpriteRenderer firstPlayButtonStickRenderer;
        [SerializeField] private Collider2D firstPlayButtonCollider;
        [SerializeField] private Transform exitButtonRoot;
        [SerializeField] private SpriteRenderer exitButtonRenderer;
        [SerializeField] private SpriteRenderer exitButtonThreadRenderer;
        [SerializeField] private Collider2D exitButtonCollider;
        [SerializeField] private Sprite exitButtonSprite;
        [SerializeField] private Sprite stickSprite;

        [Header("=== 3. SELECCIÓN DE NIVELES (PANTALLA 2 - TELÓN ABIERTO) ===")]
        [SerializeField] private GameObject levelSelectionGroup;
        [SerializeField] private SpriteRenderer level1CoverRenderer;
        [SerializeField] private SpriteRenderer level2CoverRenderer;
        [SerializeField] private SpriteRenderer level3CoverRenderer;
        [SerializeField] private Transform level1PlayButtonRoot;
        [SerializeField] private SpriteRenderer level1PlayButtonRenderer;
        [SerializeField] private Collider2D level1PlayButtonCollider;
        [SerializeField] private Transform levelBackToMenuButtonRoot;
        [SerializeField] private Collider2D levelBackToMenuButtonCollider;

        [Header("=== 4. BOTÓN DE SONIDO HUD ===")]
        [SerializeField] private SpriteRenderer soundRenderer;
        [SerializeField] private SpriteRenderer soundThreadRenderer;
        [SerializeField] private Collider2D soundCollider;
        [SerializeField] private Sprite soundOnSprite;
        [SerializeField] private Sprite soundOffSprite;
        [SerializeField] private Sprite threadSprite;

        private Camera _mainCamera;
        private bool _isInLevelSelection = false;
        private bool _isTransitioning = false;
        private float _logoTargetY = 1.35f;
        private float _logoCurrentY = 8.2f;
        private float _btnTargetY = -3.20f;
        private Vector3 _soundInitialPos = new Vector3(4.25f, 3.65f, 0f);
        private Vector3 _exitInitialPos = new Vector3(5.05f, 3.65f, 0f);
        private Vector3 _soundThreadInitialPos;

        private void Awake()
        {
            _mainCamera = Camera.main;
            AutoFindSpritesIfMissing();
            EnsureStickAndThreadExist();

            // Estado inicial: cortinas cerradas y grupo de niveles oculto
            if (curtainRenderer != null && curtainClosedSprite != null)
            {
                curtainRenderer.sprite = curtainClosedSprite;
            }

            if (levelSelectionGroup != null)
            {
                levelSelectionGroup.SetActive(false);
            }

            if (soundRenderer != null)
            {
                if (soundRenderer.transform.localPosition.x >= 5.6f || soundRenderer.transform.localPosition.x < 2.0f)
                {
                    Vector3 p = soundRenderer.transform.localPosition;
                    p.x = 4.25f;
                    soundRenderer.transform.localPosition = p;
                }
                _soundInitialPos = soundRenderer.transform.localPosition;
            }

            if (exitButtonRenderer != null)
            {
                if (exitButtonRenderer.transform.localPosition.x >= 5.6f || exitButtonRenderer.transform.localPosition.x < 2.0f)
                {
                    Vector3 p = exitButtonRenderer.transform.localPosition;
                    p.x = 5.05f;
                    exitButtonRenderer.transform.localPosition = p;
                }
                _exitInitialPos = exitButtonRenderer.transform.localPosition;
            }

            if (soundThreadRenderer != null)
            {
                _soundThreadInitialPos = soundThreadRenderer.transform.localPosition;
            }

            UpdateSoundVisuals();
        }

        private IEnumerator Start()
        {
            // Descenso teatral del logo y elevación del botón de jugar con su palito desde abajo (centrado en X = 0)
            if (logoRoot != null)
            {
                _logoCurrentY = 8.2f;
                logoRoot.localPosition = new Vector3(0f, _logoCurrentY, 0f);
            }

            if (firstPlayButtonRoot != null)
            {
                firstPlayButtonRoot.localPosition = new Vector3(0f, -6.8f, 0f);
            }

            yield return new WaitForSeconds(0.25f);

            float duration = 1.1f;
            float elapsed = 0f;
            Vector3 logoStart = new Vector3(0f, 8.2f, 0f);
            Vector3 logoEnd = new Vector3(0f, _logoTargetY, 0f);

            Vector3 playStart = new Vector3(0f, -6.8f, 0f);
            Vector3 playEnd = new Vector3(0f, _btnTargetY, 0f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                if (logoRoot != null) logoRoot.localPosition = Vector3.Lerp(logoStart, logoEnd, t);
                if (firstPlayButtonRoot != null) firstPlayButtonRoot.localPosition = Vector3.Lerp(playStart, playEnd, t);
                yield return null;
            }

            if (logoRoot != null) logoRoot.localPosition = logoEnd;
            if (firstPlayButtonRoot != null) firstPlayButtonRoot.localPosition = playEnd;
        }

        private void Update()
        {
            // Balanceo teatral continuo del logo sujeto por hilos
            if (!_isInLevelSelection && logoRoot != null && !_isTransitioning)
            {
                float bobY = Mathf.Sin(Time.time * 2.2f) * 0.08f;
                float rotZ = Mathf.Sin(Time.time * 1.6f) * 1.25f;
                logoRoot.localPosition = new Vector3(0f, _logoTargetY + bobY, 0f);
                logoRoot.localRotation = Quaternion.Euler(0f, 0f, rotZ);

                // Animación de títere en palito para el botón de jugar (al medio en X = 0)
                if (firstPlayButtonRoot != null)
                {
                    float btnBob = Mathf.Sin(Time.time * 2.3f) * 0.04f;
                    float btnTilt = Mathf.Sin(Time.time * 1.7f) * 1.6f;
                    firstPlayButtonRoot.localPosition = new Vector3(0f, _btnTargetY + btnBob, 0f);
                    firstPlayButtonRoot.localRotation = Quaternion.Euler(0f, 0f, btnTilt);
                }
            }
            else if (_isInLevelSelection && level1PlayButtonRoot != null && !_isTransitioning)
            {
                float lvl1Bob = Mathf.Sin(Time.time * 2.3f) * 0.04f;
                float lvl1Tilt = Mathf.Sin(Time.time * 1.7f) * 1.6f;
                level1PlayButtonRoot.localPosition = new Vector3(-3.75f, -2.35f + lvl1Bob, 0f);
                level1PlayButtonRoot.localRotation = Quaternion.Euler(0f, 0f, lvl1Tilt);

                if (levelBackToMenuButtonRoot != null)
                {
                    float backBob = Mathf.Sin(Time.time * 2.3f + 0.85f) * 0.04f;
                    float backTilt = Mathf.Sin(Time.time * 1.7f + 0.85f) * 1.6f;
                    levelBackToMenuButtonRoot.localPosition = new Vector3(3.75f, -2.35f + backBob, 0f);
                    levelBackToMenuButtonRoot.localRotation = Quaternion.Euler(0f, 0f, backTilt);
                }
            }

            // Animación teatral de títere con hilo para el botón de sonido (X = 4.25)
            if (soundRenderer != null)
            {
                if (_soundInitialPos.x < 2.0f || _soundInitialPos.x >= 5.6f) _soundInitialPos = new Vector3(4.25f, 3.65f, 0f);
                float soundPhase = Time.time * 2.1f + 0.45f;
                float soundSway = Mathf.Sin(soundPhase) * 2.8f;
                float soundBob = Mathf.Cos(soundPhase * 0.8f) * 0.025f;
                soundRenderer.transform.position = _soundInitialPos + new Vector3(0f, soundBob, 0f);
                soundRenderer.transform.rotation = Quaternion.Euler(0f, 0f, soundSway);
                if (soundThreadRenderer != null)
                {
                    soundThreadRenderer.transform.localPosition = new Vector3(0f, 0.20f, 0f);
                    soundThreadRenderer.transform.localRotation = Quaternion.identity;
                }
            }

            // Animación teatral de títere con hilo para el botón de salir (X = 5.05)
            if (exitButtonRenderer != null)
            {
                if (_exitInitialPos.x < 2.0f || _exitInitialPos.x >= 5.6f) _exitInitialPos = new Vector3(5.05f, 3.65f, 0f);
                float exitPhase = Time.time * 2.1f + 1.6f;
                float exitSway = Mathf.Sin(exitPhase) * 2.8f;
                float exitBob = Mathf.Cos(exitPhase * 0.8f) * 0.025f;
                exitButtonRenderer.transform.position = _exitInitialPos + new Vector3(0f, exitBob, 0f);
                exitButtonRenderer.transform.rotation = Quaternion.Euler(0f, 0f, exitSway);
                if (exitButtonThreadRenderer != null)
                {
                    exitButtonThreadRenderer.transform.localPosition = new Vector3(0f, 0.20f, 0f);
                    exitButtonThreadRenderer.transform.localRotation = Quaternion.identity;
                }
            }

            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_mainCamera == null || _isTransitioning) return;

            Vector3 mousePos = _mainCamera.ScreenToWorldPoint(mouse.position.ReadValue());
            Vector2 mousePos2D = new Vector2(mousePos.x, mousePos.y);

            // Clic en Botón de Sonido
            if (soundCollider != null && soundCollider.OverlapPoint(mousePos2D))
            {
                StageTopRightHUD.IsSoundMuted = !StageTopRightHUD.IsSoundMuted;
                AudioListener.pause = StageTopRightHUD.IsSoundMuted;
                AudioListener.volume = StageTopRightHUD.IsSoundMuted ? 0f : 1f;
                AudioManager.Instance?.SyncWithMuteState();
                UpdateSoundVisuals();
                return;
            }

            // Clic en el Primer Botón de Jugar (bajar logo y abrir telón a niveles)
            if (!_isInLevelSelection && firstPlayButtonCollider != null && firstPlayButtonCollider.OverlapPoint(mousePos2D))
            {
                StartCoroutine(OnFirstPlayButtonClickedRoutine());
                return;
            }

            // Clic en el Botón de Salir (Pantalla 1)
            if (!_isInLevelSelection && exitButtonCollider != null && exitButtonCollider.OverlapPoint(mousePos2D))
            {
                StartCoroutine(OnExitButtonClickedRoutine());
                return;
            }

            // Clic en el Botón de Jugar de Nivel 1 (cerrar telón y entrar a Stage 1)
            if (_isInLevelSelection && level1PlayButtonCollider != null && level1PlayButtonCollider.OverlapPoint(mousePos2D))
            {
                StartCoroutine(OnLevel1PlayButtonClickedRoutine());
                return;
            }

            // Clic en Volver al Menú Principal (Pantalla 2)
            if (_isInLevelSelection && levelBackToMenuButtonCollider != null && levelBackToMenuButtonCollider.OverlapPoint(mousePos2D))
            {
                StartCoroutine(OnReturnToTitleRoutine());
                return;
            }
        }

        private void UpdateSoundVisuals()
        {
            if (soundRenderer == null) return;
            soundRenderer.sprite = StageTopRightHUD.IsSoundMuted ? soundOffSprite : soundOnSprite;
        }

        private IEnumerator OnExitButtonClickedRoutine()
        {
            _isTransitioning = true;
            if (exitButtonRoot != null)
            {
                Vector3 orig = exitButtonRoot.localScale;
                exitButtonRoot.localScale = orig * 0.82f;
                yield return new WaitForSeconds(0.08f);
                exitButtonRoot.localScale = orig * 1.05f;
                yield return new WaitForSeconds(0.06f);
                exitButtonRoot.localScale = orig;
            }

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private IEnumerator OnFirstPlayButtonClickedRoutine()
        {
            _isTransitioning = true;

            // Retirada del logo hacia arriba y del botón de jugar hacia abajo
            float retractDur = 0.65f;
            float elapsed = 0f;
            Vector3 logoStart = logoRoot != null ? logoRoot.localPosition : Vector3.zero;
            Vector3 logoEnd = new Vector3(0f, 8.5f, 0f);

            Vector3 btnStart = firstPlayButtonRoot != null ? firstPlayButtonRoot.localPosition : Vector3.zero;
            Vector3 btnEnd = new Vector3(0f, -6.8f, 0f);

            while (elapsed < retractDur)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / retractDur);
                if (logoRoot != null) logoRoot.localPosition = Vector3.Lerp(logoStart, logoEnd, t);
                if (firstPlayButtonRoot != null) firstPlayButtonRoot.localPosition = Vector3.Lerp(btnStart, btnEnd, t);
                yield return null;
            }

            if (firstPlayButtonRoot != null) firstPlayButtonRoot.gameObject.SetActive(false);
            if (logoRoot != null) logoRoot.gameObject.SetActive(false);

            // Abrir el telón (Cerradas -> Entreabiertas -> Abiertas)
            yield return new WaitForSeconds(0.1f);
            if (curtainRenderer != null && curtainHalfSprite != null)
            {
                curtainRenderer.sprite = curtainHalfSprite;
                yield return new WaitForSeconds(0.18f);
            }
            if (curtainRenderer != null && curtainOpenSprite != null)
            {
                curtainRenderer.sprite = curtainOpenSprite;
            }

            // Activar las 3 portadas de niveles con animación suave
            if (levelSelectionGroup != null)
            {
                levelSelectionGroup.SetActive(true);
            }

            _isInLevelSelection = true;
            _isTransitioning = false;
        }

        private IEnumerator OnReturnToTitleRoutine()
        {
            _isTransitioning = true;
            if (levelSelectionGroup != null) levelSelectionGroup.SetActive(false);

            if (curtainRenderer != null && curtainHalfSprite != null)
            {
                curtainRenderer.sprite = curtainHalfSprite;
                yield return new WaitForSeconds(0.18f);
            }
            if (curtainRenderer != null && curtainClosedSprite != null)
            {
                curtainRenderer.sprite = curtainClosedSprite;
                yield return new WaitForSeconds(0.25f);
            }

            _isInLevelSelection = false;
            if (logoRoot != null) logoRoot.gameObject.SetActive(true);
            if (firstPlayButtonRoot != null) firstPlayButtonRoot.gameObject.SetActive(true);
            if (exitButtonRoot != null) exitButtonRoot.gameObject.SetActive(true);

            yield return StartCoroutine(Start());
            _isTransitioning = false;
        }

        private IEnumerator OnLevel1PlayButtonClickedRoutine()
        {
            _isTransitioning = true;

            // Feedback de pulsación
            if (level1PlayButtonRoot != null)
            {
                level1PlayButtonRoot.localScale = Vector3.one * 0.42f;
                yield return new WaitForSeconds(0.08f);
                level1PlayButtonRoot.localScale = Vector3.one * 0.50f;
            }

            // Cerrar el telón (Abiertas -> Entreabiertas -> Cerradas)
            if (curtainRenderer != null && curtainHalfSprite != null)
            {
                curtainRenderer.sprite = curtainHalfSprite;
                yield return new WaitForSeconds(0.20f);
            }
            if (curtainRenderer != null && curtainClosedSprite != null)
            {
                curtainRenderer.sprite = curtainClosedSprite;
                yield return new WaitForSeconds(0.35f);
            }

            CurtainStageManager.ResetAllStaticProgress();
            SceneManager.LoadScene("Stage1_Pueblo");
        }

        private void AutoFindSpritesIfMissing()
        {
            // Resolución universal con TheaterSpriteLibrary (funciona en Editor y en PC Build)
            if (curtainClosedSprite == null) curtainClosedSprite = TheaterSpriteLibrary.Get("Cortinas Rojas Cerradas");
            if (curtainHalfSprite == null) curtainHalfSprite = TheaterSpriteLibrary.Get("Cortinas Rojas Entreabiertas");
            if (curtainOpenSprite == null) curtainOpenSprite = TheaterSpriteLibrary.Get("Cortinas Rojas Abiertas");

            if (stickSprite == null) stickSprite = TheaterSpriteLibrary.Get("Herramientas y Ataques_0");
            if (soundOnSprite == null) soundOnSprite = TheaterSpriteLibrary.Get("Hud Opciones UnCuentoMas_2");
            if (soundOffSprite == null) soundOffSprite = TheaterSpriteLibrary.Get("Hud Opciones UnCuentoMas_3");
            if (exitButtonSprite == null) exitButtonSprite = TheaterSpriteLibrary.Get("Hud Opciones UnCuentoMas_4");
            if (threadSprite == null) threadSprite = TheaterSpriteLibrary.Get("Corazones y etiqueta de papel_3");

#if UNITY_EDITOR
            if (curtainClosedSprite == null || curtainHalfSprite == null || curtainOpenSprite == null)
            {
                curtainClosedSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Sprites/Cortinas Rojas Cerradas.png");
                curtainHalfSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Sprites/Cortinas Rojas Entreabiertas.png");
                curtainOpenSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Sprites/Cortinas Rojas Abiertas.png");
            }

            if (soundOnSprite == null || soundOffSprite == null || exitButtonSprite == null)
            {
                var sprites = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Sprites/Hud Opciones UnCuentoMas.png");
                foreach (var s in sprites)
                {
                    if (s is Sprite sp)
                    {
                        if (sp.name == "Hud Opciones UnCuentoMas_2") soundOnSprite = sp;
                        if (sp.name == "Hud Opciones UnCuentoMas_3") soundOffSprite = sp;
                        if (sp.name == "Hud Opciones UnCuentoMas_4") exitButtonSprite = sp;
                    }
                }
            }

            if (stickSprite == null)
            {
                var toolSprites = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Sprites/Herramientas y Ataques.png");
                foreach (var s in toolSprites)
                {
                    if (s is Sprite sp && sp.name == "Herramientas y Ataques_0") { stickSprite = sp; break; }
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
        }

        private void EnsureStickAndThreadExist()
        {
            // Ajustar posición del botón de jugar al medio (X = 0)
            if (firstPlayButtonRoot != null && Mathf.Abs(firstPlayButtonRoot.localPosition.x) > 0.1f)
            {
                Vector3 p = firstPlayButtonRoot.localPosition;
                p.x = 0f;
                firstPlayButtonRoot.localPosition = p;
            }

            // 1. Palito de soporte para el botón de jugar en el centro
            if (firstPlayButtonRoot != null)
            {
                Transform existingStick = firstPlayButtonRoot.Find("Palito_Stick");
                if (existingStick != null)
                {
                    firstPlayButtonStickRenderer = existingStick.GetComponent<SpriteRenderer>();
                    existingStick.localPosition = new Vector3(0f, 0.20f, 0f);
                    existingStick.localScale = new Vector3(1.1f, 3.5f, 1f);
                    if (firstPlayButtonStickRenderer != null)
                    {
                        if (firstPlayButtonStickRenderer.sprite == null) firstPlayButtonStickRenderer.sprite = stickSprite;
                        firstPlayButtonStickRenderer.sortingOrder = 51;
                    }
                }
                else
                {
                    GameObject stickGO = new GameObject("Palito_Stick");
                    stickGO.transform.SetParent(firstPlayButtonRoot, false);
                    stickGO.transform.localPosition = new Vector3(0f, 0.20f, 0f);
                    stickGO.transform.localScale = new Vector3(1.1f, 3.5f, 1f);
                    firstPlayButtonStickRenderer = stickGO.AddComponent<SpriteRenderer>();
                    firstPlayButtonStickRenderer.sprite = stickSprite;
                    firstPlayButtonStickRenderer.sortingOrder = 51;
                }
            }

            // 2. Botón de Salir del Menú colgado de un hilo justo al lado del botón de sonido (X = 5.05, Y = 3.65)
            if (exitButtonRoot == null)
            {
                Transform foundExit = GameObject.Find("Btn_Salir_Menu")?.transform;
                if (foundExit == null) foundExit = transform.Find("Btn_Salir_Menu");
                if (foundExit != null) exitButtonRoot = foundExit;
                else
                {
                    GameObject exitGO = new GameObject("Btn_Salir_Menu");
                    exitGO.transform.position = new Vector3(5.05f, 3.65f, 0f);
                    exitGO.transform.localScale = Vector3.one * 0.45f;
                    exitButtonRenderer = exitGO.AddComponent<SpriteRenderer>();
                    exitButtonRenderer.sprite = exitButtonSprite;
                    exitButtonRenderer.sortingOrder = 52;
                    exitButtonCollider = exitGO.AddComponent<BoxCollider2D>();
                    ((BoxCollider2D)exitButtonCollider).size = new Vector2(1.6f, 1.6f);
                    exitButtonCollider.isTrigger = true;
                    exitButtonRoot = exitGO.transform;
                }
            }

            if (exitButtonRoot != null)
            {
                exitButtonRoot.position = new Vector3(5.05f, 3.65f, 0f);
                exitButtonRoot.localScale = Vector3.one * 0.45f;
                _exitInitialPos = exitButtonRoot.position;

                if (exitButtonRenderer == null) exitButtonRenderer = exitButtonRoot.GetComponent<SpriteRenderer>();
                if (exitButtonCollider == null) exitButtonCollider = exitButtonRoot.GetComponent<Collider2D>();
                if (exitButtonRenderer != null)
                {
                    if (exitButtonRenderer.sprite == null) exitButtonRenderer.sprite = exitButtonSprite;
                    exitButtonRenderer.sortingOrder = 52;
                }

                if (exitButtonCollider is BoxCollider2D boxCol)
                {
                    boxCol.size = new Vector2(1.6f, 1.6f);
                    boxCol.isTrigger = true;
                }

                // Si tenía palito anteriormente, se elimina para que ahora cuelgue de un hilo como el botón de sonido
                Transform oldStick = exitButtonRoot.Find("Palito_Stick");
                if (oldStick != null)
                {
                    if (Application.isPlaying) Destroy(oldStick.gameObject);
                    else DestroyImmediate(oldStick.gameObject);
                }

                // Hilo de soporte para el botón de salir
                Transform existingThread = exitButtonRoot.Find("Hilo_Soporte");
                if (existingThread != null)
                {
                    exitButtonThreadRenderer = existingThread.GetComponent<SpriteRenderer>();
                    existingThread.localPosition = new Vector3(0f, 0.20f, 0f);
                    existingThread.localScale = new Vector3(1.1f, 3.2f, 1f);
                    existingThread.localRotation = Quaternion.identity;
                    if (exitButtonThreadRenderer != null)
                    {
                        if (exitButtonThreadRenderer.sprite == null) exitButtonThreadRenderer.sprite = threadSprite;
                        exitButtonThreadRenderer.sortingOrder = 51;
                    }
                }
                else
                {
                    GameObject threadGO = new GameObject("Hilo_Soporte");
                    threadGO.transform.SetParent(exitButtonRoot, false);
                    threadGO.transform.localPosition = new Vector3(0f, 0.20f, 0f);
                    threadGO.transform.localScale = new Vector3(1.1f, 3.2f, 1f);
                    exitButtonThreadRenderer = threadGO.AddComponent<SpriteRenderer>();
                    exitButtonThreadRenderer.sprite = threadSprite;
                    exitButtonThreadRenderer.sortingOrder = 51;
                }
            }

            // 3. Palito de soporte para el botón de jugar del capítulo (Nivel 1)
            if (level1PlayButtonRoot != null)
            {
                Transform existingStick = level1PlayButtonRoot.Find("Palito_Stick");
                if (existingStick != null)
                {
                    var sr = existingStick.GetComponent<SpriteRenderer>();
                    existingStick.localPosition = new Vector3(0f, 0.20f, 0f);
                    existingStick.localScale = new Vector3(1.1f, 3.5f, 1f);
                    if (sr != null)
                    {
                        if (sr.sprite == null) sr.sprite = stickSprite;
                        sr.sortingOrder = 31;
                    }
                }
                else
                {
                    GameObject stickGO = new GameObject("Palito_Stick");
                    stickGO.transform.SetParent(level1PlayButtonRoot, false);
                    stickGO.transform.localPosition = new Vector3(0f, 0.20f, 0f);
                    stickGO.transform.localScale = new Vector3(1.1f, 3.5f, 1f);
                    var sr = stickGO.AddComponent<SpriteRenderer>();
                    sr.sprite = stickSprite;
                    sr.sortingOrder = 31;
                }
            }

            // 4. Hilo de soporte para el botón de sonido en el menú principal (X = 4.25f, Y = 3.65f)
            if (soundRenderer != null)
            {
                soundRenderer.transform.position = new Vector3(4.25f, 3.65f, 0f);
                soundRenderer.transform.localScale = Vector3.one * 0.45f;
                _soundInitialPos = soundRenderer.transform.position;

                Transform existingThread = soundRenderer.transform.Find("Hilo_Soporte");
                if (existingThread != null)
                {
                    soundThreadRenderer = existingThread.GetComponent<SpriteRenderer>();
                    existingThread.localPosition = new Vector3(0f, 0.20f, 0f);
                    existingThread.localScale = new Vector3(1.1f, 3.2f, 1f);
                    existingThread.localRotation = Quaternion.identity;
                    if (soundThreadRenderer != null) soundThreadRenderer.sortingOrder = 51;
                }
                else
                {
                    GameObject threadGO = new GameObject("Hilo_Soporte");
                    threadGO.transform.SetParent(soundRenderer.transform, false);
                    threadGO.transform.localPosition = new Vector3(0f, 0.20f, 0f);
                    threadGO.transform.localScale = new Vector3(1.1f, 3.2f, 1f);
                    soundThreadRenderer = threadGO.AddComponent<SpriteRenderer>();
                    soundThreadRenderer.sprite = threadSprite;
                    soundThreadRenderer.sortingOrder = 51;
                }
            }
        }
    }
}
