import { api, ApiError, ErrorCodes } from '../api.js';
import { esc, icon, render, loading, errorState, setStatusTint } from '../ui.js';

const R = 69.75; // bán kính đường tròn giữa của vòng (ngoài 75, trong 64.5 như thiết kế)
const CIRC = 2 * Math.PI * R;

/** M9 – Kết quả bài thi. */
export function mountResult(attemptId) {
  const ac = new AbortController();
  const root = document.getElementById('view');

  async function load() {
    loading('Đang chấm điểm…');
    let r;
    try {
      r = await api.result(attemptId);
    } catch (e) {
      if (e instanceof ApiError && e.code === ErrorCodes.NotSubmitted) {
        location.hash = '#/attempt/' + attemptId;
        return;
      }
      errorState(e.message);
      return;
    }
    const pct = Math.max(0, Math.min(100, Number(r.scorePercent) || 0));
    const pctText = (Number.isInteger(pct) ? pct : pct.toFixed(1).replace('.', ',')) + '%';
    const parts = r.parts
      .map((p) => {
        const w = p.total ? (p.correct / p.total) * 100 : 0;
        return `<div class="prow"><div class="l"><span>Part ${p.partNo}</span><b>${p.correct}/${p.total}</b></div><div class="progress"><i style="width:${w}%"></i></div></div>`;
      })
      .join('');

    setStatusTint(true);
    render(
      `<div class="page-head tint"><h1>Kết quả bài thi</h1></div>` +
        `<div class="scroll"><div class="res">` +
        `<div class="score-card"><div class="ring">` +
        `<svg viewBox="0 0 150 150" aria-hidden="true"><circle cx="75" cy="75" r="${R}" fill="none" stroke="#e2e8f0" stroke-width="10.5"/>` +
        `<circle cx="75" cy="75" r="${R}" fill="none" stroke="#2563eb" stroke-width="10.5" stroke-dasharray="${(CIRC * pct) / 100} ${CIRC}"/></svg>` +
        `<div class="c"><b>${pctText}</b><span>tỷ lệ đúng</span></div></div>` +
        `<div class="name">${esc(r.title)}</div></div>` +
        `<div class="stats"><div class="stat ok"><b>${r.correctCount}</b><span>Đúng</span></div>` +
        `<div class="stat bad"><b>${r.wrongCount}</b><span>Sai</span></div>` +
        `<div class="stat skip"><b>${r.skippedCount}</b><span>Bỏ qua</span></div></div>` +
        `<p class="fine">${r.correctCount} + ${r.wrongCount} + ${r.skippedCount} = ${r.totalQuestions} câu · Điểm hiển thị là tỷ lệ đúng, chưa quy đổi sang thang TOEIC.</p>` +
        `<div class="partbox"><h4>Theo từng Part</h4>${parts}</div></div></div>` +
        `<div class="actions tint"><button class="btn pri" data-action="review">${icon('list-white')}Xem lại đáp án & giải thích</button>` +
        `<button class="btn" data-action="home">Về danh sách đề</button></div>`
    );
  }

  root.addEventListener(
    'click',
    (ev) => {
      const t = ev.target.closest('[data-action]');
      if (!t) return;
      if (t.dataset.action === 'review') location.hash = '#/review/' + attemptId;
      else if (t.dataset.action === 'home') location.hash = '#/';
      else if (t.dataset.action === 'retry') load();
    },
    { signal: ac.signal }
  );
  load();
  return () => ac.abort();
}

