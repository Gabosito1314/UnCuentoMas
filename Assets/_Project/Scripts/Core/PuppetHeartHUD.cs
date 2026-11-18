using UnityEngine;

namespace UnCuentoMas
{
    /// <summary>
    /// HUD de 3 Corazones en la esquina superior izquierda ("Corazones y etiqueta de papel"):
    /// - Frame 0: Corazón que representa 1 vida del Mago.
    /// - Frame 3: Hilo que sostiene al corazón desde arriba (con la punta metida detrás del corazón).
    /// - Cuando el Mago pierde una vida: el corazón se desprende del hilo y cae por gravedad,
    ///   mientras el hilo sube hacia el techo como si el titiritero lo estuviera retirando.
    /// </summary>
    public class PuppetHeartHUD : MonoBehaviour
    {
        [System.Serializable]
        public class HeartSlot
        {
            public Transform slotRoot;
            public SpriteRenderer heartRenderer;
            public SpriteRenderer threadRenderer;
            public Vector3 heartInitialLocalPos;
            public Vector3 threadInitialLocalPos;
            [HideInInspector] public bool isDetached;
            [HideInInspector] public float fallVelocityY;
            [HideInInspector] public float fallDriftX;
            [HideInInspector] public float fallSpinSpeed;
        }

        [SerializeField] private HeartSlot[] heartSlots = new HeartSlot[3];
        [SerializeField] private PlayerMagoController playerMago;

        private void Awake()
        {
            if (playerMago == null)
            {
                playerMago = FindFirstObjectByType<PlayerMagoController>();
            }

            if (heartSlots != null)
            {
                for (int i = 0; i < heartSlots.Length; i++)
                {
                    var s = heartSlots[i];
                    if (s == null) continue;
                    
                    // GESTIÓN DE CAPAS EN UNITY 2D (Sorting Orders):
                    // El telón rojo en primer plano (Foreground) está en sortingOrder = 50.
                    // Para que los corazones cuelguen DENTRO del escenario y se oculten detrás del telón,
                    // asignamos sortingOrder = 48 a los corazones y 47 al hilo de soporte.
                    if (s.heartRenderer != null)
                    {
                        s.heartRenderer.sortingOrder = 48; // Detrás del marco del telón rojo (50)
                        s.heartInitialLocalPos = s.heartRenderer.transform.localPosition;
                    }
                    if (s.threadRenderer != null)
                    {
                        s.threadRenderer.sortingOrder = 47; // Detrás del corazón
                        s.threadInitialLocalPos = s.threadRenderer.transform.localPosition;
                    }
                }
            }
        }

        private void OnEnable()
        {
            if (playerMago == null) playerMago = FindFirstObjectByType<PlayerMagoController>();
            if (playerMago != null)
            {
                playerMago.OnHealthChanged.RemoveListener(HandleHealthChanged);
                playerMago.OnHealthChanged.AddListener(HandleHealthChanged);
            }
        }

        private void OnDisable()
        {
            if (playerMago != null)
            {
                playerMago.OnHealthChanged.RemoveListener(HandleHealthChanged);
            }
        }

