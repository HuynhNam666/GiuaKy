# -*- coding: utf-8 -*-
"""Sinh file Word đề mẫu theo chuẩn tags v1 (docs/PHAN_TICH_THIET_KE.md §4) từ dữ liệu đã làm sạch.

Chạy:  python docs/sample/build_sample.py
Kết quả: docs/sample/TOEIC_Reading_Test05.docx  và  docs/sample/DAP_AN_Test05.md
Ảnh logo nằm trong docs/sample/images (tạo bằng make_images.ps1).
"""
import os
import sys
import zipfile
from xml.sax.saxutils import escape

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from exam05_part56 import P5, P6  # noqa: E402
from exam05_part7 import G  # noqa: E402

OUT = os.path.join(HERE, "TOEIC_Reading_Test05.docx")
KEY = os.path.join(HERE, "DAP_AN_Test05.md")

EXAM_ID = "RC-TEST-05"
EXAM_TITLE = "TOEIC Reading Test 05 (de mau theo Reading_v05.docx)"

paras = []  # mỗi phần tử: ("tag"|"text", chuỗi) hoặc ("img", tên file, alt)


def tag(s):
    paras.append(("tag", s))


def text(s):
    paras.append(("text", s))


def question(qid, no, stem, opts, ans, expl, shuffle):
    tag(f"[QUESTION id={qid} no={no} shuffle-options={shuffle}]")
    if stem:
        text(stem)
    for code, o in zip("ABCD", opts):
        tag(f"[OPTION id={code}]{o}")
    tag(f"[ANSWER ref={ans}]")
    tag("[EXPLANATION]")
    text(expl)
    tag("[/EXPLANATION]")
    tag("[/QUESTION]")


answers = []  # (no, part, ans, expl)

tag(f'[EXAM id={EXAM_ID} tagsv=1 title="{EXAM_TITLE}" duration=75 total=100]')

# ---- Part 5 ----
tag("[PART no=5 from=101 to=130 shuffle-items=on]")
for no, stem, opts, ans, expl in P5:
    question(f"Q{no}", no, stem, opts, ans, expl, "on")
    answers.append((no, 5, ans, expl))
tag("[/PART]")

# ---- Part 6 ----
tag("[PART no=6 from=131 to=146 shuffle-items=on]")
for g in P6:
    tag(f"[GROUP id={g['id']} shuffle-questions=off]")
    tag(f"[DOCUMENT id={g['doc']}]")
    for line in g["lines"]:
        text(line)
    tag("[/DOCUMENT]")
    for no, opts, ans, expl in g["qs"]:
        question(f"Q{no}", no, None, opts, ans, expl, "on")
        answers.append((no, 6, ans, expl))
    tag("[/GROUP]")
tag("[/PART]")

# ---- Part 7 ----
tag("[PART no=7 from=147 to=200 shuffle-items=on]")
images = []
for g in G:
    tag(f"[GROUP id={g['id']} shuffle-questions={g['shuffle_questions']}]")
    for doc_id, lines in g["docs"]:
        tag(f"[DOCUMENT id={doc_id}]")
        for line in lines:
            if isinstance(line, tuple):
                if line[1] not in images:
                    images.append(line[1])
                paras.append(("img", line[1], line[2]))
            else:
                text(line)
        tag("[/DOCUMENT]")
    for no, stem, opts, ans, expl, shuffle in g["qs"]:
        question(f"Q{no}", no, stem, opts, ans, expl, shuffle)
        answers.append((no, 7, ans, expl))
    tag("[/GROUP]")
tag("[/PART]")
tag("[/EXAM]")

# ---------- kiểm tra nhanh dữ liệu trước khi ghi ----------
assert [a[0] for a in answers] == list(range(101, 201)), "thiếu/trùng số câu"
assert all(a[2] in "ABCD" for a in answers)

# ---------- ghi .docx ----------
W = ('xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main" '
     'xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing" '
     'xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" '
     'xmlns:pic="http://schemas.openxmlformats.org/drawingml/2006/picture" '
     'xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"')

img_rid = {name: f"rIdImg{i + 1}" for i, name in enumerate(images)}
draw_id = [0]


