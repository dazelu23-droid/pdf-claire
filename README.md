# Claire PDF Editor

A native C# WPF desktop prototype based on the approved PDF editor design. It recreates the menu/ribbon/page workspace/properties/comments layout and implements the primary editing interactions without external packages.

## Build and test

Dependencies restore automatically during the first build. To restore them manually:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\restore-dependencies.ps1
```

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\test.ps1
```

Run the app:

```powershell
.\build\ClairePdfEditor.exe
```

Open a PDF directly:

```powershell
.\build\ClairePdfEditor.exe "$env:USERPROFILE\Downloads\vibecoding-en-web.pdf"
```

The project uses the Windows C# compiler already included on this machine, so it does not require a separately installed .NET SDK.

## Implemented interactions

- Top menus and nested File > Import PDF submenu
- Editable document text, highlighting, underlining, comments, and search
- Image import, sizing, non-destructive crop controls, and image extraction
- Freehand drawing with pen/eraser modes and clear action
- Typed digital signature insertion
- Page thumbnails, page navigation, left/right rotation, headers, footers, and page numbers
- Linked PDF import with real page previews when Poppler `pdftoppm` is available, source-page-preserving export with ink overlays, rendered-page extraction, and `.cpdf` project save including rich-text formatting, drawing strokes, comments, images, and signatures

## PDF engine boundary

The application renders linked PDF pages through Poppler when `pdftoppm` is on `PATH` (or `PDFTOPPM_PATH` points to it), edits over a non-destructive page preview, preserves original pages during PDF export, and uses PDFsharp-WPF for generated output. Native object-level editing, OCR, and cryptographic signing remain production-engine work.
"# pdf-claire" 
