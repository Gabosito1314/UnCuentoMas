using System.Collections;
using UnityEngine;

namespace UnCuentoMas
{
    /// <summary>
    /// Gestor de Oleadas para el Stage 2 (Bosque) - Sección 2:
    /// 
    /// ARQUITECTURA Y DISEÑO NARRATIVO-TEATRAL:
    /// En el teatro de títeres de papel de "Un cuento más", los personajes e infraestructuras
    /// no aparecen de la nada; suben desde la parte inferior del retablo sostenidos por sus palillos de madera.
    /// 
    /// SECUENCIA DE COMBATE:
    /// 1. Oleada 1 (Inicial):
    ///    - 1 Torre a la derecha ("Torres Medievales 2_0") con su Arquero en la cima.
    ///    - 1 Soldado enemigo terrestre en el suelo.
    /// 2. Detección de derrota de la Oleada 1:
    ///    - Al ser vencidos ambos enemigos, se activa la Oleada 2 tras una breve pausa dramática de 0.6 segundos.
    /// 3. Oleada 2 (Refuerzos desde abajo):
    ///    - Emerge desde abajo del escenario un segundo Soldado enemigo terrestre.
    ///    - Emerge desde abajo del escenario en el costado IZQUIERDO una segunda Torre ("Torres Medievales 2_0")
    ///      con su respectivo Arquero.
    /// 4. Bloqueo de avance:
    ///    - El telón no permitirá avanzar a la Cueva del Dragón (Stage 3) hasta que la Oleada 2 sea también derrotada.
    /// </summary>
    public class Stage2ForestWaveManager : MonoBehaviour
    {
        [Header("=== OLEADA 1 (INICIAL) ===")]
        [Tooltip("Arquero ubicado en la torre derecha al comenzar la Sección 2.")]
        [SerializeField] private EnemyController initialArcher;

        [Tooltip("Soldado terrestre presente al comenzar la Sección 2.")]
        [SerializeField] private EnemyController initialSoldier;

        [Header("=== OLEADA 2 (EMERGE DESDE ABAJO) ===")]
        [Tooltip("Soldado terrestre de refuerzo que sube desde el suelo.")]
        [SerializeField] private EnemyController wave2Soldier;

        [Tooltip("Estructura de la torre izquierda que sube desde abajo.")]
        [SerializeField] private GameObject wave2TowerRoot;

        [Tooltip("Arquero montado en la torre izquierda que sube junto con ella.")]
        [SerializeField] private EnemyController wave2Archer;

        [Header("=== POSICIONES OBJETIVO DE LA OLEADA 2 ===")]
        [SerializeField] private Vector3 wave2SoldierTargetPos = new Vector3(0.0f, -2.75f, 0f);
        [SerializeField] private Vector3 wave2TowerTargetPos = new Vector3(-4.3f, -2.75f, 0f);
        [SerializeField] private Vector3 wave2ArcherTargetPos = new Vector3(-4.3f, 0.45f, 0f);

        private bool _wave2Triggered = false;
        private bool _wave2SpawnComplete = false;
        private CurtainStageManager _curtainManager;

        /// <summary>
        /// Awake: Desactiva los componentes de la Oleada 2 para que no aparezcan en escena
        /// hasta que el jugador haya eliminado a la primera oleada de enemigos.
        /// </summary>
        private void Awake()
        {
            _curtainManager = FindFirstObjectByType<CurtainStageManager>();

            if (wave2Soldier != null) wave2Soldier.gameObject.SetActive(false);
            if (wave2TowerRoot != null) wave2TowerRoot.SetActive(false);
            if (wave2Archer != null) wave2Archer.gameObject.SetActive(false);
        }

        /// <summary>
        /// Update: Monitorea activamente el estado de los enemigos iniciales en la Sección 2.
        /// Cuando ambos mueren, dispara la entrada de la Oleada 2.
        /// </summary>
        private void Update()
        {
            if (_wave2Triggered) return;

            // Solo evaluar si la cámara/telón está en la Sección 2 (índice 1 del array de subsecciones)
            if (_curtainManager != null && _curtainManager.CurrentSubSectionIndex != 1) return;

            bool archerDead = initialArcher == null || !initialArcher.gameObject.activeInHierarchy || initialArcher.IsDefeated;
            bool soldierDead = initialSoldier == null || !initialSoldier.gameObject.activeInHierarchy || initialSoldier.IsDefeated;

            if (archerDead && soldierDead)
            {
                _wave2Triggered = true;
                StartCoroutine(SpawnWave2Routine());
            }
        }

