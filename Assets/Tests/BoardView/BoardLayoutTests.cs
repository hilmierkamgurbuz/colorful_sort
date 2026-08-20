using System;
using System.Collections.Generic;
using ColorfulSort.View;
using NUnit.Framework;
using UnityEngine;

namespace ColorfulSort.View.Tests
{
    /// <summary>
    /// The layout is the one part of the view that can be wrong while looking plausible: a
    /// column half a cell too tall, a row pitch that drifts, a camera that fits the board on
    /// one aspect ratio and crops it on another. None of that shows up in a code review, so it
    /// is arithmetic here and it is pinned to the numbers the art pack actually ships —
    /// including Level 79's 12 columns of 4 cells, which is the shape this project has to hit.
    /// </summary>
    [TestFixture]
    public sealed class BoardLayoutTests
    {
        // The pack's own numbers: a 2-cell slot is 640×1664 px at 512 px per unit, bordered
        // (left, bottom, right, top) = (160, 832, 160, 320); the ice slot is 640×1984 with a
        // 1152 px bottom because its icicles hang below the base.
        private const float PixelsPerUnit = 512f;

        /// <summary>
        /// The shipped row limit, for the tests that are not about the limit itself. Wide enough that
        /// those keep asking what they asked before it existed.
        /// </summary>
        private const int RowLimit = 5;

        private readonly List<Texture2D> textures = new List<Texture2D>();
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<RenderTexture> targets = new List<RenderTexture>();

        /// <summary>
        /// A camera framed the way <c>BoardView.FrameCamera</c> frames one, so a projection test is
        /// measuring the real pipeline and not a camera nobody would ship. The render target is
        /// what gives it a deterministic pixel rect in an EditMode test.
        /// </summary>
        private Camera FramedCamera(Vector2 boardSize, float padding, float tiltDegrees, float planeDistance)
        {
            var target = new RenderTexture(1440, 2560, 0);
            targets.Add(target);

            var cameraObject = new GameObject("BoardCamera");
            objects.Add(cameraObject);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.targetTexture = target;
            camera.orthographicSize = BoardLayout.OrthographicSize(boardSize, camera.aspect, padding, 0f, 0f, tiltDegrees);

            Transform transform = cameraObject.transform;
            transform.rotation = Quaternion.Euler(tiltDegrees, 0f, 0f);

            // Back along its own ray, far enough to stand `planeDistance` away from the board in
            // depth. The view frames itself the same way, from the ray it is already on.
            transform.position = -transform.forward * (planeDistance / Mathf.Cos(tiltDegrees * Mathf.Deg2Rad));

            return camera;
        }

        private Sprite SlotSprite(int widthPixels, int heightPixels, Vector4 border)
        {
            return SlotSprite(widthPixels, heightPixels, border, PixelsPerUnit);
        }

