﻿/*
 * Lone EFT DMA Radar
 * Brought to you by Lone (Lone DMA)
 * 
MIT License

Copyright (c) 2025 Lone DMA

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
 *
*/

using Collections.Pooled;
using LoneEftDmaRadar.Misc;
using LoneEftDmaRadar.Tarkov.GameWorld.Player;
using LoneEftDmaRadar.Tarkov.GameWorld.Player.Helpers;
using SkiaSharp.Views.WPF;

namespace LoneEftDmaRadar.UI.Skia
{
    public sealed class PlayerInfoWidget : AbstractSKWidget
    {
        /// <summary>
        /// Constructs a Player Info Overlay.
        /// </summary>
        public PlayerInfoWidget(SKGLElement parent, SKRect location, bool minimized, float scale)
            : base(parent, "Player Info", new SKPoint(location.Left, location.Top),
                new SKSize(location.Width, location.Height), scale, false)
        {
            Minimized = minimized;
            SetScaleFactor(scale);
        }


        public void Draw(SKCanvas canvas, AbstractPlayer localPlayer, IEnumerable<AbstractPlayer> players)
        {
            if (Minimized)
            {
                Draw(canvas);
                return;
            }

            static string MakeRow(string c1, string c2, string c3)
            {
                // known widths: Name (10), Group (4), Value (10)
                const int W1 = 10, W2 = 4, W3 = 10;
                const int len = W1 + W2 + W3;

                return string.Create(len, (c1, c2, c3), static (span, cols) =>
                {
                    int pos = 0;
                    WriteAligned(span, ref pos, cols.c1, W1);
                    WriteAligned(span, ref pos, cols.c2, W2);
                    WriteAligned(span, ref pos, cols.c3, W3);
                });
            }

            static void WriteAligned(Span<char> span, ref int pos, string value, int width)
            {
                int padding = width - value.Length;
                if (padding < 0) padding = 0;

                // write the value left-aligned
                value.AsSpan(0, Math.Min(value.Length, width))
                     .CopyTo(span.Slice(pos));

                // pad the rest with spaces
                span.Slice(pos + value.Length, padding).Fill(' ');

                pos += width;
            }

            // Sort & filter
            var localPos = localPlayer.Position;
            using var filteredPlayers = players
                .Where(p => p.IsHumanHostileActive)
                .OrderBy(p => Vector3.Distance(localPos, p.Position))
                .ToPooledList();

            // Setup Frame and Draw Header
            var font = SKFonts.InfoWidgetFont;
            float pad = 2.5f * ScaleFactor;
            float maxLength = 0f;
            var drawPt = new SKPoint(
                ClientRectangle.Left + pad,
                ClientRectangle.Top + font.Spacing / 2 + pad);

            string header = MakeRow("Name", "Grp", "Value");

            var len = font.MeasureText(header);
            if (len > maxLength) maxLength = len;

            Size = new SKSize(maxLength + pad, (1 + filteredPlayers.Count) * font.Spacing); // 1 extra for header
            Draw(canvas); // Background/frame

            canvas.DrawText(header,
                drawPt,
                SKTextAlign.Left,
                font,
                SKPaints.TextPlayersOverlay);
            drawPt.Offset(0, font.Spacing);

            foreach (var player in filteredPlayers)
            {
                string name = player.Name;
                string grp = player.GroupID != -1 ? player.GroupID.ToString() : "--";
                string value = "--";

                if (player is ObservedPlayer obs)
                {
                    value = Utilities.FormatNumberKM(obs.Equipment.Value);
                }

                string line = MakeRow(name, grp, value);

                canvas.DrawText(line,
                    drawPt,
                    SKTextAlign.Left,
                    font,
                    GetTextPaint(player));
                drawPt.Offset(0, font.Spacing);
            }
        }

        private static SKPaint GetTextPaint(AbstractPlayer player)
        {
            if (player.IsFocused)
                return SKPaints.TextPlayersOverlayFocused;
            switch (player.Type)
            {
                case PlayerType.PMC:
                    return SKPaints.TextPlayersOverlayPMC;
                case PlayerType.PScav:
                    return SKPaints.TextPlayersOverlayPScav;
                default:
                    return SKPaints.TextPlayersOverlay;
            }
        }


        public override void SetScaleFactor(float newScale)
        {
            base.SetScaleFactor(newScale);
        }
    }
}