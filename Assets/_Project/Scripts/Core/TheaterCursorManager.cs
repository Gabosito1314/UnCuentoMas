using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace UnCuentoMas
{
    /// <summary>
    /// Gestor del Cursor del Ratón (Mouse) para la Experiencia Teatral:
    /// - Al ingresar a Stage 1 (y durante todo el gameplay teatral), el cursor se oculta automáticamente
    ///   para mantener la inmersión total con los títeres y marionetas.
    /// - Al pulsar la tecla 'ESC', el cursor se vuelve visible (por ejemplo, para pausar o pulsar los botones del HUD).
    /// - Si se vuelve a pulsar la tecla 'ESC', el cursor se vuelve a ocultar.
    /// - En el Menú Principal (Stage 0), el cursor siempre permanece visible para permitir la navegación de botones.
    /// </summary>
    [DisallowMultipleComponent]
    public class TheaterCursorManager : MonoBehaviour
    {
        public static TheaterCursorManager Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null)
            {
                GameObject host = new GameObject("TheaterCursorManager");
                host.AddComponent<TheaterCursorManager>();
                DontDestroyOnLoad(host);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void Start()
        {
            ApplyCursorPolicyForScene(SceneManager.GetActiveScene().name);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyCursorPolicyForScene(scene.name);
        }

        /// <summary>
        /// Aplica la regla de visibilidad del cursor según la escena actual:
        /// - Stage0_MainMenu: Visible.
        /// - Stage1_Pueblo, Stage2_Bosque, Stage3_CuevaDragon, etc.: Oculto por defecto.
        /// </summary>
        public void ApplyCursorPolicyForScene(string sceneName)
        {
            if (string.Equals(sceneName, "Stage0_MainMenu", System.StringComparison.OrdinalIgnoreCase))
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
            else
            {
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.None;
            }
        }

        private void Update()
        {
            bool escPressed = false;

            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                escPressed = true;
            }

#if ENABLE_LEGACY_INPUT_MANAGER
            if (!escPressed && Input.GetKeyDown(KeyCode.Escape))
            {
                escPressed = true;
            }
#endif

            if (escPressed)
            {
                ToggleCursorVisibility();
            }
        }

        /// <summary>
        /// Conmuta la visibilidad del cursor entre visible e invisible.
        /// </summary>
        public void ToggleCursorVisibility()
        {
            Cursor.visible = !Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
        }

        /// <summary>
        /// Establece explícitamente si el cursor debe estar visible.
        /// </summary>
        public static void SetCursorVisible(bool visible)
        {
            Cursor.visible = visible;
            Cursor.lockState = CursorLockMode.None;
        }

        public static bool IsCursorVisible => Cursor.visible;
    }
}
