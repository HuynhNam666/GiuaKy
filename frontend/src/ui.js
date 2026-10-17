// Tiện ích dựng giao diện dùng chung (icon lấy từ file SVG đã tải về từ thiết kế Figma).
export const esc = (s) => String(s ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

/** Icon từ /icons/<tên>.svg (kích thước do CSS của chỗ đặt quyết định). */
export const icon = (name, extra = '') => `<img class="ico ${extra}" src="/icons/${name}.svg" alt="" draggable="false" />`;

/** Icon đổi màu được (mask) – dùng cho tab bar có trạng thái đang chọn/không chọn. */
export const iconMask = (name) => `<span class="ico-mask" style="--i:url(/icons/${name}.svg)"></span>`;

export const view = () => document.getElementById('view');
export const layer = () => document.getElementById('layer');

export function setStatusTint(tint) {
  document.querySelector('.statusbar').classList.toggle('tint', !!tint);
}

export function render(html) {
  view().innerHTML = html;
}

export function mmss(totalSeconds) {
  const s = Math.max(0, Math.floor(totalSeconds));
  return String(Math.floor(s / 60)).padStart(2, '0') + ':' + String(s % 60).padStart(2, '0');
}

export function loading(text = 'Đang tải…') {
  render(`<div class="state">${esc(text)}</div>`);
}

export function errorState(message, retryLabel = 'Thử lại') {
  render(`<div class="state"><p>${esc(message)}</p><button class="btn pri" data-action="retry">${esc(retryLabel)}</button></div>`);
}

/** Thanh tab dưới cùng của danh sách đề / kết quả. */
export function tabbar(active) {
  const tab = (key, label, ic, href) =>
    `<a class="tab ${active === key ? 'active' : ''}" href="${href}">${iconMask(ic)}<span>${label}</span></a>`;
  return `<nav class="tabbar">${tab('exams', 'Đề thi', 'book-blue', '#/')}${tab('results', 'Kết quả', 'award-gray', '#/results')}</nav>`;
}

/** Văn bản bài đọc: xuống dòng + đánh dấu chỗ trống Part 6 ({{blank:N}} với N = số thứ tự hiển thị của câu). */
export function passageText(text) {
  return String(text ?? '')
    .split('\n')
    .map((line) => `<p>${esc(line).replace(/\{\{blank:(\d+)\}\}/gi, '<span class="blank">$1</span>')}</p>`)
    .join('');
}

export function toast(title, body, ms = 4000, onClick) {
  const el = document.createElement('button');
  el.className = 'toast';
  el.innerHTML = `${icon('alert-light')}<div><b>${esc(title)}</b><span>${esc(body)}</span></div>`;
  el.addEventListener('click', () => { if (onClick) onClick(); el.remove(); });
  layer().appendChild(el);
  if (ms) setTimeout(() => el.remove(), ms);
  return el;
}

export function formatDate(iso) {
  const d = new Date(/[zZ]$|[+-]\d\d:\d\d$/.test(iso) ? iso : iso + 'Z');
  return d.toLocaleString('vi-VN', { dateStyle: 'short', timeStyle: 'short' });
}
