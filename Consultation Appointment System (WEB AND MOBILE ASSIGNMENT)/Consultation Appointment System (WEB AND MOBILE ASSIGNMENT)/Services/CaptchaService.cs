using System.Security.Cryptography;
using System.Text;

namespace ExpertLink.Services;

// Self-written captcha: random code rendered as a distorted SVG image.
// The answer is kept in the user's session (never sent to the browser as text).
public class CaptchaService
{
    private const string Chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";   // no 0/O/1/I to avoid confusion

    public (string Code, string Svg) Generate(int length = 5)
    {
        var code = new string(Enumerable.Range(0, length).Select(_ => Chars[RandomNumberGenerator.GetInt32(Chars.Length)]).ToArray());
        const int w = 170, h = 54;
        var sb = new StringBuilder();
        sb.Append($"<svg xmlns='http://www.w3.org/2000/svg' width='{w}' height='{h}' viewBox='0 0 {w} {h}'>");
        sb.Append($"<rect width='{w}' height='{h}' rx='8' fill='#eef2f7'/>");

        // background noise dots
        for (int i = 0; i < 40; i++)
            sb.Append($"<circle cx='{R(w)}' cy='{R(h)}' r='{R(3) + 1}' fill='{Color(160, 220)}' />");

        // characters with random rotation, size and position
        for (int i = 0; i < code.Length; i++)
        {
            int x = 18 + i * 30 + R(6);
            int y = 36 + R(10) - 5;
            int rot = R(50) - 25;
            int size = 24 + R(8);
            sb.Append($"<text x='{x}' y='{y}' font-family='Verdana,monospace' font-weight='bold' font-size='{size}' fill='{Color(20, 110)}' transform='rotate({rot} {x} {y})'>{code[i]}</text>");
        }

        // interference curves
        for (int i = 0; i < 4; i++)
            sb.Append($"<path d='M{R(20)} {R(h)} C{R(w)} {R(h)}, {R(w)} {R(h)}, {w - R(20)} {R(h)}' stroke='{Color(60, 160)}' stroke-width='{R(2) + 1}' fill='none'/>");

        sb.Append("</svg>");
        return (code, sb.ToString());
    }

    private static int R(int max) => RandomNumberGenerator.GetInt32(max);

    private static string Color(int min, int max) =>
        $"rgb({RandomNumberGenerator.GetInt32(min, max)},{RandomNumberGenerator.GetInt32(min, max)},{RandomNumberGenerator.GetInt32(min, max)})";
}
