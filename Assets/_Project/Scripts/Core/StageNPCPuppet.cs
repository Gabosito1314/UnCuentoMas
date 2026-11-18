using UnityEngine;

namespace UnCuentoMas
{
    /// <summary>
    /// Controlador de habitantes en palito ("Aldeanos y Rey Inocencia y Traicion" y "Campesinos Inocencia y Traicion"):
    /// - Siguen con la mirada al Player en todo momento haciendo flipX automáticamente ("Something's Off").
    /// - Permite cambiar entre su Frame 0 (Inocencia/Amistosos) y Frame 1 (Traición/Enojados) y caminar en cinemáticas.
    /// </summary>
    [RequireComponent(typeof(PuppetWobble))]
    public class StageNPCPuppet : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer npcRenderer;
        [SerializeField] private Sprite[] npcFrames = new Sprite[2];
        [SerializeField] private bool followPlayerGaze = true;
        [Tooltip("En los PNG originales, Aldeanos y Rey y Campesinos miran hacia la IZQUIERDA cuando flipX es false.")]
        [SerializeField] private bool rawSpriteFacesLeft = true;

        private PuppetWobble _wobble;
        private PlayerMagoController _player;

        public SpriteRenderer NpcRenderer => npcRenderer;
        public PuppetWobble Wobble => _wobble;

        /// <summary>
        /// Inicialización: obtiene el componente de balanceo PuppetWobble y busca el SpriteRenderer.
        /// </summary>
        private void Awake()
        {
            _wobble = GetComponent<PuppetWobble>();
            if (npcRenderer == null)
            {
                Transform vp = transform.Find("VisualPivot");
                if (vp != null) npcRenderer = vp.GetComponent<SpriteRenderer>();
                if (npcRenderer == null) npcRenderer = GetComponentInChildren<SpriteRenderer>();
            }
            _player = FindFirstObjectByType<PlayerMagoController>();
        }

        private void OnEnable()
        {
            UpdateGazeTowardsPlayer(immediate: true);
        }

        private void Update()
        {
            UpdateGazeTowardsPlayer(immediate: false);
        }

        /// <summary>
        /// Seguimiento dinámico de la mirada hacia el Player:
        /// Calcula la diferencia vectorial en X (dx = player.x - npc.x).
        /// Si el Mago pasa de un lado a otro, activa o desactiva SpriteRenderer.flipX.
        /// Al voltear la cabeza, dispara un micro-squash de papel (_wobble.TriggerPaperSquashStretch)
        /// simulando que el recorte de cartón gira sobre su palito.
        /// </summary>
        private void UpdateGazeTowardsPlayer(bool immediate)
        {
            if (!followPlayerGaze || npcRenderer == null) return;
            if (_player == null) _player = FindFirstObjectByType<PlayerMagoController>();
            if (_player == null) return;

            float dx = _player.transform.position.x - transform.position.x;
            if (Mathf.Abs(dx) > 0.05f)
            {
                bool playerIsToRight = dx > 0f;
                // En la hoja de sprites original los personajes miran a la izquierda de forma nativa
                bool desiredFlipX = rawSpriteFacesLeft ? playerIsToRight : !playerIsToRight;

                if (npcRenderer.flipX != desiredFlipX)
                {
                    npcRenderer.flipX = desiredFlipX;
                    if (!immediate && _wobble != null)
                    {
                        // Micro-amortiguación de papel al cambiar bruscamente de orientación
                        _wobble.TriggerPaperSquashStretch(0.92f, 1.06f);
                    }
                }
            }
        }

        /// <summary>
        /// Cambia el fotograma del títere:
        /// Frame 0 = Amistoso / Inocencia (Día 2 inicial pidiendo auxilio)
        /// Frame 1 = Hostil / Traición (Cinemática del Flashback y emboscada del Final B)
        /// </summary>
        public void SetFrame(int index)
        {
            if (npcRenderer == null || npcFrames == null || npcFrames.Length == 0) return;
            int clamped = Mathf.Clamp(index, 0, npcFrames.Length - 1);
            if (npcFrames[clamped] != null)
            {
                npcRenderer.sprite = npcFrames[clamped];
            }
        }

        /// <summary>
        /// Activa o pausa el seguimiento ocular hacia el jugador (usado durante cinemáticas coreografiadas).
        /// </summary>
        public void SetFollowPlayerGaze(bool follow)
        {
            followPlayerGaze = follow;
            if (follow)
            {
                UpdateGazeTowardsPlayer(immediate: true);
            }
        }

        /// <summary>
        /// Fuerza una orientación visual manual hacia una dirección determinada (+1 derecha, -1 izquierda).
        /// </summary>
        public void FaceDirection(float horizontalDir)
        {
            if (npcRenderer == null || Mathf.Abs(horizontalDir) < 0.01f) return;
            bool lookRight = horizontalDir > 0f;
            npcRenderer.flipX = rawSpriteFacesLeft ? lookRight : !lookRight;
        }
    }
}
