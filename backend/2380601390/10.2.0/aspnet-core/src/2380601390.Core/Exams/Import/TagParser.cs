using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace _2380601390.Exams.Import;

/// <summary>
/// Phân tích các DocItem theo chuẩn tags v1 thành ParsedExam (máy trạng thái có stack mở/đóng).
/// Chỉ kiểm tra cấu trúc, cú pháp thẻ, vị trí nội dung và ảnh. Các ràng buộc ngữ nghĩa
/// (số câu, đáp án, chỗ trống...) do ExamValidator xử lý.
/// </summary>
public class TagParser
{
    private static readonly HashSet<string> KnownTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "EXAM", "PART", "GROUP", "DOCUMENT", "QUESTION", "OPTION", "ANSWER", "EXPLANATION"
    };

    private static readonly Dictionary<string, (string[] Required, string[] Optional)> AttrSpec =
        new Dictionary<string, (string[], string[])>
        {
            ["EXAM"] = (new[] { "id", "tagsv", "title", "duration", "total" }, new string[0]),
            ["PART"] = (new[] { "no", "from", "to", "shuffle-items" }, new string[0]),
            ["GROUP"] = (new[] { "id" }, new[] { "shuffle-questions" }),
            ["DOCUMENT"] = (new[] { "id" }, new[] { "title" }),
            ["QUESTION"] = (new[] { "id", "no", "shuffle-options" }, new string[0]),
            ["OPTION"] = (new[] { "id" }, new string[0]),
            ["ANSWER"] = (new[] { "ref" }, new string[0]),
            ["EXPLANATION"] = (new string[0], new string[0]),
        };

    private static readonly Regex TagRx = new Regex(@"^\s*\[\s*(/)?\s*([A-Za-z]+)([^\]]*)\]\s*(.*)$", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex AttrRx = new Regex(@"\G\s*([A-Za-z][A-Za-z0-9-]*)\s*=\s*(?:""([^""]*)""|([^\s""]+))", RegexOptions.Compiled);
    private static readonly Regex IdRx = new Regex(@"^[A-Za-z0-9_-]{1,40}$", RegexOptions.Compiled);
    private static readonly string[] OptionCodes = { "A", "B", "C", "D" };

    private class Frame
    {
        public string Tag;
        public int Paragraph;
        public string Label;
        public object Obj;
        public List<string> Lines = new List<string>();
        public int FirstLinePara;
        public int QState; // QUESTION: 0 thân, 1 có option, 2 đã có ANSWER, 3 đã có EXPLANATION
        public bool StemSet;
    }

    private readonly ExamImportOptions _opt;
    private readonly List<ImportError> _errors = new List<ImportError>();
    private readonly List<Frame> _frames = new List<Frame>();
    private ParsedExam _exam;
    private bool _examSeen;

    private TagParser(ExamImportOptions opt)
    {
        _opt = opt;
    }

    public static ImportResult Parse(IList<DocItem> items, ExamImportOptions opt)
    {
        var p = new TagParser(opt);
        foreach (var item in items)
        {
            if (item.IsTable) p.HandleTable(item);
            else p.HandleParagraph(item);
        }

        p.Finish();
        return new ImportResult { Exam = p._exam, Errors = p._errors };
    }

    // ---------- tiện ích báo lỗi ----------

    private Frame Top => _frames.Count > 0 ? _frames[_frames.Count - 1] : null;

    private Frame Nearest(string tag) => _frames.LastOrDefault(f => f.Tag == tag);

    private void Err(string code, string text, DocItem item, string tag = null, int? table = null)
    {
        var part = Nearest("PART")?.Obj as ParsedPart;
        var group = Nearest("GROUP")?.Obj as ParsedGroup;
        var q = Nearest("QUESTION")?.Obj as ParsedQuestion;
        _errors.Add(ImportError.Create(
            code, text,
            paragraph: item != null && !item.IsTable ? item.Index : (int?)null,
            table: item != null && item.IsTable ? item.Index : table,
            partNo: part != null && part.PartNo != 0 ? part.PartNo : (int?)null,
            groupCode: group?.Code,
            questionNo: q != null && q.No != 0 ? q.No : (int?)null,
            tag: tag));
    }

    private static string SmartQuotesToAscii(string s)
    {
        // Giữ nguyên độ dài chuỗi để chỉ số vẫn trỏ đúng vào văn bản gốc.
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            switch (ch)
            {
                case '“': case '”': case '„': case '«': case '»': sb.Append('"'); break;
                case '‘': case '’': sb.Append('\''); break;
                default: sb.Append(ch); break;
            }
        }

        return sb.ToString();
    }

    // ---------- bảng ----------

    private void HandleTable(DocItem t)
    {
        var tagFound = false;
        for (var r = 0; r < t.Rows.Count; r++)
        {
            for (var c = 0; c < t.Rows[r].Count; c++)
            {
                if (DocxReader.TagLikeRegex.IsMatch(t.Rows[r][c]))
                {
                    tagFound = true;
                    Err("E_TAG_IN_TABLE",
                        "hàng " + (r + 1) + ", ô " + (c + 1) + ": không được đặt thẻ '" + Short(t.Rows[r][c]) + "' trong bảng.",
                        t, Short(t.Rows[r][c]));
                }
            }
        }

        if (t.ImageCountInTable > 0)
        {
            Err("E_IMAGE_UNSUPPORTED", "ảnh/đối tượng trong bảng chưa được hỗ trợ; hãy đặt ảnh ngoài bảng.", t);
        }

        if (tagFound) return;

        var lines = t.Rows
            .Select(row => string.Join(" | ", row.Where(x => !string.IsNullOrWhiteSpace(x))))
            .Where(x => x.Length > 0)
            .ToList();
        if (lines.Count == 0) return;

        var top = Top;
        if (top != null && (top.Tag == "DOCUMENT" || top.Tag == "EXPLANATION"))
        {
            foreach (var l in lines) AddLine(top, l, t.Index);
        }
        else
        {
            Err("E_STRAY_TEXT", "bảng chứa nội dung nằm ngoài [DOCUMENT]/[EXPLANATION]: '" + Short(lines[0]) + "'.", t);
        }
    }

    // ---------- đoạn văn ----------

    private void HandleParagraph(DocItem item)
    {
        foreach (var s in item.Segments.Where(s => s.Kind == SegmentKind.Unsupported))
        {
            Err(s.ErrorCode, s.ErrorText, item);
        }

        var norm = SmartQuotesToAscii(item.Text);
        var m = TagRx.Match(norm);
        if (m.Success)
        {
            var name = m.Groups[2].Value;
            var isKnown = KnownTags.Contains(name);
            var isUnknownCaps = !isKnown && name.Length >= 3 && name == name.ToUpperInvariant();
            if (isKnown || isUnknownCaps)
            {
                if (isUnknownCaps)
                {
                    Err("E_TAG_UNKNOWN", "thẻ '" + Short(item.Text) + "' không thuộc chuẩn tags v1.", item, Short(item.Text));
                    return;
                }

                if (item.Segments.Any(s => s.Kind == SegmentKind.Image))
                {
                    Err("E_IMAGE_PLACEMENT", "ảnh không được nằm trong dòng thẻ; hãy đặt ảnh ở đoạn riêng trong [DOCUMENT].", item, Short(item.Text));
                }

                var remainder = item.Text.Substring(m.Groups[4].Index).Trim();
                HandleTag(m.Groups[1].Success, name.ToUpperInvariant(), m.Groups[3].Value, remainder, item);
                return;
            }
        }

        HandleContent(item);
    }

    // ---------- thẻ ----------

    private void HandleTag(bool isClose, string name, string attrRaw, string remainder, DocItem item)
    {
        var tagText = Short(item.Text);
        if (isClose)
        {
            if (!string.IsNullOrWhiteSpace(attrRaw) || remainder.Length > 0)
            {
                Err("E_TAG_SYNTAX", "thẻ đóng '" + tagText + "' không được có thuộc tính hoặc nội dung đi kèm.", item, tagText);
            }

            Close(name, item);
            return;
        }

        if (name != "OPTION" && remainder.Length > 0)
        {
            Err("E_TAG_SYNTAX", "sau thẻ [" + name + "] không được có nội dung cùng dòng; hãy xuống dòng.", item, tagText);
        }

        var attrs = ParseAttrs(name, attrRaw, item, tagText);

        switch (name)
        {
            case "EXAM": OpenExam(attrs, item); break;
            case "PART": OpenPart(attrs, item); break;
            case "GROUP": OpenGroup(attrs, item); break;
            case "DOCUMENT": OpenDocument(attrs, item); break;
            case "QUESTION": OpenQuestion(attrs, item); break;
            case "OPTION": AddOption(attrs, remainder, item); break;
            case "ANSWER": SetAnswer(attrs, item); break;
            case "EXPLANATION": OpenExplanation(item); break;
        }
    }

    private Dictionary<string, string> ParseAttrs(string tag, string raw, DocItem item, string tagText)
    {
        var dict = new Dictionary<string, string>();
        var pos = 0;
        while (pos < raw.Length)
        {
            if (string.IsNullOrWhiteSpace(raw.Substring(pos))) break;
            var m = AttrRx.Match(raw, pos);
            if (!m.Success || m.Index != pos)
            {
                Err("E_TAG_SYNTAX", "cú pháp thuộc tính sai tại '" + Short(raw.Substring(pos).Trim()) + "' (dạng đúng: key=value hoặc key=\"giá trị có khoảng trắng\").", item, tagText);
                break;
            }

            var key = m.Groups[1].Value.ToLowerInvariant();
            var val = m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value;
            if (dict.ContainsKey(key))
            {
                Err("E_ATTR_DUP", "thuộc tính '" + key + "' bị khai báo hai lần trong thẻ [" + tag + "].", item, tagText);
            }
            else
            {
                dict[key] = val;
            }

            pos = m.Index + m.Length;
        }

        var spec = AttrSpec[tag];
        foreach (var k in dict.Keys.ToList())
        {
            if (!spec.Required.Contains(k) && !spec.Optional.Contains(k))
            {
                Err("E_ATTR_UNKNOWN", "thuộc tính '" + k + "' không hợp lệ cho thẻ [" + tag + "].", item, tagText);
            }
        }

        foreach (var k in spec.Required)
        {
            if (!dict.ContainsKey(k))
            {
                Err("E_ATTR_MISSING", "thẻ [" + tag + "] thiếu thuộc tính bắt buộc '" + k + "'.", item, tagText);
            }
        }

        return dict;
    }

    private string Id(Dictionary<string, string> a, string key, DocItem item)
    {
        if (!a.TryGetValue(key, out var v)) return null;
        if (!IdRx.IsMatch(v))
        {
            Err("E_ATTR_INVALID", "giá trị " + key + "='" + Short(v) + "' không hợp lệ (chỉ gồm chữ, số, '_' hoặc '-', tối đa 40 ký tự).", item);
            return null;
        }

        return v;
    }

    private int Int(Dictionary<string, string> a, string key, DocItem item)
    {
        if (!a.TryGetValue(key, out var v)) return 0;
        if (!int.TryParse(v, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n) || n <= 0)
        {
            Err("E_ATTR_INVALID", "thuộc tính " + key + "='" + Short(v) + "' phải là số nguyên dương.", item);
            return 0;
        }

        return n;
    }

    private bool OnOff(Dictionary<string, string> a, string key, DocItem item)
    {
        if (!a.TryGetValue(key, out var v)) return false;
        if (v.Equals("on", StringComparison.OrdinalIgnoreCase)) return true;
        if (v.Equals("off", StringComparison.OrdinalIgnoreCase)) return false;
        Err("E_ATTR_INVALID", "thuộc tính " + key + "='" + Short(v) + "' chỉ nhận 'on' hoặc 'off'.", item);
        return false;
    }

    private void Push(string tag, DocItem item, object obj)
    {
        _frames.Add(new Frame { Tag = tag, Paragraph = item.Index, Label = Short(item.Text), Obj = obj });
    }

    private void OrderErr(string tag, string expected, DocItem item)
    {
        var top = Top;
        Err("E_TAG_ORDER",
            "thẻ [" + tag + "] " + expected + (top != null ? " (hiện đang ở trong [" + top.Tag + "])." : "."),
            item, Short(item.Text));
    }

    private void OpenExam(Dictionary<string, string> a, DocItem item)
    {
        var exam = new ParsedExam { Paragraph = item.Index };
        exam.Id = Id(a, "id", item);
        if (a.TryGetValue("title", out var title))
        {
            if (string.IsNullOrWhiteSpace(title)) Err("E_ATTR_INVALID", "title không được rỗng.", item);
            exam.Title = title;
        }

        exam.DurationMinutes = Int(a, "duration", item);
        exam.TotalQuestions = Int(a, "total", item);
        var tv = Int(a, "tagsv", item);
        exam.TagsVersion = tv;
        if (tv != 0 && tv != _opt.SupportedTagsVersion)
        {
            Err("E_TAGSV_UNSUPPORTED", "phiên bản chuẩn tags tagsv=" + tv + " chưa được hỗ trợ (hỗ trợ: " + _opt.SupportedTagsVersion + ").", item);
        }

        if (_examSeen || _frames.Count > 0)
        {
            OrderErr("EXAM", "chỉ được xuất hiện một lần ở đầu tài liệu", item);
        }
        else
        {
            _exam = exam;
            _examSeen = true;
        }

        Push("EXAM", item, exam);
    }

    private void OpenPart(Dictionary<string, string> a, DocItem item)
    {
        var part = new ParsedPart { Paragraph = item.Index };
        var no = Int(a, "no", item);
        part.From = Int(a, "from", item);
        part.To = Int(a, "to", item);
        part.ShuffleItems = OnOff(a, "shuffle-items", item);
        var ok = true;

        if (no != 0 && no != 5 && no != 6 && no != 7)
        {
            Err("E_ATTR_INVALID", "no=" + no + " không hợp lệ, Part chỉ nhận 5, 6 hoặc 7.", item);
            no = 0;
            ok = false;
        }

        part.PartNo = no;
        if (part.From != 0 && part.To != 0 && part.From > part.To)
        {
            Err("E_ATTR_INVALID", "from=" + part.From + " lớn hơn to=" + part.To + ".", item);
            ok = false;
        }

        if (Top?.Tag != "EXAM")
        {
            OrderErr("PART", "phải nằm trực tiếp trong [EXAM]", item);
            ok = false;
        }
        else if (no != 0)
        {
            var exam = (ParsedExam)Top.Obj;
            if (exam.Parts.Any(p => p.PartNo == no))
            {
                Err("E_PART_DUP", "Part " + no + " được khai báo nhiều lần.", item);
                ok = false;
            }
            else if (exam.Parts.Count > 0 && no < exam.Parts[exam.Parts.Count - 1].PartNo)
            {
                Err("E_PART_ORDER", "các Part phải xuất hiện theo thứ tự 5, 6, 7.", item);
                ok = false;
            }
        }
        else
        {
            ok = false;
        }

        if (ok) ((ParsedExam)Top.Obj).Parts.Add(part);
        Push("PART", item, part);
    }

    private void OpenGroup(Dictionary<string, string> a, DocItem item)
    {
        var g = new ParsedGroup { Code = Id(a, "id", item), Paragraph = item.Index };
        g.ShuffleQuestions = OnOff(a, "shuffle-questions", item);

        if (Top?.Tag != "PART")
        {
            OrderErr("GROUP", "phải nằm trực tiếp trong [PART 6] hoặc [PART 7]", item);
        }
        else
        {
            var part = (ParsedPart)Top.Obj;
            if (part.PartNo == 5)
            {
                Err("E_GROUP_IN_PART5", "Part 5 là câu độc lập, không được dùng [GROUP].", item, Short(item.Text));
            }
            else
            {
                part.Groups.Add(g);
            }
        }

        Push("GROUP", item, g);
    }

    private void OpenDocument(Dictionary<string, string> a, DocItem item)
    {
        var d = new ParsedDocument { Code = Id(a, "id", item), Paragraph = item.Index };
        if (a.TryGetValue("title", out var title)) d.Title = title;

        if (Top?.Tag != "GROUP")
        {
            OrderErr("DOCUMENT", "phải nằm trực tiếp trong [GROUP]", item);
        }
        else
        {
            var g = (ParsedGroup)Top.Obj;
            if (g.Questions.Count > 0)
            {
                OrderErr("DOCUMENT", "phải đặt trước các [QUESTION] của nhóm", item);
            }
            else
            {
                g.Documents.Add(d);
            }
        }

        Push("DOCUMENT", item, d);
    }

    private void OpenQuestion(Dictionary<string, string> a, DocItem item)
    {
        var q = new ParsedQuestion { Code = Id(a, "id", item), No = Int(a, "no", item), Paragraph = item.Index };
        q.ShuffleOptions = OnOff(a, "shuffle-options", item);

        var top = Top;
        if (top?.Tag == "PART")
        {
            var part = (ParsedPart)top.Obj;
            if (part.PartNo == 5) part.Questions.Add(q);
            else
            {
                Err("E_QUESTION_OUTSIDE_GROUP", "câu hỏi Part " + part.PartNo + " phải nằm trong một [GROUP].", item, Short(item.Text));
            }
        }
        else if (top?.Tag == "GROUP")
        {
            ((ParsedGroup)top.Obj).Questions.Add(q);
        }
        else
        {
            OrderErr("QUESTION", "phải nằm trong [PART 5] hoặc trong [GROUP]", item);
        }

        _frames.Add(new Frame { Tag = "QUESTION", Paragraph = item.Index, Label = Short(item.Text), Obj = q });
    }

    private void AddOption(Dictionary<string, string> a, string text, DocItem item)
    {
        var top = Top;
        if (top?.Tag != "QUESTION" || top.QState >= 2)
        {
            OrderErr("OPTION", "phải nằm trong [QUESTION] và đặt trước [ANSWER]", item);
            return;
        }

        var q = (ParsedQuestion)top.Obj;
        FinalizeStem(top);
        top.QState = 1;

        a.TryGetValue("id", out var id);
        if (id != null && !OptionCodes.Contains(id))
        {
            Err("E_ATTR_INVALID", "mã lựa chọn '" + Short(id) + "' không hợp lệ, chỉ nhận A, B, C hoặc D (chữ hoa).", item, Short(item.Text));
            return;
        }

        if (id == null) return;
        if (q.Options.Any(o => o.Code == id))
        {
            Err("E_OPTION_DUP", "lựa chọn '" + id + "' bị khai báo hai lần.", item, Short(item.Text));
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            Err("E_OPTION_EMPTY", "lựa chọn '" + id + "' không có nội dung (nội dung phải nằm cùng dòng với thẻ).", item, Short(item.Text));
        }

        q.Options.Add(new ParsedOption { Code = id, Text = text, Paragraph = item.Index });
    }

    private void SetAnswer(Dictionary<string, string> a, DocItem item)
    {
        var top = Top;
        if (top?.Tag != "QUESTION")
        {
            OrderErr("ANSWER", "phải nằm trong [QUESTION]", item);
            return;
        }

        var q = (ParsedQuestion)top.Obj;
        if (top.QState == 0)
        {
            OrderErr("ANSWER", "phải đặt sau các [OPTION]", item);
            return;
        }

        if (top.QState >= 2)
        {
            Err("E_ANSWER_DUP", "câu hỏi có nhiều hơn một [ANSWER] hoặc [ANSWER] đặt sau [EXPLANATION].", item, Short(item.Text));
            return;
        }

        top.QState = 2;
        if (a.TryGetValue("ref", out var r)) q.AnswerRef = r;
    }

    private void OpenExplanation(DocItem item)
    {
        var top = Top;
        if (top?.Tag != "QUESTION")
        {
            OrderErr("EXPLANATION", "phải nằm trong [QUESTION]", item);
            Push("EXPLANATION", item, null);
            return;
        }

        if (top.QState == 1)
        {
            Err("E_ANSWER_MISSING", "thiếu [ANSWER] trước [EXPLANATION].", item, Short(item.Text));
        }
        else if (top.QState == 0)
        {
            OrderErr("EXPLANATION", "phải đặt sau các [OPTION] và [ANSWER]", item);
        }
        else if (top.QState == 3)
        {
            Err("E_EXPLANATION_DUP", "câu hỏi có nhiều hơn một [EXPLANATION].", item, Short(item.Text));
        }

        top.QState = 3;
        Push("EXPLANATION", item, top.Obj);
    }

    // ---------- đóng thẻ ----------

    private void Close(string name, DocItem item)
    {
        var idx = _frames.FindLastIndex(f => f.Tag == name);
        if (idx < 0)
        {
            Err("E_TAG_UNEXPECTED_CLOSE", "thẻ đóng [/" + name + "] không có thẻ mở tương ứng.", item, Short(item.Text));
            return;
        }

        for (var i = _frames.Count - 1; i > idx; i--)
        {
            var f = _frames[i];
            Err("E_TAG_UNCLOSED", "thẻ " + f.Label + " mở ở đoạn " + f.Paragraph + " chưa được đóng trước [/" + name + "].", item, f.Label);
            FinalizeFrame(f);
        }

        FinalizeFrame(_frames[idx]);
        _frames.RemoveRange(idx, _frames.Count - idx);
    }

    private void Finish()
    {
        for (var i = _frames.Count - 1; i >= 0; i--)
        {
            var f = _frames[i];
            _errors.Add(ImportError.Create("E_TAG_UNCLOSED",
                "thẻ " + f.Label + " mở ở đoạn " + f.Paragraph + " chưa được đóng ([/" + f.Tag + "] bị thiếu trước khi hết tệp).",
                paragraph: f.Paragraph, tag: f.Label));
            FinalizeFrame(f);
        }

        _frames.Clear();
        if (!_examSeen)
        {
            _errors.Add(ImportError.Create("E_TAG_MISSING", "không tìm thấy thẻ [EXAM] mở đầu tài liệu."));
        }
    }

    private void FinalizeFrame(Frame f)
    {
        switch (f.Tag)
        {
            case "DOCUMENT":
                FlushText(f);
                break;
            case "QUESTION":
                FinalizeStem(f);
                break;
            case "EXPLANATION":
                if (f.Obj is ParsedQuestion q)
                {
                    q.Explanation = string.Join("\n", f.Lines).Trim();
                }

                break;
        }
    }

    private static void FinalizeStem(Frame f)
    {
        if (f.StemSet) return;
        f.StemSet = true;
        ((ParsedQuestion)f.Obj).Stem = string.Join("\n", f.Lines).Trim();
        f.Lines.Clear();
    }

    // ---------- nội dung ngoài thẻ ----------

    private static void AddLine(Frame f, string line, int paragraph)
    {
        if (f.Lines.Count == 0) f.FirstLinePara = paragraph;
        f.Lines.Add(line);
    }

    private void FlushText(Frame docFrame)
    {
        if (docFrame.Lines.Count == 0) return;
        var doc = (ParsedDocument)docFrame.Obj;
        doc.Blocks.Add(new ParsedBlock
        {
            Type = ContentBlockType.Text,
            Text = string.Join("\n", docFrame.Lines),
            Paragraph = docFrame.FirstLinePara
        });
        docFrame.Lines.Clear();
    }

    private void HandleContent(DocItem item)
    {
        var top = Top;
        var texts = item.Segments.Where(s => s.Kind == SegmentKind.Text && !string.IsNullOrWhiteSpace(s.Text)).ToList();
        var hasImage = item.Segments.Any(s => s.Kind == SegmentKind.Image);
        if (texts.Count == 0 && !hasImage) return;

        if (top != null && top.Tag == "DOCUMENT")
        {
            var doc = (ParsedDocument)top.Obj;
            foreach (var s in item.Segments)
            {
                if (s.Kind == SegmentKind.Text)
                {
                    if (!string.IsNullOrWhiteSpace(s.Text)) AddLine(top, s.Text.Trim(), item.Index);
                }
                else if (s.Kind == SegmentKind.Image)
                {
                    FlushText(top);
                    var img = BuildImage(s, item);
                    if (img != null)
                    {
                        doc.Blocks.Add(new ParsedBlock { Type = ContentBlockType.Image, Image = img, Paragraph = item.Index });
                    }
                }
            }

            return;
        }

        if (hasImage)
        {
            Err("E_IMAGE_PLACEMENT", "ảnh chỉ được đặt trong [DOCUMENT] (đoạn riêng giữa [DOCUMENT] và [/DOCUMENT]).", item);
        }

        if (texts.Count == 0) return;
        var joined = string.Join(" ", texts.Select(s => s.Text.Trim()));

        if (top != null && top.Tag == "QUESTION" && top.QState == 0)
        {
            AddLine(top, joined, item.Index);
        }
        else if (top != null && top.Tag == "EXPLANATION")
        {
            AddLine(top, joined, item.Index);
        }
        else
        {
            Err("E_STRAY_TEXT", "nội dung nằm ngoài thẻ hợp lệ: '" + Short(joined) + "'.", item);
        }
    }

    private ParsedImage BuildImage(Segment s, DocItem item)
    {
        var data = s.ImageData;
        if (data == null || data.Length == 0)
        {
            Err("E_IMAGE_UNREADABLE", "ảnh rỗng hoặc không đọc được.", item);
            return null;
        }

        if (data.Length > _opt.MaxImageBytes)
        {
            Err("E_IMAGE_TOO_LARGE", "ảnh " + (data.Length / 1024) + " KB vượt giới hạn " + (_opt.MaxImageBytes / 1024) + " KB.", item);
            return null;
        }

        var type = DetectImageType(data);
        if (type == null)
        {
            Err("E_IMAGE_UNSUPPORTED", "định dạng ảnh '" + s.ContentType + "' không được hỗ trợ (chấp nhận PNG, JPEG, GIF, BMP) hoặc dữ liệu ảnh hỏng.", item);
            return null;
        }

        using var sha = SHA256.Create();
        var hash = BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
        return new ParsedImage { Data = data, ContentType = type, Sha256 = hash, AltText = s.AltText };
    }

    /// <summary>Xác định kiểu ảnh theo chữ ký byte (không tin vào content-type khai báo).</summary>
    public static string DetectImageType(byte[] d)
    {
        if (d.Length >= 8 && d[0] == 0x89 && d[1] == 0x50 && d[2] == 0x4E && d[3] == 0x47) return "image/png";
        if (d.Length >= 3 && d[0] == 0xFF && d[1] == 0xD8 && d[2] == 0xFF) return "image/jpeg";
        if (d.Length >= 6 && d[0] == 'G' && d[1] == 'I' && d[2] == 'F' && d[3] == '8') return "image/gif";
        if (d.Length >= 2 && d[0] == 'B' && d[1] == 'M') return "image/bmp";
        return null;
    }

    private static string Short(string s)
    {
        s = (s ?? "").Trim();
        return s.Length <= 60 ? s : s.Substring(0, 57) + "...";
    }
}
