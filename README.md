# Apache FOP for .NET

A modern, cross-platform **C# port of [Apache FOP](https://xmlgraphics.apache.org/fop/)**
(Formatting Objects Processor): read an [XSL-FO](https://www.w3.org/TR/xsl/) document and
render it to **PDF**, **plain text**, **Markdown** or **HTML**. It targets **.NET 10**, runs
anywhere .NET runs (Windows, Linux, macOS), and replaces FOP's Java AWT/Java2D/Batik stack
with managed libraries ([PdfSharp](https://www.nuget.org/packages/PdfSharp),
[SixLabors.ImageSharp](https://www.nuget.org/packages/SixLabors.ImageSharp),
[SixLabors.Fonts](https://www.nuget.org/packages/SixLabors.Fonts)).

> This is the C# port. The original Java sources live alongside it in this repository
> (`fop-core/`, `fop-util/`, `fop/`, …) and remain the source of truth for the port; see the
> upstream Apache FOP [`README`](./README) for the Java project. The .NET solution lives under
> [`dotnet/`](./dotnet).

## Packages

The port is split into many small component projects internally, but ships as **two NuGet
packages**:

| Package | Kind | Description |
|---------|------|-------------|
| [`FOP.Sharp`](https://www.nuget.org/packages/FOP.Sharp) | Library | The full engine in a single package. Reference it and use the `FopProcessor` facade (FO in → PDF out) or the text renderers. |
| [`FOP.Sharp.Cli`](https://www.nuget.org/packages/FOP.Sharp.Cli) | .NET tool | The `fop` command-line tool. Install globally, then run `fop in.fo out.pdf`. |

### What's inside the `FOP.Sharp` package

The `FOP.Sharp` library package is self-contained: it **bundles every component assembly** of the
port (you only ever reference `FOP.Sharp`). The bundled assemblies are:

| Assembly | Responsibility |
|----------|----------------|
| `Fop.Util` | Dependency-free foundational utilities |
| `Fop.Events` | Type-safe event broadcasting infrastructure |
| `Fop.DataTypes` | XSL-FO datatype layer (lengths, percentages, numerics, keeps) |
| `Fop.Traits` | Layout trait value types (`MinOptMax`, trait enums) |
| `Fop.Colors` | Managed colour model and colour parsing/serialization |
| `Fop.Configuration` | XML-backed configuration subsystem |
| `Fop.Fonts` | Font value types, metrics and registry |
| `Fop.Hyphenation` | Hyphenation data structures and Liang algorithm |
| `Fop.Pdf` | Low-level PDF object model |
| `Fop.Svg` | Static-SVG parser → renderer-neutral vector primitives |
| `Fop.Fo` | XSL-FO object model (typed FO tree with resolved properties) |
| `Fop.Core` | Engine core and the ImageSharp-backed image pipeline |
| `Fop.Layout` | Lays the FO tree out into a positioned area tree |
| `Fop.Render.Pdf.Native` | Native, PdfSharp-free PDF renderer (text/image/font embedding, encryption) |
| `Fop.Render.Pdf` | PdfSharp-based PDF renderer and the high-level `FopProcessor` facade |
| `Fop.Render.Text` | Plain-text / Markdown / HTML back-ends |

Its third-party NuGet dependencies (`PdfSharp`, `SixLabors.ImageSharp`, `SixLabors.Fonts`,
`SixLabors.ImageSharp.Drawing`) are declared on the package and restored automatically.

## Installation

```bash
# Library
dotnet add package FOP.Sharp

# Command-line tool (global)
dotnet tool install --global FOP.Sharp.Cli
```

## Usage

### Render XSL-FO to PDF (library)

```csharp
using Fop.Render.Pdf;

var processor = new FopProcessor();

// From a file
processor.ConvertFile("input.fo", "output.pdf");

// From a string, into a byte[]
byte[] pdf = processor.Convert(File.ReadAllText("input.fo"));

// From streams
using var fo  = File.OpenRead("input.fo");
using var pdf2 = File.Create("output.pdf");
processor.Convert(fo, pdf2);
```

Register custom fonts before converting (Liberation faces are bundled as the fallback):

```csharp
var processor = new FopProcessor();
processor.RegisterFont("MyFont-Regular.ttf", "My Font");
processor.RegisterFontsDirectory("./fonts");
processor.ConvertFile("input.fo", "output.pdf");
```

Use the native, PdfSharp-free renderer (supports font subsetting and encryption):

```csharp
byte[] pdf = processor.ConvertNative(File.ReadAllText("input.fo"));
```

### Render to plain text, Markdown or HTML (library)

```csharp
using Fop.Render.Text;

string text     = new PlainTextRenderer().Convert(foXml);
string markdown = new MarkdownRenderer().Convert(foXml);
string html     = new HtmlRenderer().Convert(foXml);
```

### Command-line tool

```bash
# Output format inferred from the extension
fop input.fo output.pdf
fop input.fo output.md

# Explicit flags
fop -fo input.fo -pdf output.pdf
fop -xml data.xml -xsl style.xsl -html output.html

# Register fonts / use the native renderer
fop -fo input.fo -pdf out.pdf -fontdir ./fonts -native
```

Run `fop -help` for the full option list.

## Building from source

```bash
cd dotnet
dotnet build Fop.slnx
dotnet test  Fop.slnx
```

Requires the **.NET 10 SDK** (pinned in [`dotnet/global.json`](./dotnet/global.json)).

To produce the NuGet packages locally:

```bash
cd dotnet
dotnet pack src/Fop/Fop.csproj         -c Release   # the Fop library
dotnet pack src/Fop.Cli/Fop.Cli.csproj -c Release   # the fop .NET tool
```

## Status

The port is an incremental, bottom-up effort. A working end-to-end FO → PDF pipeline already
covers a substantial XSL-FO subset (block/inline text, fonts, colour, the box model, tables,
lists, static content, keeps & breaks, footnotes, floats, markers, links, leaders, hyphenation,
Knuth–Plass line breaking, embedded SVG, bookmarks and more). See
[`dotnet/CLAUDE.md`](./dotnet/CLAUDE.md) for the architecture and
[`dotnet/TODO.md`](./dotnet/TODO.md) for the phased plan and current progress.

## License

Licensed under the [Apache License, Version 2.0](./LICENSE), the same license as upstream
Apache FOP.
