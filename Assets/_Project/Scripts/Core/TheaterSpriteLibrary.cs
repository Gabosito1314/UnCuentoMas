using System.Collections.Generic;
using UnityEngine;

namespace UnCuentoMas
{
    /// <summary>
    /// Biblioteca Central de Sprites Teatrales para Runtime y Builds de PC.
    /// Resuelve de raíz el problema de los sprites que desaparecían al compilar para PC:
    /// - En el Editor de Unity, los scripts solían buscar sprites mediante AssetDatabase.
    /// - Al compilar el juego (PC Standalone), Unity elimina AssetDatabase y los bloques #if UNITY_EDITOR,
    ///   dejando variables en null y haciendo invisibles botones, palitos, llamas y diseños.
    /// - TheaterSpriteLibrary es un ScriptableObject ubicado en Resources/ que almacena referencias serializadas directas.
    /// - Unity empaqueta automáticamente todos los sprites referenciados dentro del ejecutable del juego.
    /// - Funciona al 100% tanto en Play Mode como en Standalone Windows Build.
    /// </summary>
    [CreateAssetMenu(fileName = "TheaterSpriteLibrary", menuName = "Un Cuento Mas/Theater Sprite Library")]
    public class TheaterSpriteLibrary : ScriptableObject
    {
        private static TheaterSpriteLibrary _instance;

        public static TheaterSpriteLibrary Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<TheaterSpriteLibrary>("TheaterSpriteLibrary");
                }
                return _instance;
            }
        }

        [SerializeField] private List<Sprite> registeredSprites = new List<Sprite>();

        private Dictionary<string, Sprite> _cache;

        private void OnEnable()
        {
            RebuildCache();
        }

        public void RebuildCache()
        {
            if (_cache == null) _cache = new Dictionary<string, Sprite>(System.StringComparer.OrdinalIgnoreCase);
            else _cache.Clear();

            if (registeredSprites != null)
            {
                foreach (var sp in registeredSprites)
                {
                    if (sp != null && !string.IsNullOrEmpty(sp.name) && !_cache.ContainsKey(sp.name))
                    {
                        _cache[sp.name] = sp;
                    }
                }
            }
        }

        public Sprite GetSprite(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName)) return null;
            if (_cache == null || _cache.Count == 0) RebuildCache();

            if (_cache != null && _cache.TryGetValue(spriteName, out Sprite found))
            {
                return found;
            }
            return null;
        }

        public void RegisterSprites(IEnumerable<Sprite> sprites)
        {
            if (registeredSprites == null) registeredSprites = new List<Sprite>();
            else registeredSprites.Clear();

            foreach (var sp in sprites)
            {
                if (sp != null && !registeredSprites.Contains(sp))
                {
                    registeredSprites.Add(sp);
                }
            }
            RebuildCache();
        }

        public static Sprite Get(string spriteName)
        {
            return Instance != null ? Instance.GetSprite(spriteName) : null;
        }
    }
}
