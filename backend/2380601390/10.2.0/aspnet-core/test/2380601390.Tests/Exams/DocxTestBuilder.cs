using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace _2380601390.Tests.Exams;

/// <summary>Sinh file .docx trong bộ nhớ để kiểm thử parser (không phụ thuộc file ngoài).</summary>
public class DocxTestBuilder
{
    private static readonly byte[] PngHeader = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private readonly MemoryStream _ms = new MemoryStream();
    private readonly WordprocessingDocument _doc;
    private readonly MainDocumentPart _main;
    private readonly Body _body;
    private uint _nextId = 1;

    public DocxTestBuilder()
    {
        _doc = WordprocessingDocument.Create(_ms, WordprocessingDocumentType.Document);
        _main = _doc.AddMainDocumentPart();
        _main.Document = new Document(new Body());
        _body = _main.Document.Body;
    }

    /// <summary>PNG hợp lệ, mỗi seed cho một nội dung (và SHA-256) khác nhau.</summary>
    public static byte[] Png(int seed = 0) => PngHeader.Concat(Enumerable.Repeat((byte)seed, seed + 1)).ToArray();

    public DocxTestBuilder Line(string text, bool splitRuns = false)
    {
        _body.AppendChild(MakeParagraph(text, splitRuns));
        return this;
    }

    public DocxTestBuilder Lines(IEnumerable<string> lines, bool splitRuns = false)
    {
        foreach (var l in lines) Line(l, splitRuns);
        return this;
    }

    /// <summary>Đoạn chứa chữ ở giữa, hoặc chỉ ảnh khi text = null.</summary>
    public DocxTestBuilder ImageLine(byte[] data, string alt = "alt", bool anchored = false, bool linked = false)
    {
        var p = new Paragraph();
        p.AppendChild(new Run(MakeDrawing(data, alt, anchored, linked)));
        _body.AppendChild(p);
        return this;
    }

    public DocxTestBuilder Table(params string[][] rows)
    {
        var t = new Table();
        foreach (var r in rows)
        {
            var tr = new TableRow();
            foreach (var c in r)
            {
                tr.AppendChild(new TableCell(MakeParagraph(c, false)));
            }

            t.AppendChild(tr);
        }

        _body.AppendChild(t);
        return this;
    }

    public MemoryStream Build()
    {
        _main.Document.Save();
        _doc.Dispose();
        var copy = new MemoryStream(_ms.ToArray());
        return copy;
    }

    private static Paragraph MakeParagraph(string text, bool splitRuns)
    {
        var p = new Paragraph();
        if (!splitRuns)
        {
            p.AppendChild(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
            return p;
        }

        // Word hay tách một dòng thẻ thành nhiều run (đổi định dạng, spell-check...).
        var i = 0;
        var n = 0;
        while (i < text.Length)
        {
            var len = Math.Min(3 + (n % 4), text.Length - i);
            var run = new Run(new Text(text.Substring(i, len)) { Space = SpaceProcessingModeValues.Preserve });
            if (n % 2 == 1) run.PrependChild(new RunProperties(new Bold()));
            p.AppendChild(run);
            i += len;
            n++;
        }

        return p;
    }

    private Drawing MakeDrawing(byte[] data, string alt, bool anchored, bool linked)
    {
        string rid = null;
        if (!linked)
        {
            var part = _main.AddImagePart(ImagePartType.Png);
            using (var s = new MemoryStream(data)) part.FeedData(s);
            rid = _main.GetIdOfPart(part);
        }

        var id = _nextId++;
        var graphic = new A.Graphic(
            new A.GraphicData(
                new PIC.Picture(
                    new PIC.NonVisualPictureProperties(
                        new PIC.NonVisualDrawingProperties { Id = 0U, Name = "img" + id + ".png" },
                        new PIC.NonVisualPictureDrawingProperties()),
                    new PIC.BlipFill(
                        linked ? new A.Blip { Link = "rIdMissing" } : new A.Blip { Embed = rid },
                        new A.Stretch(new A.FillRectangle())),
                    new PIC.ShapeProperties(
                        new A.Transform2D(new A.Offset { X = 0L, Y = 0L }, new A.Extents { Cx = 990000L, Cy = 792000L }),
                        new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
            { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" });

        if (anchored)
        {
            return new Drawing(new DW.Anchor(
                new DW.Extent { Cx = 990000L, Cy = 792000L },
                new DW.DocProperties { Id = id, Name = "img" + id, Description = alt },
                graphic));
        }

        return new Drawing(new DW.Inline(
            new DW.Extent { Cx = 990000L, Cy = 792000L },
            new DW.DocProperties { Id = id, Name = "img" + id, Description = alt },
            graphic));
    }
}
