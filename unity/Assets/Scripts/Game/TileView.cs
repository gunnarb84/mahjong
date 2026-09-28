using System.Collections;
using Mahjong.Core;
using UnityEngine;

namespace Mahjong.Game
{
    /// <summary>
    /// Sichtbarer Stein: Wuerfel mit Symbol-Textur (Atlas) auf der Oberseite.
    /// Die Symbole kommen aus der einmalig im Editor generierten
    /// TileAtlas.png (Menue "Mahjong/Stein-Atlas generieren") — korrekter
    /// Tiefentest, keine Font-Abhaengigkeit im Spiel.
    /// </summary>
    public class TileView : MonoBehaviour
    {
        static readonly Color TileBase = new Color(0.93f, 0.89f, 0.78f);

        static Texture2D _atlas;
        static Material _topMaterial;
        static Material _sideMaterial;
        static Mesh _bodyMesh;
        static float _bodyThickness = -1f;
        const float BodyRadius = 0.05f; // Rundung (Anteil der Einheitskante)

        public Pos Pos { get; private set; }

        public bool IsFree { get; private set; }

        MeshRenderer _sideRenderer;
        MeshRenderer _topRenderer;
        MaterialPropertyBlock _sideBlock;
        MaterialPropertyBlock _topBlock;
        bool _selected;
        bool _hinted;

        public static TileView Create(
            Transform parent,
            Pos pos,
            TileType type,
            float quarterSize,
            float tileThickness,
            float offsetX,
            float offsetZ)
        {
            EnsureSharedAssets(tileThickness);

            // Abgerundeter Quader (geteiltes Mesh) statt harter Wuerfel.
            var go = new GameObject(type.Id + " " + pos, typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider));
            go.transform.SetParent(parent, false);

            // Zentriert: Stein belegt [X, X+2) x [Y, Y+2) Vierteil-Kacheln.
            var x = (pos.X + 1) * quarterSize - offsetX;
            var z = (pos.Y + 1) * quarterSize - offsetZ;
            go.transform.localPosition = new Vector3(x, pos.Layer * tileThickness + tileThickness * 0.5f, z);
            go.transform.localScale = new Vector3(0.94f, tileThickness, 0.94f);

            go.GetComponent<MeshFilter>().sharedMesh = _bodyMesh;
            var collider = go.GetComponent<BoxCollider>();
            collider.size = Vector3.one; // Einheitsmesh: Kollider skaliert mit

            var sideRenderer = go.GetComponent<MeshRenderer>();
            sideRenderer.sharedMaterial = _sideMaterial;

            // Symbol-Flaeche auf der Oberseite.
            var topGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.Destroy(topGo.GetComponent<Collider>());
            topGo.name = "Face";
            topGo.transform.SetParent(go.transform, false);
            topGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            topGo.transform.localPosition = new Vector3(0f, 0.505f, 0f);
            topGo.transform.localScale = new Vector3(0.84f, 1f, 0.84f); // innerhalb der gerundeten Flaeche

            var topRenderer = topGo.GetComponent<MeshRenderer>();
            topRenderer.sharedMaterial = _topMaterial;

            var view = go.AddComponent<TileView>();
            view.Pos = pos;
            view._sideRenderer = sideRenderer;
            view._topRenderer = topRenderer;
            view._sideBlock = new MaterialPropertyBlock();
            view._topBlock = new MaterialPropertyBlock();

            // Atlas-Zelle dieses Typs als UV-Offset im Property-Block.
            var index = TileFaces.Index(type);
            view._topBlock.SetVector("_MainTex_ST", new Vector4(
                1f / TileFaces.Columns,
                1f / TileFaces.Rows,
                index % TileFaces.Columns / (float)TileFaces.Columns,
                index / TileFaces.Columns / (float)TileFaces.Rows));

            view.ApplyBlocks();
            return view;
        }

