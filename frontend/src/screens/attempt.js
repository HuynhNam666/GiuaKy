import { api, ApiError, ErrorCodes, mediaUrl, parseUtc } from '../api.js';
import { AnswerSaver } from '../saver.js';
import { esc, icon, render, loading, errorState, mmss, layer, setStatusTint, toast, passageText } from '../ui.js';

const WARN_SECONDS = 300; // cảnh báo khi còn 5 phút

/** M3/M4 – làm bài (Part 5, Part 7 có bài đọc), M5 – lưới câu hỏi, M6 – sắp hết giờ & lưu lỗi, M7 – xác nhận nộp. */
export function mountAttempt(attemptId) {
  const ac = new AbortController();
  const root = document.getElementById('view');

  let attempt = null;
  let saver = null;
  let idx = 0;
  let expiresAt = 0;
  let serverNow = 0;
  let t0 = 0;
  let tickId = null;
  let finishing = false;
  let warned = false;
  let lastSaveStatus = 'saved';
  const docIdx = new Map(); // groupId -> tài liệu đang xem
  const closedPassage = new Set(); // groupId bài đọc đang thu gọn
  let gridPart = null;

  const idxKey = 'toeic.idx.' + attemptId;
  const remaining = () => (expiresAt - (serverNow + (performance.now() - t0))) / 1000;
  const cur = () => attempt.questions[idx];
  const selectedOf = (q) => saver.selected(q.questionId);
  const answeredCount = () => attempt.questions.filter((q) => selectedOf(q) != null).length;

  async function boot() {
    loading('Đang mở bài thi…');
    try {
      attempt = await api.getAttempt(attemptId);
    } catch (e) {
      errorState(e.message);
      return;
    }
    if (attempt.status !== 0) {
      location.hash = '#/result/' + attemptId;
      return;
    }
    serverNow = parseUtc(attempt.serverNowUtc);
    expiresAt = parseUtc(attempt.expiresAtUtc);
    t0 = performance.now();

    saver = new AnswerSaver(attemptId, {
      onStatus: onSaveStatus,
      onLocked: () => finish(true),
    });
    saver.init(attempt.questions);

    const saved = Number(sessionStorage.getItem(idxKey));
    if (Number.isInteger(saved) && saved >= 0 && saved < attempt.questions.length) idx = saved;
    else {
      const firstOpen = attempt.questions.findIndex((q) => selectedOf(q) == null);
      idx = firstOpen < 0 ? 0 : firstOpen;
    }

    setStatusTint(false);
    draw();
    tickId = setInterval(tick, 1000);
    tick();
  }

  // ---------- vẽ màn hình ----------

  function saveChip(status) {
    if (status === 'saving') return `<span class="save saving">${icon('refresh-amber')}Đang lưu…</span>`;
    if (status === 'error') return `<button class="save error" data-action="retry-save">${icon('alert-red')}Lưu lỗi · Thử lại</button>`;
    return `<span class="save saved">${icon('cloud-green')}Đã lưu</span>`;
  }

  function passageHtml(q) {
    if (q.groupId == null) return '';
    const g = attempt.groups.find((x) => x.groupId === q.groupId);
    if (!g || g.documents.length === 0) return '';
    const di = Math.min(docIdx.get(g.groupId) || 0, g.documents.length - 1);
    const doc = g.documents[di];
    const closed = closedPassage.has(g.groupId);
    const tabs =
      g.documents.length > 1
        ? `<div class="doc-tabs">${g.documents
            .map((_, i) => `<button class="doc-tab ${i === di ? 'on' : ''}" data-action="doc" data-group="${g.groupId}" data-i="${i}">Tài liệu ${i + 1}/${g.documents.length}</button>`)
            .join('')}</div>`
        : '';
    const blocks = closed
      ? ''
      : doc.blocks
          .map((b) =>
            b.type === 'Image' && b.mediaSha256
              ? `<button class="img-btn" data-action="img" data-sha="${esc(b.mediaSha256)}"><img src="${esc(mediaUrl(b.mediaSha256))}" alt="${esc(b.altText || 'Ảnh trong bài đọc')}" /><small>Chạm để phóng to ảnh</small></button>`
              : `<div class="txt">${passageText(b.text)}</div>`
          )
          .join('');
    return `<div class="passage ${closed ? 'closed' : ''}"><button class="ph" data-action="toggle-passage" data-group="${g.groupId}"><span class="t">Bài đọc</span>${icon('chevup')}</button>${tabs}${blocks}</div>`;
  }

  function draw(keepScroll = false) {
    const q = cur();
    const prevScroll = keepScroll ? (root.querySelector('.scroll') || {}).scrollTop || 0 : 0;
    const rem = remaining();
    const danger = rem <= WARN_SECONDS;
    const sel = selectedOf(q);
    const stem =
      q.stem && q.stem.trim()
        ? esc(q.stem)
        : `Chọn đáp án đúng cho chỗ trống <span class="blank">${q.number}</span> trong bài đọc.`;
    const options = q.options
      .map(
        (o) =>
          `<button class="opt ${sel === o.optionId ? 'sel' : ''}" data-action="opt" data-id="${o.optionId}"><span class="letter">${esc(o.label)}</span><span class="t"><b>${esc(o.text)}</b></span></button>`
      )
      .join('');

    render(
      `<div class="topbar">` +
        `<div class="row"><span class="badge pri">Part ${q.partNo}</span><span class="qlabel">Câu ${q.number} / ${attempt.totalQuestions}</span>` +
        `<span class="timer ${danger ? 'danger' : ''}" id="timer">${icon(danger ? 'clock-red' : 'clock-dark')}<span id="timer-text">${mmss(rem)}</span></span>` +
        `<button class="grid-btn" data-action="grid" aria-label="Danh sách câu hỏi">${icon('grid')}</button></div>` +
        `<div class="progress"><i style="width:${((idx + 1) / attempt.totalQuestions) * 100}%"></i></div>` +
        `<div class="status-row"><span class="grow">Đã trả lời ${answeredCount()}/${attempt.totalQuestions}</span><span id="save">${saveChip(saver.status())}</span></div>` +
        `</div>` +
        `<div class="scroll"><div class="content">` +
        (danger
          ? `<div class="banner">${icon('alert-red-18')}<span id="warn-text">Chỉ còn ${Math.max(1, Math.ceil(rem / 60))} phút! Hãy kiểm tra các câu chưa trả lời. Bài sẽ tự động nộp khi hết giờ.</span></div>`
          : '') +
        passageHtml(q) +
        `<div class="qcard"><div class="stem">${stem}</div><div class="options">${options}</div>` +
        (sel != null ? `<button class="clear" data-action="clear">${icon('x-gray')}Bỏ chọn câu này</button>` : '') +
        `</div></div></div>` +
        `<div class="bottom-nav">` +
        `<button class="btn" data-action="prev" ${idx === 0 ? 'disabled' : ''}>${icon('left-dark')}Câu trước</button>` +
        (idx === attempt.questions.length - 1
          ? `<button class="btn pri" data-action="grid">${icon('check-white')}Xem lại & nộp</button>`
          : `<button class="btn pri" data-action="next">${icon('right-white')}Câu sau</button>`) +
        `</div>`
    );
    if (keepScroll) {
      const sc = root.querySelector('.scroll');
      if (sc) sc.scrollTop = prevScroll;
    }
  }

  // ---------- đồng hồ & trạng thái lưu ----------

  function tick() {
    if (finishing) return;
    const rem = remaining();
    if (rem <= 0) {
      finish(true);
      return;
    }
    const danger = rem <= WARN_SECONDS;
    const el = document.getElementById('timer-text');
    if (el) el.textContent = mmss(rem);
    if (danger && !warned) {
      warned = true;
      draw(true);
    }
    const w = document.getElementById('warn-text');
    if (w) w.textContent = `Chỉ còn ${Math.max(1, Math.ceil(rem / 60))} phút! Hãy kiểm tra các câu chưa trả lời. Bài sẽ tự động nộp khi hết giờ.`;
  }

  function onSaveStatus(status) {
    const el = document.getElementById('save');
    if (el) el.innerHTML = saveChip(status);
    if (status === 'error' && lastSaveStatus !== 'error') {
      toast(`Chưa lưu được câu ${cur().number}`, 'Lựa chọn vẫn được giữ trên máy. Chạm để thử lại.', 6000, () => saver.retryAll());
    }
    lastSaveStatus = status;
  }

  // ---------- thao tác ----------

  function go(i) {
    idx = Math.max(0, Math.min(attempt.questions.length - 1, i));
    sessionStorage.setItem(idxKey, String(idx));
    layer().innerHTML = '';
    draw();
  }

  function choose(optionId) {
    if (finishing) return;
    const q = cur();
    const next = selectedOf(q) === optionId ? null : optionId; // chạm lại lựa chọn đang chọn = hủy chọn
    saver.select(q.questionId, next);
    draw(true);
  }

  function openGrid() {
    const parts = [...new Set(attempt.questions.map((q) => q.partNo))].sort();
    if (gridPart == null || !parts.includes(gridPart)) gridPart = cur().partNo;
    const tabs = parts.map((p) => `<button class="ptab ${p === gridPart ? 'on' : ''}" data-action="gpart" data-part="${p}">Part ${p}</button>`).join('');
    const chips = attempt.questions
      .map((q, i) => ({ q, i }))
      .filter(({ q }) => q.partNo === gridPart)
      .map(({ q, i }) => {
        const cls = i === idx ? 'cur' : selectedOf(q) != null ? 'done' : '';
        return `<button class="qchip ${cls}" data-action="goto" data-i="${i}">${q.number}</button>`;
      })
      .join('');
    layer().innerHTML =
      `<div class="overlay" data-action="close"><div class="sheet grid" data-stop="1">` +
      `<div style="display:flex;align-items:center"><h2 style="flex:1;font-size:18px">Danh sách câu hỏi</h2><button data-action="close" aria-label="Đóng">${icon('x-gray-22')}</button></div>` +
      `<div class="ptabs">${tabs}</div><div class="qgrid">${chips}</div>` +
      `<div class="legend"><span><i class="a"></i>Đã chọn</span><span><i></i>Chưa chọn</span><span><i class="c"></i>Đang xem</span></div>` +
      `<button class="btn pri" data-action="ask-submit">${icon('check-white')}Nộp bài</button></div></div>`;
  }

  function askSubmit() {
    const open = attempt.questions.filter((q) => selectedOf(q) == null).map((q) => q.number);
    const shown = open.slice(0, 12).map((n) => `<span class="badge warn">${n}</span>`).join('');
    const more = open.length > 12 ? `<span class="badge gray">+${open.length - 12}</span>` : '';
    layer().innerHTML =
      `<div class="overlay center"><div class="dialog" data-stop="1"><div class="ic">${icon('alert-amber')}</div>` +
      `<h2>Nộp bài?</h2>` +
      `<p>${open.length > 0 ? `Bạn còn ${open.length} câu chưa trả lời. ` : 'Bạn đã trả lời tất cả các câu. '}Sau khi nộp sẽ không thể sửa đáp án.</p>` +
      (open.length ? `<div class="chips">${shown}${more}</div>` : '') +
      `<button class="btn" data-action="close">Quay lại làm tiếp</button>` +
      `<button class="btn pri" data-action="confirm-submit">${icon('check-white')}Nộp bài</button></div></div>`;
  }

  async function finish(auto) {
    if (finishing) return;
    finishing = true;
    clearInterval(tickId);
    layer().innerHTML = '';
    render(`<div class="state">${auto ? 'Hết giờ — đang nộp bài…' : 'Đang lưu đáp án và nộp bài…'}</div>`);
    try { await saver.flush(auto ? 1500 : 5000); } catch { /* vẫn thử nộp: máy chủ chấm theo những gì đã lưu */ }
    try {
      await api.submit(attemptId);
    } catch (e) {
      if (!(e instanceof ApiError) || e.network) {
        finishing = false;
        errorState(e.message + ' Đáp án đã chọn vẫn được giữ.', 'Thử nộp lại');
        root.querySelector('[data-action=retry]').dataset.action = 'retry-submit';
        return;
      }
    }
    sessionStorage.setItem('toeic.answered.' + attemptId, String(answeredCount()));
    sessionStorage.removeItem(idxKey);
    saver.dispose();
    location.hash = (auto ? '#/timeup/' : '#/result/') + attemptId;
  }

  root.addEventListener(
    'click',
    (ev) => {
      const t = ev.target.closest('[data-action]');
      if (!t) return;
      const a = t.dataset.action;
      if (a === 'opt') choose(Number(t.dataset.id));
      else if (a === 'clear') { saver.select(cur().questionId, null); draw(true); }
      else if (a === 'prev') go(idx - 1);
      else if (a === 'next') go(idx + 1);
      else if (a === 'grid') openGrid();
      else if (a === 'retry-save') saver.retryAll();
      else if (a === 'retry') boot();
      else if (a === 'retry-submit') finish(false);
      else if (a === 'doc') { docIdx.set(Number(t.dataset.group), Number(t.dataset.i)); draw(true); }
      else if (a === 'toggle-passage') {
        const g = Number(t.dataset.group);
        if (closedPassage.has(g)) closedPassage.delete(g); else closedPassage.add(g);
        draw(true);
      } else if (a === 'img') {
        layer().innerHTML = `<div class="lightbox" data-action="close"><img src="${esc(mediaUrl(t.dataset.sha))}" alt="" /></div>`;
      }
    },
    { signal: ac.signal }
  );
  layer().addEventListener(
    'click',
    (ev) => {
      if (ev.target.closest('[data-stop]') && !ev.target.closest('[data-action]')) return;
      const t = ev.target.closest('[data-action]');
      if (!t) return;
      const a = t.dataset.action;
      if (a === 'close') layer().innerHTML = '';
      else if (a === 'goto') go(Number(t.dataset.i));
      else if (a === 'gpart') { gridPart = Number(t.dataset.part); openGrid(); }
      else if (a === 'ask-submit') askSubmit();
      else if (a === 'confirm-submit') finish(false);
    },
    { signal: ac.signal }
  );

  boot();

  return () => {
    ac.abort();
    clearInterval(tickId);
    if (saver && !finishing) saver.locked = true; // rời màn hình: dừng gửi; bản đệm cục bộ vẫn còn để phục hồi
    layer().innerHTML = '';
  };
}

/** M8 – hết giờ, bài đã được tự động nộp. */
export function mountTimeup(attemptId) {
  const ac = new AbortController();
  const answered = sessionStorage.getItem('toeic.answered.' + attemptId);
  setStatusTint(false);
  render(
    `<div class="timeup"><div class="big">${icon('clock-red-big')}</div><h1>Hết giờ!</h1>` +
      `<p>Bài làm của bạn đã được tự động nộp. Các đáp án đã lưu trước khi hết giờ vẫn được chấm; không thể chỉnh sửa thêm.</p>` +
      (answered != null ? `<span class="chip">${icon('list-gray-16')}Đã trả lời ${esc(answered)}/100 câu</span>` : '') +
      `</div><div class="actions"><button class="btn pri" data-action="result">${icon('right-white')}Xem kết quả</button></div>`
  );
  document.getElementById('view').addEventListener(
    'click',
    (ev) => { if (ev.target.closest('[data-action=result]')) location.hash = '#/result/' + attemptId; },
    { signal: ac.signal }
  );
  return () => ac.abort();
}