        private Sprite SlotSprite(int widthPixels, int heightPixels, Vector4 border, float pixelsPerUnit)
        {
            var texture = new Texture2D(widthPixels, heightPixels, TextureFormat.Alpha8, false);
            textures.Add(texture);

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, widthPixels, heightPixels),
                new Vector2(0.5f, 0f),
                pixelsPerUnit,
                0,
                SpriteMeshType.FullRect,
                border);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Texture2D texture in textures)
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }

            foreach (GameObject go in objects)
            {
                UnityEngine.Object.DestroyImmediate(go);
            }

            foreach (RenderTexture target in targets)
            {
                UnityEngine.Object.DestroyImmediate(target);
            }

            textures.Clear();
            objects.Clear();
            targets.Clear();
        }

        [Test]
        public void Metrics_FromTheNormalSlotSprite_ReadCellsSkirtAndCrown()
        {
            // The pack's tray, on the one border rule: bottom is the skirt, top is the crown, and
            // the 1024 px between them is two cells (D-048). The numbers it produces are the ones
            // it produced when the bottom border folded a cell in — that is the point of the
            // re-statement, and this is the test that pins it.
            ColumnMetrics metrics = ColumnMetrics.FromSprite(SlotSprite(640, 1664, new Vector4(160f, 320f, 160f, 320f)));

            Assert.That(metrics.Width, Is.EqualTo(1.25f).Within(0.0001f));
            Assert.That(metrics.CellsInSprite, Is.EqualTo(2), "the band between the borders is two cells");
            Assert.That(metrics.TilesPerCell, Is.True, "so it repeats one cell at a time");
            Assert.That(metrics.Skirt, Is.EqualTo(0.625f).Within(0.0001f), "the stud seat below the first cell floor");
            Assert.That(metrics.Crown, Is.EqualTo(0.625f).Within(0.0001f));

            Assert.That(metrics.Height(2), Is.EqualTo(1664f / PixelsPerUnit).Within(0.0001f), "two cells is the sprite's own height");
            Assert.That(metrics.Height(4), Is.EqualTo(5.25f).Within(0.0001f), "Level 79's four-cell column");
            Assert.That(metrics.CellCentreY(0), Is.EqualTo(1.125f).Within(0.0001f));
            Assert.That(metrics.CellCentreY(3) - metrics.CellCentreY(2), Is.EqualTo(1f).Within(0.0001f), "cells step by exactly one unit");
        }

        [Test]
        public void Metrics_FromTheIceSlotSprite_HaveATallerSkirt()
        {
            ColumnMetrics metrics = ColumnMetrics.FromSprite(SlotSprite(640, 1984, new Vector4(160f, 640f, 160f, 320f)));

            Assert.That(metrics.CellsInSprite, Is.EqualTo(2));
            Assert.That(metrics.Skirt, Is.EqualTo(1.25f).Within(0.0001f), "the icicles hang below the base");
            Assert.That(metrics.Crown, Is.EqualTo(0.625f).Within(0.0001f));
            Assert.That(metrics.Height(2), Is.EqualTo(1984f / PixelsPerUnit).Within(0.0001f));
            Assert.That(metrics.CellCentreY(0), Is.EqualTo(1.75f).Within(0.0001f), "an ice column's first cell sits higher than a normal one's");
        }

        [Test]
        public void Metrics_FromTheHandDrawnTray_StretchInsteadOfTiling()
        {
            // The user's tray: 199×262 px at 160 px per unit, bordered (24, 46, 24, 28). Its skirt
            // is the Block_Base plate's height and its middle band is 188 px — 1.175 cells, which is
            // no number of cells at all, so the art stretches and the dividers draw the boundaries.
            ColumnMetrics metrics = ColumnMetrics.FromSprite(SlotSprite(199, 262, new Vector4(24f, 46f, 24f, 28f), 160f));

            Assert.That(metrics.Width, Is.EqualTo(199f / 160f).Within(0.0001f), "1.244 units, near enough the old 1.25 pitch that no level re-frames");
            Assert.That(metrics.Skirt, Is.EqualTo(46f / 160f).Within(0.0001f), "0.2875 — the base plate's measured height");
            Assert.That(metrics.CellsInSprite, Is.Zero, "the band is not a whole number of cells");
            Assert.That(metrics.TilesPerCell, Is.False);

            // The crown is the wave at the top of the tray, and it has to be shallow enough that the
            // highest brick's studs — which reach 0.045 above their cell's ceiling — end up inside
            // it rather than under it (D-051).
            Assert.That(metrics.Crown, Is.EqualTo(28f / 160f).Within(0.0001f));
            Assert.That(metrics.Crown, Is.GreaterThan(0.045f), "the wave still rises above the studs it frames");

            // A cell is still one unit, whatever the art: that is the invariant the tray had to
            // meet, not the other way round.
            Assert.That(metrics.CellCentreY(1) - metrics.CellCentreY(0), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(metrics.Height(4), Is.EqualTo(46f / 160f + 4f + 28f / 160f).Within(0.0001f));
        }

        [Test]
        public void Metrics_WithoutABorder_AreRefusedRatherThanGuessed()
        {
            Assert.That(() => ColumnMetrics.FromSprite(SlotSprite(640, 1664, Vector4.zero)), Throws.ArgumentException);
        }

        [Test]
        public void Metrics_WhenTheBordersLeaveNoCell_AreRefused()
        {
            // Borders that meet in the middle describe a tray with no cells in it. Reading a
            // negative band as "zero cells, stretch it" would draw a column of no height.
            Assert.That(
                () => ColumnMetrics.FromSprite(SlotSprite(199, 262, new Vector4(24f, 200f, 24f, 100f), 160f)),
                Throws.ArgumentException);
        }

        [Test]
        public void CellToGrid_NumbersRowByRowFromTheTop()
        {
            int row;
            int column;

            BoardLayout.CellToGrid(0, 3, out row, out column);
            Assert.That(new[] { row, column }, Is.EqualTo(new[] { 0, 0 }));

            BoardLayout.CellToGrid(2, 3, out row, out column);
            Assert.That(new[] { row, column }, Is.EqualTo(new[] { 0, 2 }));

            BoardLayout.CellToGrid(4, 3, out row, out column);
            Assert.That(new[] { row, column }, Is.EqualTo(new[] { 1, 1 }));

            Assert.That(BoardLayout.GridToCell(1, 1, 3), Is.EqualTo(4), "and back again");
        }

        [Test]
        public void BoardSize_CountsGapsBetweenColumnsNotAroundThem()
        {
            Vector2 size = BoardLayout.BoardSize(Dense(6), 3, 1.25f, 4.25f, 0.15f, 0.6f);

            Assert.That(size.x, Is.EqualTo(3f * 1.25f + 2f * 0.15f).Within(0.0001f));
            Assert.That(size.y, Is.EqualTo(2f * 4.25f + 0.6f).Within(0.0001f));
        }

        [Test]
        public void BoardSize_MeasuresTheOccupiedCellsNotTheDeclaredGrid()
        {
            // One column in the middle of a 3×4 grid: the board is one column big, so the
            // camera frames a column rather than a screenful of empty cells (D-026).
            Vector2 size = BoardLayout.BoardSize(new[] { 5 }, 4, 1.25f, 4.25f, 0.15f, 0.6f);

            Assert.That(size.x, Is.EqualTo(1.25f).Within(0.0001f));
            Assert.That(size.y, Is.EqualTo(4.25f).Within(0.0001f));
        }

        [Test]
        public void SlotBottomCentre_CentresTheBoardAndStacksRowsDownward()
        {
            const float Width = 1.25f;
            const float Height = 4.25f;
            const float ColumnGap = 0.15f;
            const float RowGap = 0.6f;

            int[] cells = Dense(6);

            Vector2 topLeft = BoardLayout.SlotBottomCentre(0, cells, 3, Width, Height, ColumnGap, RowGap);
            Vector2 topMiddle = BoardLayout.SlotBottomCentre(1, cells, 3, Width, Height, ColumnGap, RowGap);
            Vector2 topRight = BoardLayout.SlotBottomCentre(2, cells, 3, Width, Height, ColumnGap, RowGap);
            Vector2 bottomLeft = BoardLayout.SlotBottomCentre(3, cells, 3, Width, Height, ColumnGap, RowGap);

            Assert.That(topMiddle.x, Is.EqualTo(0f).Within(0.0001f), "an odd column count centres its middle column");
            Assert.That(topLeft.x, Is.EqualTo(-(Width + ColumnGap)).Within(0.0001f));
            Assert.That(topRight.x, Is.EqualTo(Width + ColumnGap).Within(0.0001f));
            Assert.That(topLeft.x, Is.EqualTo(-topRight.x).Within(0.0001f), "the board is symmetric about the origin");

            Vector2 size = BoardLayout.BoardSize(cells, 3, Width, Height, ColumnGap, RowGap);
            Assert.That(topLeft.y, Is.EqualTo(size.y * 0.5f - Height).Within(0.0001f), "row 0 hangs from the top edge");
            Assert.That(bottomLeft.y, Is.EqualTo(-size.y * 0.5f).Within(0.0001f), "the last row sits on the bottom edge");
            Assert.That(topLeft.y - bottomLeft.y, Is.EqualTo(Height + RowGap).Within(0.0001f), "one row pitch apart");
            Assert.That(bottomLeft.x, Is.EqualTo(topLeft.x).Within(0.0001f), "columns line up down the grid");
        }

        [Test]
        public void SlotBottomCentre_WithOneRow_CentresItVertically()
        {
            Vector2 only = BoardLayout.SlotBottomCentre(0, Dense(1), 1, 1.25f, 4.25f, 0f, 0f);

            Assert.That(only, Is.EqualTo(new Vector2(0f, -4.25f * 0.5f)));
        }

        [Test]
        public void SlotBottomCentre_CentresEachRowOnItsOwnSpan()
        {
            // Three over four, on a grid four wide: the short row reads as centred over the
            // long one rather than pushed left (D-034).
            const float Width = 1.25f;
            const float Height = 4.25f;
            const float ColumnGap = 0.15f;
            const float Pitch = Width + ColumnGap;

            int[] cells = { 0, 1, 2, 4, 5, 6, 7 };

            Vector2 topLeft = BoardLayout.SlotBottomCentre(0, cells, 4, Width, Height, ColumnGap, 0.6f);
            Vector2 topMiddle = BoardLayout.SlotBottomCentre(1, cells, 4, Width, Height, ColumnGap, 0.6f);
            Vector2 topRight = BoardLayout.SlotBottomCentre(2, cells, 4, Width, Height, ColumnGap, 0.6f);
            Vector2 bottomLeft = BoardLayout.SlotBottomCentre(3, cells, 4, Width, Height, ColumnGap, 0.6f);
            Vector2 bottomRight = BoardLayout.SlotBottomCentre(6, cells, 4, Width, Height, ColumnGap, 0.6f);

            Assert.That(topMiddle.x, Is.EqualTo(0f).Within(0.0001f), "the short row's middle column is on the centre line");
            Assert.That(topLeft.x, Is.EqualTo(-Pitch).Within(0.0001f));
            Assert.That(topRight.x, Is.EqualTo(Pitch).Within(0.0001f));
            Assert.That(bottomLeft.x, Is.EqualTo(-1.5f * Pitch).Within(0.0001f), "the four-wide row straddles the centre line");
            Assert.That(bottomRight.x, Is.EqualTo(1.5f * Pitch).Within(0.0001f));
            Assert.That(topLeft.x + topRight.x, Is.EqualTo(0f).Within(0.0001f), "both rows stay symmetric about the origin");
            Assert.That(bottomLeft.x + bottomRight.x, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void SlotBottomCentre_KeepsAHoleInsideARow()
        {
            // Two columns with the middle cell left empty: the hole was authored, so it keeps
            // its width instead of the two columns closing up.
            const float Width = 1.25f;
            const float ColumnGap = 0.15f;

            int[] cells = { 0, 2 };

            Vector2 left = BoardLayout.SlotBottomCentre(0, cells, 3, Width, 4.25f, ColumnGap, 0f);
            Vector2 right = BoardLayout.SlotBottomCentre(1, cells, 3, Width, 4.25f, ColumnGap, 0f);

            Assert.That(right.x - left.x, Is.EqualTo(2f * (Width + ColumnGap)).Within(0.0001f));
            Assert.That(left.x, Is.EqualTo(-right.x).Within(0.0001f));
        }

        [Test]
        public void OrthographicSize_FitsLevel79OnAPortraitPhone()
        {
            // Level 79: 12 columns of 4 cells, laid out 2 rows of 6.
            const float Width = 1.25f;
            const float ColumnGap = 0.15f;
            const float RowGap = 0.6f;
            const float Padding = 0.5f;
            const float PortraitAspect = 1440f / 2560f;

            float columnHeight = 0.625f + 4f + 0.625f;
            Vector2 size = BoardLayout.BoardSize(Dense(12), 6, Width, columnHeight, ColumnGap, RowGap);
            float orthographicSize = BoardLayout.OrthographicSize(size, PortraitAspect, Padding);

            float visibleHeight = orthographicSize * 2f;
            float visibleWidth = visibleHeight * PortraitAspect;

            Assert.That(visibleWidth, Is.GreaterThanOrEqualTo(size.x + 2f * Padding - 0.0001f), "the whole board is on screen horizontally");
            Assert.That(visibleHeight, Is.GreaterThanOrEqualTo(size.y + 2f * Padding - 0.0001f), "and vertically");
            Assert.That(visibleWidth, Is.EqualTo(size.x + 2f * Padding).Within(0.0001f), "a six-wide board on a portrait phone is width-bound, so width is the exact fit");
        }

        [Test]
        public void OrthographicSize_IsHeightBoundForATallNarrowBoard()
        {
            Vector2 size = BoardLayout.BoardSize(Dense(4), 1, 1.25f, 5.25f, 0f, 0.6f);
            float orthographicSize = BoardLayout.OrthographicSize(size, 1440f / 2560f, 0.5f);

            Assert.That(orthographicSize * 2f, Is.EqualTo(size.y + 1f).Within(0.0001f));
        }

        [Test]
        public void OrthographicSize_OnAWiderViewport_NeedsLessHeight()
        {
            Vector2 size = BoardLayout.BoardSize(Dense(12), 6, 1.25f, 5.25f, 0.15f, 0.6f);

            float portrait = BoardLayout.OrthographicSize(size, 1440f / 2560f, 0.5f);
            float tablet = BoardLayout.OrthographicSize(size, 3f / 4f, 0.5f);

            Assert.That(tablet, Is.LessThan(portrait), "the same board needs a smaller camera on a wider screen");
        }

        [Test]
        public void OrthographicSize_WithNoReserves_IsTheOldFraming()
        {
            Vector2 size = BoardLayout.BoardSize(Dense(4), 2, 1.25f, 5.25f, 0.15f, 0.6f);

            Assert.That(
                BoardLayout.OrthographicSize(size, 1440f / 2560f, 0.5f, 0f, 0f),
                Is.EqualTo(BoardLayout.OrthographicSize(size, 1440f / 2560f, 0.5f)).Within(0.0001f));
        }

        [Test]
        public void OrthographicSize_WithReservedBands_LeavesTheBoardClearOfThem()
        {
            // A tall board on a portrait phone, so the framing is height-bound and the reserves
            // are what decides it. The HUD takes the top 15%, the booster bar the bottom 18%.
            const float Padding = 0.5f;
            const float TopReserve = 0.15f;
            const float BottomReserve = 0.18f;
            const float PortraitAspect = 1440f / 2560f;

            Vector2 size = BoardLayout.BoardSize(Dense(4), 1, 1.25f, 5.25f, 0f, 0.6f);
            float orthographicSize = BoardLayout.OrthographicSize(size, PortraitAspect, Padding, TopReserve, BottomReserve);

            float visibleHeight = orthographicSize * 2f;
            float bandHeight = visibleHeight * (1f - TopReserve - BottomReserve);

            Assert.That(bandHeight, Is.GreaterThanOrEqualTo(size.y + 2f * Padding - 0.0001f),
                "the board plus its padding fits between the two reserved bands");
            Assert.That(orthographicSize, Is.GreaterThan(BoardLayout.OrthographicSize(size, PortraitAspect, Padding)),
                "sharing the screen with a HUD costs zoom");
        }

        [Test]
        public void OrthographicSize_WithReservedBands_DoesNotChangeAWidthBoundBoard()
        {
            // Reserves take height, never width: a board already zoomed out to fit horizontally
            // is unaffected, which is why a wide level does not shrink twice.
            Vector2 size = BoardLayout.BoardSize(Dense(12), 6, 1.25f, 2.25f, 0.15f, 0.6f);
            const float PortraitAspect = 1440f / 2560f;

            float withoutBands = BoardLayout.OrthographicSize(size, PortraitAspect, 0.5f);
            float withBands = BoardLayout.OrthographicSize(size, PortraitAspect, 0.5f, 0.05f, 0.05f);

            Assert.That(withBands, Is.EqualTo(withoutBands).Within(0.0001f));
        }

        [Test]
        public void OrthographicSize_WhenTheBandsLeaveNoHeight_Throws()
        {
            Vector2 size = BoardLayout.BoardSize(Dense(2), 2, 1.25f, 3.25f, 0f, 0f);

            Assert.That(
                () => BoardLayout.OrthographicSize(size, 0.5625f, 0.5f, 0.5f, 0.5f),
                Throws.TypeOf<ArgumentOutOfRangeException>(),
                "a camera size for a board with nowhere to go would crop it silently");
            Assert.That(
                () => BoardLayout.OrthographicSize(size, 0.5625f, 0.5f, -0.1f, 0.2f),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void OrthographicSize_UnderATiltedCamera_NeedsLessHeight()
        {
            // A tall board on a portrait phone, so the framing is height-bound and the tilt decides
            // it. The board stays upright and the camera leans, so what it projects onto the
            // screen's vertical axis is the cosine of the angle — the camera zooms in, not out.
            const float Padding = 0.5f;
            const float PortraitAspect = 1440f / 2560f;

            Vector2 size = BoardLayout.BoardSize(Dense(4), 1, 1.25f, 5.25f, 0f, 0.6f);

            float straight = BoardLayout.OrthographicSize(size, PortraitAspect, Padding, 0f, 0f, 0f);
            float tilted = BoardLayout.OrthographicSize(size, PortraitAspect, Padding, 0f, 0f, 25f);

            Assert.That(tilted, Is.LessThan(straight));
            Assert.That(tilted, Is.EqualTo(size.y * Mathf.Cos(25f * Mathf.Deg2Rad) * 0.5f + Padding).Within(0.0001f));
            Assert.That(straight, Is.EqualTo(BoardLayout.OrthographicSize(size, PortraitAspect, Padding, 0f, 0f)).Within(0.0001f),
                "no tilt is the same framing as before the camera could lean");
        }

        [Test]
        public void OrthographicSize_UnderATiltedCamera_LeavesAWidthBoundBoardAlone()
        {
            // A tilt about the camera's X axis costs no width, so a board already zoomed out to fit
            // horizontally does not move — which is why a wide level does not shrink twice.
            Vector2 size = BoardLayout.BoardSize(Dense(12), 6, 1.25f, 2.25f, 0.15f, 0.6f);
            const float PortraitAspect = 1440f / 2560f;

            Assert.That(
                BoardLayout.OrthographicSize(size, PortraitAspect, 0.5f, 0f, 0f, 25f),
                Is.EqualTo(BoardLayout.OrthographicSize(size, PortraitAspect, 0.5f, 0f, 0f, 0f)).Within(0.0001f));
        }

        [Test]
        public void ATiltOfNinetyDegrees_IsRefusedRatherThanFlattened()
        {
            Vector2 size = BoardLayout.BoardSize(Dense(2), 2, 1.25f, 3.25f, 0f, 0f);

            Assert.That(() => BoardLayout.OrthographicSize(size, 0.5625f, 0.5f, 0f, 0f, 90f), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                BoardLayout.OrthographicSize(size, 0.5625f, 0.5f, 0f, 0f, -25f),
                Is.EqualTo(BoardLayout.OrthographicSize(size, 0.5625f, 0.5f, 0f, 0f, 25f)).Within(0.0001f),
                "leaning up or down costs the same height");
        }

        [Test]
        public void FramingTheSameBoardTwice_LeavesTheCameraWhereItWas()
        {
            // The framing reads how far the camera stands along its own view ray and then rebuilds
            // the position from it. That has to be a fixed point, or every resync — every booster
            // press — would nudge the camera and the board would creep. Measured as a depth in z
            // instead, the reserved band's own z component is what does the nudging.
            const float PlaneDistance = 10f;
            const float Tilt = 25f;

            Vector2 board = BoardLayout.BoardSize(Dense(4), 2, 1.25f, 4.25f, 0.15f, 0.6f);
            Camera camera = FramedCamera(board, 0.5f, Tilt, PlaneDistance);
            Transform transform = camera.transform;

            var centre = Vector3.zero;
            float band = BoardLayout.CameraCentreOffset(camera.orthographicSize, 0.15f, 0.18f);

            for (int pass = 0; pass < 3; pass++)
            {
                float ray = Vector3.Dot(centre - transform.position, transform.forward);
                transform.position = centre + transform.up * band - transform.forward * ray;
            }

            float finalRay = Vector3.Dot(centre - transform.position, transform.forward);
            Assert.That(finalRay, Is.EqualTo(PlaneDistance / Mathf.Cos(Tilt * Mathf.Deg2Rad)).Within(0.0001f),
                "three framings later, the camera is still the same distance along its ray");
        }

        [Test]
        public void EverySlotStillReadsBackAsItself_StraightOnAndTilted()
        {
            // The reason the tap became a ray against the board's plane. Each slot's centre is
            // projected to the screen and read back the way the view reads it; at 25° the naive
            // near-plane answer drifts a whole row, and this is what would catch it.
            const int Columns = 3;
            const float Width = 1.25f;
            const float Height = 4.25f;
            const float ColumnGap = 0.15f;
            const float RowGap = 0.6f;
            const float Padding = 0.5f;
            const float PlaneDistance = 10f;

            int[] cells = Dense(6);
            Vector2 board = BoardLayout.BoardSize(cells, Columns, Width, Height, ColumnGap, RowGap);
            var plane = new Plane(Vector3.forward, Vector3.zero);

            foreach (float tilt in new[] { 0f, 25f })
            {
                Camera camera = FramedCamera(board, Padding, tilt, PlaneDistance);

                for (int slot = 0; slot < cells.Length; slot++)
                {
                    Vector2 bottomCentre = BoardLayout.SlotBottomCentre(slot, cells, Columns, Width, Height, ColumnGap, RowGap);
                    var centre = new Vector3(bottomCentre.x, bottomCentre.y + Height * 0.5f, 0f);

                    Vector3 screen = camera.WorldToScreenPoint(centre);
                    Ray ray = camera.ScreenPointToRay(screen);

                    float distance;
                    Assert.That(plane.Raycast(ray, out distance), Is.True, "the press ray meets the board");

                    Vector3 hit = ray.GetPoint(distance);
                    int read = BoardLayout.SlotAt(
                        new Vector2(hit.x, hit.y), cells, Columns, Width, Height, ColumnGap, RowGap);

                    Assert.That(read, Is.EqualTo(slot), "slot " + slot + " at a tilt of " + tilt + "°");
                }
            }
        }

        [Test]
        public void CameraCentreOffset_PutsTheBoardInTheMiddleOfTheBand()
        {
            const float Size = 10f;
            const float TopReserve = 0.2f;
            const float BottomReserve = 0.1f;

            float offset = BoardLayout.CameraCentreOffset(Size, TopReserve, BottomReserve);

            // Where the board's centre lands on screen, 0 at the bottom edge and 1 at the top:
            // the camera spans centre ± Size, and the board sits `offset` below the camera.
            float onScreen = (-offset) / (2f * Size) + 0.5f;
            float bandCentre = BottomReserve + (1f - TopReserve - BottomReserve) * 0.5f;

            Assert.That(onScreen, Is.EqualTo(bandCentre).Within(0.0001f));
            Assert.That(offset, Is.GreaterThan(0f), "a top-heavy HUD draws the board lower");
        }

        [Test]
        public void CameraCentreOffset_WithEqualBands_IsZero()
        {
            Assert.That(BoardLayout.CameraCentreOffset(8f, 0.2f, 0.2f), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(BoardLayout.CameraCentreOffset(8f, 0f, 0f), Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void PlaceColumns_KeepsWhatTheLevelAuthored()
        {
            int[] authored = { 0, 2, 5 };
            int rows;

            int[] placement = BoardLayout.PlaceColumns(authored, 3, 3, 2, RowLimit, out rows);

            Assert.That(placement, Is.EqualTo(authored));
            Assert.That(rows, Is.EqualTo(2));
        }

        [Test]
        public void PlaceColumns_GivesTheFirstAddedColumnTheTopRow()
        {
            // A 2×3 grid with columns in cells 0, 2 and 5 — two above, one below. The added one goes
            // to the TOP row even though the bottom is emptier, because the rows take it in turn and
            // the turn starts at the top (D-084). This is the case that separates taking turns from
            // choosing the emptiest row, which is what it used to do.
            int[] authored = { 0, 2, 5 };
            int rows;

            int[] placement = BoardLayout.PlaceColumns(authored, 4, 3, 2, RowLimit, out rows);

            Assert.That(placement[3], Is.EqualTo(1), "the turn begins at the top row, however full it is");
            Assert.That(rows, Is.EqualTo(2), "both rows had room, so the grid did not have to grow");
        }

        [Test]
        public void PlaceColumns_AlternatesOneRowAtATime()
        {
            // One to the top, one to the row below, round again.
            int[] authored = { 0, 4 };
            int rows;

            int[] placement = BoardLayout.PlaceColumns(authored, 6, 4, 2, RowLimit, out rows);

            Assert.That(placement[2], Is.EqualTo(1), "the first added column takes the top row");
            Assert.That(placement[3], Is.EqualTo(5), "the second takes the row below");
            Assert.That(placement[4], Is.EqualTo(2), "and the turn comes back round to the top");
            Assert.That(placement[5], Is.EqualTo(6), "and down again");
            Assert.That(rows, Is.EqualTo(2));
        }

        [Test]
        public void PlaceColumns_SkipsAFullRowAndDoesNotOweItATurn()
        {
            // The sequence the rule was written from: a 5-wide grid with two above and four below,
            // and five columns added. The bottom row fills on the second addition and is stepped over
            // from then on — so the top row takes two in a row, which is exactly the difference
            // between taking turns and counting whose turn was missed (D-084).
            int[] authored = { 0, 1, 5, 6, 7, 8 };
            int rows;

            int[] placement = BoardLayout.PlaceColumns(authored, 11, 5, 2, 5, out rows);

            Assert.That(placement[6], Is.EqualTo(2), "top row: 2 -> 3");
            Assert.That(placement[7], Is.EqualTo(9), "bottom row: 4 -> 5, now full");
            Assert.That(placement[8], Is.EqualTo(3), "back to the top: 3 -> 4");
            Assert.That(placement[9], Is.EqualTo(4), "the full row is skipped, so the top takes it again: 4 -> 5");
            Assert.That(rows, Is.EqualTo(3), "both rows full, so the board grew one");
            Assert.That(placement[10], Is.EqualTo(10), "and the new row starts at its leftmost cell");
        }

        [Test]
        public void PlaceColumns_WhenEveryCellIsTaken_GrowsARow()
        {
            int[] authored = { 0, 1, 2, 3 };
            int rows;

            int[] placement = BoardLayout.PlaceColumns(authored, 5, 4, 1, RowLimit, out rows);

            Assert.That(rows, Is.EqualTo(2));
            Assert.That(placement[4], Is.EqualTo(4), "the new column starts the new row");
        }

        [Test]
        public void PlaceColumns_FillsTheTopRowToTheLimit_ThenTheOneBelowIt()
        {
            // The single-row case of the turn rule (D-084), and the reason it needs no clause of its
            // own: a board with one row has nothing to alternate with, so every turn comes back to
            // the same row until it is full and the sixth column starts the one below. Five wide, one
            // authored column, five added.
            int[] authored = { 0 };
            int rows;

            int[] placement = BoardLayout.PlaceColumns(authored, 6, 5, 1, 5, out rows);

            Assert.That(placement, Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5 }));
            Assert.That(rows, Is.EqualTo(2), "the sixth column needed a row of its own");
        }

        [Test]
        public void PlaceColumns_StopsARowAtTheLimit_EvenWhereTheGridHasRoom()
        {
            // Six cells of room in the top row, but the limit is four: the fifth added column goes
            // below rather than making a row nobody can read on a phone.
            int[] authored = { 0 };
            int rows;

            int[] placement = BoardLayout.PlaceColumns(authored, 5, 6, 1, 4, out rows);

            Assert.That(placement, Is.EqualTo(new[] { 0, 1, 2, 3, 6 }));
            Assert.That(rows, Is.EqualTo(2));
        }

        [Test]
        public void PlaceColumns_FillsTheSecondRowBeforeStartingAThird()
        {
            // Two authored rows, both short of the limit: the added columns finish the top row, then
            // the second, and only then does the board grow. This is the case the user described —
            // one goes up, the next goes down.
            int[] authored = { 0, 1, 5, 6 };
            int rows;

            int[] placement = BoardLayout.PlaceColumns(authored, 6, 5, 2, 3, out rows);

            Assert.That(placement[4], Is.EqualTo(2), "the fifth column finishes the top row");
            Assert.That(placement[5], Is.EqualTo(7), "the sixth goes into the row below");
            Assert.That(rows, Is.EqualTo(2), "neither of them needed a new row");
        }

        [Test]
        public void PlaceColumns_WithEveryRowAtTheLimit_StartsANewRow()
        {
            int[] authored = { 0, 1, 5, 6 };
            int rows;

            int[] placement = BoardLayout.PlaceColumns(authored, 5, 5, 2, 2, out rows);

            Assert.That(placement[4], Is.EqualTo(10), "both rows are as wide as they may be");
            Assert.That(rows, Is.EqualTo(3));
        }

        [Test]
        public void PlaceColumns_AfterAnAddedColumnIsUndone_IsTheAuthoredShapeAgain()
        {
            // The defect this function exists to prevent: the add-column booster grew the grid,
            // its undo took the column away, and the board went on reserving the cell — and the
            // row it had started — for a column that was no longer there.
            int[] authored = { 0, 1, 2, 3 };
            int grownRows;
            int shrunkRows;

            int[] grown = BoardLayout.PlaceColumns(authored, 5, 4, 1, RowLimit, out grownRows);
            int[] shrunk = BoardLayout.PlaceColumns(authored, 4, 4, 1, RowLimit, out shrunkRows);

            Assert.That(grown.Length, Is.EqualTo(5));
            Assert.That(grownRows, Is.EqualTo(2));

            Assert.That(shrunk, Is.EqualTo(authored), "the placement is the level's again");
            Assert.That(shrunkRows, Is.EqualTo(1), "and so is the number of rows");
        }

        [Test]
        public void PlaceColumns_RefusesACellOutsideTheAuthoredGrid()
        {
            int rows;

            Assert.That(
                () => BoardLayout.PlaceColumns(new[] { 0, 9 }, 2, 3, 2, RowLimit, out rows),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => BoardLayout.PlaceColumns(new[] { 0, 1 }, 0, 3, 2, RowLimit, out rows),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => BoardLayout.PlaceColumns(new[] { 0, 1 }, 2, 3, 0, RowLimit, out rows),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void SlotAt_IsTheInverseOfSlotBottomCentre()
        {
            // Level 79's shape: 2 rows of 6, four-cell columns.
            const int Columns = 6;
            const float Width = 1.25f;
            const float Height = 5.25f;
            const float ColumnGap = 0.15f;
            const float RowGap = 0.6f;

            int[] cells = Dense(12);

            for (int slot = 0; slot < cells.Length; slot++)
            {
                Vector2 bottomCentre = BoardLayout.SlotBottomCentre(slot, cells, Columns, Width, Height, ColumnGap, RowGap);
                Vector2 middle = bottomCentre + new Vector2(0f, Height * 0.5f);

                Assert.That(
                    BoardLayout.SlotAt(middle, cells, Columns, Width, Height, ColumnGap, RowGap),
                    Is.EqualTo(slot),
                    "a tap in the middle of slot " + slot + " has to find slot " + slot);
            }
        }

        [Test]
        public void SlotAt_IsTheInverseOfSlotBottomCentre_OnARaggedBoard()
        {
            // Three over four: the rows have different spans and different centres, which is
            // exactly where a hit test drifts away from the layout it is supposed to invert.
            const float Width = 1.25f;
            const float Height = 5.25f;
            const float ColumnGap = 0.15f;
            const float RowGap = 0.6f;

            int[] cells = { 0, 1, 2, 4, 5, 6, 7 };

            for (int slot = 0; slot < cells.Length; slot++)
            {
                Vector2 bottomCentre = BoardLayout.SlotBottomCentre(slot, cells, 4, Width, Height, ColumnGap, RowGap);
                Vector2 middle = bottomCentre + new Vector2(0f, Height * 0.5f);

                Assert.That(
                    BoardLayout.SlotAt(middle, cells, 4, Width, Height, ColumnGap, RowGap),
                    Is.EqualTo(slot),
                    "a tap in the middle of slot " + slot + " has to find slot " + slot);
            }
        }

        [Test]
        public void SlotAt_LeavesNoDeadLaneBetweenColumns()
        {
            const float Width = 1.25f;
            const float ColumnGap = 0.15f;

            int[] cells = Dense(3);

            // The seam between column 0 and column 1, and a hair either side of it.
            float seam = BoardLayout.SlotBottomCentre(0, cells, 3, Width, 4.25f, ColumnGap, 0f).x + (Width + ColumnGap) * 0.5f;

            Assert.That(BoardLayout.SlotAt(new Vector2(seam - 0.01f, 0f), cells, 3, Width, 4.25f, ColumnGap, 0f), Is.EqualTo(0));
            Assert.That(BoardLayout.SlotAt(new Vector2(seam + 0.01f, 0f), cells, 3, Width, 4.25f, ColumnGap, 0f), Is.EqualTo(1));
            Assert.That(BoardLayout.SlotAt(new Vector2(seam, 0f), cells, 3, Width, 4.25f, ColumnGap, 0f), Is.EqualTo(1),
                "the seam itself belongs to the column on its right — arbitrary, but decided");
        }

        [Test]
        public void SlotAt_OnACellNobodyStandsIn_FindsNothing()
        {
            // The hole in {0, _, 2}: an authored gap is a gap, not the nearest column.
            int[] cells = { 0, 2 };

            Assert.That(
                BoardLayout.SlotAt(Vector2.zero, cells, 3, 1.25f, 4.25f, 0.15f, 0f),
                Is.EqualTo(BoardLayout.NoSlot));
        }

        [Test]
        public void SlotAt_OnAnEmptyRow_FindsNothing()
        {
            // Rows 0 and 2 are used and row 1 is deliberately empty: it is vertical space the
            // level asked for, so it keeps its height and swallows no tap.
            int[] cells = { 0, 1, 2, 6, 7, 8 };

            Assert.That(
                BoardLayout.SlotAt(Vector2.zero, cells, 3, 1.25f, 4.25f, 0.15f, 0.6f),
                Is.EqualTo(BoardLayout.NoSlot));
        }

        [Test]
        public void SlotAt_OffTheBoard_FindsNothing()
        {
            const int Columns = 3;
            const float Width = 1.25f;
            const float Height = 4.25f;

            int[] cells = Dense(6);
            Vector2 size = BoardLayout.BoardSize(cells, Columns, Width, Height, 0.15f, 0.6f);

            Assert.That(BoardLayout.SlotAt(new Vector2(size.x, 0f), cells, Columns, Width, Height, 0.15f, 0.6f), Is.EqualTo(BoardLayout.NoSlot));
            Assert.That(BoardLayout.SlotAt(new Vector2(0f, size.y), cells, Columns, Width, Height, 0.15f, 0.6f), Is.EqualTo(BoardLayout.NoSlot));
            Assert.That(BoardLayout.SlotAt(new Vector2(0f, -size.y), cells, Columns, Width, Height, 0.15f, 0.6f), Is.EqualTo(BoardLayout.NoSlot));
        }

        [Test]
        public void SlotAt_JustInsideTheBoardEdge_FindsTheEdgeColumn()
        {
            const int Columns = 3;
            const float Width = 1.25f;
            const float Height = 4.25f;
            const float ColumnGap = 0.15f;
            const float RowGap = 0.6f;

            int[] cells = Dense(6);
            Vector2 size = BoardLayout.BoardSize(cells, Columns, Width, Height, ColumnGap, RowGap);
            float insideX = size.x * 0.5f - 0.01f;
            float insideY = size.y * 0.5f - 0.01f;

            Assert.That(BoardLayout.SlotAt(new Vector2(-insideX, insideY), cells, Columns, Width, Height, ColumnGap, RowGap), Is.EqualTo(0));
            Assert.That(BoardLayout.SlotAt(new Vector2(insideX, -insideY), cells, Columns, Width, Height, ColumnGap, RowGap), Is.EqualTo(5));
        }

        [Test]
        public void Layout_RefusesShapesThatCannotExist()
        {
            Assert.That(() => BoardLayout.BoardSize(new int[0], 3, 1f, 1f, 0f, 0f), Throws.TypeOf<ArgumentException>());
            Assert.That(() => BoardLayout.BoardSize(Dense(6), 0, 1f, 1f, 0f, 0f), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => BoardLayout.BoardSize(Dense(6), 3, 1f, 1f, -0.1f, 0f), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => BoardLayout.BoardSize(new[] { -1 }, 3, 1f, 1f, 0f, 0f), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => BoardLayout.SlotBottomCentre(6, Dense(6), 3, 1f, 1f, 0f, 0f), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => BoardLayout.OrthographicSize(new Vector2(1f, 1f), 0f, 0f), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => BoardLayout.OrthographicSize(new Vector2(1f, 1f), 0.5f, -1f), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        /// <summary>
        /// The plain fill — row by row, left to right, no holes — which is what a level with
        /// no authored placement means (D-033).
        /// </summary>
        private static int[] Dense(int count)
        {
            var cells = new int[count];

            for (int slot = 0; slot < count; slot++)
            {
                cells[slot] = slot;
            }

            return cells;
        }
    }
}
