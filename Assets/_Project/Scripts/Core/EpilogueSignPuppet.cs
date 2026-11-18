using System.Collections;
using TMPro;
using UnityEngine;

namespace UnCuentoMas
{
    /// <summary>
    /// Cartel Ancho de Epílogo Teatral con Cuerdas:
    /// - Diseñado para el cierre solemne de los Finales A y B.
    /// - Desciende desde arriba del telón cerrado tras una pausa dramática de 1.0 segundo.
    /// - Utiliza dos cuerdas ("Corazones y etiqueta de papel_3") para sostener el cartel ancho ("Caja de Texto_0").
    /// - Tipografía clásica IM Fell English con tono tinta #1E1E24.
    /// - Permite una lectura cómoda y solemne sin botones abruptos.
    /// </summary>
    public class EpilogueSignPuppet : MonoBehaviour
    {
        [Header("=== 1. COMPONENTES VISUALES ===")]
        [SerializeField] private Transform signVisualRoot;
        [SerializeField] private SpriteRenderer boxRenderer;
        [SerializeField] private SpriteRenderer leftRopeRenderer;
        [SerializeField] private SpriteRenderer rightRopeRenderer;
        [SerializeField] private TMP_Text epilogueTextTMP;
        [SerializeField] private TMP_FontAsset imFellFontAsset;

        [Header("=== 2. POSICIONAMIENTO Y ANIMACIÓN ===")]
        [SerializeField] private float raisedHiddenY = 8.8f;
        [SerializeField] private float loweredTargetY = 0.40f;
        [SerializeField] private float descentDuration = 2.2f;

        private readonly Color _inkTonedColor = new Color(30f / 255f, 30f / 255f, 36f / 255f, 1f); // #1E1E24
        private bool _isDisplayed = false;

        private void Awake()
        {
            EnsureVisualsAndRopesExist();
            if (signVisualRoot != null)
            {
                signVisualRoot.localPosition = new Vector3(0f, raisedHiddenY, 0f);
            }
        }

        /// <summary>
        /// Asegura la existencia de la jerarquía visual del cartel ancho, cuerdas dobles y TextMeshPro.
        /// </summary>
        public void EnsureVisualsAndRopesExist()
        {
            if (signVisualRoot == null)
            {
                Transform foundRoot = transform.Find("EpilogueVisualRoot");
                if (foundRoot == null)
                {
                    GameObject rootGO = new GameObject("EpilogueVisualRoot");
                    rootGO.transform.SetParent(transform, false);
                    signVisualRoot = rootGO.transform;
                }
                else
                {
                    signVisualRoot = foundRoot;
                }
            }

            Sprite boxSprite = TheaterSpriteLibrary.Get("Caja de Texto_0");
            Sprite ropeSprite = TheaterSpriteLibrary.Get("Corazones y etiqueta de papel_3");

#if UNITY_EDITOR
            if (boxSprite == null)
            {
                var boxAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Sprites/Caja de Texto.png");
                foreach (var a in boxAssets)
                {
                    if (a is Sprite sp && sp.name == "Caja de Texto_0") { boxSprite = sp; break; }
                }
            }

            if (ropeSprite == null)
            {
                var ropeAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Sprites/Corazones y etiqueta de papel.png");
                foreach (var a in ropeAssets)
                {
                    if (a is Sprite sp && sp.name == "Corazones y etiqueta de papel_3") { ropeSprite = sp; break; }
                }
            }
#endif

            // 1. Caja de Pergamino Ancha (Caja de Texto_0)
            if (boxRenderer == null)
            {
                Transform boxTransform = signVisualRoot.Find("Cartel_Box");
                if (boxTransform == null)
                {
                    GameObject boxGO = new GameObject("Cartel_Box");
                    boxGO.transform.SetParent(signVisualRoot, false);
                    boxRenderer = boxGO.AddComponent<SpriteRenderer>();
                }
                else
                {
                    boxRenderer = boxTransform.GetComponent<SpriteRenderer>();
                }
            }

            if (boxRenderer != null)
            {
                if (boxSprite != null) boxRenderer.sprite = boxSprite;
                boxRenderer.sortingOrder = 55; // Por delante del telón cerrado (orden 50)
                boxRenderer.transform.localPosition = Vector3.zero;
                boxRenderer.transform.localScale = new Vector3(1.10f, 1.34f, 1f);
            }

            // 2. Cuerda Izquierda (Corazones y etiqueta de papel_3)
            if (leftRopeRenderer == null)
            {
                Transform ropeL = signVisualRoot.Find("Cuerda_Izquierda");
                if (ropeL == null)
                {
                    GameObject ropeLGO = new GameObject("Cuerda_Izquierda");
                    ropeLGO.transform.SetParent(signVisualRoot, false);
                    leftRopeRenderer = ropeLGO.AddComponent<SpriteRenderer>();
                }
                else
                {
                    leftRopeRenderer = ropeL.GetComponent<SpriteRenderer>();
                }
            }

            if (leftRopeRenderer != null)
            {
                if (ropeSprite != null) leftRopeRenderer.sprite = ropeSprite;
                leftRopeRenderer.sortingOrder = 54; // Detrás del cartel pergamino, por delante del telón
                leftRopeRenderer.transform.localPosition = new Vector3(-4.60f, 1.85f, 0f);
                leftRopeRenderer.transform.localScale = new Vector3(1.4f, 1.75f, 1f);
            }

            // 3. Cuerda Derecha (Corazones y etiqueta de papel_3)
            if (rightRopeRenderer == null)
            {
                Transform ropeR = signVisualRoot.Find("Cuerda_Derecha");
                if (ropeR == null)
                {
                    GameObject ropeRGO = new GameObject("Cuerda_Derecha");
                    ropeRGO.transform.SetParent(signVisualRoot, false);
                    rightRopeRenderer = ropeRGO.AddComponent<SpriteRenderer>();
                }
                else
                {
                    rightRopeRenderer = ropeR.GetComponent<SpriteRenderer>();
                }
            }

            if (rightRopeRenderer != null)
            {
                if (ropeSprite != null) rightRopeRenderer.sprite = ropeSprite;
                rightRopeRenderer.sortingOrder = 54;
                rightRopeRenderer.transform.localPosition = new Vector3(4.60f, 1.85f, 0f);
                rightRopeRenderer.transform.localScale = new Vector3(1.4f, 1.75f, 1f);
            }

            // 4. Texto con tipografía IM Fell English
            if (epilogueTextTMP == null)
            {
                Transform textTransform = signVisualRoot.Find("Epilogue_Text");
                if (textTransform == null)
                {
                    GameObject textGO = new GameObject("Epilogue_Text");
                    textGO.transform.SetParent(signVisualRoot, false);
                    epilogueTextTMP = textGO.AddComponent<TextMeshPro>();
                }
                else
                {
                    epilogueTextTMP = textTransform.GetComponent<TMP_Text>();
                }
            }

            ApplyTypography();
        }

