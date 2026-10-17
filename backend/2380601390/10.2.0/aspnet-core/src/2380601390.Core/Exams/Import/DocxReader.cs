using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace _2380601390.Exams.Import;

public enum SegmentKind
{
    Text,
    Image,
    Unsupported
}

/// <summary>Một mẩu trong đoạn văn, theo đúng thứ tự xuất hiện trong Word.</summary>
public class Segment
{
    public SegmentKind Kind { get; set; }
    public string Text { get; set; }

    public byte[] ImageData { get; set; }
    public string ContentType { get; set; }
    public string AltText { get; set; }

    /// <summary>Với Unsupported: mã lỗi và mô tả.</summary>
    public string ErrorCode { get; set; }
    public string ErrorText { get; set; }
}

public class DocItem
{
    public bool IsTable { get; set; }

    /// <summary>Số thứ tự đoạn (1..) hoặc số thứ tự bảng (1..), đếm riêng ở mức body.</summary>
    public int Index { get; set; }

    // Paragraph
    public List<Segment> Segments { get; set; } = new List<Segment>();

    /// <summary>Văn bản của đoạn: ghép mọi run, đã bỏ ký tự rộng 0, NBSP -> khoảng trắng.</summary>
    public string Text { get; set; } = "";

    // Table
    public List<List<string>> Rows { get; set; } = new List<List<string>>();
    public int ImageCountInTable { get; set; }
}

public class DocxReadResult
{
    public List<DocItem> Items { get; set; } = new List<DocItem>();
    public List<ImportError> Errors { get; set; } = new List<ImportError>();
}

/// <summary>
/// Đọc .docx thành danh sách item theo thứ tự body. Xử lý trường hợp Word tách một dòng thẻ
/// thành nhiều run bằng cách ghép toàn bộ văn bản các run trong cùng một đoạn.
/// </summary>
public static class DocxReader
{
    public static readonly Regex TagLikeRegex = new Regex(@"^\s*\[\s*/?\s*[A-Za-z]+[^\]]*\]", RegexOptions.Compiled);

    public static DocxReadResult Read(Stream input)
    {
        var result = new DocxReadResult();
        MemoryStream ms;
        try
        {
            ms = new MemoryStream();
            input.CopyTo(ms);
            ms.Position = 0;
        }
        catch (Exception)
        {
            result.Errors.Add(ImportError.Create("E_FILE_INVALID", "Không đọc được tệp tải lên."));
            return result;
        }

        if (ms.Length < 4 || ms.ReadByte() != 'P' || ms.ReadByte() != 'K')
        {
            result.Errors.Add(ImportError.Create("E_FILE_INVALID", "Tệp không phải .docx hợp lệ (không phải gói Office Open XML)."));
            return result;
        }
        ms.Position = 0;

        try
        {
            using var doc = WordprocessingDocument.Open(ms, false);
            var main = doc.MainDocumentPart;
            var body = main?.Document?.Body;
            if (body == null)
            {
                result.Errors.Add(ImportError.Create("E_FILE_INVALID", "Tệp .docx không có nội dung (thiếu phần body)."));
                return result;
            }

            var pIndex = 0;
            var tIndex = 0;
            foreach (var el in BodyElements(body))
            {
                if (el is Paragraph p)
                {
                    pIndex++;
                    var item = new DocItem { Index = pIndex };
                    ReadParagraph(p, main, item);
                    result.Items.Add(item);
                }
                else if (el is Table t)
                {
                    tIndex++;
                    result.Items.Add(ReadTable(t, main, tIndex));
                }
            }

            ScanOtherAreas(main, result);
        }
        catch (Exception ex) when (ex is OpenXmlPackageException || ex is InvalidDataException || ex is IOException
                                   || ex is FileFormatException || ex is System.Xml.XmlException || ex is InvalidOperationException
                                   || ex is ArgumentException || ex is UnauthorizedAccessException)
        {
            result.Items.Clear();
            result.Errors.Add(ImportError.Create("E_FILE_INVALID", "Tệp không phải .docx hợp lệ hoặc không đọc được (" + ex.GetType().Name + ")."));
        }

        return result;
    }

