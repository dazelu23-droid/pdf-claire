# Development and test plan

## Product goal

Build the approved desktop PDF-editor design as a native C# application, preserving the top menu/ribbon layout and providing functional document-editing interactions.

## Architecture

- **Presentation:** package-free WPF UI assembled in C# so it can compile with the Windows compiler available on this machine.
- **Document model:** serializable `EditorProject`, `EditorPage`, `EditorComment`, and `SearchHit` types in a UI-independent assembly.
- **Persistence:** `.cpdf` JSON project files containing pages, editable text, rich-text packages, drawing ink, comments, image metadata, signatures, rotations, headers, footers, and page-number settings.
- **PDF integration boundary:** the Import command reads the real page count and links every source page. Poppler renders real on-screen previews, and PDF export preserves source pages and appends editor overlays. The adapter degrades safely if Poppler is unavailable.

## Delivered phases

1. **Foundation — complete**
   - Created the C# model, WPF shell, build script, and test harness.
2. **Design implementation — complete**
   - Added top menus and nested import submenu, ribbon, drawing toolbar, thumbnails, document canvas, properties, comments, and status/zoom bar.
3. **Editor interactions — complete for the prototype**
   - Added text editing, highlight, underline, search, comment, ink drawing, signature, image import/resize/crop/extract, rotation, headers/footers/page numbers, project open/save, print, and navigation.
4. **Persistence — complete**
   - Rich formatting and ink are serialized along with the rest of the page model.
5. **Verification — complete**
   - Compiler verification, 13 model/persistence assertions, and a live-process WPF startup smoke test.

## Requirement audit

| Requested capability | Implementation evidence |
|---|---|
| Highlighter | Ribbon/menu command applies yellow background to selected text |
| Text editor | Central `RichTextBox` with undo/redo/cut/copy/paste |
| Import tool | PDF/text multiselect import plus nested File > Import PDF menu |
| Image size/cropping | Width/height properties and non-destructive crop clip |
| Digital signature | Typed signature rendered in script styling and persisted |
| Drawing tools | Ink overlay with pen, eraser, clear, six colors, and four widths |
| Rotate page | Left/right commands with normalized 0/90/180/270° state |
| Headers/footers/page numbers | Editable header/footer and toggleable page number |
| Print | Native WPF `PrintDialog` prints the page visual |
| Search | Case-insensitive cross-page model search and result selection |
| Extract images | Saves inserted images directly or exports a linked PDF page as a rendered PNG |
| Underline | Applies underline formatting to selected text |
| Comments | Page-scoped comment creation and right-panel display |
| Save | `.cpdf` save/save-as with document and annotation state plus real PDF export through PDFsharp-WPF |
| Top menus/submenus | File, Edit, View, Insert, Annotate, Tools, Page, Form, Window, Help; nested PDF import submenu |

## Production hardening backlog

These items require a real PDF SDK rather than additional UI work:

- Native text/image object extraction and direct object-level editing
- Standards-compliant PDF writing, incremental updates, and encryption
- Cryptographic certificate-backed signatures
- OCR, scanner provider integration, and AcroForm authoring
- Large-document virtualization and conformance testing across real PDF corpora
