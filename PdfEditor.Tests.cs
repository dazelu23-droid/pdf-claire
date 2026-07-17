using System;
using System.IO;
using System.Linq;
using ClairePdfEditor;

internal static class PdfEditorTests
{
    private static int failures;

    private static void Check(bool condition, string name)
    {
        if (condition) Console.WriteLine("PASS " + name);
        else { Console.WriteLine("FAIL " + name); failures++; }
    }

    public static int Main()
    {
        var project = EditorProject.CreateSample();
        Check(project.Pages.Count == 3, "sample contains three pages");
        Check(project.ActivePage.Number == 1, "first page is active");

        project.RotateCurrent(90);
        project.RotateCurrent(360);
        Check(project.ActivePage.Rotation == 90, "rotation is normalized");
        project.RotateCurrent(-180);
        Check(project.ActivePage.Rotation == 270, "negative rotation is normalized");

        Check(project.Search("project").Count() >= 2, "search is case-insensitive and returns multiple hits");
        Check(project.Search("").Count() == 0, "empty search is safe");

        project.AddPage("Imported page");
        Check(project.Pages.Count == 4 && project.CurrentPage == 3, "add page activates the new page");

        project.Pages[0].RichTextData = "sample-rich-text";
        project.Pages[0].InkData = "sample-ink";
        project.Pages[0].ShowPageNumber = false;

        string path = Path.Combine(Path.GetTempPath(), "ClairePdfEditor-test.cpdf");
        project.Save(path);
        var loaded = EditorProject.Load(path);
        Check(loaded.Title == project.Title, "save/load preserves title");
        Check(loaded.Pages.Count == project.Pages.Count, "save/load preserves pages");
        Check(loaded.Pages[0].Rotation == 270, "save/load preserves rotation");
        Check(loaded.Pages[0].RichTextData == "sample-rich-text", "save/load preserves rich text state");
        Check(loaded.Pages[0].InkData == "sample-ink", "save/load preserves drawing state");
        Check(!loaded.Pages[0].ShowPageNumber, "save/load preserves page-number visibility");
        var large = new EditorProject { Title = "Large project" };
        large.AddPage(new string('x', 3000000));
        string largePath = Path.Combine(Path.GetTempPath(), "ClairePdfEditor-large-test.cpdf");
        large.Save(largePath);
        Check(EditorProject.Load(largePath).ActivePage.Body.Length == 3000000, "large projects exceed the old JSON length limit safely");
        File.Delete(largePath);
        string pdfPath = Path.Combine(Path.GetTempPath(), "ClairePdfEditor-test.pdf");
        PdfProjectExporter.Export(loaded, pdfPath);
        byte[] signature = File.ReadAllBytes(pdfPath);
        Check(signature.Length > 1000 && signature[0] == 0x25 && signature[1] == 0x50 && signature[2] == 0x44 && signature[3] == 0x46, "PDF export creates a valid PDF file signature");
        Check(PdfProjectExporter.GetPageCount(pdfPath) == loaded.Pages.Count, "exported PDF contains every project page");
        if (PdfRenderService.IsAvailable)
        {
            string rendered = PdfRenderService.RenderPage(pdfPath, 0);
            byte[] png = File.ReadAllBytes(rendered);
            Check(png.Length > 1000 && png[0] == 0x89 && png[1] == 0x50 && png[2] == 0x4E && png[3] == 0x47, "PDF renderer produces a valid PNG page preview");
            File.Delete(rendered);
        }
        else Console.WriteLine("SKIP PDF renderer preview (pdftoppm unavailable)");
        var linked = new EditorProject { Title = "Linked export" };
        linked.Pages.Add(new EditorPage { Number = 1, SourcePdfPath = pdfPath, SourcePdfPageIndex = 0, Body = "[Linked PDF page 1]" });
        string linkedPath = Path.Combine(Path.GetTempPath(), "ClairePdfEditor-linked-test.pdf");
        PdfProjectExporter.Export(linked, linkedPath);
        Check(PdfProjectExporter.GetPageCount(linkedPath) == 1 && new FileInfo(linkedPath).Length > 1000, "linked PDF pages are preserved in re-export");
        File.Delete(linkedPath);
        File.Delete(pdfPath);

        string realPdf = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "vibecoding-en-web.pdf");
        if (File.Exists(realPdf))
        {
            int realPages = PdfProjectExporter.GetPageCount(realPdf);
            Check(realPages > 0, "vibecoding PDF opens through the PDF engine");
            if (PdfRenderService.IsAvailable)
            {
                string realPreview = PdfRenderService.RenderPage(realPdf, 0);
                Check(new FileInfo(realPreview).Length > 1000, "vibecoding PDF first page renders successfully");
                File.Delete(realPreview);
            }
        }
        File.Delete(path);

        Console.WriteLine(failures == 0 ? "ALL TESTS PASSED" : failures + " TEST(S) FAILED");
        return failures == 0 ? 0 : 1;
    }
}
