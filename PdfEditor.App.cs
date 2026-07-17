using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Automation;
using Microsoft.Win32;

namespace ClairePdfEditor
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            var app = new Application();
            app.Run(new EditorWindow(args != null && args.Length > 0 ? args[0] : null));
        }
    }

    public sealed class EditorWindow : Window
    {
        private static readonly Brush Navy = BrushFrom("#17233F");
        private static readonly Brush Blue = BrushFrom("#0B63F6");
        private static readonly Brush Border = BrushFrom("#D8DEE9");
        private static readonly Brush Surface = BrushFrom("#F5F7FA");
        private EditorProject project;
        private string projectPath;
        private bool loading;
        private RichTextBox editor;
        private Border pageSheet;
        private ListBox pageList;
        private ListBox commentList;
        private TextBlock headerText;
        private TextBlock footerText;
        private TextBlock pageNumberText;
        private TextBlock signatureText;
        private Image documentImage;
        private Image pdfPageBackground;
        private Border imageFrame;
        private InkCanvas ink;
        private TextBox searchBox;
        private TextBlock statusText;
        private TextBlock zoomText;
        private Slider zoomSlider;
        private ScaleTransform zoomTransform;
        private RotateTransform pageRotation;
        private StackPanel imageProperties;
        private TextBox imageWidthBox;
        private TextBox imageHeightBox;
        private ScrollViewer documentScroll;
        private StackPanel continuousPagesHost;
        private readonly Dictionary<string, ImageSource> pdfPreviewCache = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);
        private InkCanvasEditingMode drawingMode = InkCanvasEditingMode.Ink;
        private readonly EditorInteractionState interactionState = new EditorInteractionState();

        public EditorWindow() : this(null) { }

        public EditorWindow(string initialPath)
        {
            Title = "Claire PDF Editor";
            Width = 1500;
            Height = 960;
            MinWidth = 1100;
            MinHeight = 700;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Surface;
            FontFamily = new FontFamily("Segoe UI");
            project = EditorProject.CreateSample();
            Content = BuildInterface();
            LoadPage(0);
            if (!String.IsNullOrEmpty(initialPath) && File.Exists(initialPath)) OpenPath(initialPath);
        }

        private UIElement BuildInterface()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition());
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48) });

            var menu = BuildMenu();
            Grid.SetRow(menu, 0);
            root.Children.Add(menu);

            var ribbon = BuildRibbon();
            Grid.SetRow(ribbon, 1);
            root.Children.Add(ribbon);

            var drawingBar = BuildDrawingBar();
            Grid.SetRow(drawingBar, 2);
            root.Children.Add(drawingBar);

            var workspace = BuildWorkspace();
            Grid.SetRow(workspace, 3);
            root.Children.Add(workspace);

            var status = BuildStatusBar();
            Grid.SetRow(status, 4);
            root.Children.Add(status);
            return root;
        }

        private Menu BuildMenu()
        {
            var menu = new Menu { Background = Brushes.White, Foreground = Navy, Padding = new Thickness(8, 2, 8, 2) };
            var file = MenuItem("_File", null);
            file.Items.Add(MenuItem("_New", delegate { NewProject(); }, Key.N, ModifierKeys.Control));
            file.Items.Add(MenuItem("_Open...", delegate { OpenProject(); }, Key.O, ModifierKeys.Control));
            var import = MenuItem("_Import PDF...", null);
            import.Items.Add(MenuItem("From _File...", delegate { ImportDocument(); }));
            import.Items.Add(MenuItem("From _Scanner...", delegate { Info("Scanner integration is ready for a TWAIN/WIA provider."); }));
            import.Items.Add(MenuItem("_Combine Files...", delegate { ImportDocument(); }));
            file.Items.Add(import);
            file.Items.Add(new Separator());
            file.Items.Add(MenuItem("_Save", delegate { SaveProject(false); }, Key.S, ModifierKeys.Control));
            file.Items.Add(MenuItem("Save _As...", delegate { SaveProject(true); }, Key.S, ModifierKeys.Control | ModifierKeys.Shift));
            file.Items.Add(MenuItem("_Export PDF...", delegate { ExportPdf(); }));
            file.Items.Add(new Separator());
            file.Items.Add(MenuItem("_Print...", delegate { Print(); }, Key.P, ModifierKeys.Control));
            file.Items.Add(MenuItem("Document _Properties", delegate { ShowProperties(); }));
            file.Items.Add(new Separator());
            file.Items.Add(MenuItem("_Close", delegate { Close(); }));
            menu.Items.Add(file);

            var edit = MenuItem("_Edit", null);
            edit.Items.Add(MenuItem("_Undo", delegate { editor.Undo(); }, Key.Z, ModifierKeys.Control));
            edit.Items.Add(MenuItem("_Redo", delegate { editor.Redo(); }, Key.Y, ModifierKeys.Control));
            edit.Items.Add(new Separator());
            edit.Items.Add(MenuItem("Cu_t", delegate { editor.Cut(); }, Key.X, ModifierKeys.Control));
            edit.Items.Add(MenuItem("_Copy", delegate { editor.Copy(); }, Key.C, ModifierKeys.Control));
            edit.Items.Add(MenuItem("_Paste", delegate { editor.Paste(); }, Key.V, ModifierKeys.Control));
            edit.Items.Add(MenuItem("_Find", delegate { searchBox.Focus(); }, Key.F, ModifierKeys.Control));
            menu.Items.Add(edit);

            var view = MenuItem("_View", null);
            view.Items.Add(MenuItem("Zoom _In", delegate { zoomSlider.Value += 10; }, Key.OemPlus, ModifierKeys.Control));
            view.Items.Add(MenuItem("Zoom _Out", delegate { zoomSlider.Value -= 10; }, Key.OemMinus, ModifierKeys.Control));
            view.Items.Add(MenuItem("_Actual Size", delegate { zoomSlider.Value = 100; }));
            menu.Items.Add(view);

            var insert = MenuItem("_Insert", null);
            insert.Items.Add(MenuItem("_Image...", delegate { ImportImage(); }));
            insert.Items.Add(MenuItem("_Signature...", delegate { AddSignature(); }));
            insert.Items.Add(MenuItem("_Header & Footer...", delegate { EditHeaderFooter(); }));
            insert.Items.Add(MenuItem("Page _Numbers", delegate { TogglePageNumbers(); }));
            menu.Items.Add(insert);

            var annotate = MenuItem("_Annotate", null);
            annotate.Items.Add(MenuItem("_Highlight", delegate { Highlight(); }));
            annotate.Items.Add(MenuItem("_Underline", delegate { Underline(); }));
            annotate.Items.Add(MenuItem("_Comment...", delegate { AddComment(); }));
            annotate.Items.Add(MenuItem("_Draw", delegate { ToggleDrawing(); }));
            menu.Items.Add(annotate);

            var tools = MenuItem("_Tools", null);
            tools.Items.Add(MenuItem("Edit _Text", delegate { SetTextSelectionMode(true); }));
            tools.Items.Add(MenuItem("_Crop Image", delegate { CropImage(); }));
            tools.Items.Add(MenuItem("_Resize Image", delegate { ApplyImageSize(); }));
            tools.Items.Add(MenuItem("E_xtract Images...", delegate { ExtractImage(); }));
            menu.Items.Add(tools);

            var page = MenuItem("_Page", null);
            page.Items.Add(MenuItem("Rotate _Left", delegate { Rotate(-90); }));
            page.Items.Add(MenuItem("Rotate _Right", delegate { Rotate(90); }));
            page.Items.Add(MenuItem("_Add Page", delegate { AddPage(); }));
            menu.Items.Add(page);

            menu.Items.Add(MenuItem("_Form", delegate { Info("Form field authoring is reserved for the PDF engine integration."); }));
            menu.Items.Add(MenuItem("_Window", delegate { WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; }));
            menu.Items.Add(MenuItem("_Help", delegate { Info("Claire PDF Editor\nNative C# WPF prototype"); }));
            return menu;
        }

        private Border BuildRibbon()
        {
            var wrap = new WrapPanel { Margin = new Thickness(10, 8, 10, 8) };
            AddTool(wrap, "⇩", "Import", delegate { ImportDocument(); });
            AddTool(wrap, "T", "Edit Text", delegate { SetTextSelectionMode(true); });
            AddTool(wrap, "▰", "Highlight", delegate { Highlight(); });
            AddTool(wrap, "U", "Underline", delegate { Underline(); });
            AddTool(wrap, "▣", "Comment", delegate { AddComment(); });
            AddTool(wrap, "〰", "Draw", delegate { ToggleDrawing(); });
            AddTool(wrap, "✎", "Signature", delegate { AddSignature(); });
            AddTool(wrap, "⌗", "Crop Image", delegate { CropImage(); });
            AddTool(wrap, "↗", "Resize Image", delegate { ApplyImageSize(); });
            AddTool(wrap, "↻", "Rotate Page", delegate { Rotate(90); });
            AddTool(wrap, "▤", "Header & Footer", delegate { EditHeaderFooter(); });
            AddTool(wrap, "#", "Page Numbers", delegate { TogglePageNumbers(); });
            AddTool(wrap, "▧", "Extract Images", delegate { ExtractImage(); });

            searchBox = new TextBox { Width = 190, Height = 32, Margin = new Thickness(18, 15, 4, 0), Padding = new Thickness(9, 5, 9, 5), ToolTip = "Search document" };
            searchBox.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) Search(); };
            wrap.Children.Add(searchBox);
            AddTool(wrap, "⌕", "Search", delegate { Search(); });
            AddTool(wrap, "▦", "Print", delegate { Print(); });
            AddTool(wrap, "▣", "Save", delegate { SaveProject(false); }, true);
            return Card(wrap, new Thickness(0), new CornerRadius(0));
        }

        private Border BuildDrawingBar()
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 8) };
            row.Children.Add(SmallButton("Pen", delegate { EnableInk(InkCanvasEditingMode.Ink); }));
            row.Children.Add(SmallButton("Eraser", delegate { EnableInk(InkCanvasEditingMode.EraseByStroke); }));
            row.Children.Add(SmallButton("Clear", delegate { if (ink != null) ink.Strokes.Clear(); }));
            row.Children.Add(new Separator { Margin = new Thickness(8, 4, 8, 4) });
            string[] colors = { "#111111", "#EF2B2D", "#0B63F6", "#22A447", "#7D24A8", "#F28C18" };
            foreach (string color in colors)
            {
                string local = color;
                var dot = new Button { Width = 28, Height = 28, Margin = new Thickness(4), Background = BrushFrom(color), BorderBrush = Brushes.Transparent, ToolTip = color };
                dot.Click += delegate { if (ink != null) ink.DefaultDrawingAttributes.Color = ((SolidColorBrush)BrushFrom(local)).Color; };
                row.Children.Add(dot);
            }
            var thickness = new ComboBox { Width = 90, Margin = new Thickness(14, 3, 4, 3) };
            thickness.Items.Add("1 pt"); thickness.Items.Add("2 pt"); thickness.Items.Add("4 pt"); thickness.Items.Add("8 pt");
            thickness.SelectedIndex = 1;
            thickness.SelectionChanged += delegate { if (ink != null) { double v = new double[] { 1, 2, 4, 8 }[thickness.SelectedIndex]; ink.DefaultDrawingAttributes.Width = v; ink.DefaultDrawingAttributes.Height = v; } };
            row.Children.Add(thickness);
            return Card(row, new Thickness(215, 0, 345, 0), new CornerRadius(7));
        }

        private Grid BuildWorkspace()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(335) });

            pageList = new ListBox { Background = Brushes.White, BorderThickness = new Thickness(0, 1, 1, 0), BorderBrush = Border, Padding = new Thickness(12) };
            pageList.SelectionChanged += delegate { if (!loading && pageList.SelectedIndex >= 0) SavePageTextAndLoad(pageList.SelectedIndex); };
            Grid.SetColumn(pageList, 0);
            grid.Children.Add(pageList);

            documentScroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = BrushFrom("#EDF0F4") };
            continuousPagesHost = new StackPanel { Margin = new Thickness(40, 28, 40, 35), HorizontalAlignment = HorizontalAlignment.Center };
            zoomTransform = new ScaleTransform(1, 1);
            continuousPagesHost.LayoutTransform = zoomTransform;
            pageSheet = new Border { Width = 780, MinHeight = 900, Background = Brushes.White, BorderBrush = Border, BorderThickness = new Thickness(1), Padding = new Thickness(58, 42, 58, 42), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top };
            pageRotation = new RotateTransform(0);
            pageSheet.LayoutTransform = pageRotation;

            var page = new Grid();
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition());
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            headerText = new TextBlock { Foreground = Brushes.Gray, FontSize = 11, Margin = new Thickness(0, 0, 0, 18) };
            page.Children.Add(headerText);

            var content = new Grid();
            pdfPageBackground = new Image { Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false, Visibility = Visibility.Collapsed };
            content.Children.Add(pdfPageBackground);
            editor = new RichTextBox { BorderThickness = new Thickness(0), Background = Brushes.Transparent, Foreground = Navy, FontSize = 15, Padding = new Thickness(0), AcceptsTab = true, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
            editor.TextChanged += delegate { if (!loading) project.ActivePage.Body = TextOf(editor); };
            content.Children.Add(editor);

            imageFrame = new Border { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 150, 0, 0), BorderBrush = Blue, BorderThickness = new Thickness(2), Visibility = Visibility.Collapsed };
            documentImage = new Image { Stretch = Stretch.UniformToFill, Width = 280, Height = 180 };
            imageFrame.Child = documentImage;
            content.Children.Add(imageFrame);

            signatureText = new TextBlock { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 10, 35), FontSize = 32, FontFamily = new FontFamily("Segoe Script"), FontStyle = FontStyles.Italic, Foreground = BrushFrom("#7D24A8") };
            content.Children.Add(signatureText);

            ink = new InkCanvas { Background = Brushes.Transparent, Visibility = Visibility.Visible, IsHitTestVisible = false, EditingMode = InkCanvasEditingMode.Ink };
            ink.DefaultDrawingAttributes.Color = Colors.Blue;
            ink.DefaultDrawingAttributes.Width = 2;
            ink.DefaultDrawingAttributes.Height = 2;
            content.Children.Add(ink);
            Grid.SetRow(content, 1);
            page.Children.Add(content);

            var footer = new Grid { Margin = new Thickness(0, 18, 0, 0) };
            footer.ColumnDefinitions.Add(new ColumnDefinition()); footer.ColumnDefinitions.Add(new ColumnDefinition());
            footerText = new TextBlock { Foreground = Brushes.Gray, FontSize = 11 };
            pageNumberText = new TextBlock { Foreground = Brushes.Gray, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetColumn(pageNumberText, 1);
            footer.Children.Add(footerText); footer.Children.Add(pageNumberText);
            Grid.SetRow(footer, 2);
            page.Children.Add(footer);
            pageSheet.Child = page;
            AutomationProperties.SetName(pageSheet, "Active editable page");
            documentScroll.Content = continuousPagesHost;
            Grid.SetColumn(documentScroll, 1);
            grid.Children.Add(documentScroll);

            var right = BuildPropertiesPanel();
            Grid.SetColumn(right, 2);
            grid.Children.Add(right);
            return grid;
        }

        private Border BuildPropertiesPanel()
        {
            var panel = new StackPanel { Margin = new Thickness(16) };
            panel.Children.Add(Heading("Properties"));
            panel.Children.Add(Label("Text"));
            var format = new WrapPanel();
            format.Children.Add(SmallButton("Bold", delegate { ToggleBold(); }));
            format.Children.Add(SmallButton("Italic", delegate { ToggleItalic(); }));
            format.Children.Add(SmallButton("Underline", delegate { Underline(); }));
            format.Children.Add(SmallButton("Highlight", delegate { Highlight(); }));
            panel.Children.Add(format);
            panel.Children.Add(new Separator { Margin = new Thickness(0, 14, 0, 12) });
            panel.Children.Add(Heading("Image"));
            imageProperties = new StackPanel();
            var sizeRow = new WrapPanel();
            imageWidthBox = SizedTextBox("280");
            imageHeightBox = SizedTextBox("180");
            sizeRow.Children.Add(Label("W")); sizeRow.Children.Add(imageWidthBox); sizeRow.Children.Add(Label("H")); sizeRow.Children.Add(imageHeightBox);
            imageProperties.Children.Add(sizeRow);
            var actions = new WrapPanel();
            actions.Children.Add(SmallButton("Apply Size", delegate { ApplyImageSize(); }));
            actions.Children.Add(SmallButton("Crop 10%", delegate { CropImage(); }));
            actions.Children.Add(SmallButton("Remove", delegate { RemoveImage(); }));
            imageProperties.Children.Add(actions);
            panel.Children.Add(imageProperties);
            panel.Children.Add(new Separator { Margin = new Thickness(0, 14, 0, 12) });
            panel.Children.Add(Heading("Comments"));
            commentList = new ListBox { BorderThickness = new Thickness(0), Background = Brushes.Transparent, MinHeight = 200 };
            panel.Children.Add(commentList);
            panel.Children.Add(SmallButton("+ Add Comment", delegate { AddComment(); }));
            return Card(panel, new Thickness(0), new CornerRadius(0));
        }

        private Border BuildStatusBar()
        {
            var grid = new Grid { Background = Brushes.White };
            grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            statusText = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(20, 0, 0, 0), Foreground = Navy };
            grid.Children.Add(statusText);
            var zoom = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 20, 0) };
            zoom.Children.Add(SmallButton("−", delegate { zoomSlider.Value -= 10; }));
            zoomText = new TextBlock { Width = 50, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            zoom.Children.Add(zoomText);
            zoomSlider = new Slider { Minimum = 50, Maximum = 175, Value = 100, Width = 150, Margin = new Thickness(8, 0, 8, 0) };
            zoomSlider.ValueChanged += delegate { if (zoomTransform != null) { zoomTransform.ScaleX = zoomSlider.Value / 100.0; zoomTransform.ScaleY = zoomSlider.Value / 100.0; zoomText.Text = ((int)zoomSlider.Value) + "%"; } };
            zoom.Children.Add(zoomSlider);
            zoom.Children.Add(SmallButton("+", delegate { zoomSlider.Value += 10; }));
            Grid.SetColumn(zoom, 1); grid.Children.Add(zoom);
            return new Border { BorderBrush = Border, BorderThickness = new Thickness(0, 1, 0, 0), Child = grid };
        }

        private void LoadPage(int index)
        {
            loading = true;
            project.CurrentPage = Math.Max(0, Math.Min(index, project.Pages.Count - 1));
            RefreshPageList();
            var page = project.ActivePage;
            LoadEditorContent(page);
            LoadPdfBackground(page);
            headerText.Text = page.Header;
            footerText.Text = page.Footer;
            pageNumberText.Text = page.Number > 0 ? "Page " + page.Number : String.Empty;
            pageNumberText.Visibility = page.ShowPageNumber ? Visibility.Visible : Visibility.Collapsed;
            pageRotation.Angle = page.Rotation;
            signatureText.Text = page.Signature;
            LoadInk(page);
            LoadPageImage(page);
            SetTextSelectionMode(false);
            RebuildContinuousPages();
            RefreshComments();
            pageList.SelectedIndex = project.CurrentPage;
            statusText.Text = "Page " + (project.CurrentPage + 1) + " of " + project.Pages.Count + "   •   " + project.Title;
            Title = project.Title + " — Claire PDF Editor";
            loading = false;
        }

        private void RebuildContinuousPages()
        {
            if (continuousPagesHost == null) return;
            continuousPagesHost.Children.Clear();
            for (int i = 0; i < project.Pages.Count; i++)
            {
                int pageIndex = i;
                if (i == project.CurrentPage)
                {
                    pageSheet.Margin = new Thickness(0, 0, 0, 26);
                    AutomationProperties.SetName(pageSheet, "Page " + (i + 1) + " editable");
                    continuousPagesHost.Children.Add(pageSheet);
                }
                else
                {
                    Border preview = BuildContinuousPagePreview(project.Pages[i], i);
                    preview.MouseLeftButtonDown += delegate
                    {
                        if (loading) return;
                        CapturePageState();
                        LoadPage(pageIndex);
                        pageSheet.BringIntoView();
                    };
                    continuousPagesHost.Children.Add(preview);
                }
            }
        }

        private Border BuildContinuousPagePreview(EditorPage source, int pageIndex)
        {
            var card = new Border { Width = 780, Height = 900, Background = Brushes.White, BorderBrush = Border, BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 0, 26), Padding = new Thickness(28), Cursor = Cursors.Hand, ToolTip = "Click to edit page " + (pageIndex + 1) };
            AutomationProperties.SetName(card, "Page " + (pageIndex + 1) + " preview");
            var grid = new Grid();
            ImageSource previewSource = GetPdfPreview(source);
            if (previewSource != null)
                grid.Children.Add(new Image { Source = previewSource, Stretch = Stretch.Uniform, IsHitTestVisible = false });
            else
                grid.Children.Add(new TextBlock { Text = source.Body, TextWrapping = TextWrapping.Wrap, Foreground = Navy, FontSize = 14, Margin = new Thickness(32) });
            grid.Children.Add(new TextBlock { Text = "Page " + (pageIndex + 1), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Foreground = Brushes.Gray, Background = Brushes.White, Padding = new Thickness(8, 3, 8, 3) });
            card.Child = grid;
            return card;
        }

        private ImageSource GetPdfPreview(EditorPage source)
        {
            if (String.IsNullOrEmpty(source.SourcePdfPath) || !File.Exists(source.SourcePdfPath)) return null;
            string key = source.SourcePdfPath + "|" + source.SourcePdfPageIndex;
            ImageSource cached;
            if (pdfPreviewCache.TryGetValue(key, out cached)) return cached;
            string rendered = null;
            try
            {
                rendered = PdfRenderService.RenderPage(source.SourcePdfPath, source.SourcePdfPageIndex);
                var bitmap = new BitmapImage();
                bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.UriSource = new Uri(rendered); bitmap.EndInit(); bitmap.Freeze();
                pdfPreviewCache[key] = bitmap;
                return bitmap;
            }
            catch { return null; }
            finally { if (!String.IsNullOrEmpty(rendered) && File.Exists(rendered)) try { File.Delete(rendered); } catch { } }
        }

        private void SavePageTextAndLoad(int index)
        {
            CapturePageState();
            LoadPage(index);
        }

        private void RefreshPageList()
        {
            pageList.Items.Clear();
            for (int i = 0; i < project.Pages.Count; i++)
            {
                var card = new Border { Width = 160, Height = 150, Margin = new Thickness(4, 8, 4, 8), Background = Brushes.White, BorderBrush = i == project.CurrentPage ? Blue : Border, BorderThickness = new Thickness(i == project.CurrentPage ? 2 : 1), CornerRadius = new CornerRadius(4), Padding = new Thickness(10) };
                var panel = new StackPanel();
                panel.Children.Add(new TextBlock { Text = FirstLine(project.Pages[i].Body), FontWeight = FontWeights.SemiBold, Foreground = Navy, TextWrapping = TextWrapping.Wrap, MaxHeight = 42 });
                panel.Children.Add(new TextBlock { Text = Preview(project.Pages[i].Body), Foreground = Brushes.Gray, FontSize = 10, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0), MaxHeight = 65 });
                panel.Children.Add(new TextBlock { Text = (i + 1).ToString(), HorizontalAlignment = HorizontalAlignment.Center, Foreground = Blue, Margin = new Thickness(0, 8, 0, 0) });
                card.Child = panel;
                pageList.Items.Add(card);
            }
        }

        private void RefreshComments()
        {
            commentList.Items.Clear();
            foreach (var comment in project.Comments.Where(c => c.PageNumber == project.ActivePage.Number))
            {
                var box = new Border { Background = BrushFrom("#FFF8DD"), BorderBrush = BrushFrom("#F2D57A"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5), Padding = new Thickness(9), Margin = new Thickness(0, 4, 0, 5) };
                box.Child = new TextBlock { Text = comment.Author + "\n" + comment.Text, TextWrapping = TextWrapping.Wrap, Foreground = Navy };
                commentList.Items.Add(box);
            }
        }

        private void NewProject()
        {
            project = new EditorProject { Title = "Untitled Document" };
            project.AddPage(String.Empty);
            project.CurrentPage = 0;
            projectPath = null;
            LoadPage(0);
        }

        private void OpenProject()
        {
            var dialog = new OpenFileDialog { Filter = "Supported documents (*.cpdf;*.pdf;*.txt)|*.cpdf;*.pdf;*.txt|PDF files (*.pdf)|*.pdf|Claire PDF projects (*.cpdf)|*.cpdf|Text files (*.txt)|*.txt" };
            if (dialog.ShowDialog(this) == true)
                OpenPath(dialog.FileName);
        }

        private void OpenPath(string path)
        {
            string extension = Path.GetExtension(path);
            if (String.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase)) OpenPdfAsProject(path);
            else if (String.Equals(extension, ".txt", StringComparison.OrdinalIgnoreCase)) OpenTextAsProject(path);
            else
            {
                try { project = EditorProject.Load(path); projectPath = path; LoadPage(project.CurrentPage); }
                catch (Exception ex) { Error("Could not open the project.\n" + ex.Message); }
            }
        }

        private void OpenPdfAsProject(string path)
        {
            try
            {
                var opened = new EditorProject { Title = Path.GetFileName(path) };
                List<string> extractedPages = PdfTextExtractionService.ExtractAllPages(path);
                int count = extractedPages.Count;
                for (int pageIndex = 0; pageIndex < count; pageIndex++) AddLinkedPdfPage(opened, path, pageIndex, count, extractedPages[pageIndex]);
                opened.CurrentPage = 0;
                project = opened;
                projectPath = null;
                LoadPage(0);
                statusText.Text = "Opened " + Path.GetFileName(path) + " — " + count + " page(s)";
            }
            catch (Exception ex) { Error("Could not open the PDF.\n" + ex.Message); }
        }

        private void OpenTextAsProject(string path)
        {
            try
            {
                project = new EditorProject { Title = Path.GetFileName(path) };
                project.AddPage(File.ReadAllText(path));
                project.CurrentPage = 0;
                projectPath = null;
                LoadPage(0);
            }
            catch (Exception ex) { Error("Could not open the text document.\n" + ex.Message); }
        }

        private static void AddLinkedPdfPage(EditorProject destination, string path, int pageIndex, int count, string extractedText)
        {
            string fallback = "[Linked PDF page " + (pageIndex + 1) + " of " + count + "]\n\n" + Path.GetFileName(path) + "\n\nNo selectable text was found on this page. You can still draw, add images, comments, or a signature.";
            string editableText = String.IsNullOrWhiteSpace(extractedText) ? fallback : extractedText;
            destination.AddPage(editableText);
            destination.ActivePage.SourcePdfPath = path;
            destination.ActivePage.SourcePdfPageIndex = pageIndex;
            destination.ActivePage.OriginalPdfText = editableText;
            destination.ActivePage.ShowPageNumber = false;
        }

        private void SaveProject(bool saveAs)
        {
            CapturePageState();
            if (saveAs || String.IsNullOrEmpty(projectPath))
            {
                var dialog = new SaveFileDialog { Filter = "Claire PDF projects (*.cpdf)|*.cpdf", FileName = Path.GetFileNameWithoutExtension(project.Title) + ".cpdf" };
                if (dialog.ShowDialog(this) != true) return;
                projectPath = dialog.FileName;
            }
            try { project.Save(projectPath); statusText.Text = "Saved " + projectPath; }
            catch (Exception ex) { Error("Could not save the project.\n" + ex.Message); }
        }

        private void ExportPdf()
        {
            CapturePageState();
            var dialog = new SaveFileDialog { Filter = "PDF files (*.pdf)|*.pdf", FileName = Path.GetFileNameWithoutExtension(project.Title) + ".pdf" };
            if (dialog.ShowDialog(this) != true) return;
            try { PdfProjectExporter.Export(project, dialog.FileName); statusText.Text = "Exported PDF to " + dialog.FileName; }
            catch (Exception ex) { Error("Could not export the PDF.\n" + ex.Message); }
        }

        private void ImportDocument()
        {
            var dialog = new OpenFileDialog { Filter = "Documents (*.pdf;*.txt)|*.pdf;*.txt|PDF files (*.pdf)|*.pdf|Text files (*.txt)|*.txt", Multiselect = true };
            if (dialog.ShowDialog(this) != true) return;
            foreach (string path in dialog.FileNames)
            {
                if (String.Equals(Path.GetExtension(path), ".txt", StringComparison.OrdinalIgnoreCase))
                    project.AddPage(File.ReadAllText(path));
                else
                {
                    try
                    {
                        List<string> extractedPages = PdfTextExtractionService.ExtractAllPages(path);
                        int count = extractedPages.Count;
                        for (int pageIndex = 0; pageIndex < count; pageIndex++)
                            AddLinkedPdfPage(project, path, pageIndex, count, extractedPages[pageIndex]);
                    }
                    catch (Exception ex) { Error("Could not import " + Path.GetFileName(path) + ".\n" + ex.Message); }
                }
            }
            project.Title = Path.GetFileName(dialog.FileNames[0]);
            LoadPage(project.CurrentPage);
        }

        private void AddPage() { project.AddPage("NEW PAGE"); LoadPage(project.CurrentPage); }

        private void Rotate(int degrees) { project.RotateCurrent(degrees); pageRotation.Angle = project.ActivePage.Rotation; statusText.Text = "Page rotated to " + project.ActivePage.Rotation + "°"; }

        private void Highlight()
        {
            if (editor.Selection.IsEmpty) { Info("Select text first, then choose Highlight."); return; }
            editor.Selection.ApplyPropertyValue(TextElement.BackgroundProperty, Brushes.Yellow);
        }

        private void Underline()
        {
            if (editor.Selection.IsEmpty) { Info("Select text first, then choose Underline."); return; }
            editor.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty, TextDecorations.Underline);
        }

        private void ToggleBold() { ToggleTextProperty(TextElement.FontWeightProperty, FontWeights.Bold, FontWeights.Normal); }
        private void ToggleItalic() { ToggleTextProperty(TextElement.FontStyleProperty, FontStyles.Italic, FontStyles.Normal); }

        private void ToggleTextProperty(DependencyProperty property, object on, object off)
        {
            if (editor.Selection.IsEmpty) return;
            object current = editor.Selection.GetPropertyValue(property);
            editor.Selection.ApplyPropertyValue(property, Object.Equals(current, on) ? off : on);
        }

        private void AddComment()
        {
            string value = Prompt("Add Comment", "Comment text:", String.Empty);
            if (value == null || value.Trim().Length == 0) return;
            project.Comments.Add(new EditorComment { PageNumber = project.ActivePage.Number, Author = Environment.UserName, Text = value.Trim() });
            RefreshComments();
        }

        private void AddSignature()
        {
            string value = Prompt("Digital Signature", "Type the signer name:", project.ActivePage.Signature);
            if (value == null) return;
            project.ActivePage.Signature = value;
            signatureText.Text = value;
        }

        private void EditHeaderFooter()
        {
            string header = Prompt("Header", "Header text:", project.ActivePage.Header);
            if (header == null) return;
            string footer = Prompt("Footer", "Footer text:", project.ActivePage.Footer);
            if (footer == null) return;
            project.ActivePage.Header = header; project.ActivePage.Footer = footer;
            headerText.Text = header; footerText.Text = footer;
        }

        private void TogglePageNumbers()
        {
            project.ActivePage.ShowPageNumber = !project.ActivePage.ShowPageNumber;
            pageNumberText.Visibility = project.ActivePage.ShowPageNumber ? Visibility.Visible : Visibility.Collapsed;
        }

        private void CapturePageState()
        {
            var page = project.ActivePage;
            page.Body = TextOf(editor);
            try
            {
                using (var stream = new MemoryStream())
                {
                    new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Save(stream, DataFormats.XamlPackage);
                    page.RichTextData = Convert.ToBase64String(stream.ToArray());
                }
                using (var stream = new MemoryStream())
                {
                    ink.Strokes.Save(stream);
                    page.InkData = Convert.ToBase64String(stream.ToArray());
                }
            }
            catch { page.RichTextData = String.Empty; page.InkData = String.Empty; }
        }

        private void LoadEditorContent(EditorPage page)
        {
            editor.Background = !String.IsNullOrEmpty(page.SourcePdfPath) ? BrushFrom("#F2FFFFFF") : Brushes.Transparent;
            if (!String.IsNullOrEmpty(page.RichTextData))
            {
                try
                {
                    editor.Document = new FlowDocument();
                    using (var stream = new MemoryStream(Convert.FromBase64String(page.RichTextData)))
                        new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Load(stream, DataFormats.XamlPackage);
                    return;
                }
                catch { }
            }
            editor.Document = DocumentFromText(page.Body);
        }

        private void LoadPdfBackground(EditorPage page)
        {
            pdfPageBackground.Source = null;
            pdfPageBackground.Visibility = Visibility.Collapsed;
            if (String.IsNullOrEmpty(page.SourcePdfPath) || !File.Exists(page.SourcePdfPath)) return;
            string rendered = null;
            try
            {
                rendered = PdfRenderService.RenderPage(page.SourcePdfPath, page.SourcePdfPageIndex);
                var bitmap = new BitmapImage();
                bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.UriSource = new Uri(rendered); bitmap.EndInit(); bitmap.Freeze();
                pdfPageBackground.Source = bitmap;
                pdfPageBackground.Visibility = Visibility.Visible;
            }
            catch (Exception ex) { statusText.Text = "Linked PDF preserved; preview unavailable: " + ex.Message; }
            finally { if (!String.IsNullOrEmpty(rendered) && File.Exists(rendered)) try { File.Delete(rendered); } catch { } }
        }

        private void LoadInk(EditorPage page)
        {
            ink.Strokes.Clear();
            if (String.IsNullOrEmpty(page.InkData)) return;
            try
            {
                using (var stream = new MemoryStream(Convert.FromBase64String(page.InkData)))
                    ink.Strokes = new StrokeCollection(stream);
            }
            catch { ink.Strokes.Clear(); }
        }

        private void ToggleDrawing()
        {
            if (interactionState.DrawingActive) SetTextSelectionMode(true);
            else EnableInk(drawingMode);
        }

        private void EnableInk(InkCanvasEditingMode mode)
        {
            drawingMode = mode;
            interactionState.UseDrawing();
            ink.Visibility = Visibility.Visible;
            ink.EditingMode = mode;
            ink.IsHitTestVisible = true;
            editor.IsHitTestVisible = false;
            ink.Focus();
            statusText.Text = mode == InkCanvasEditingMode.EraseByStroke ? "Eraser active — click Draw to return to text selection" : "Drawing active — click Draw again to select text";
        }

        private void SetTextSelectionMode(bool focusEditor)
        {
            interactionState.UseTextSelection();
            ink.Visibility = Visibility.Visible;
            ink.IsHitTestVisible = false;
            editor.IsHitTestVisible = true;
            if (focusEditor)
            {
                editor.Focus();
                statusText.Text = "Text selection active — drawings remain visible";
            }
        }

        private void Search()
        {
            SetTextSelectionMode(false);
            string query = searchBox.Text;
            var hits = project.Search(query).ToList();
            if (hits.Count == 0) { statusText.Text = "No results for “" + query + "”"; return; }
            SearchHit hit = hits[0];
            LoadPage(hit.PageNumber - 1);
            TextPointer start = editor.Document.ContentStart.GetPositionAtOffset(hit.Index + 1, LogicalDirection.Forward);
            TextPointer end = start == null ? null : start.GetPositionAtOffset(hit.Length, LogicalDirection.Forward);
            if (start != null && end != null) { editor.Selection.Select(start, end); editor.Focus(); }
            statusText.Text = hits.Count + " result(s) for “" + query + "”";
        }

        private void ImportImage()
        {
            var dialog = new OpenFileDialog { Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp" };
            if (dialog.ShowDialog(this) != true) return;
            project.ActivePage.ImagePath = dialog.FileName;
            project.ActivePage.CropLeft = project.ActivePage.CropTop = project.ActivePage.CropRight = project.ActivePage.CropBottom = 0;
            LoadPageImage(project.ActivePage);
        }

        private void LoadPageImage(EditorPage page)
        {
            if (String.IsNullOrEmpty(page.ImagePath) || !File.Exists(page.ImagePath)) { imageFrame.Visibility = Visibility.Collapsed; documentImage.Source = null; return; }
            try
            {
                var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.UriSource = new Uri(page.ImagePath); bitmap.EndInit();
                documentImage.Source = bitmap; documentImage.Width = page.ImageWidth; documentImage.Height = page.ImageHeight;
                imageFrame.Visibility = Visibility.Visible;
                imageWidthBox.Text = ((int)page.ImageWidth).ToString(); imageHeightBox.Text = ((int)page.ImageHeight).ToString();
                ApplyCropClip(page);
            }
            catch { imageFrame.Visibility = Visibility.Collapsed; }
        }

        private void ApplyImageSize()
        {
            double width, height;
            if (!Double.TryParse(imageWidthBox.Text, out width) || !Double.TryParse(imageHeightBox.Text, out height) || width < 20 || height < 20) { Error("Enter valid image dimensions of at least 20 × 20."); return; }
            project.ActivePage.ImageWidth = width; project.ActivePage.ImageHeight = height;
            documentImage.Width = width; documentImage.Height = height; ApplyCropClip(project.ActivePage);
        }

        private void CropImage()
        {
            if (documentImage.Source == null) { Info("Import an image before cropping."); return; }
            var p = project.ActivePage;
            p.CropLeft = Math.Min(.35, p.CropLeft + .05); p.CropTop = Math.Min(.35, p.CropTop + .05);
            p.CropRight = Math.Min(.35, p.CropRight + .05); p.CropBottom = Math.Min(.35, p.CropBottom + .05);
            ApplyCropClip(p);
            statusText.Text = "Applied a non-destructive 5% crop on each edge";
        }

        private void ApplyCropClip(EditorPage p)
        {
            double x = p.ImageWidth * p.CropLeft, y = p.ImageHeight * p.CropTop;
            double w = p.ImageWidth * (1 - p.CropLeft - p.CropRight), h = p.ImageHeight * (1 - p.CropTop - p.CropBottom);
            documentImage.Clip = new RectangleGeometry(new Rect(x, y, Math.Max(1, w), Math.Max(1, h)));
        }

        private void RemoveImage() { project.ActivePage.ImagePath = String.Empty; documentImage.Source = null; imageFrame.Visibility = Visibility.Collapsed; }

        private void ExtractImage()
        {
            string source = project.ActivePage.ImagePath;
            if (String.IsNullOrEmpty(source) || !File.Exists(source))
            {
                if (!String.IsNullOrEmpty(project.ActivePage.SourcePdfPath) && File.Exists(project.ActivePage.SourcePdfPath))
                {
                    string rendered = null;
                    try
                    {
                        rendered = PdfRenderService.RenderPage(project.ActivePage.SourcePdfPath, project.ActivePage.SourcePdfPageIndex);
                        var pageDialog = new SaveFileDialog { Filter = "PNG image (*.png)|*.png", FileName = Path.GetFileNameWithoutExtension(project.ActivePage.SourcePdfPath) + "-page-" + (project.ActivePage.SourcePdfPageIndex + 1) + ".png" };
                        if (pageDialog.ShowDialog(this) == true) { File.Copy(rendered, pageDialog.FileName, true); statusText.Text = "Extracted rendered page to " + pageDialog.FileName; }
                    }
                    catch (Exception ex) { Error("Could not extract the linked PDF page.\n" + ex.Message); }
                    finally { if (!String.IsNullOrEmpty(rendered) && File.Exists(rendered)) try { File.Delete(rendered); } catch { } }
                    return;
                }
                Info("The current page has no extractable image."); return;
            }
            var dialog = new SaveFileDialog { Filter = "Image|*" + Path.GetExtension(source), FileName = Path.GetFileName(source) };
            if (dialog.ShowDialog(this) == true) { File.Copy(source, dialog.FileName, true); statusText.Text = "Extracted image to " + dialog.FileName; }
        }

        private void Print()
        {
            var dialog = new PrintDialog();
            if (dialog.ShowDialog() == true) dialog.PrintVisual(pageSheet, project.Title + " — page " + project.ActivePage.Number);
        }

        private void ShowProperties()
        {
            Info("Title: " + project.Title + "\nPages: " + project.Pages.Count + "\nComments: " + project.Comments.Count + "\nCurrent rotation: " + project.ActivePage.Rotation + "°");
        }

        private static FlowDocument DocumentFromText(string text)
        {
            var document = new FlowDocument { PagePadding = new Thickness(0), FontFamily = new FontFamily("Segoe UI"), FontSize = 15, Foreground = Navy };
            string[] lines = (text ?? String.Empty).Replace("\r", String.Empty).Split('\n');
            foreach (string line in lines)
            {
                var paragraph = new Paragraph(new Run(line)) { Margin = new Thickness(0, 0, 0, line.Length == 0 ? 8 : 5) };
                if (line == line.ToUpperInvariant() && line.Length > 4) { paragraph.FontSize = 27; paragraph.FontWeight = FontWeights.Bold; }
                else if (line.Length > 2 && Char.IsDigit(line[0]) && line[1] == '.') { paragraph.FontSize = 18; paragraph.FontWeight = FontWeights.Bold; paragraph.Foreground = Blue; }
                document.Blocks.Add(paragraph);
            }
            return document;
        }

        private static string TextOf(RichTextBox box)
        {
            return new TextRange(box.Document.ContentStart, box.Document.ContentEnd).Text.TrimEnd('\r', '\n');
        }

        private static MenuItem MenuItem(string header, RoutedEventHandler click)
        {
            var item = new MenuItem { Header = header };
            if (click != null) item.Click += click;
            return item;
        }

        private static MenuItem MenuItem(string header, RoutedEventHandler click, Key key, ModifierKeys modifiers)
        {
            var item = MenuItem(header, click);
            item.InputGestureText = (modifiers == ModifierKeys.Control ? "Ctrl+" : modifiers == (ModifierKeys.Control | ModifierKeys.Shift) ? "Ctrl+Shift+" : String.Empty) + key;
            return item;
        }

        private void AddTool(Panel panel, string glyph, string label, RoutedEventHandler action) { AddTool(panel, glyph, label, action, false); }

        private void AddTool(Panel panel, string glyph, string label, RoutedEventHandler action, bool primary)
        {
            var content = new StackPanel();
            content.Children.Add(new TextBlock { Text = glyph, FontSize = 25, HorizontalAlignment = HorizontalAlignment.Center, FontWeight = FontWeights.SemiBold });
            content.Children.Add(new TextBlock { Text = label, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center });
            var button = new Button { Content = content, MinWidth = 72, Height = 67, Margin = new Thickness(2), Padding = new Thickness(8, 4, 8, 4), Background = primary ? Blue : Brushes.Transparent, Foreground = primary ? Brushes.White : Navy, BorderBrush = primary ? Blue : Brushes.Transparent, Cursor = Cursors.Hand };
            AutomationProperties.SetName(button, label);
            button.Click += action;
            panel.Children.Add(button);
        }

        private static Button SmallButton(string text, RoutedEventHandler action)
        {
            var button = new Button { Content = text, Margin = new Thickness(3), Padding = new Thickness(10, 6, 10, 6), Background = Brushes.White, Foreground = Navy, BorderBrush = Border, Cursor = Cursors.Hand };
            button.Click += action;
            return button;
        }

        private static TextBox SizedTextBox(string text) { return new TextBox { Text = text, Width = 58, Margin = new Thickness(4), Padding = new Thickness(5) }; }
        private static TextBlock Label(string text) { return new TextBlock { Text = text, Foreground = Brushes.Gray, Margin = new Thickness(4, 7, 4, 4), VerticalAlignment = VerticalAlignment.Center }; }
        private static TextBlock Heading(string text) { return new TextBlock { Text = text, FontWeight = FontWeights.SemiBold, FontSize = 16, Foreground = Navy, Margin = new Thickness(4, 4, 4, 8) }; }

        private static Border Card(UIElement child, Thickness margin, CornerRadius radius)
        {
            return new Border { Child = child, Margin = margin, Background = Brushes.White, BorderBrush = Border, BorderThickness = new Thickness(1), CornerRadius = radius };
        }

        private static Brush BrushFrom(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
        private static string FirstLine(string text) { string value = text ?? String.Empty; int i = value.IndexOf('\n'); return i < 0 ? value : value.Substring(0, i); }
        private static string Preview(string text) { string value = (text ?? String.Empty).Replace("\r", " ").Replace("\n", " "); return value.Length > 100 ? value.Substring(0, 100) + "…" : value; }
        private void Info(string text) { MessageBox.Show(this, text, "Claire PDF Editor", MessageBoxButton.OK, MessageBoxImage.Information); }
        private void Error(string text) { MessageBox.Show(this, text, "Claire PDF Editor", MessageBoxButton.OK, MessageBoxImage.Error); }

        private string Prompt(string title, string label, string initial)
        {
            var window = new Window { Title = title, Owner = this, Width = 420, Height = 175, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, Background = Brushes.White };
            var grid = new Grid { Margin = new Thickness(18) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); grid.RowDefinitions.Add(new RowDefinition());
            grid.Children.Add(new TextBlock { Text = label, Foreground = Navy, Margin = new Thickness(0, 0, 0, 7) });
            var box = new TextBox { Text = initial ?? String.Empty, Padding = new Thickness(8), MinHeight = 32 };
            Grid.SetRow(box, 1); grid.Children.Add(box);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 15, 0, 0) };
            var cancel = SmallButton("Cancel", delegate { window.DialogResult = false; });
            var ok = SmallButton("OK", delegate { window.DialogResult = true; }); ok.Background = Blue; ok.Foreground = Brushes.White;
            buttons.Children.Add(cancel); buttons.Children.Add(ok); Grid.SetRow(buttons, 2); grid.Children.Add(buttons);
            window.Content = grid; box.Focus(); box.SelectAll();
            return window.ShowDialog() == true ? box.Text : null;
        }
    }
}