    private static IEnumerable<OpenXmlElement> BodyElements(OpenXmlElement parent)
    {
        foreach (var child in parent.ChildElements)
        {
            if (child is Paragraph || child is Table)
            {
                yield return child;
            }
            else if (child is SdtBlock sdt)
            {
                var content = sdt.SdtContentBlock;
                if (content != null)
                {
                    foreach (var inner in BodyElements(content)) yield return inner;
                }
            }
            else if (child is CustomXmlBlock cx)
            {
                foreach (var inner in BodyElements(cx)) yield return inner;
            }
        }
    }

    private static void ReadParagraph(Paragraph p, MainDocumentPart main, DocItem item)
    {
        var cur = new StringBuilder();
        var full = new StringBuilder();

        void FlushText()
        {
            if (cur.Length == 0) return;
            item.Segments.Add(new Segment { Kind = SegmentKind.Text, Text = cur.ToString() });
            cur.Clear();
        }

        void Append(string s)
        {
            cur.Append(s);
            full.Append(s);
        }

        void WalkRun(Run r)
        {
            foreach (var c in r.ChildElements)
            {
                switch (c)
                {
                    case Text t:
                        Append(Clean(t.Text));
                        break;
                    case TabChar _:
                        Append("\t");
                        break;
                    case Break _:
                    case CarriageReturn _:
                        Append("\n");
                        break;
                    case NoBreakHyphen _:
                        Append("-");
                        break;
                    case Drawing d:
                        FlushText();
                        item.Segments.Add(ReadDrawing(d, main));
                        break;
                    case Picture _:
                    case EmbeddedObject _:
                        FlushText();
                        item.Segments.Add(Unsupported("E_IMAGE_UNSUPPORTED",
                            "ảnh/đối tượng dạng cũ (VML/OLE) không được hỗ trợ; hãy chèn ảnh bằng Insert > Pictures, kiểu 'In line with text'."));
                        break;
                    default:
                        if (c.LocalName == "AlternateContent")
                        {
                            var choice = c.ChildElements.FirstOrDefault(x => x.LocalName == "Choice");
                            if (choice != null)
                            {
                                foreach (var d in choice.Descendants<Drawing>())
                                {
                                    FlushText();
                                    item.Segments.Add(ReadDrawing(d, main));
                                }
                            }
                        }
                        break;
                }
            }
        }

        void Walk(OpenXmlElement parent)
        {
            foreach (var child in parent.ChildElements)
            {
                switch (child)
                {
                    case ParagraphProperties _:
                    case DeletedRun _:
                    case MoveFromRun _:
                        break;
                    case Run r:
                        WalkRun(r);
                        break;
                    default:
                        Walk(child);
                        break;
                }
            }
        }

        Walk(p);
        FlushText();
        item.Text = full.ToString();
    }

    private static string Clean(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            if (ch == '​' || ch == '‌' || ch == '‍' || ch == '﻿' || ch == '­') continue;
            if (ch == ' ' || ch == ' ' || ch == ' ' || ch == '　' || (ch >= ' ' && ch <= ' '))
            {
                sb.Append(' ');
            }
            else
            {
                sb.Append(ch);
            }
        }

