using System.Collections.Generic;
using UnityEngine;

namespace Mahjong.Game
{
    /// <summary>
    /// Prozedural erzeugter, abgerundeter Quader — ersetzt den harten Wuerfel-Primitive.
    /// Vorgehen: die 6 Seitenflaechen werden segmentiert; jeder Vertex wird vom
    /// Flaeche-Ort auf das innere (geschrumpfte) Quader-Volumen geklemmt und um den
    /// Radius in Richtung nach aussen verschoben. Ergebnis: flache Flaechen mit
    /// glatt gerundeten Kanten, eine Normalen-kontinuierliche Oberflaeche.
    /// Das Mesh wird einmal erzeugt und von allen Steinen geteilt (WebGL-freundlich).
    /// </summary>
    public static class RoundedBox
    {
        public static Mesh Create(float width, float height, float depth, float radius, int segments)
        {
            var mesh = new Mesh { name = "RoundedBox" };

            var halfX = width * 0.5f;
            var halfY = height * 0.5f;
            var halfZ = depth * 0.5f;
            var inner = new Vector3(halfX - radius, halfY - radius, halfZ - radius);

            // Basis je Flaeche: Normale, u-Achse, v-Achse (Reihenfolge = außen zeigend).
            var faces = new[]
            {
                (N: new Vector3(0f, 0f, 1f), U: new Vector3(1f, 0f, 0f), V: new Vector3(0f, 1f, 0f)),
                (N: new Vector3(0f, 0f, -1f), U: new Vector3(-1f, 0f, 0f), V: new Vector3(0f, 1f, 0f)),
                (N: new Vector3(1f, 0f, 0f), U: new Vector3(0f, 0f, -1f), V: new Vector3(0f, 1f, 0f)),
                (N: new Vector3(-1f, 0f, 0f), U: new Vector3(0f, 0f, 1f), V: new Vector3(0f, 1f, 0f)),
                (N: new Vector3(0f, 1f, 0f), U: new Vector3(1f, 0f, 0f), V: new Vector3(0f, 0f, -1f)),
                (N: new Vector3(0f, -1f, 0f), U: new Vector3(1f, 0f, 0f), V: new Vector3(0f, 0f, 1f)),
            };

            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            foreach (var face in faces)
            {
                var baseIndex = verts.Count;
                var halfU = Extent(face.U, halfX, halfY, halfZ);
                var halfV = Extent(face.V, halfX, halfY, halfZ);

                for (var j = 0; j <= segments; j++)
                {
                    for (var i = 0; i <= segments; i++)
                    {
                        var fu = Mathf.Lerp(-halfU, halfU, i / (float)segments);
                        var fv = Mathf.Lerp(-halfV, halfV, j / (float)segments);

                        var p = face.N * Extent(face.N, halfX, halfY, halfZ)
                            + face.U * fu + face.V * fv;

                        // Klemmen auf das innere Volumen + Radius nach aussen = Rundung.
                        var q = new Vector3(
                            Mathf.Clamp(p.x, -inner.x, inner.x),
                            Mathf.Clamp(p.y, -inner.y, inner.y),
                            Mathf.Clamp(p.z, -inner.z, inner.z));

                        var dir = p - q;
                        var len = dir.magnitude;

                        if (len < 1e-6f)
                        {
                            dir = face.N; // flache Flaeche
                        }
                        else
                        {
                            dir /= len;
                        }

                        verts.Add(q + dir * radius);
                        norms.Add(dir);
                        uvs.Add(new Vector2(i / (float)segments, j / (float)segments));
                    }
                }

                for (var j = 0; j < segments; j++)
                {
                    for (var i = 0; i < segments; i++)
                    {
                        var a = baseIndex + j * (segments + 1) + i;
                        var b = a + 1;
                        var c = a + segments + 1;
                        var d = c + 1;

                        tris.Add(a);
                        tris.Add(c);
                        tris.Add(b);
                        tris.Add(b);
                        tris.Add(c);
                        tris.Add(d);
                    }
                }
            }

            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static float Extent(Vector3 axis, float halfX, float halfY, float halfZ)
        {
            if (Mathf.Abs(axis.x) > 0.5f) return halfX;
            if (Mathf.Abs(axis.y) > 0.5f) return halfY;
            return halfZ;
        }
    }
}