using System.Collections;
using TMPro;
using UnityEngine;

namespace UnCuentoMas
{
    /// <summary>
    /// Cartel de Texto teatral ("Caja de Texto") sostenido por un Palito desde arriba:
    /// - Baja desde arriba del escenario para mostrar instrucciones de tutorial, pistas del pueblo vacío
    ///   o diálogos clave, y puede retirarse hacia arriba cuando no se necesita.
    /// </summary>
    public class HangingTextSignPuppet : MonoBehaviour
    {
        [SerializeField] private Transform signVisualRoot;
        [SerializeField] private SpriteRenderer signBoxRenderer;
        [SerializeField] private SpriteRenderer topStickRenderer;
        [SerializeField] private TMP_Text signTextTMP;
        [SerializeField] private TMP_FontAsset imFellFontAsset;
        [SerializeField] private float loweredY = 2.75f;
        [SerializeField] private float raisedHiddenY = 7.15f;
        [SerializeField] private float moveSpeed = 7.5f;

        private bool _isLowered = false;
        private Coroutine _autoHideRoutine;

        private readonly Color _inkNormalColor = new Color(0.18f, 0.10f, 0.05f, 1f);
        private readonly Color _inkOminousColor = new Color(0.58f, 0.08f, 0.06f, 1f);

        /// <summary>
        /// Inicialización: ubica el cartel en su posición elevada y oculta (fuera de la vista superior del telón).
        /// </summary>
        private void Awake()
        {
            if (signVisualRoot == null)
            {
                Transform child = transform.Find("SignVisualRoot");
                signVisualRoot = child != null ? child : transform;
            }

            if (signVisualRoot != null)
            {
                Vector3 initialPos = signVisualRoot.localPosition;
                initialPos.y = raisedHiddenY;
                signVisualRoot.localPosition = initialPos;
            }

            if (signTextTMP == null)
            {
                signTextTMP = GetComponentInChildren<TMP_Text>(true);
            }

            ApplyFableTypography();
        }

        public void ApplyFableTypography()
        {
            if (imFellFontAsset == null)
            {
                imFellFontAsset = Resources.Load<TMP_FontAsset>("Fonts/IMFellEnglish-Regular SDF");
                if (imFellFontAsset == null)
                {
                    imFellFontAsset = Resources.Load<TMP_FontAsset>("Fonts/IMFellDWPica-Regular SDF");
                }
            }

            if (imFellFontAsset == null)
            {
                Font ttf = Resources.Load<Font>("Fonts/IMFellEnglish-Regular");
                if (ttf == null) ttf = Resources.Load<Font>("Fonts/IMFellDWPica-Regular");
                if (ttf != null)
                {
                    imFellFontAsset = TMP_FontAsset.CreateFontAsset(ttf);
                }
            }

            if (signTextTMP != null)
            {
                if (imFellFontAsset != null)
                {
                    signTextTMP.font = imFellFontAsset;
                }
                RectTransform rt = signTextTMP.rectTransform;
                if (rt != null)
                {
                    rt.sizeDelta = new Vector2(11.8f, 3.5f);
                }
                signTextTMP.margin = new Vector4(0.35f, 0.15f, 0.35f, 0.15f);
                signTextTMP.lineSpacing = 2f;
                signTextTMP.enableAutoSizing = true;
                signTextTMP.fontSizeMin = 3.4f;
                signTextTMP.fontSizeMax = 5.4f;
                signTextTMP.fontSize = 4.8f;
                signTextTMP.textWrappingMode = TextWrappingModes.Normal;
            }
        }