/** M10 – Xem lại đáp án & giải thích (chỉ sau khi đã nộp). */
export function mountReview(attemptId) {
  const ac = new AbortController();
  const root = document.getElementById('view');
  let data = null;
  let filter = 'Wrong';
  let pos = 0;

  const outcomeOf = (f) => (f === 'Wrong' ? 'Wrong' : f === 'Skipped' ? 'Skipped' : null);
  const list = () => (filter === 'all' ? data.questions : data.questions.filter((q) => q.outcome === outcomeOf(filter)));

  async function load() {
    loading();
    try {
      data = await api.result(attemptId);
    } catch (e) {
      if (e instanceof ApiError && e.code === ErrorCodes.NotSubmitted) {
        location.hash = '#/attempt/' + attemptId;
        return;
      }
      errorState(e.message);
      return;
    }
    filter = data.wrongCount > 0 ? 'Wrong' : data.skippedCount > 0 ? 'Skipped' : 'all';
    setStatusTint(true);
    draw();
  }

  function optionHtml(o) {
    let cls = '';
    let tag = '';
    let mark = '';
    if (o.isCorrect) {
      cls = 'ok';
      tag = o.isSelected ? 'Đáp án đúng · Bạn đã chọn' : 'Đáp án đúng';
      mark = icon('check-green-20');
    } else if (o.isSelected) {
      cls = 'bad';
      tag = 'Bạn đã chọn';
      mark = icon('x-red');
    }
    return `<div class="opt ${cls}"><span class="letter">${esc(o.label)}</span><span class="t"><b>${esc(o.text)}</b>${tag ? `<small>${tag}</small>` : ''}</span>${mark}</div>`;
  }

  function draw() {
    const items = list();
    if (pos >= items.length) pos = Math.max(0, items.length - 1);
    const q = items[pos];
    const label = filter === 'Wrong' ? 'Câu sai' : filter === 'Skipped' ? 'Bỏ qua' : 'Câu';
    const outcome = q ? (q.outcome === 'Correct' ? ['Đúng', 'pri'] : q.outcome === 'Wrong' ? ['Sai', 'bad'] : ['Bỏ qua', 'warn']) : null;
    const filters = [
      ['all', 'Tất cả'],
      ['Wrong', `Sai (${data.wrongCount})`],
      ['Skipped', `Bỏ qua (${data.skippedCount})`],
    ]
      .map(([k, t]) => `<button class="filter ${filter === k ? 'on' : ''}" data-action="filter" data-f="${k}">${t}</button>`)
      .join('');

    const body = q
      ? `<div class="qcard"><div class="stem">${q.stem && q.stem.trim() ? esc(q.stem) : `Câu điền vào chỗ trống <span class="blank">${q.number}</span> của bài đọc (Part ${q.partNo}).`}</div>` +
        `<div class="options">${q.options.map(optionHtml).join('')}</div></div>` +
        `<div class="explain"><b>Giải thích</b><p>${esc(q.explanation || 'Chưa có giải thích.')}</p></div>`
      : `<div class="empty">Không có câu nào trong mục này.</div>`;

    render(
      `<div class="rhead"><div class="row"><button class="back" data-action="back" aria-label="Quay lại">${icon('left-dark-22')}</button>` +
        `<span class="t">Xem lại${q ? ` · Câu ${q.number} / ${data.totalQuestions}` : ''}</span>` +
        (outcome ? `<span class="badge ${outcome[1]}">${outcome[0]}</span>` : '') +
        `</div><div class="filters">${filters}</div></div>` +
        `<div class="scroll"><div class="content">${body}</div></div>` +
        `<div class="bottom-nav"><button class="btn" data-action="prev" ${pos === 0 ? 'disabled' : ''}>${icon('left-dark')}${label} trước</button>` +
        `<button class="btn pri" data-action="next" ${pos >= items.length - 1 ? 'disabled' : ''}>${icon('right-white')}${label} sau</button></div>`
    );
  }

  root.addEventListener(
    'click',
    (ev) => {
      const t = ev.target.closest('[data-action]');
      if (!t || !data) {
        if (t && t.dataset.action === 'retry') load();
        return;
      }
      const a = t.dataset.action;
      if (a === 'back') location.hash = '#/result/' + attemptId;
      else if (a === 'filter') { filter = t.dataset.f; pos = 0; draw(); }
      else if (a === 'prev') { pos = Math.max(0, pos - 1); draw(); }
      else if (a === 'next') { pos += 1; draw(); }
    },
    { signal: ac.signal }
  );
  load();
  return () => ac.abort();
}
