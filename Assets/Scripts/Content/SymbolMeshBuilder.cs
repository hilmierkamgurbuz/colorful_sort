#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace ColorfulSort.Content
{
    /// <summary>
    /// Lifts a brick's engraved symbol out of the brick, as a mesh of its own.
    /// <para>
    /// The symbol is not a sprite and has never been one: it is the second submesh of the brick mesh,
    /// which is exactly why <see cref="BlockSkin"/> carries a second material for it (D-052). A burst
    /// of particles shaped like that symbol therefore needs the geometry extracted once, at authoring
    /// time, into an asset — nothing here runs in a build (D-003: what can be baked is baked).
    /// </para>
    /// <para>
    /// It lives in <c>Content</c> behind <c>UNITY_EDITOR</c> rather than under <c>Assets/Editor/</c>,
    /// for the reason D-035 recorded when the solver went the same way: an editor folder compiles
    /// where no test assembly can reach it, and index arithmetic that nothing checks is the kind of
    /// code that is wrong invisibly. A triangle list remapped one index out still produces a mesh —
    /// just the wrong one.
    /// </para>
    /// </summary>
    public static class SymbolMeshBuilder
    {
        /// <summary>The submesh a brick keeps its symbol in; slot 1, matching the skin's second material.</summary>
        public const int SymbolSubMesh = 1;

        /// <summary>
        /// The symbol's faces alone, as a mesh centred on the origin and one unit across.
        /// <para>
        /// Three transforms are baked in, and each of them is a fact about how the brick is drawn
        /// rather than a preference about how the spark should look.
        /// </para>
        /// <para>
        /// **Turned 180° about Y**, because the `Block` prefab carries exactly that rotation so the
        /// symbol faces the camera (D-047) — a particle mesh is handed no transform at all, so without
        /// it every spark would be a mirror image with its back turned. A half turn is a proper
        /// rotation, so the winding survives it and the triangles are not re-ordered.
        /// </para>
        /// <para>
        /// **Recentred on its own bounds**, because a particle is drawn around its position and the
        /// symbol sits on the brick's face, nowhere near the brick's middle.
        /// </para>
        /// <para>
        /// **Scaled so its widest extent is one unit**, so a particle size means the same thing for
        /// every symbol — the cat and the crown are not drawn at the same size on their bricks — and
        /// the tuned number survives a re-skin instead of quietly meaning something else.
        /// </para>
        /// </summary>
        /// <returns>The new mesh, or null with <paramref name="error"/> saying what was wrong.</returns>
        public static Mesh Build(Mesh source, string name, out string error)
        {
            if (source == null)
            {
                error = "there is no mesh to take a symbol out of";
                return null;
            }

            if (source.subMeshCount <= SymbolSubMesh)
            {
                error = source.name + " has " + source.subMeshCount +
                        " submesh(es); the symbol is submesh " + SymbolSubMesh +
                        ", which is the slot the skin paints with its symbol material";
                return null;
            }

            int[] triangles = source.GetTriangles(SymbolSubMesh);

            if (triangles.Length == 0)
            {
                error = source.name + " has an empty submesh " + SymbolSubMesh + ", so there is no symbol to lift out";
                return null;
            }

            Vector3[] sourceVertices = source.vertices;
            Vector3[] sourceNormals = source.normals;
            Vector2[] sourceUv = source.uv;

            bool hasNormals = sourceNormals != null && sourceNormals.Length == sourceVertices.Length;
            bool hasUv = sourceUv != null && sourceUv.Length == sourceVertices.Length;

            // Only the vertices these triangles actually use, renumbered from zero. The map is what
            // makes that safe: every index is looked up, never recomputed from a stride.
            var moved = new Dictionary<int, int>(triangles.Length);
            var vertices = new List<Vector3>(triangles.Length);
            var normals = hasNormals ? new List<Vector3>(triangles.Length) : null;
            var uv = hasUv ? new List<Vector2>(triangles.Length) : null;
            var indices = new int[triangles.Length];

            for (int i = 0; i < triangles.Length; i++)
            {
                int original = triangles[i];
                int index;

                if (!moved.TryGetValue(original, out index))
                {
                    index = vertices.Count;
                    moved.Add(original, index);

                    vertices.Add(HalfTurn(sourceVertices[original]));

                    if (hasNormals)
                    {
                        normals.Add(HalfTurn(sourceNormals[original]));
                    }

                    if (hasUv)
                    {
                        uv.Add(sourceUv[original]);
                    }
                }

                indices[i] = index;
            }

            Place(vertices);

            var symbol = new Mesh { name = name };
            symbol.SetVertices(vertices);

            if (hasNormals)
            {
                symbol.SetNormals(normals);
            }

            if (hasUv)
            {
                symbol.SetUVs(0, uv);
            }

            symbol.SetTriangles(indices, 0);

            if (!hasNormals)
            {
                symbol.RecalculateNormals();
            }

            symbol.RecalculateBounds();
            error = null;
            return symbol;
        }

        /// <summary>The half turn about Y the brick is drawn with: x and z both flip, y is untouched.</summary>
        private static Vector3 HalfTurn(Vector3 point)
        {
            return new Vector3(-point.x, point.y, -point.z);
        }

        /// <summary>
        /// Moves the symbol onto the origin and scales it to one unit across its widest side. Done in
        /// one pass over the list rather than through a temporary mesh, and guarded at zero: a
        /// degenerate symbol — every vertex in one place — is left where it is rather than divided by
        /// its own size.
        /// </summary>
        private static void Place(List<Vector3> vertices)
        {
            Vector3 min = vertices[0];
            Vector3 max = vertices[0];

            for (int i = 1; i < vertices.Count; i++)
            {
                min = Vector3.Min(min, vertices[i]);
                max = Vector3.Max(max, vertices[i]);
            }

            Vector3 centre = (min + max) * 0.5f;
            Vector3 size = max - min;
            float widest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            float scale = widest > 0.0001f ? 1f / widest : 1f;

            for (int i = 0; i < vertices.Count; i++)
            {
                vertices[i] = (vertices[i] - centre) * scale;
            }
        }
    }
}
#endif
