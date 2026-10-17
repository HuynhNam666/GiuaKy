import { api, ApiError, parseUtc } from '../api.js';
import { esc, icon, render, loading, errorState, tabbar, mmss, layer, setStatusTint, toast, formatDate } from '../ui.js';

/** M1 – Danh sách đề (+ M2 – bottom sheet bắt đầu). */
export function mountList() {
  const ac = new AbortController();
  const root = document.getElementById('view');
  let exams = [];
  let timerId = null;

  async function load() {
    loading();
    try {
      exams = await api.published();
      draw();
    } catch (e) {
      errorState(e.message);
    }
  }

  const meta = (e) =>
    `<div class="meta"><span>${icon('clock-gray')}${e.durationMinutes} phút</span><span>${icon('list-gray')}${e.totalQuestions} câu</span></div>`;

  function card(e) {
    let extra = '';
    if (e.inProgressAttemptId) {
      extra =
        `<div class="note warn">${icon('clock-amber')}<span>Đang làm dở · còn <b data-exp="${esc(e.inProgressExpiresAtUtc)}"></b></span></div>` +
        `<button class="btn pri" data-action="resume" data-attempt="${e.inProgressAttemptId}">${icon('right-white')}Tiếp tục làm bài</button>`;
    } else if (e.lastAttemptId) {
      extra =
        `<div class="note ok">${icon('check-green')}<span>Đã nộp · ${Math.round(e.lastScorePercent ?? 0)}/100 câu đúng</span></div>` +
        `<button class="btn" data-action="result" data-attempt="${e.lastAttemptId}">Xem kết quả</button>` +
        `<button class="btn pri" data-action="open-start" data-id="${e.examVersionId}">${icon('right-white')}Làm lại</button>`;
    } else {
      extra = `<button class="btn pri" data-action="open-start" data-id="${e.examVersionId}">${icon('right-white')}Bắt đầu làm bài</button>`;
    }
    return `<div class="card"><h3>${esc(e.title)}</h3>${meta(e)}${extra}</div>`;
  }

  function draw() {
    setStatusTint(false);
    render(
      `<div class="page-head"><h1>Đề thi TOEIC Reading</h1><p>Chọn một đề đã được xuất bản để bắt đầu làm bài.</p></div>` +
        `<div class="scroll"><div class="list">${exams.map(card).join('') || '<div class="empty">Chưa có đề nào được xuất bản.</div>'}</div></div>` +
        tabbar('exams')
    );
    tickCountdown();
  }

  function tickCountdown() {
    root.querySelectorAll('[data-exp]').forEach((el) => {
      el.textContent = mmss((parseUtc(el.dataset.exp) - Date.now()) / 1000);
    });
  }

  function openSheet(examId) {
    const e = exams.find((x) => x.examVersionId === examId);
    if (!e) return;
    const rule = (ic, text) => `<div class="rule"><div class="ib">${icon(ic)}</div><p>${text}</p></div>`;
    layer().innerHTML =
      `<div class="overlay" data-action="close-sheet"><div class="sheet" data-stop="1">` +
      `<div class="grab"></div><h2>${esc(e.title)}</h2>` +
      rule('list-blue', `${e.totalQuestions} câu · 3 phần (Part 5, 6, 7)`) +
      rule('clock-blue', `${e.durationMinutes} phút — đồng hồ chạy trên máy chủ, đóng app hoặc đổi giờ máy không kéo dài thời gian`) +
      rule('cloud-blue', 'Đáp án được tự động lưu; có thể đổi hoặc bỏ chọn trước khi nộp') +
      rule('lock-blue', 'Hết giờ bài sẽ tự động nộp và khóa không cho sửa') +
      `<button class="btn pri" data-action="start" data-id="${e.examVersionId}">${icon('right-white')}Bắt đầu làm bài</button>` +
      `<button class="btn" data-action="close-sheet">Để sau</button></div></div>`;
  }

  async function start(examId, btn) {
    btn.disabled = true;
    try {
      const a = await api.start(examId);
      location.hash = '#/attempt/' + a.attemptId;
    } catch (e) {
      btn.disabled = false;
      layer().innerHTML = '';
      toast('Không bắt đầu được', e.message, 5000);
      if (e instanceof ApiError && e.code === 4104) load();
    }
  }

  root.addEventListener(
    'click',
    (ev) => {
      const t = ev.target.closest('[data-action]');
      if (!t) return;
      const a = t.dataset.action;
      if (a === 'retry') load();
      else if (a === 'open-start') openSheet(Number(t.dataset.id));
      else if (a === 'resume') location.hash = '#/attempt/' + t.dataset.attempt;
      else if (a === 'result') location.hash = '#/result/' + t.dataset.attempt;
    },
    { signal: ac.signal }
  );
  layer().addEventListener(
    'click',
    (ev) => {
      if (ev.target.closest('[data-stop]') && !ev.target.closest('[data-action]')) return;
      const t = ev.target.closest('[data-action]');
      if (!t) return;
      if (t.dataset.action === 'close-sheet') layer().innerHTML = '';
      else if (t.dataset.action === 'start') start(Number(t.dataset.id), t);
    },
    { signal: ac.signal }
  );

  timerId = setInterval(tickCountdown, 1000);
  load();

  return () => {
    ac.abort();
    clearInterval(timerId);
    layer().innerHTML = '';
  };
}

/** Tab "Kết quả": lịch sử các lượt đã nộp. */
export function mountResults() {
  const ac = new AbortController();
  const root = document.getElementById('view');

  async function load() {
    loading();
    try {
      const list = await api.myResults();
      setStatusTint(false);
      const items = list
        .map(
          (r) =>
            `<button class="card hist" data-action="result" data-attempt="${r.attemptId}">` +
            `<div class="pct">${Math.round(r.scorePercent)}%</div>` +
            `<div class="info"><b>${esc(r.title)}</b>${esc(formatDate(r.startedAtUtc))}<br/>Đúng ${r.correctCount} · Sai ${r.wrongCount} · Bỏ qua ${r.skippedCount}</div>` +
            `<img class="ico" src="/icons/left-dark.svg" alt="" style="transform:rotate(180deg);width:18px;height:18px"/></button>`
        )
        .join('');
      render(
        `<div class="page-head"><h1>Kết quả của bạn</h1><p>Các lượt thi đã nộp, mới nhất trước.</p></div>` +
          `<div class="scroll"><div class="list">${items || '<div class="empty">Bạn chưa nộp bài nào.</div>'}</div></div>` +
          tabbar('results')
      );
    } catch (e) {
      errorState(e.message);
    }
  }

  root.addEventListener(
    'click',
    (ev) => {
      const t = ev.target.closest('[data-action]');
      if (!t) return;
      if (t.dataset.action === 'retry') load();
      else if (t.dataset.action === 'result') location.hash = '#/result/' + t.dataset.attempt;
    },
    { signal: ac.signal }
  );
  load();
  return () => ac.abort();
}
