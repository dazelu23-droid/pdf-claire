using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using System.Windows.Ink;

namespace ClairePdfEditor
{
    public static class PdfProjectExporter
    {
        public static void Export(EditorProject project, string path)
        {
            if (project == null) throw new ArgumentNullException("project");
            if (String.IsNullOrWhiteSpace(path)) throw new ArgumentException("An output path is required.", "path");

            var document = new PdfDocument();
            document.Info.Title = project.Title ?? "Claire PDF Editor Document";
            document.Info.Creator = "Claire PDF Editor";

            var imports = new Dictionary<string, PdfDocument>(StringComparer.OrdinalIgnoreCase);
            foreach (EditorPage source in project.Pages)
            {
                PdfPage page;
                bool imported = !String.IsNullOrEmpty(source.SourcePdfPath) && File.Exists(source.SourcePdfPath);
                if (imported)
                {
                    PdfDocument input;
                    if (!imports.TryGetValue(source.SourcePdfPath, out input))
                    {
                        input = PdfReader.Open(source.SourcePdfPath, PdfDocumentOpenMode.Import);
                        imports.Add(source.SourcePdfPath, input);
                    }
                    int index = Math.Max(0, Math.Min(source.SourcePdfPageIndex, input.PageCount - 1));
                    page = document.AddPage(input.Pages[index]);
                }
                else page = document.AddPage();
                if (!imported && (source.Rotation == 90 || source.Rotation == 270)) page.Orientation = PdfSharp.PageOrientation.Landscape;
                using (XGraphics graphics = imported ? XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append) : XGraphics.FromPdfPage(page))
                    DrawPage(graphics, page, source, project.Pages.Count, imported);
            }

            if (project.Pages.Count == 0) document.AddPage();
            document.Save(path);
            foreach (PdfDocument input in imports.Values) input.Dispose();
        }

        public static int GetPageCount(string path)
        {
            using (PdfDocument document = PdfReader.Open(path, PdfDocumentOpenMode.Import)) return document.PageCount;
        }

        private static void DrawPage(XGraphics graphics, PdfPage page, EditorPage source, int pageCount, bool imported)
        {
            double width = page.Width.Point;
            double height = page.Height.Point;
            double margin = 52;
            var normal = new XFont("Segoe UI", 11, XFontStyleEx.Regular);
            var heading = new XFont("Segoe UI", 18, XFontStyleEx.Bold);
            var small = new XFont("Segoe UI", 8, XFontStyleEx.Regular);
            var signature = new XFont("Segoe Script", 22, XFontStyleEx.Italic);

            if (!imported || !String.IsNullOrWhiteSpace(source.Header))
            {
                graphics.DrawString(source.Header ?? String.Empty, small, XBrushes.Gray, new XRect(margin, 25, width - margin * 2, 20), XStringFormats.TopLeft);
                graphics.DrawLine(new XPen(XColors.LightGray, .5), margin, 45, width - margin, 45);
            }

            double y = 65;
            string body = source.Body ?? String.Empty;
            if (imported && body.StartsWith("[Linked PDF page", StringComparison.Ordinal)) body = String.Empty;
            string[] lines = body.Replace("\r", String.Empty).Split('\n');
            foreach (string raw in lines)
            {
                bool isHeading = raw.Length > 4 && raw == raw.ToUpperInvariant();
                XFont font = isHeading ? heading : normal;
                if (raw.Length == 0) { y += 8; continue; }
                y = DrawWrapped(graphics, raw, font, XBrushes.Black, margin, y, width - margin * 2, isHeading ? 24 : 16);
                if (y > height - 130) break;
            }

            if (!String.IsNullOrEmpty(source.ImagePath) && File.Exists(source.ImagePath))
            {
                try
                {
                    using (XImage image = XImage.FromFile(source.ImagePath))
                    {
                        double maxWidth = Math.Min(source.ImageWidth, width * .36);
                        double maxHeight = Math.Min(source.ImageHeight, height * .25);
                        double ratio = Math.Min(maxWidth / image.PixelWidth, maxHeight / image.PixelHeight);
                        double imageWidth = image.PixelWidth * ratio;
                        double imageHeight = image.PixelHeight * ratio;
                        graphics.DrawImage(image, width - margin - imageWidth, 135, imageWidth, imageHeight);
                    }
                }
                catch { }
            }

            if (!String.IsNullOrEmpty(source.Signature))
                graphics.DrawString(source.Signature, signature, new XSolidBrush(XColor.FromArgb(125, 36, 168)), new XRect(width - 250, height - 110, 195, 35), XStringFormats.TopLeft);

            DrawInk(graphics, source, width, height);

            if (!imported || !String.IsNullOrWhiteSpace(source.Footer) || source.ShowPageNumber)
                graphics.DrawLine(new XPen(XColors.LightGray, .5), margin, height - 45, width - margin, height - 45);
            graphics.DrawString(source.Footer ?? String.Empty, small, XBrushes.Gray, new XRect(margin, height - 35, width / 2, 16), XStringFormats.TopLeft);
            if (source.ShowPageNumber)
                graphics.DrawString("Page " + source.Number + " of " + pageCount, small, XBrushes.Gray, new XRect(width / 2, height - 35, width / 2 - margin, 16), XStringFormats.TopRight);
        }