def run(t, bold=False, color=None, mono=False):
    rpr = ""
    if bold or color or mono:
        rpr = "<w:rPr>"
        if mono:
            rpr += '<w:rFonts w:ascii="Consolas" w:hAnsi="Consolas"/>'
        if bold:
            rpr += "<w:b/>"
        if color:
            rpr += f'<w:color w:val="{color}"/>'
        rpr += "</w:rPr>"
    return f'<w:r>{rpr}<w:t xml:space="preserve">{escape(t)}</w:t></w:r>'


def para_xml(p):
    kind = p[0]
    if kind == "tag":
        return f"<w:p>{run(p[1], bold=True, color='1D4ED8', mono=True)}</w:p>"
    if kind == "text":
        return f"<w:p>{run(p[1])}</w:p>"
    # ảnh nhúng dạng inline (In line with text), trong đoạn riêng
    draw_id[0] += 1
    i = draw_id[0]
    cx, cy = 4572000, 1000125  # 5 inch x ~1.09 inch (ảnh 640x140)
    return (
        f'<w:p><w:r><w:drawing><wp:inline distT="0" distB="0" distL="0" distR="0"><wp:extent cx="{cx}" cy="{cy}"/>'
        f'<wp:docPr id="{i}" name="Picture {i}" descr="{escape(p[2], {chr(34): "&quot;"})}"/>'
        '<a:graphic><a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/picture"><pic:pic>'
        f'<pic:nvPicPr><pic:cNvPr id="{i}" name="{escape(p[1])}"/><pic:cNvPicPr/></pic:nvPicPr>'
        f'<pic:blipFill><a:blip r:embed="{img_rid[p[1]]}"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill>'
        f'<pic:spPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="{cx}" cy="{cy}"/></a:xfrm><a:prstGeom prst="rect"><a:avLst/></a:prstGeom></pic:spPr>'
        "</pic:pic></a:graphicData></a:graphic></wp:inline></w:drawing></w:r></w:p>"
    )


body = "".join(para_xml(p) for p in paras)
document = (f'<?xml version="1.0" encoding="UTF-8" standalone="yes"?><w:document {W}><w:body>{body}'
            '<w:sectPr><w:pgSz w:w="11906" w:h="16838"/><w:pgMar w:top="1134" w:right="1134" w:bottom="1134" w:left="1134" w:header="708" w:footer="708" w:gutter="0"/></w:sectPr>'
            "</w:body></w:document>")

content_types = (
    '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
    '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">'
    '<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>'
    '<Default Extension="xml" ContentType="application/xml"/>'
    '<Default Extension="png" ContentType="image/png"/>'
    '<Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>'
    "</Types>"
)
rels = (
    '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
    '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">'
    '<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>'
    "</Relationships>"
)
doc_rels = (
    '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
    '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">'
    + "".join(
        f'<Relationship Id="{img_rid[n]}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" Target="media/{n}"/>'
        for n in images
    )
    + "</Relationships>"
)

with zipfile.ZipFile(OUT, "w", zipfile.ZIP_DEFLATED) as z:
    z.writestr("[Content_Types].xml", content_types)
    z.writestr("_rels/.rels", rels)
    z.writestr("word/document.xml", document)
    z.writestr("word/_rels/document.xml.rels", doc_rels)
    for n in images:
        with open(os.path.join(HERE, "images", n), "rb") as f:
            z.writestr(f"word/media/{n}", f.read())

# ---------- bảng đáp án để giảng viên đối chiếu ----------
with open(KEY, "w", encoding="utf-8") as f:
    f.write("# Đáp án và giải thích – TOEIC Reading Test 05 (đề mẫu)\n\n")
    f.write("> File nguồn `Reading_v05.docx` **không kèm đáp án**. Đáp án và giải thích dưới đây do người soạn tự xác định ")
    f.write("(ngữ pháp, từ vựng, bằng chứng trong bài đọc) nên **cần đối chiếu với đáp án chính thức** trước khi dùng thi thật.\n\n")
    for part in (5, 6, 7):
        f.write(f"## Part {part}\n\n| Câu | Đáp án | Giải thích |\n|---|---|---|\n")
        for no, p, ans, expl in answers:
            if p == part:
                f.write(f"| {no} | {ans} | {expl.replace('|', '/')} |\n")
        f.write("\n")
    dist = {c: sum(1 for a in answers if a[2] == c) for c in "ABCD"}
    f.write(f"Phân bố đáp án: {dist}\n")

print("OK:", OUT, "-", len(paras), "đoạn,", len(images), "ảnh")
