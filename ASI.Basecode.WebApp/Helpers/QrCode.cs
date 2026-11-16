using System.Collections.Generic;
using System.Text;
using QRCoder;

namespace ASI.Basecode.WebApp.Helpers
{
    /// <summary>
    /// Real, scannable QR symbols for member passes.
    ///
    /// A pass encodes nothing but the student number — "2022-00101" — because
    /// that is exactly what sp_Attendance_TimeIn looks a student up by. Keeping
    /// the payload to the number itself means a phone camera, a hardware wedge
    /// scanner, and an officer typing all produce the same input, so the scanned
    /// and typed paths at the door cannot diverge.
    /// </summary>
    public static class QrCode
    {
        /// <summary>
        /// Error correction level. Q (25%) survives a pass that has been folded
        /// into a wallet or shown on a cracked phone screen.
        /// </summary>
        private const QRCodeGenerator.ECCLevel Ecc = QRCodeGenerator.ECCLevel.Q;

        /// <summary>A symbol as an SVG path, with the module count to size the viewBox by.</summary>
        public record Symbol(string Path, int Modules);

        /// <summary>
        /// Renders the symbol as one SVG path covering every dark module. Inline
        /// SVG rather than an image request: the pass has to print, and it has to
        /// render without a round trip.
        /// </summary>
        public static Symbol Render(string content)
        {
            var matrix = Matrix(content);
            var modules = matrix.Count;
            var path = new StringBuilder();

            for (var y = 0; y < modules; y++)
            {
                for (var x = 0; x < modules; x++)
                {
                    if (matrix[y][x])
                    {
                        path.Append($"M{x} {y}h1v1h-1z");
                    }
                }
            }

            return new Symbol(path.ToString(), modules);
        }

        /// <summary>The same symbol as a PNG, for saving to a phone or printing.</summary>
        public static byte[] Png(string content, int pixelsPerModule = 10)
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(Payload(content), Ecc);

            return new PngByteQRCode(data).GetGraphic(pixelsPerModule);
        }

        /// <summary>
        /// The module grid, trimmed of the four-module quiet zone QRCoder adds —
        /// the SVG viewBox puts the margin back so the path starts at 0,0.
        /// </summary>
        private static List<bool[]> Matrix(string content)
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(Payload(content), Ecc);

            var raw = data.ModuleMatrix;
            const int quiet = 4;
            var size = raw.Count - (quiet * 2);
            var rows = new List<bool[]>(size);

            for (var y = 0; y < size; y++)
            {
                var row = new bool[size];
                for (var x = 0; x < size; x++)
                {
                    row[x] = raw[y + quiet][x + quiet];
                }
                rows.Add(row);
            }

            return rows;
        }

        /// <summary>QRCoder rejects an empty payload, and a blank pass is a bug anyway.</summary>
        private static string Payload(string content) =>
            string.IsNullOrWhiteSpace(content) ? "0000-00000" : content.Trim();
    }
}