        /// <summary>
        /// Bucle de animación:
        /// Interpola suavemente (Mathf.Lerp) la posición Y entre la altura oculta (raisedHiddenY)
        /// y la altura visible de lectura (loweredY).
        /// Añade un leve balanceo pendular (Mathf.Sin) para simular que el letrero cuelga de un palito sostenido a mano.
        /// </summary>
        private void Update()
        {
            if (signVisualRoot == null) return;

            float targetY = _isLowered ? loweredY : raisedHiddenY;
            float swayY = _isLowered ? Mathf.Sin(Time.time * 2.2f) * 0.04f : 0f;
            float swayRot = _isLowered ? Mathf.Sin(Time.time * 1.8f) * 1.15f : 0f;

            Vector3 pos = signVisualRoot.localPosition;
            pos.y = Mathf.Lerp(pos.y, targetY + swayY, Time.deltaTime * moveSpeed);
            signVisualRoot.localPosition = pos;
            signVisualRoot.localRotation = Quaternion.Slerp(
                signVisualRoot.localRotation,
                Quaternion.Euler(0f, 0f, swayRot),
                Time.deltaTime * 6f
            );
        }

        /// <summary>
        /// Hace descender el cartel con un mensaje determinado.
        /// Permite cambiar el color de la tinta (normal marrón o tétrica roja para revelaciones oscuras)
        /// y programar su retirada automática tras unos segundos si se desea.
        /// </summary>
        public void ShowSign(string message, bool ominousInk = false, float autoHideAfterSeconds = 0f)
        {
            if (_autoHideRoutine != null)
            {
                StopCoroutine(_autoHideRoutine);
                _autoHideRoutine = null;
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                HideSign();
                return;
            }

            if (signTextTMP == null)
            {
                signTextTMP = GetComponentInChildren<TMP_Text>(true);
            }

            if (signTextTMP != null)
            {
                ApplyFableTypography();
                signTextTMP.text = message;
                signTextTMP.color = ominousInk ? _inkOminousColor : _inkNormalColor;
            }

            if (_isLowered && signVisualRoot != null)
            {
                Vector3 pos = signVisualRoot.localPosition;
                pos.y = Mathf.Min(raisedHiddenY, pos.y + 0.45f);
                signVisualRoot.localPosition = pos;
            }

            _isLowered = true;

            if (autoHideAfterSeconds > 0.1f)
            {
                _autoHideRoutine = StartCoroutine(AutoHideRoutine(autoHideAfterSeconds));
            }
        }

        /// <summary>
        /// Retira el cartel subiéndolo hacia arriba fuera del escenario.
        /// </summary>
        public void HideSign()
        {
            if (_autoHideRoutine != null)
            {
                StopCoroutine(_autoHideRoutine);
                _autoHideRoutine = null;
            }
            _isLowered = false;
        }

        /// <summary>
        /// Ajusta la velocidad de bajada y subida del cartel teatral.
        /// </summary>
        public void SetMoveSpeed(float speed)
        {
            moveSpeed = speed;
        }

        /// <summary>
        /// Ajusta la altura objetivo a la que desciende el cartel.
        /// </summary>
        public void SetLoweredY(float y)
        {
            loweredY = y;
        }

        /// <summary>
        /// Ajusta el orden de capa (sortingOrder) del cartel, palito y texto.
        /// Útil para mostrar el cartel por delante del telón cerrado (orden 55) durante cinemáticas o títulos de acto.
        /// </summary>
        public void SetSortingOrder(int baseOrder)
        {
            if (signBoxRenderer == null && signVisualRoot != null)
            {
                signBoxRenderer = signVisualRoot.GetComponent<SpriteRenderer>();
            }
            if (topStickRenderer == null && signVisualRoot != null)
            {
                Transform stick = signVisualRoot.Find("Palito_Superior_Cartel");
                if (stick != null) topStickRenderer = stick.GetComponent<SpriteRenderer>();
            }

            if (signBoxRenderer != null) signBoxRenderer.sortingOrder = baseOrder;
            if (topStickRenderer != null) topStickRenderer.sortingOrder = baseOrder - 1;
            if (signTextTMP != null)
            {
                var mr = signTextTMP.GetComponent<MeshRenderer>();
                if (mr != null) mr.sortingOrder = baseOrder + 1;
                var canvas = signTextTMP.GetComponentInParent<Canvas>();
                if (canvas != null) canvas.sortingOrder = baseOrder + 1;
            }
        }

        /// <summary>
        /// Corrutina para ocultar el cartel automáticamente tras una demora.
        /// </summary>
        private IEnumerator AutoHideRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            _isLowered = false;
            _autoHideRoutine = null;
        }
    }
}
