using UnityEngine;

namespace UnCuentoMas
{
    /// <summary>
    /// Muñeco de Entrenamiento ("Maniqui Intro" Frame 0 intacto -> Frame 1 dañado/chamuscado)
    /// colocado al lado de la casa del Mago en el Acto 1 - Día 1.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    [RequireComponent(typeof(PuppetWobble))]
    public class TrainingDummyPuppet : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer dummyRenderer;
        [SerializeField] private Sprite intactDummySprite;
        [SerializeField] private Sprite damagedDummySprite;
        [SerializeField] private bool isElevatedOnTower = false;

        private PuppetWobble _wobble;
        private bool _isDamaged;

        public bool IsDamaged => _isDamaged;
        public bool IsElevatedOnTower => isElevatedOnTower;

        /// <summary>
        /// Inicialización: obtiene el componente de bamboleo PuppetWobble y busca el SpriteRenderer.
        /// </summary>
        private void Awake()
        {
            _wobble = GetComponent<PuppetWobble>();
            if (dummyRenderer == null) dummyRenderer = GetComponentInChildren<SpriteRenderer>();
            ResetDummy();
        }

        public void SetElevated(bool elevated)
        {
            isElevatedOnTower = elevated;
        }

        public void ConfigureSprites(Sprite intact, Sprite damaged)
        {
            intactDummySprite = intact;
            damagedDummySprite = damaged;
            ResetDummy();
        }

        /// <summary>
        /// Restablece el muñeco a su estado intacto inicial (Frame 0).
        /// </summary>
        public void ResetDummy()
        {
            _isDamaged = false;
            if (dummyRenderer != null && intactDummySprite != null)
            {
                dummyRenderer.sprite = intactDummySprite;
            }
        }

        /// <summary>
        /// Procesa el impacto de un hechizo lanzado por el Mago:
        /// 1. Aplica retroceso físico y sacudida al palito según la potencia del hechizo (normal vs cargado).
        /// 2. Cambia el sprite al Frame 1 (dañado/chamuscado).
        /// 3. Notifica a StoryAndEndingManager para avanzar el paso correspondiente del tutorial.
        /// </summary>
        public bool TryHitWithSpell(bool isDiagonalShot = false, bool isCharged = false)
        {
            AudioManager.Instance?.PlayDamageDummy();

            if (_wobble != null)
            {
                _wobble.TriggerPaperSquashStretch(isCharged ? 1.25f : 1.15f, isCharged ? 0.78f : 0.86f);
                _wobble.TriggerHitShake(isCharged ? 0.40f : 0.30f, isCharged ? 16f : 12f);
            }

            if (!_isDamaged)
            {
                _isDamaged = true;
                if (dummyRenderer != null && damagedDummySprite != null)
                {
                    dummyRenderer.sprite = damagedDummySprite;
                }
            }

            StoryAndEndingManager story = FindFirstObjectByType<StoryAndEndingManager>();
            if (story != null)
            {
                story.OnTrainingDummyHit(this, isDiagonalShot, isCharged);
            }

            return true;
        }
    }
}
