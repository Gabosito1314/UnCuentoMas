using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace UnCuentoMas
{
    /// <summary>
    /// HUD superior derecho con estilo teatral de títeres:
    /// - Botón de sonido (conmuta entre 'Hud Opciones UnCuentoMas_2' activado y '_3' desactivado).
    /// - Botón de salir (icono de puerta '_4' para volver al Menú Principal).
    /// - Botón de Menú Final ('Boton Menu Final_0') que aparece al terminar cualquiera de los 2 finales.
    /// </summary>
    public class StageTopRightHUD : MonoBehaviour
    {
        public static bool IsSoundMuted = false;

        [Header("=== 1. SPRITES DEL HUD DE OPCIONES ===")]
        [SerializeField] private Sprite soundOnSprite;
        [SerializeField] private Sprite soundOffSprite;
        [SerializeField] private Sprite exitButtonSprite;
        [SerializeField] private Sprite finalMenuButtonSprite;
        [SerializeField] private Sprite threadSprite;

        [Header("=== 2. REFERENCIAS A RENDERERS ===")]
        [SerializeField] private SpriteRenderer soundRenderer;
        [SerializeField] private SpriteRenderer exitRenderer;
        [SerializeField] private SpriteRenderer finalMenuRenderer;
        [SerializeField] private SpriteRenderer soundThreadRenderer;
        [SerializeField] private SpriteRenderer exitThreadRenderer;
        [SerializeField] private Collider2D soundCollider;
        [SerializeField] private Collider2D exitCollider;
        [SerializeField] private Collider2D finalMenuCollider;

        private Camera _mainCamera;
        private bool _isFinalButtonVisible = false;
        private Vector3 _soundInitialPos = new Vector3(4.25f, 3.65f, 0f);
        private Vector3 _exitInitialPos = new Vector3(5.05f, 3.65f, 0f);

        private void Awake()
        {
            _mainCamera = Camera.main;
            AutoFindSpritesIfMissing();
            EnsureThreadsExist();
            UpdateSoundVisuals();

            if (finalMenuRenderer != null)
            {
                finalMenuRenderer.gameObject.SetActive(false);
            }
        }

        private void Start()
        {
            if (_mainCamera == null) _mainCamera = Camera.main;
            EnsureThreadsExist();
            UpdateSoundVisuals();
        }

        private void Update()
        {
            // Seguridad: asegurar que nunca queden en el centro (X = 0) ni solapen con el telón
            if (_soundInitialPos.x < 2.0f || _soundInitialPos.x > 5.6f) _soundInitialPos = new Vector3(4.25f, 3.65f, 0f);
            if (_exitInitialPos.x < 2.0f || _exitInitialPos.x > 5.6f) _exitInitialPos = new Vector3(5.05f, 3.65f, 0f);

            // Animación armónica de títeres suspendidos por hilo (idéntica a PuppetHeartHUD)
            if (soundRenderer != null)
            {
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

            if (exitRenderer != null)
            {
                float exitPhase = Time.time * 2.1f + 1.6f;
                float exitSway = Mathf.Sin(exitPhase) * 2.8f;
                float exitBob = Mathf.Cos(exitPhase * 0.8f) * 0.025f;
                exitRenderer.transform.position = _exitInitialPos + new Vector3(0f, exitBob, 0f);
                exitRenderer.transform.rotation = Quaternion.Euler(0f, 0f, exitSway);
                if (exitThreadRenderer != null)
                {
                    exitThreadRenderer.transform.localPosition = new Vector3(0f, 0.20f, 0f);
                    exitThreadRenderer.transform.localRotation = Quaternion.identity;
                }
            }

            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_mainCamera == null) return;

            Vector3 mousePos = _mainCamera.ScreenToWorldPoint(mouse.position.ReadValue());
            Vector2 mousePos2D = new Vector2(mousePos.x, mousePos.y);

            // 1. Clic en Botón de Menú Final (si está activo sobre el telón cerrado)
            if (_isFinalButtonVisible && finalMenuCollider != null && finalMenuCollider.OverlapPoint(mousePos2D))
            {
                StartCoroutine(PunchScaleAndExecuteRoutine(finalMenuRenderer.transform, () =>
                {
                    CurtainStageManager.ResetAllStaticProgress();
                    SceneManager.LoadScene("Stage0_MainMenu");
                }));
                return;
            }

            // 2. Clic en Botón de Sonido (conmutar ON/OFF)
            if (soundCollider != null && soundCollider.OverlapPoint(mousePos2D))
            {
                StartCoroutine(PunchScaleAndExecuteRoutine(soundRenderer.transform, () =>
                {
                    ToggleSound();
                }));
                return;
            }

            // 3. Clic en Botón de Salir (volver al Menú Principal)
            if (exitCollider != null && exitCollider.OverlapPoint(mousePos2D))
            {
                StartCoroutine(PunchScaleAndExecuteRoutine(exitRenderer.transform, () =>
                {
                    CurtainStageManager.ResetAllStaticProgress();
                    SceneManager.LoadScene("Stage0_MainMenu");
                }));
                return;
            }
        }

        public void ToggleSound()
        {
            IsSoundMuted = !IsSoundMuted;
            AudioListener.pause = IsSoundMuted;
            AudioListener.volume = IsSoundMuted ? 0f : 1f;
            AudioManager.Instance?.SyncWithMuteState();
            UpdateSoundVisuals();
        }

        private void UpdateSoundVisuals()
        {
            if (soundRenderer == null) return;
            soundRenderer.sprite = IsSoundMuted ? soundOffSprite : soundOnSprite;
        }

        private Vector3 _finalMenuBasePos = new Vector3(0f, -3.20f, 0f);

        /// <summary>
        /// Muestra el botón de regreso al menú principal con una animación teatral al cerrar el telón en los finales.
        /// Se ubica en la parte inferior para no superponerse con el cartel de epílogo.
        /// </summary>
        public void ShowFinalMenuButton(Vector3? customPosition = null)
        {
            if (finalMenuRenderer == null) return;

            if (finalMenuRenderer.sprite == null)
            {
                if (finalMenuButtonSprite == null) finalMenuButtonSprite = TheaterSpriteLibrary.Get("Boton Menu Final_0");
                finalMenuRenderer.sprite = finalMenuButtonSprite;
            }

            if (customPosition.HasValue)
            {
                _finalMenuBasePos = customPosition.Value;
            }
            else
            {
                _finalMenuBasePos = new Vector3(0f, -3.20f, 0f);
            }

            _isFinalButtonVisible = true;
            finalMenuRenderer.gameObject.SetActive(true);
            finalMenuRenderer.sortingOrder = 58; // Por delante del telón cerrado (50) y epílogo (55)
            TheaterCursorManager.SetCursorVisible(true);
            StartCoroutine(ShowFinalButtonAnimationRoutine());
        }

        private IEnumerator ShowFinalButtonAnimationRoutine()
        {
            Transform t = finalMenuRenderer.transform;
            Vector3 targetScale = Vector3.one * 0.78f;
            t.localScale = Vector3.zero;
            t.localPosition = _finalMenuBasePos;

            float elapsed = 0f;
            float duration = 0.55f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;
                // Efecto de rebote elástico teatral
                float overshoot = Mathf.Sin(progress * Mathf.PI * 0.5f);
                float bounce = 1f + Mathf.Sin(progress * Mathf.PI * 3f) * 0.15f * (1f - progress);
                t.localScale = targetScale * (overshoot * bounce);
                yield return null;
            }
            t.localScale = targetScale;

            // Suave flotación continua
            while (_isFinalButtonVisible)
            {
                float bob = Mathf.Sin(Time.time * 2.8f) * 0.04f;
                t.localPosition = _finalMenuBasePos + new Vector3(0f, bob, 0f);

                var kb = Keyboard.current;
                if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.rKey.wasPressedThisFrame))
                {
                    CurtainStageManager.ResetAllStaticProgress();
                    SceneManager.LoadScene("Stage0_MainMenu");
                    yield break;
                }

                yield return null;
            }
        }

        private IEnumerator PunchScaleAndExecuteRoutine(Transform target, System.Action onComplete)
        {
            if (target == null)
            {
                onComplete?.Invoke();
                yield break;
            }

            Vector3 orig = target.localScale;
            target.localScale = orig * 0.85f;
            yield return new WaitForSecondsRealtime(0.08f);
            target.localScale = orig * 1.05f;
            yield return new WaitForSecondsRealtime(0.06f);
            target.localScale = orig;

            onComplete?.Invoke();
        }

        private void AutoFindSpritesIfMissing()
        {
            // Resolución universal con TheaterSpriteLibrary (funciona tanto en PC Standalone como en Editor)
            if (soundOnSprite == null) soundOnSprite = TheaterSpriteLibrary.Get("Hud Opciones UnCuentoMas_2");
            if (soundOffSprite == null) soundOffSprite = TheaterSpriteLibrary.Get("Hud Opciones UnCuentoMas_3");
            if (exitButtonSprite == null) exitButtonSprite = TheaterSpriteLibrary.Get("Hud Opciones UnCuentoMas_4");
            if (finalMenuButtonSprite == null) finalMenuButtonSprite = TheaterSpriteLibrary.Get("Boton Menu Final_0");
            if (threadSprite == null) threadSprite = TheaterSpriteLibrary.Get("Corazones y etiqueta de papel_3");

#if UNITY_EDITOR
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

            if (finalMenuButtonSprite == null)
            {
                var sprites = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Sprites/Boton Menu Final.png");
                foreach (var s in sprites)
                {
                    if (s is Sprite sp && sp.name == "Boton Menu Final_0")
                    {
                        finalMenuButtonSprite = sp;
                        break;
                    }
                }
            }

            if (threadSprite == null)
            {
                var sprites = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Sprites/Corazones y etiqueta de papel.png");
                foreach (var s in sprites)
                {
                    if (s is Sprite sp && sp.name == "Corazones y etiqueta de papel_3")
                    {
                        threadSprite = sp;
                        break;
                    }
                }
            }
#endif
        }

        private void EnsureThreadsExist()
        {
            // Ajustar posición firmemente en la esquina superior derecha
            if (soundRenderer != null)
            {
                if (Mathf.Abs(soundRenderer.transform.position.x) < 2.0f || soundRenderer.transform.position.x > 5.6f)
                {
                    soundRenderer.transform.position = new Vector3(4.25f, 3.65f, 0f);
                }
                _soundInitialPos = soundRenderer.transform.position;

                Transform existingThread = soundRenderer.transform.Find("Hilo_Soporte");
                if (existingThread != null)
                {
                    soundThreadRenderer = existingThread.GetComponent<SpriteRenderer>();
                    existingThread.localPosition = new Vector3(0f, 0.20f, 0f);
                    existingThread.localScale = new Vector3(1.1f, 3.2f, 1f);
                    existingThread.localRotation = Quaternion.identity;
                }
                else
                {
                    GameObject threadGO = new GameObject("Hilo_Soporte");
                    threadGO.transform.SetParent(soundRenderer.transform, false);
                    threadGO.transform.localPosition = new Vector3(0f, 0.20f, 0f);
                    threadGO.transform.localScale = new Vector3(1.1f, 3.2f, 1f);
                    soundThreadRenderer = threadGO.AddComponent<SpriteRenderer>();
                    soundThreadRenderer.sprite = threadSprite;
                    soundThreadRenderer.sortingOrder = 47;
                }
            }

            if (exitRenderer != null)
            {
                if (Mathf.Abs(exitRenderer.transform.position.x) < 2.0f || exitRenderer.transform.position.x > 5.6f)
                {
                    exitRenderer.transform.position = new Vector3(5.05f, 3.65f, 0f);
                }
                _exitInitialPos = exitRenderer.transform.position;

                Transform existingThread = exitRenderer.transform.Find("Hilo_Soporte");
                if (existingThread != null)
                {
                    exitThreadRenderer = existingThread.GetComponent<SpriteRenderer>();
                    existingThread.localPosition = new Vector3(0f, 0.20f, 0f);
                    existingThread.localScale = new Vector3(1.1f, 3.2f, 1f);
                    existingThread.localRotation = Quaternion.identity;
                }
                else
                {
                    GameObject threadGO = new GameObject("Hilo_Soporte");
                    threadGO.transform.SetParent(exitRenderer.transform, false);
                    threadGO.transform.localPosition = new Vector3(0f, 0.20f, 0f);
                    threadGO.transform.localScale = new Vector3(1.1f, 3.2f, 1f);
                    exitThreadRenderer = threadGO.AddComponent<SpriteRenderer>();
                    exitThreadRenderer.sprite = threadSprite;
                    exitThreadRenderer.sortingOrder = 47;
                }
            }
        }
    }
}