        private static double DrawWrapped(XGraphics graphics, string text, XFont font, XBrush brush, double x, double y, double maxWidth, double lineHeight)
        {
            string[] words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string line = String.Empty;
            foreach (string word in words)
            {
                string candidate = line.Length == 0 ? word : line + " " + word;
                if (graphics.MeasureString(candidate, font).Width > maxWidth && line.Length > 0)
                {
                    graphics.DrawString(line, font, brush, new XPoint(x, y + lineHeight));
                    y += lineHeight;
                    line = word;
                }
                else line = candidate;
            }
            if (line.Length > 0) graphics.DrawString(line, font, brush, new XPoint(x, y + lineHeight));
            return y + lineHeight + 2;
        }

        private static void DrawInk(XGraphics graphics, EditorPage source, double pageWidth, double pageHeight)
        {
            if (String.IsNullOrEmpty(source.InkData)) return;
            try
            {
                StrokeCollection strokes;
                using (var stream = new MemoryStream(Convert.FromBase64String(source.InkData))) strokes = new StrokeCollection(stream);
                const double editorWidth = 664;
                const double editorHeight = 790;
                foreach (Stroke stroke in strokes)
                {
                    if (stroke.StylusPoints.Count < 2) continue;
                    var color = stroke.DrawingAttributes.Color;
                    var pen = new XPen(XColor.FromArgb(color.A, color.R, color.G, color.B), Math.Max(.5, stroke.DrawingAttributes.Width * pageWidth / editorWidth));
                    for (int i = 1; i < stroke.StylusPoints.Count; i++)
                    {
                        double x1 = stroke.StylusPoints[i - 1].X * pageWidth / editorWidth;
                        double y1 = stroke.StylusPoints[i - 1].Y * pageHeight / editorHeight;
                        double x2 = stroke.StylusPoints[i].X * pageWidth / editorWidth;
                        double y2 = stroke.StylusPoints[i].Y * pageHeight / editorHeight;
                        graphics.DrawLine(pen, x1, y1, x2, y2);
                    }
                }
            }
            catch { }
        }
    }

    public static class PdfRenderService
    {
        public static bool IsAvailable { get { return FindRenderer() != null; } }

        public static string RenderPage(string pdfPath, int zeroBasedPage)
        {
            string renderer = FindRenderer();
            if (renderer == null) throw new FileNotFoundException("Poppler pdftoppm was not found. Set PDFTOPPM_PATH to enable native page rendering.");
            string folder = Path.Combine(Path.GetTempPath(), "ClairePdfEditor", "renders");
            Directory.CreateDirectory(folder);
            string prefix = Path.Combine(folder, Guid.NewGuid().ToString("N"));
            int pageNumber = zeroBasedPage + 1;
            string toolArguments = "-f " + pageNumber + " -l " + pageNumber + " -singlefile -png -r 120 " + Quote(pdfPath) + " " + Quote(prefix);
            var info = new ProcessStartInfo();
            if (String.Equals(Path.GetExtension(renderer), ".cmd", StringComparison.OrdinalIgnoreCase) || String.Equals(Path.GetExtension(renderer), ".bat", StringComparison.OrdinalIgnoreCase))
            {
                info.FileName = "cmd.exe";
                info.Arguments = "/d /s /c \"\"" + renderer + "\" " + toolArguments + "\"";
            }
            else { info.FileName = renderer; info.Arguments = toolArguments; }
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            info.RedirectStandardError = true;
            using (Process process = Process.Start(info))
            {
                string error = process.StandardError.ReadToEnd();
                if (!process.WaitForExit(30000)) { process.Kill(); throw new TimeoutException("PDF page rendering timed out."); }
                if (process.ExitCode != 0) throw new InvalidOperationException("PDF renderer failed: " + error.Trim());
            }
            string output = prefix + ".png";
            if (!File.Exists(output)) throw new InvalidOperationException("PDF renderer did not produce an image.");
            return output;
        }

        private static string FindRenderer()
        {
            string configured = Environment.GetEnvironmentVariable("PDFTOPPM_PATH");
            if (!String.IsNullOrEmpty(configured) && File.Exists(configured)) return configured;
            string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string codexExe = Path.Combine(user, ".cache", "codex-runtimes", "codex-primary-runtime", "dependencies", "native", "poppler", "Library", "bin", "pdftoppm.exe");
            if (File.Exists(codexExe)) return codexExe;
            string codex = Path.Combine(user, ".cache", "codex-runtimes", "codex-primary-runtime", "dependencies", "bin", "override", "pdftoppm.cmd");
            if (File.Exists(codex)) return codex;
            try
            {
                var info = new ProcessStartInfo("where.exe", "pdftoppm.cmd") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
                using (Process process = Process.Start(info))
                {
                    string value = process.StandardOutput.ReadLine(); process.WaitForExit(2000);
                    if (!String.IsNullOrEmpty(value) && File.Exists(value)) return value;
                }
            }
            catch { }
            return null;
        }

        private static string Quote(string value) { return "\"" + value.Replace("\"", String.Empty) + "\""; }
    }
}