        static void EnsureSharedAssets(float tileThickness)
        {
            if (_topMaterial != null && Mathf.Approximately(_bodyThickness, tileThickness))
            {
                return;
            }

            _atlas = Resources.Load<Texture2D>("Art/TileAtlas");

            if (_atlas == null)
            {
                Debug.LogError(
                    "[Mahjong] TileAtlas.png nicht gefunden — bitte einmal im Menue " +
                    "'Mahjong/Stein-Atlas generieren' ausfuehren.");
            }

            _sideMaterial = new Material(Shader.Find("Standard"));
            _sideMaterial.color = TileBase;

            _topMaterial = new Material(Shader.Find("Standard"));

            if (_atlas != null)
            {
                _topMaterial.mainTexture = _atlas;
            }

            // Einheits-Quader mit abgerundeten Kanten; Hoehe via localScale.
            _bodyMesh = RoundedBox.Create(1f, 1f, 1f, BodyRadius, segments: 6);
            _bodyThickness = tileThickness;
        }

        /// <summary>
        /// Wechselt den Typ nachtraeglich (nach Shuffle): nur die Atlas-Zelle im
        /// Property-Block austauschen, das GameObject bleibt.
        /// </summary>
        public void SetType(TileType type)
        {
            var index = TileFaces.Index(type);
            _topBlock.SetVector("_MainTex_ST", new Vector4(
                1f / TileFaces.Columns,
                1f / TileFaces.Rows,
                index % TileFaces.Columns / (float)TileFaces.Columns,
                index / TileFaces.Columns / (float)TileFaces.Rows));
            _topRenderer.SetPropertyBlock(_topBlock);
        }

        public void SetFree(bool free)
        {
            IsFree = free;
            ApplyBlocks();
        }

        public void SetSelected(bool selected)
        {
            _selected = selected;
            ApplyBlocks();
        }

        public void SetHint(bool hinted)
        {
            _hinted = hinted;
            ApplyBlocks();
        }

        void ApplyBlocks()
        {
            // Ausgewaehlt: gold; Hinweis: cyan; freistehend: voll deckend;
            // blockiert: abgedunkelt.
            Color tint;

            if (_selected)
            {
                tint = new Color(1.0f, 0.86f, 0.38f);
            }
            else if (_hinted)
            {
                tint = new Color(0.55f, 0.90f, 1.0f);
            }
            else if (IsFree)
            {
                tint = Color.white;
            }
            else
            {
                tint = new Color(0.56f, 0.56f, 0.56f);
            }

            _sideBlock.SetColor("_Color", tint);
            _topBlock.SetColor("_Color", tint);
            _sideRenderer.SetPropertyBlock(_sideBlock);
            _topRenderer.SetPropertyBlock(_topBlock);
        }

        /// <summary>Schrumpft weg und zerstoert sich — ersetzt spaeter einen schoenen Effekt.</summary>
        public void PlayRemove()
        {
            IsFree = false;

            foreach (var collider in GetComponentsInChildren<Collider>())
            {
                Destroy(collider);
            }

            StartCoroutine(ShrinkAway());
        }

        IEnumerator ShrinkAway()
        {
            // Kurzes Aufploppen, dann Schrumpfen mit leichtem Aufstieg.
            var start = transform.localScale;
            var popped = start * 1.12f;
            var target = start * 0.01f;

            for (var t = 0f; t < 1f; t += Time.deltaTime * 14f)
            {
                transform.localScale = Vector3.Lerp(start, popped, t);
                yield return null;
            }

            var lift = transform.localPosition + new Vector3(0f, 0.15f, 0f);

            for (var t = 0f; t < 1f; t += Time.deltaTime * 5f)
            {
                transform.localScale = Vector3.Lerp(popped, target, t);
                transform.localPosition = Vector3.Lerp(transform.localPosition, lift, 0.08f);
                yield return null;
            }

            Destroy(gameObject);
        }
    }
}