        /// <summary>
        /// Bucle de animación física y oscilatoria del HUD:
        /// 1. Mientras el corazón esté intacto: se mece sutilmente como un adorno de papel colgado de un hilo.
        /// 2. Cuando el corazón se desprende: simula caída libre por gravedad (aceleración en Y) y rotación,
        ///    mientras el hilo sube rápidamente hacia arriba como si el titiritero tirara de él para retirarlo.
        /// </summary>
        private void Update()
        {
            if (heartSlots == null) return;

            for (int i = 0; i < heartSlots.Length; i++)
            {
                var s = heartSlots[i];
                if (s == null || s.slotRoot == null) continue;

                // Desfase armónico (phase) para que cada corazón se balancee con un ritmo ligeramente diferente
                float phase = Time.time * 2.1f + i * 1.15f;

                if (!s.isDetached)
                {
                    // Balanceo armónico del corazón colgado
                    float swayAngle = Mathf.Sin(phase) * 2.8f;
                    float bobY = Mathf.Cos(phase * 0.8f) * 0.025f;

                    if (s.heartRenderer != null)
                    {
                        s.heartRenderer.transform.localPosition = s.heartInitialLocalPos + new Vector3(0f, bobY, 0f);
                        s.heartRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, swayAngle);
                    }

                    if (s.threadRenderer != null)
                    {
                        s.threadRenderer.transform.localPosition = s.threadInitialLocalPos + new Vector3(0f, bobY, 0f);
                        s.threadRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, swayAngle * 0.45f);
                    }
                }
                else
                {
                    // 1) El corazón se desprende del hilo y cae hacia abajo por gravedad
                    if (s.heartRenderer != null && s.heartRenderer.enabled)
                    {
                        s.fallVelocityY -= 16.5f * Time.deltaTime; // Gravedad artificial constante
                        Transform ht = s.heartRenderer.transform;
                        ht.localPosition += new Vector3(s.fallDriftX * Time.deltaTime, s.fallVelocityY * Time.deltaTime, 0f);
                        ht.Rotate(0f, 0f, s.fallSpinSpeed * Time.deltaTime);

                        // Al caer por debajo de la parte inferior de la pantalla, desactivamos su renderizado
                        if (ht.position.y < -6.8f)
                        {
                            s.heartRenderer.enabled = false;
                        }
                    }

                    // 2) El hilo se va hacia arriba como si el titiritero lo estuviera retirando del escenario
                    if (s.threadRenderer != null && s.threadRenderer.enabled)
                    {
                        Transform tt = s.threadRenderer.transform;
                        tt.localPosition += new Vector3(0f, 4.8f * Time.deltaTime, 0f);
                        if (tt.localPosition.y > s.threadInitialLocalPos.y + 4.5f)
                        {
                            s.threadRenderer.enabled = false;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Recibe el cambio de salud del Mago.
        /// Si la salud bajó, desprende los corazones correspondientes.
        /// Si la salud se restauró por completo (ej. tras una caída), reinicia todos los corazones e hilos.
        /// </summary>
        public void HandleHealthChanged(int currentHealth, int maxHealth)
        {
            if (heartSlots == null) return;

            int clampedCurrent = Mathf.Clamp(currentHealth, 0, heartSlots.Length);

            if (clampedCurrent >= heartSlots.Length)
            {
                for (int i = 0; i < heartSlots.Length; i++)
                {
                    ResetHeartSlot(heartSlots[i]);
                }
                return;
            }

            for (int i = 0; i < heartSlots.Length; i++)
            {
                var s = heartSlots[i];
                if (s == null) continue;

                if (i >= clampedCurrent && !s.isDetached)
                {
                    DetachHeartAndRetractThread(s);
                }
            }
        }

        /// <summary>
        /// Configura la física de desprendimiento del corazón: impulso inicial hacia arriba y giro aleatorio.
        /// </summary>
        private void DetachHeartAndRetractThread(HeartSlot s)
        {
            s.isDetached = true;
            s.fallVelocityY = 1.6f; // Pequeño salto hacia arriba antes de caer
            s.fallDriftX = Random.Range(-0.65f, 0.65f);
            s.fallSpinSpeed = Random.Range(-150f, 150f);
            if (Mathf.Abs(s.fallSpinSpeed) < 50f) s.fallSpinSpeed = 95f;
        }

        /// <summary>
        /// Reinicia un corazón a su posición original colgado del hilo.
        /// </summary>
        private void ResetHeartSlot(HeartSlot s)
        {
            if (s == null) return;
            s.isDetached = false;
            s.fallVelocityY = 0f;

            if (s.heartRenderer != null)
            {
                s.heartRenderer.enabled = true;
                s.heartRenderer.transform.localPosition = s.heartInitialLocalPos;
                s.heartRenderer.transform.localRotation = Quaternion.identity;
            }

            if (s.threadRenderer != null)
            {
                s.threadRenderer.enabled = true;
                s.threadRenderer.transform.localPosition = s.threadInitialLocalPos;
                s.threadRenderer.transform.localRotation = Quaternion.identity;
            }
        }
    }
}
