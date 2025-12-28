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
using LoneEftDmaRadar.Web.TarkovDev.Data;
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

            static string MakeRow(string grp, string name, string hands, string secure, string value, string dist)
            {
                // Column widths: Grp (4), Name (7), Hands (12), Secure (8), Value (6), Dist (5)
                const int W_GRP = 4, W_NAME = 7, W_HANDS = 12, W_SECURE = 8, W_VALUE = 6, W_DIST = 5;
                const int len = W_GRP + W_NAME + W_HANDS + W_SECURE + W_VALUE + W_DIST;

                return string.Create(len, (grp, name, hands, secure, value, dist), static (span, cols) =>
                {
                    int pos = 0;
                    WriteAligned(span, ref pos, cols.grp, W_GRP);
                    WriteAligned(span, ref pos, cols.name, W_NAME);
                    WriteAligned(span, ref pos, cols.hands, W_HANDS);
                    WriteAligned(span, ref pos, cols.secure, W_SECURE);
                    WriteAligned(span, ref pos, cols.value, W_VALUE);
                    WriteAligned(span, ref pos, cols.dist, W_DIST);
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
                .OrderBy(p => Vector3.DistanceSquared(localPos, p.Position))
                .ToPooledList();

            // Setup Frame and Draw Header
            var font = SKFonts.InfoWidgetFont;
            float pad = 2.5f * ScaleFactor;
            float maxLength = 0f;
            var drawPt = new SKPoint(
                ClientRectangle.Left + pad,
                ClientRectangle.Top + font.Spacing / 2 + pad);

            string header = MakeRow("Grp", "Name", "In Hands", "Secure", "Value", "Dist");

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
                string name = Truncate(player.Name ?? "--", 8);
                string grp = player.GroupID != -1 ? Truncate(player.GroupID.ToString(), 4) : "--";
                string hands = "--";
                string secure = "--";
                string value = "--";
                string dist = "--";

                if (player is ObservedPlayer obs)
                {
                    hands = Truncate(obs.Equipment?.InHands?.ShortName ?? "--", 15);
                    secure = Truncate(obs.Equipment?.SecuredContainer?.ShortName ?? "--", 8);
                    value = Truncate(Utilities.FormatNumberKM(obs.Equipment?.Value ?? 0), 6);
                    dist = Truncate(((int)Vector3.Distance(player.Position, localPos)).ToString(), 6);
                }

                string line = MakeRow(grp, name, hands, secure, value, dist);

                canvas.DrawText(line,
                    drawPt,
                    SKTextAlign.Left,
                    font,
                    GetTextPaint(player));
                drawPt.Offset(0, font.Spacing);
            }
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
                return value;
            return value.Substring(0, maxLength);
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