        /// <summary>
        /// Corrutina de aparición teatral de la Oleada 2:
        /// 1. Espera 2.5 segundos tras la caída de la primera oleada (evitando que aparezcan de golpe
        ///    y dando tiempo a que los enemigos derrotados completen sus 2 segundos de Frame 2 y bajen).
        /// 2. Posiciona al soldado, la torre izquierda y su arquero debajo del escenario (offset Y = -4.8f).
        /// 3. Activa sus GameObjects y los eleva suavemente (Mathf.SmoothStep) durante 1.35s simulando que el titiritero
        ///    los introduce empujando sus palitos hacia arriba desde el foso.
        /// 4. Habilita el combate para la segunda oleada tras finalizar la elevación.
        /// </summary>
        private IEnumerator SpawnWave2Routine()
        {
            // Pausa teatral de 2.5 segundos para que no aparezcan de golpe
            yield return new WaitForSeconds(2.5f);

            float depthOffset = -4.8f;
            Vector3 sStart = new Vector3(wave2SoldierTargetPos.x, wave2SoldierTargetPos.y + depthOffset, wave2SoldierTargetPos.z);
            Vector3 tStart = new Vector3(wave2TowerTargetPos.x, wave2TowerTargetPos.y + depthOffset, wave2TowerTargetPos.z);
            Vector3 aStart = new Vector3(wave2ArcherTargetPos.x, wave2ArcherTargetPos.y + depthOffset, wave2ArcherTargetPos.z);

            float riseDuration = 1.35f;

            if (wave2Soldier != null)
            {
                wave2Soldier.transform.localPosition = sStart;
                wave2Soldier.gameObject.SetActive(true);
                wave2Soldier.ApplyStun(riseDuration + 0.2f);
            }
            if (wave2TowerRoot != null)
            {
                wave2TowerRoot.transform.localPosition = tStart;
                wave2TowerRoot.SetActive(true);
            }
            if (wave2Archer != null)
            {
                wave2Archer.transform.localPosition = aStart;
                wave2Archer.gameObject.SetActive(true);
                wave2Archer.ApplyStun(riseDuration + 0.2f);
            }

            float elapsed = 0f;

            while (elapsed < riseDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / riseDuration);

                if (wave2Soldier != null)
                {
                    wave2Soldier.transform.localPosition = Vector3.Lerp(sStart, wave2SoldierTargetPos, t);
                }
                if (wave2TowerRoot != null)
                {
                    wave2TowerRoot.transform.localPosition = Vector3.Lerp(tStart, wave2TowerTargetPos, t);
                }
                if (wave2Archer != null)
                {
                    wave2Archer.transform.localPosition = Vector3.Lerp(aStart, wave2ArcherTargetPos, t);
                }

                yield return null;
            }

            // Fijar posiciones finales exactas
            if (wave2Soldier != null) wave2Soldier.transform.localPosition = wave2SoldierTargetPos;
            if (wave2TowerRoot != null) wave2TowerRoot.transform.localPosition = wave2TowerTargetPos;
            if (wave2Archer != null) wave2Archer.transform.localPosition = wave2ArcherTargetPos;

            _wave2SpawnComplete = true;
        }

        /// <summary>
        /// Comprueba si la sección 2 ha sido completamente superada (ambas oleadas derrotadas).
        /// Llamado por CurtainStageManager para determinar si el Mago puede cruzar el telón.
        /// </summary>
        /// <param name="sectionIndex">Índice de la subsección actual.</param>
        /// <returns>True si la sección está despejada; False si aún quedan enemigos pendientes.</returns>
        public bool IsSectionComplete(int sectionIndex)
        {
            // La sección 1 (índice 0) se evalúa independientemente con sus propios enemigos
            if (sectionIndex != 1) return true;

            // Si la segunda oleada ni siquiera ha terminado de aparecer, no se puede salir
            if (!_wave2SpawnComplete) return false;

            // Verificar si el soldado de la oleada 2 sigue con vida
            if (wave2Soldier != null && wave2Soldier.gameObject.activeInHierarchy && !wave2Soldier.IsDefeated)
            {
                return false;
            }

            // Verificar si el arquero de la oleada 2 sigue con vida
            if (wave2Archer != null && wave2Archer.gameObject.activeInHierarchy && !wave2Archer.IsDefeated)
            {
                return false;
            }

            return true;
        }
    }
}