        /// <summary>
        /// Aplica la fuente IM Fell English, dimensiones del contenedor y el color exacto #1E1E24.
        /// </summary>
        public void ApplyTypography()
        {
            if (imFellFontAsset == null)
            {
                imFellFontAsset = Resources.Load<TMP_FontAsset>("Fonts/IMFellEnglish-Regular SDF");
                if (imFellFontAsset == null)
                {
                    imFellFontAsset = Resources.Load<TMP_FontAsset>("Fonts/IMFellDWPica-Regular SDF");
                }
            }

            if (epilogueTextTMP != null)
            {
                if (imFellFontAsset != null)
                {
                    epilogueTextTMP.font = imFellFontAsset;
                }

                RectTransform rt = epilogueTextTMP.rectTransform;
                if (rt != null)
                {
                    rt.sizeDelta = new Vector2(11.4f, 4.5f);
                    rt.localPosition = new Vector3(0f, 0.05f, 0f);
                }

                epilogueTextTMP.alignment = TextAlignmentOptions.Center;
                epilogueTextTMP.margin = new Vector4(0.40f, 0.35f, 0.40f, 0.35f);
                epilogueTextTMP.lineSpacing = 1.4f;
                epilogueTextTMP.enableAutoSizing = true;
                epilogueTextTMP.fontSizeMin = 2.4f;
                epilogueTextTMP.fontSizeMax = 4.2f;
                epilogueTextTMP.fontSize = 3.4f;
                epilogueTextTMP.textWrappingMode = TextWrappingModes.Normal;
                epilogueTextTMP.color = _inkTonedColor;

                var mr = epilogueTextTMP.GetComponent<MeshRenderer>();
                if (mr != null) mr.sortingOrder = 56; // Sobre la caja
            }
        }

        private void Update()
        {
            if (!_isDisplayed || signVisualRoot == null) return;

            // Balanceo pendular armónico simulando suspensión por cuerdas de titiritero
            float swayRot = Mathf.Sin(Time.time * 1.3f) * 0.70f;
            float bobY = Mathf.Sin(Time.time * 1.6f) * 0.025f;

            signVisualRoot.localRotation = Quaternion.Euler(0f, 0f, swayRot);
            Vector3 pos = signVisualRoot.localPosition;
            pos.y = loweredTargetY + bobY;
            signVisualRoot.localPosition = pos;
        }

        /// <summary>
        /// Corrutina para descender el cartel de epílogo teatral suavemente con cuerdas.
        /// </summary>
        public IEnumerator ShowEpilogueRoutine(string epilogueMessage)
        {
            EnsureVisualsAndRopesExist();
            ApplyTypography();

            if (epilogueTextTMP != null)
            {
                epilogueTextTMP.text = epilogueMessage;
                epilogueTextTMP.color = _inkTonedColor;
            }

            if (signVisualRoot != null)
            {
                signVisualRoot.localPosition = new Vector3(0f, raisedHiddenY, 0f);
                signVisualRoot.localRotation = Quaternion.identity;
            }

            float elapsed = 0f;
            Vector3 startPos = new Vector3(0f, raisedHiddenY, 0f);
            Vector3 endPos = new Vector3(0f, loweredTargetY, 0f);

            while (elapsed < descentDuration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / descentDuration;

                // Curva de desaceleración teatral con suave asentamiento
                float t = Mathf.SmoothStep(0f, 1f, progress);
                float settle = Mathf.Sin(progress * Mathf.PI) * 0.12f;

                if (signVisualRoot != null)
                {
                    signVisualRoot.localPosition = Vector3.Lerp(startPos, endPos, t) + new Vector3(0f, settle, 0f);
                    float sway = Mathf.Sin(progress * Mathf.PI * 2f) * 0.9f * (1f - progress);
                    signVisualRoot.localRotation = Quaternion.Euler(0f, 0f, sway);
                }

                yield return null;
            }

            if (signVisualRoot != null)
            {
                signVisualRoot.localPosition = endPos;
                signVisualRoot.localRotation = Quaternion.identity;
            }

            _isDisplayed = true;
        }
    }
}
