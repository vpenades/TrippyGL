﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SixLabors.Fonts;
using SixLabors.Fonts.Unicode;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace TrippyGL.Fonts.Building
{
    /// <summary>
    /// An implementation of <see cref="IGlyphSource"/> that sources it's glyphs from
    /// a <see cref="SixLabors.Fonts"/> font.
    /// </summary>
    public sealed class FontGlyphSource : IGlyphSource
    {
        /// <summary>The DPI to use for drawing the glyphs.</summary>
        private const float DrawDpi = 96;

        /// <summary>The DPI to use for calculations.</summary>
        private const float CalcDpi = 72;

        /// <summary>The <see cref="Font"/> from which this <see cref="FontGlyphSource"/> gets glyph data.</summary>
        public readonly Font Font;

        /// <summary>The path collections that make up each character.</summary>
        private readonly IPathCollection[] glyphPaths;

        /// <summary>The colors of the paths that make up each character. Might be null.</summary>
        private readonly Color?[][] pathColors;

        /// <summary>Configuration for how glyphs should be rendered.</summary>
        public DrawingOptions DrawingOptions;

        /// <summary>The color with which to draw glyphs when no color is present. Default is <see cref="Color.White"/>.</summary>
        public Color DefaultGlyphColor = Color.White;

        /// <summary>The sizes for all characters.</summary>
        private readonly System.Drawing.Point[] glyphSizes;

        /// <summary>The render offsets for all characters.</summary>
        private readonly Vector2[] renderOffsets;

        /// <summary>Whether to include kerning if present in the font. Default is true.</summary>
        public bool IncludeKerningIfPresent = true;

        public char FirstChar { get; }

        public char LastChar { get; }

        public float Size => Font.Size;

        public string Name { get; }

        public float Ascender => Font.Size * (DrawDpi / CalcDpi) * Font.FontMetrics.Ascender / Font.FontMetrics.UnitsPerEm;

        public float Descender => Font.Size * (DrawDpi / CalcDpi) * Font.FontMetrics.Descender / Font.FontMetrics.UnitsPerEm;

        public float LineGap => Font.Size * (DrawDpi / CalcDpi) * Font.FontMetrics.LineGap / Font.FontMetrics.UnitsPerEm;

        public int CharCount => LastChar - FirstChar + 1;

        /// <summary>
        /// Creates a <see cref="FontGlyphSource"/> instance.
        /// </summary>
        public FontGlyphSource(Font font, string name, char firstChar = ' ', char lastChar = '~')
        {
            if (lastChar < firstChar)
                throw new ArgumentException(nameof(LastChar) + " can't be lower than " + nameof(firstChar));

            Font = font ?? throw new ArgumentNullException(nameof(Font));

            FirstChar = firstChar;
            LastChar = lastChar;
            Name = name;

            glyphPaths = CreatePaths(out pathColors, out glyphSizes, out renderOffsets);

            DrawingOptions = new DrawingOptions
            {
                ShapeOptions = { IntersectionRule = IntersectionRule.Nonzero },
            };
        }

        /// <summary>
        /// Creates a <see cref="FontGlyphSource"/> instance.
        /// </summary>
        public FontGlyphSource(Font font, char firstChar = ' ', char lastChar = '~')
            : this(font, font.FontMetrics.Description.FontNameInvariantCulture, firstChar, lastChar) { }

        /// <summary>
        /// Creates the <see cref="IPathCollection"/> for all the characters, also getting their colors,
        /// glyph sizes and render offsets.
        /// </summary>
        private IPathCollection[] CreatePaths(out Color?[][] colors, out System.Drawing.Point[] sizes, out Vector2[] offsets)
        {
            FontMetrics fontMetrics = Font.FontMetrics;
            //float glyphRenderY = Size / CalcDpi * fontMetrics.Ascender / fontMetrics.UnitsPerEm; // TODO: Decide if this needs to be handled differently now or what
            ColorGlyphRenderer glyphRenderer = new ColorGlyphRenderer();
            TextRenderer textRenderer = new TextRenderer(glyphRenderer);
            TextOptions textOpts = new TextOptions(Font) { Dpi = DrawDpi };

            Span<char> singleCharSpan = stackalloc char[1];

            IPathCollection[] paths = new IPathCollection[CharCount];
            sizes = new System.Drawing.Point[paths.Length];
            offsets = new Vector2[paths.Length];
            colors = null;

            for (int i = 0; i < paths.Length; i++)
            {
                char c = (char)(i + FirstChar);
                glyphRenderer.Reset();
                GlyphMetrics glyphMetrics = fontMetrics.GetGlyphMetrics(new CodePoint(c), ColorFontSupport.None).FirstOrDefault();
                if (glyphMetrics == null)
                    continue;

                singleCharSpan[0] = c;
                textRenderer.RenderText(singleCharSpan, textOpts);
                //glyphMetrics.RenderTo(glyphRenderer, Size, new Vector2(0, glyphRenderY), new Vector2(DrawDpi, DrawDpi), 0); // TODO: Save todo as above
                IPathCollection p = glyphRenderer.Paths;
                RectangleF bounds = p.Bounds;

                float area = bounds.Width * bounds.Height;
                if (float.IsFinite(area) && area != 0 && (c > char.MaxValue || !char.IsWhiteSpace(c)))
                {
                    paths[i] = p;
                    sizes[i] = new System.Drawing.Point((int)Math.Ceiling(bounds.Width), (int)Math.Ceiling(bounds.Height));
                    renderOffsets[i] = new Vector2(bounds.X, bounds.Y);
                }

                if (glyphRenderer.HasAnyPathColors())
                {
                    if (colors == null)
                        colors = new Color?[CharCount][];

                    colors[i] = glyphRenderer.PathColors;
                }
            }

            return paths;
        }

        public bool GetAdvances(out float[] advances)
        {
            FontMetrics fontMetrics = Font.FontMetrics;

            advances = null;
            float? adv = null;

            for (int i = FirstChar; i <= LastChar; i++)
            {
                GlyphMetrics inst = fontMetrics.GetGlyphMetrics(new CodePoint(i), ColorFontSupport.None).FirstOrDefault();
                if (inst == null)
                    continue;

                float iAdv = inst.AdvanceWidth * Font.Size * (DrawDpi / CalcDpi) / inst.UnitsPerEm;

                if (advances == null)
                {
                    if (adv == null)
                    {
                        adv = iAdv;
                    }
                    else if (iAdv != adv)
                    {
                        advances = new float[CharCount];
                        for (int c = 0; c < i - FirstChar; c++)
                            advances[c] = adv.Value;
                        advances[i - FirstChar] = iAdv;
                    }
                }
                else
                    advances[i - FirstChar] = iAdv;
            }

            if (adv == null || advances == null)
            {
                advances = new float[1] { adv.GetValueOrDefault() };
                return false;
            }

            return true;
        }

        public bool TryGetKerning(out Vector2[,] kerningOffsets)
        {
            FontMetrics fontMetrics = Font.FontMetrics;

            kerningOffsets = null;
            if (!IncludeKerningIfPresent)
                return false;

            for (int a = FirstChar; a <= LastChar; a++)
            {
                GlyphMetrics aMetrics = fontMetrics.GetGlyphMetrics(new CodePoint(a), ColorFontSupport.None).FirstOrDefault();
                if (aMetrics == null)
                    continue;

                for (int b = FirstChar; b <= LastChar; b++)
                {
                    //Vector2 offset = FontMetrics.GetOffset(FontMetrics.GetGlyph(b), aMetrics);
                    Vector2 offset = Vector2.Zero; // TODO: Find out how the fuck to get kerning THEY BETTER NOT HAVE REMOVED THIS AAAAAHHH
                    if (offset.X != 0 || offset.Y != 0)
                    {
                        if (kerningOffsets == null)
                            kerningOffsets = new Vector2[CharCount, CharCount];
                        kerningOffsets[a - FirstChar, b - FirstChar] = offset * Font.Size * (DrawDpi / CalcDpi) / fontMetrics.UnitsPerEm;
                    }
                }
            }

            return kerningOffsets != null;
        }

        public System.Drawing.Point GetGlyphSize(int charCode)
        {
            return glyphSizes[charCode - FirstChar];
        }

        public Vector2[] GetRenderOffsets()
        {
            return renderOffsets;
        }

        public void DrawGlyphToImage(int charCode, System.Drawing.Point location, Image<Rgba32> image)
        {
            int charIndex = charCode - FirstChar;
            IPathCollection paths = glyphPaths[charIndex];
            paths = paths.Translate(location.X - renderOffsets[charIndex].X, location.Y - renderOffsets[charIndex].Y);
            DrawColoredPaths(image, paths, pathColors?[charIndex]);
        }

        /// <summary>
        /// Draws a collection of paths with the given colors onto the image.
        /// </summary>
        private void DrawColoredPaths(Image<Rgba32> image, IPathCollection paths, Color?[] pathColors)
        {
            IEnumerator<IPath> pathEnumerator = paths.GetEnumerator();

            int i = 0;
            while (pathEnumerator.MoveNext())
            {
                IPath path = pathEnumerator.Current;
                Color color = (pathColors != null && i < pathColors.Length && pathColors[i].HasValue) ? pathColors[i].Value : DefaultGlyphColor;
                image.Mutate(x => x.Fill(DrawingOptions, color, path));
                i++;
            }
        }

        public override string ToString()
        {
            return string.Concat(Font.FontMetrics.Description.FontNameInvariantCulture ?? "Unnamed " + nameof(FontGlyphSource),
                " - ", CharCount.ToString(), " characters");
        }
    }
}
