using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnCuentoMas
{
    /// <summary>
    /// Gestor Central de Audio (Música de fondo y Efectos de sonido teatrales).
    /// - Persiste entre todas las escenas con DontDestroyOnLoad.
    /// - Se auto-instancia antes de cargar cualquier escena (RuntimeInitializeOnLoadMethod).
    /// - Reproduce la música adecuada para cada Stage y Final en bucle continuo:
    ///   * Stage 0 y Stage 1: "Stage 0 y Stage 1.mp3" (inicia en menú y sigue ininterrumpida en Stage 1)
    ///   * Stage 2: "Stage 2.mp3"
    ///   * Stage 3: "Stage 3.mp3"
    ///   * Final Matar Dragón: "Final Matar Dragon.mp3"
    ///   * Final Perdonar Dragón: "Final Perdonar Dragon.mp3"
    /// - Dispara los Efectos de Sonido (SFX):
    ///   * Recibir Daño Player y Enemigos
    ///   * Recibe Daño Dragón
    ///   * Recibe Daño Muñeco
    ///   * Bola de Fuego Dragón
    ///   * Ataque Player Aéreo (clic derecho)
    ///   * Ataque Player (clic izquierdo)
    /// </summary>
    [DisallowMultipleComponent]
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        public enum EndingAudioChoice
        {
            None,
            SpareDragon,
            KillDragon
        }

        [Header("=== CONFIGURACIÓN DE VOLUMEN ===")]
        [Range(0f, 1f)] [SerializeField] private float bgmVolume = 0.70f;
        [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1.0f;

        [Header("=== PISTAS DE MÚSICA (BGM) ===")]
        [SerializeField] private AudioClip bgmStage0And1;
        [SerializeField] private AudioClip bgmStage2;
        [SerializeField] private AudioClip bgmStage3;
        [SerializeField] private AudioClip bgmFinalKill;
        [SerializeField] private AudioClip bgmFinalSpare;

        [Header("=== EFECTOS DE SONIDO (SFX) ===")]
        [SerializeField] private AudioClip sfxDamagePlayerOrEnemy;
        [SerializeField] private AudioClip sfxDamageDragon;
        [SerializeField] private AudioClip sfxDamageDummy;
        [SerializeField] private AudioClip sfxDragonFireball;
        [SerializeField] private AudioClip sfxPlayerAttackAir;
        [SerializeField] private AudioClip sfxPlayerAttackNormal;

        private AudioSource _bgmSource;
        private AudioSource _sfxSource;
        private Coroutine _bgmFadeCoroutine;
        private EndingAudioChoice _activeEnding = EndingAudioChoice.None;

        public EndingAudioChoice ActiveEnding => _activeEnding;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null)
            {
                GameObject host = new GameObject("AudioManager");
                host.AddComponent<AudioManager>();
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

            InitializeAudioSources();
            LoadAudioClipsIfMissing();
            SyncWithMuteState();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void Start()
        {
            // Al arrancar la primera escena
            HandleSceneMusic(SceneManager.GetActiveScene().name);
        }

        private void InitializeAudioSources()
        {
            if (_bgmSource == null)
            {
                _bgmSource = gameObject.AddComponent<AudioSource>();
                _bgmSource.playOnAwake = false;
                _bgmSource.loop = true;
                _bgmSource.volume = bgmVolume;
            }

            if (_sfxSource == null)
            {
                _sfxSource = gameObject.AddComponent<AudioSource>();
                _sfxSource.playOnAwake = false;
                _sfxSource.loop = false;
                _sfxSource.volume = sfxVolume;
            }
        }

        public void SyncWithMuteState()
        {
            bool muted = StageTopRightHUD.IsSoundMuted;
            AudioListener.pause = muted;
            AudioListener.volume = muted ? 0f : 1f;
        }

        public void ResetEndingState()
        {
            _activeEnding = EndingAudioChoice.None;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SyncWithMuteState();
            HandleSceneMusic(scene.name);
        }

        /// <summary>
        /// Selecciona y reproduce la música en bucle correspondiente a la escena activa.
        /// </summary>
        public void HandleSceneMusic(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return;

            if (sceneName == "Stage0_MainMenu")
            {
                _activeEnding = EndingAudioChoice.None;
                PlayBgmStage0And1();
            }
            else if (sceneName == "Stage1_Pueblo")
            {
                if (CurtainStageManager.IsReturningForEndingA || _activeEnding == EndingAudioChoice.SpareDragon)
                {
                    PlayBgmFinalSpare();
                }
                else if (CurtainStageManager.IsReturningForEndingB || _activeEnding == EndingAudioChoice.KillDragon)
                {
                    PlayBgmFinalKill();
                }
                else
                {
                    // Inicia o continúa la música del Stage 0/1 sin reiniciar si ya estaba sonando
                    PlayBgmStage0And1();
                }
            }
            else if (sceneName == "Stage2_Bosque")
            {
                _activeEnding = EndingAudioChoice.None;
                PlayBgmStage2();
            }
            else if (sceneName == "Stage3_CuevaDragon")
            {
                _activeEnding = EndingAudioChoice.None;
                PlayBgmStage3();
            }
            else if (sceneName == "Stage4_BosqueNoche" || sceneName == "Stage5_PuebloNoche")
            {
                if (CurtainStageManager.IsReturningForEndingB || _activeEnding == EndingAudioChoice.KillDragon)
                {
                    PlayBgmFinalKill();
                }
                else
                {
                    PlayBgmFinalSpare();
                }
            }
        }

        // =========================================================================
        // MÉTODOS DE REPRODUCCIÓN BGM (Música de fondo)
        // =========================================================================

        public void PlayBgmStage0And1()
        {
            PlayBgm(bgmStage0And1, loop: true, allowContinuous: true);
        }

        public void PlayBgmStage2()
        {
            PlayBgm(bgmStage2, loop: true, allowContinuous: false);
        }

        public void PlayBgmStage3()
        {
            PlayBgm(bgmStage3, loop: true, allowContinuous: false);
        }

        public void PlayBgmFinalKill()
        {
            _activeEnding = EndingAudioChoice.KillDragon;
            PlayBgm(bgmFinalKill, loop: true, allowContinuous: true);
        }

        public void PlayBgmFinalSpare()
        {
            _activeEnding = EndingAudioChoice.SpareDragon;
            PlayBgm(bgmFinalSpare, loop: true, allowContinuous: true);
        }

        /// <summary>
        /// Reproduce una pista de música con transición suave.
        /// Si allowContinuous es true y la pista solicitada ya está sonando, continúa sin interrupciones.
        /// </summary>
        public void PlayBgm(AudioClip clip, bool loop = true, bool allowContinuous = false)
        {
            if (clip == null || _bgmSource == null) return;

            if (allowContinuous && _bgmSource.clip == clip && _bgmSource.isPlaying)
            {
                return;
            }

            if (_bgmFadeCoroutine != null)
            {
                StopCoroutine(_bgmFadeCoroutine);
            }

            _bgmFadeCoroutine = StartCoroutine(CrossfadeBgmRoutine(clip, loop));
        }

        private IEnumerator CrossfadeBgmRoutine(AudioClip newClip, bool loop)
        {
            float targetVol = bgmVolume;

            if (_bgmSource.isPlaying && _bgmSource.clip != null)
            {
                // Fundido de salida rápido
                float startVol = _bgmSource.volume;
                float elapsed = 0f;
                float fadeOutDuration = 0.22f;

                while (elapsed < fadeOutDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    _bgmSource.volume = Mathf.Lerp(startVol, 0f, elapsed / fadeOutDuration);
                    yield return null;
                }
            }

            _bgmSource.clip = newClip;
            _bgmSource.loop = loop;
            _bgmSource.volume = 0f;
            _bgmSource.Play();

            // Fundido de entrada
            float inElapsed = 0f;
            float fadeInDuration = 0.28f;
            while (inElapsed < fadeInDuration)
            {
                inElapsed += Time.unscaledDeltaTime;
                _bgmSource.volume = Mathf.Lerp(0f, targetVol, inElapsed / fadeInDuration);
                yield return null;
            }

            _bgmSource.volume = targetVol;
            _bgmFadeCoroutine = null;
        }

        /// <summary>
        /// Detiene la música de fondo con un desvanecimiento suave (usado en transiciones dramáticas y cinemáticas).
        /// </summary>
        public void StopBgm(float fadeDuration = 0.35f)
        {
            if (_bgmFadeCoroutine != null)
            {
                StopCoroutine(_bgmFadeCoroutine);
            }
            _bgmFadeCoroutine = StartCoroutine(FadeOutAndStopRoutine(fadeDuration));
        }

        private IEnumerator FadeOutAndStopRoutine(float duration)
        {
            if (_bgmSource != null && _bgmSource.isPlaying)
            {
                float startVol = _bgmSource.volume;
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    _bgmSource.volume = Mathf.Lerp(startVol, 0f, elapsed / duration);
                    yield return null;
                }
                _bgmSource.Stop();
                _bgmSource.clip = null;
            }
            _bgmFadeCoroutine = null;
        }

        // =========================================================================
        // MÉTODOS DE REPRODUCCIÓN SFX (Efectos de sonido)
        // =========================================================================

        public void PlayDamagePlayerOrEnemy()
        {
            PlaySfx(sfxDamagePlayerOrEnemy, sfxVolume);
        }

        public void PlayDamageDragon()
        {
            PlaySfx(sfxDamageDragon, sfxVolume);
        }

        public void PlayDamageDummy()
        {
            PlaySfx(sfxDamageDummy, sfxVolume);
        }

        public void PlayDragonFireball()
        {
            PlaySfx(sfxDragonFireball, sfxVolume);
        }

        public void PlayPlayerAttackAir()
        {
            PlaySfx(sfxPlayerAttackAir, sfxVolume * 0.95f);
        }

        public void PlayPlayerAttackNormal()
        {
            PlaySfx(sfxPlayerAttackNormal, sfxVolume * 0.95f);
        }

        private void PlaySfx(AudioClip clip, float volume)
        {
            if (clip == null || _sfxSource == null) return;
            _sfxSource.PlayOneShot(clip, volume);
        }

        // =========================================================================
        // CARGA AUTOMÁTICA DE CLIPS DE AUDIO (Resources + AssetDatabase)
        // =========================================================================

        private void LoadAudioClipsIfMissing()
        {
            AudioClip[] resClips = Resources.LoadAll<AudioClip>("Audio");
            var map = new Dictionary<string, AudioClip>(System.StringComparer.OrdinalIgnoreCase);

            if (resClips != null)
            {
                foreach (var c in resClips)
                {
                    if (c != null && !map.ContainsKey(c.name))
                    {
                        map[c.name] = c;
                    }
                }
            }

            AudioClip FindClip(string searchName)
            {
                if (map.TryGetValue(searchName, out var found)) return found;

                // Búsqueda tolerante a acentos o caracteres especiales
                string searchNorm = searchName.Replace("ñ", "n").Replace(" ", "").ToLowerInvariant();
                foreach (var kvp in map)
                {
                    string kNorm = kvp.Key.Replace("ñ", "n").Replace(" ", "").ToLowerInvariant();
                    if (kNorm.Contains(searchNorm) || searchNorm.Contains(kNorm))
                    {
                        return kvp.Value;
                    }
                }

#if UNITY_EDITOR
                string path = "Assets/_Project/Audio/" + searchName + ".mp3";
                var edClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (edClip != null) return edClip;
#endif
                return null;
            }

            if (bgmStage0And1 == null) bgmStage0And1 = FindClip("Stage 0 y Stage 1");
            if (bgmStage2 == null) bgmStage2 = FindClip("Stage 2");
            if (bgmStage3 == null) bgmStage3 = FindClip("Stage 3");
            if (bgmFinalKill == null) bgmFinalKill = FindClip("Final Matar Dragon");
            if (bgmFinalSpare == null) bgmFinalSpare = FindClip("Final Perdonar Dragon");

            if (sfxDamagePlayerOrEnemy == null) sfxDamagePlayerOrEnemy = FindClip("Recibir Daño Player y Enemigos");
            if (sfxDamageDragon == null) sfxDamageDragon = FindClip("Recibe Daño Dragon");
            if (sfxDamageDummy == null) sfxDamageDummy = FindClip("Recibe Daño Muñeco");
            if (sfxDragonFireball == null) sfxDragonFireball = FindClip("Bola de Fuego Dragon");
            if (sfxPlayerAttackAir == null) sfxPlayerAttackAir = FindClip("Ataque Player Aereo");
            if (sfxPlayerAttackNormal == null) sfxPlayerAttackNormal = FindClip("Ataque Player");
        }
    }
}
