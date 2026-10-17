import { api, ApiError, ErrorCodes } from './api.js';

/**
 * Lưu đáp án lên máy chủ theo từng câu, an toàn khi mạng chập chờn:
 *  - mỗi lựa chọn mang một "revision" tăng dần theo câu; máy chủ bỏ qua yêu cầu cũ hơn bản đã lưu;
 *  - mỗi câu chỉ có một yêu cầu đang bay, lựa chọn mới hơn được gửi tiếp sau đó;
 *  - lỗi mạng: tự thử lại (backoff), trạng thái "error" hiển thị rõ cho người dùng;
 *  - bản đệm trong localStorage chỉ để phục hồi khi gián đoạn, KHÔNG được coi là đã lưu: chỉ khi máy chủ trả OK mới xóa.
 */
export class AnswerSaver {
  constructor(attemptId, handlers = {}) {
    this.attemptId = attemptId;
    this.h = handlers; // onStatus(status), onLocked(kind: 'expired'|'submitted')
    this.entries = new Map(); // questionId -> { optionId, rev, sentRev }
    this.inflight = new Set();
    this.failed = new Set();
    this.timers = new Map();
    this.attempts = new Map();
    this.locked = false;
    this.key = 'toeic.buf.' + attemptId;
  }

  /** Nạp trạng thái từ máy chủ rồi áp dụng các lựa chọn đang nằm trong bản đệm mà máy chủ chưa có. */
  init(questions) {
    for (const q of questions) {
      this.entries.set(q.questionId, { optionId: q.selectedOptionId ?? null, rev: q.revision || 0, sentRev: q.revision || 0 });
    }
    let buf = {};
    try { buf = JSON.parse(localStorage.getItem(this.key) || '{}'); } catch { buf = {}; }
    for (const [qid, v] of Object.entries(buf)) {
      const e = this.entries.get(Number(qid));
      if (e && v.rev > e.rev) {
        e.optionId = v.optionId;
        e.rev = v.rev;
        this.send(Number(qid));
      }
    }
    this.notify();
  }

  selected(qid) {
    const e = this.entries.get(qid);
    return e ? e.optionId : null;
  }

  select(qid, optionId) {
    if (this.locked) return false;
    const e = this.entries.get(qid) || { optionId: null, rev: 0, sentRev: 0 };
    e.optionId = optionId;
    e.rev += 1;
    this.entries.set(qid, e);
    this.persist();
    this.send(qid);
    this.notify();
    return true;
  }

  persist() {
    const buf = {};
    for (const [qid, e] of this.entries) if (e.rev > e.sentRev) buf[qid] = { optionId: e.optionId, rev: e.rev };
    try {
      if (Object.keys(buf).length) localStorage.setItem(this.key, JSON.stringify(buf));
      else localStorage.removeItem(this.key);
    } catch { /* bộ nhớ đầy / bị chặn: bỏ qua, vẫn gửi lên máy chủ */ }
  }

  status() {
    if (this.failed.size > 0) return 'error';
    for (const e of this.entries.values()) if (e.rev > e.sentRev) return 'saving';
    return 'saved';
  }

  notify() {
    if (this.h.onStatus) this.h.onStatus(this.status());
  }

  async send(qid) {
    if (this.locked || this.inflight.has(qid)) return;
    const e = this.entries.get(qid);
    if (!e || e.rev <= e.sentRev) return;
    this.inflight.add(qid);
    clearTimeout(this.timers.get(qid));
    const snap = { optionId: e.optionId, rev: e.rev };
    try {
      const r = await api.saveAnswer({ attemptId: this.attemptId, questionId: qid, optionId: snap.optionId, revision: snap.rev });
      e.sentRev = Math.max(e.sentRev, Math.max(snap.rev, r.revision || 0));
      this.failed.delete(qid);
      this.attempts.delete(qid);
    } catch (err) {
      this.inflight.delete(qid);
      if (err instanceof ApiError && (err.code === ErrorCodes.Expired || err.code === ErrorCodes.AlreadySubmitted)) {
        this.locked = true;
        if (this.h.onLocked) this.h.onLocked(err.code === ErrorCodes.Expired ? 'expired' : 'submitted');
        return;
      }
      this.failed.add(qid);
      const n = (this.attempts.get(qid) || 0) + 1;
      this.attempts.set(qid, n);
      this.timers.set(qid, setTimeout(() => this.send(qid), Math.min(15000, 1500 * 2 ** Math.min(n, 4))));
      this.notify();
      return;
    }
    this.inflight.delete(qid);
    this.persist();
    this.notify();
    if (e.rev > e.sentRev) this.send(qid); // người dùng đã đổi lựa chọn trong lúc gửi
  }

  /** Thử lại ngay các câu đang lỗi. */
  retryAll() {
    for (const qid of [...this.failed]) {
      this.attempts.set(qid, 0);
      this.send(qid);
    }
  }

  /** Chờ các lần lưu đang chờ hoàn tất (tối đa timeoutMs). Trả về true nếu đã lưu hết. */
  async flush(timeoutMs = 5000) {
    const end = Date.now() + timeoutMs;
    this.retryAll();
    while (Date.now() < end) {
      let pending = false;
      for (const e of this.entries.values()) if (e.rev > e.sentRev) pending = true;
      if (!pending) return true;
      await new Promise((r) => setTimeout(r, 150));
    }
    return false;
  }

  dispose() {
    this.locked = true;
    for (const t of this.timers.values()) clearTimeout(t);
    try { localStorage.removeItem(this.key); } catch { /* bỏ qua */ }
  }
}
