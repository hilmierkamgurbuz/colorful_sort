using ColorfulSort.Content;
using NUnit.Framework;
using UnityEngine;

namespace ColorfulSort.Content.Tests
{
    /// <summary>
    /// The symbol lifted out of a brick mesh. This is index arithmetic over two submeshes, which is
    /// the shape of code that is wrong *invisibly*: a triangle list remapped one index out still
    /// produces a mesh, just the wrong one, and on screen it is a shrug rather than an error.
    /// <para>
    /// It is testable at all because the extraction lives in <c>Content</c> behind
    /// <c>UNITY_EDITOR</c> rather than under <c>Assets/Editor/</c> — the same choice D-035 made for the
    /// solver, and for the same reason: an editor folder compiles where no test assembly can reach it.
    /// </para>
    /// <para>
    /// The fixture is a brick in miniature: submesh 0 is a body the extraction must leave behind,
    /// submesh 1 is a symbol on its face, off-centre and half a unit wide, facing −z the way a real
    /// brick's does before the prefab's half turn puts it towards the camera (D-047).
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class SymbolMeshBuilderTests
    {
        /// <summary>Where the symbol sits on the brick's face: off to one side and well off the middle.</summary>
        private static readonly Vector3 SymbolCentre = new Vector3(0.3f, 0.25f, -0.5f);

        /// <summary>Half a unit across, so "one unit across" is a scale the test can actually see.</summary>
        private const float SymbolWidth = 0.5f;

        private Mesh brick;
        private Mesh built;

        [SetUp]
        public void SetUp()
        {
            brick = BuildBrick();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(brick);

            if (built != null)
            {
                Object.DestroyImmediate(built);
                built = null;
            }
        }

        [Test]
        public void Build_TakesTheSymbolAndLeavesTheBody()
        {
            built = Build();

            // Two triangles came out of submesh 1, and none of the body's four vertices came with them.
            Assert.That(built.subMeshCount, Is.EqualTo(1), "the symbol is one piece, not two");
            Assert.That(built.triangles.Length, Is.EqualTo(6), "submesh 1's two triangles, and only those");
            Assert.That(built.vertexCount, Is.EqualTo(4), "only the vertices those triangles use are carried over");
        }

        [Test]
        public void Build_RemapsEveryIndexIntoItsOwnVertices()
        {
            built = Build();

            int[] triangles = built.triangles;

            // The fault this guards is the quiet one: indices copied straight from the source still
            // address *something* in a shorter list, so the mesh exists and its shape is nonsense.
            foreach (int index in triangles)
            {
                Assert.That(index, Is.InRange(0, built.vertexCount - 1), "an index outside its own vertices");
            }

            Assert.That(
                new[] { triangles[0], triangles[1], triangles[2] },
                Is.Unique,
                "a triangle whose corners collapsed onto one vertex is a remap that lost its map");
        }

        [Test]
        public void Build_CentresTheSymbolOnTheOrigin()
        {
            built = Build();

            // A particle is drawn around its own position; a symbol still sitting on the brick's face
            // would be thrown from somewhere off to the side of every spark.
            Assert.That(built.bounds.center.magnitude, Is.LessThan(0.0001f), "the symbol is not on the origin");
        }

        [Test]
        public void Build_ScalesTheWidestSideToOneUnit()
        {
            built = Build();

            Vector3 size = built.bounds.size;
            float widest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));

            // So a particle size means the same thing for all twelve symbols, and survives a re-skin.
            Assert.That(widest, Is.EqualTo(1f).Within(0.0001f), "the symbol is not one unit across");
        }

        [Test]
        public void Build_TurnsTheSymbolToFaceTheCamera()
        {
            built = Build();

            // The Block prefab is rotated 180° about Y so its symbol faces the camera. A particle mesh
            // is handed no transform at all, so that half turn is baked in here — and the sign of x is
            // how you can see it happened: the symbol was authored to the +x side of the brick's face.
            float x = 0f;

            foreach (Vector3 vertex in built.vertices)
            {
                x += vertex.x;
            }

            Assert.That(
                Mathf.Sign(x / built.vertexCount),
                Is.EqualTo(-1f),
                "without the half turn every spark is a mirror image with its back to the camera");
        }

        [Test]
        public void Build_WithNoSecondSubMesh_IsRefusedAndSaysWhy()
        {
            var bodyOnly = new Mesh { name = "BodyOnly" };
            bodyOnly.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
            bodyOnly.SetTriangles(new[] { 0, 1, 2 }, 0);

            string error;
            Mesh result = SymbolMeshBuilder.Build(bodyOnly, "Symbol_None", out error);

            Assert.That(result, Is.Null, "a mesh with no symbol submesh has no symbol to give");
            Assert.That(error, Does.Contain("submesh"), "the complaint has to name what is missing: " + error);

            Object.DestroyImmediate(bodyOnly);
        }

        [Test]
        public void Build_WithNoMesh_IsRefusedAndSaysWhy()
        {
            string error;

            Assert.That(SymbolMeshBuilder.Build(null, "Symbol_None", out error), Is.Null);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
        }

        private Mesh Build()
        {
            string error;
            Mesh result = SymbolMeshBuilder.Build(brick, "Symbol_Test", out error);

            Assert.That(result, Is.Not.Null, error);
            return result;
        }

        /// <summary>
        /// A brick in miniature: a body quad at the back and a symbol quad on the face in front of it,
        /// in their own submeshes and sharing none of their vertices — which is what a mesh authored
        /// with two materials looks like.
        /// </summary>
        private static Mesh BuildBrick()
        {
            float half = SymbolWidth * 0.5f;

            var vertices = new[]
            {
                // The body: a unit quad centred on the brick's middle.
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),

                // The symbol: half a unit across, off to +x and up, standing on the brick's face.
                // Deliberately NOT symmetric — its top edge is pulled towards +x. A symmetric shape
                // could not test the half turn at all: recentring cancels the translation, so only the
                // shape's own lopsidedness still remembers which way round it was.
                SymbolCentre + new Vector3(-half, -half, 0f),
                SymbolCentre + new Vector3(half, -half, 0f),
                SymbolCentre + new Vector3(half, half, 0f),
                SymbolCentre + new Vector3(half * 0.4f, half, 0f),
            };

            var mesh = new Mesh { name = "Block_Test" };
            mesh.vertices = vertices;
            mesh.subMeshCount = 2;
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.SetTriangles(new[] { 4, 5, 6, 4, 6, 7 }, 1);
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
