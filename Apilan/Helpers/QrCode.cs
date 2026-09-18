using System.Text;
using QRCoder;

namespace Apilan.Helpers;

/// <summary>
/// Real, scannable QR symbols for registration stubs.
///
/// A stub encodes nothing but its own check-in code — "APL-7K2M" — because the
/// door reads that code and looks it up. Keeping the payload to the code itself
/// means any scanner app, phone camera, or hardware wedge produces exactly what
/// an officer would have typed, so the scanned and typed paths cannot diverge.
/// </summary>
public static class QrCode
{
    /// <summary>
    /// Error correction level. Q (25%) survives a stub that has been folded
    /// into a pocket for a week, which is the normal life of one of these.
    /// </summary>
    private const QRCodeGenerator.ECCLevel Ecc = QRCodeGenerator.ECCLevel.Q;

    /// <summary>A symbol as an SVG path, with the module count to size the viewBox by.</summary>
    public record Symbol(string Path, int Modules);

    /// <summary>
    /// Renders the symbol as one SVG path covering every dark module. Inline
    /// SVG rather than an image request: the stub has to print, and it has to
    /// render on the landing page without a round trip.
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

    /// <summary>
    /// The same symbol as a PNG, for downloading or embedding somewhere that
    /// cannot take inline SVG.
    /// </summary>
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

    /// <summary>QRCoder rejects an empty payload, and a blank stub is a bug anyway.</summary>
    private static string Payload(string content) =>
        string.IsNullOrWhiteSpace(content) ? "APL-0000" : content.Trim();
}