        return sb.ToString();
    }

    private static Segment Unsupported(string code, string text) =>
        new Segment { Kind = SegmentKind.Unsupported, ErrorCode = code, ErrorText = text };

    private static Segment ReadDrawing(Drawing d, MainDocumentPart main)
    {
        var blip = d.Descendants<A.Blip>().FirstOrDefault();
        if (blip == null)
        {
            if (d.Descendants<TextBoxContent>().Any())
            {
                var inner = string.Join(" ", d.Descendants<Paragraph>().Select(x => string.Concat(x.Descendants<Text>().Select(t => t.Text))));
                if (DocxReader.TagLikeRegex.IsMatch(inner))
                {
                    return Unsupported("E_TAG_UNSUPPORTED_AREA", "thẻ nằm trong hộp văn bản (textbox); thẻ chỉ được đặt trong đoạn văn thường của phần thân tài liệu.");
                }

                return Unsupported("E_UNSUPPORTED_CONTENT", "hộp văn bản (textbox) không được hỗ trợ; hãy đưa nội dung vào đoạn văn thường.");
            }

            return Unsupported("E_IMAGE_UNSUPPORTED", "đối tượng vẽ (shape/biểu đồ) không phải ảnh nhúng nên không được hỗ trợ.");
        }

        if (d.Anchor != null)
        {
            return Unsupported("E_IMAGE_UNSUPPORTED", "ảnh không phải dạng 'In line with text' (đang là ảnh nổi/floating).");
        }

        if (blip.Embed?.Value == null)
        {
            return Unsupported("E_IMAGE_UNSUPPORTED", "ảnh được liên kết ra ngoài, không nhúng trong tệp .docx.");
        }

        try
        {
            var part = main.GetPartById(blip.Embed.Value) as ImagePart;
            if (part == null)
            {
                return Unsupported("E_IMAGE_UNREADABLE", "không đọc được dữ liệu ảnh nhúng.");
            }

            using var s = part.GetStream();
            using var buf = new MemoryStream();
            s.CopyTo(buf);
            return new Segment
            {
                Kind = SegmentKind.Image,
                ImageData = buf.ToArray(),
                ContentType = part.ContentType,
                AltText = d.Inline?.DocProperties?.Description?.Value
            };
        }
        catch (Exception ex) when (ex is ArgumentOutOfRangeException || ex is ArgumentException || ex is IOException || ex is InvalidOperationException)
        {
            return Unsupported("E_IMAGE_UNREADABLE", "không đọc được dữ liệu ảnh nhúng.");
        }
    }

    private static DocItem ReadTable(Table t, MainDocumentPart main, int index)
    {
        var item = new DocItem { IsTable = true, Index = index };
        foreach (var row in t.Elements<TableRow>())
        {
            var cells = new List<string>();
            foreach (var cell in row.Elements<TableCell>())
            {
                var parts = new List<string>();
                foreach (var p in cell.Descendants<Paragraph>())
                {
                    var pi = new DocItem();
                    ReadParagraph(p, main, pi);
                    item.ImageCountInTable += pi.Segments.Count(s => s.Kind != SegmentKind.Text);
                    if (!string.IsNullOrWhiteSpace(pi.Text)) parts.Add(pi.Text.Trim());
                }

                cells.Add(string.Join(" ", parts));
            }

            item.Rows.Add(cells);
        }

        return item;
    }

    /// <summary>Thẻ trong header/footer/footnote/endnote không được hỗ trợ -> báo lỗi thay vì bỏ qua âm thầm.</summary>
    private static void ScanOtherAreas(MainDocumentPart main, DocxReadResult result)
    {
        void Scan(IEnumerable<Paragraph> paragraphs, string area)
        {
            foreach (var p in paragraphs)
            {
                var pi = new DocItem();
                ReadParagraph(p, main, pi);
                if (TagLikeRegex.IsMatch(pi.Text))
                {
                    result.Errors.Add(ImportError.Create("E_TAG_UNSUPPORTED_AREA",
                        "thẻ '" + pi.Text.Trim() + "' nằm trong " + area + "; thẻ chỉ được đặt trong phần thân tài liệu."));
                }
            }
        }

        foreach (var h in main.HeaderParts) Scan(h.Header?.Descendants<Paragraph>() ?? Enumerable.Empty<Paragraph>(), "header");
        foreach (var f in main.FooterParts) Scan(f.Footer?.Descendants<Paragraph>() ?? Enumerable.Empty<Paragraph>(), "footer");
        if (main.FootnotesPart?.Footnotes != null) Scan(main.FootnotesPart.Footnotes.Descendants<Paragraph>(), "footnote");
        if (main.EndnotesPart?.Endnotes != null) Scan(main.EndnotesPart.Endnotes.Descendants<Paragraph>(), "endnote");
    }
}
