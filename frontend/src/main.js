import '@fontsource/inter/400.css';
import '@fontsource/inter/500.css';
import '@fontsource/inter/600.css';
import '@fontsource/inter/700.css';
import './styles.css';
import { mountList, mountResults } from './screens/list.js';
import { mountAttempt, mountTimeup } from './screens/attempt.js';
import { mountResult, mountReview } from './screens/result.js';
import { layer, view } from './ui.js';

const routes = [
  [/^#?\/?$/, () => mountList()],
  [/^#\/results$/, () => mountResults()],
  [/^#\/attempt\/(\d+)$/, (id) => mountAttempt(Number(id))],
  [/^#\/timeup\/(\d+)$/, (id) => mountTimeup(Number(id))],
  [/^#\/result\/(\d+)$/, (id) => mountResult(Number(id))],
  [/^#\/review\/(\d+)$/, (id) => mountReview(Number(id))],
];

let dispose = null;

function navigate() {
  if (dispose) dispose();
  dispose = null;
  layer().innerHTML = '';
  view().innerHTML = '';
  const hash = location.hash || '#/';
  for (const [re, mount] of routes) {
    const m = hash.match(re);
    if (m) {
      dispose = mount(m[1]);
      return;
    }
  }
  location.hash = '#/';
}

// Đồng hồ giả lập trên thanh trạng thái của khung điện thoại (chỉ hiển thị khi xem trên desktop).
function tickClock() {
  const d = new Date();
  document.getElementById('clock').textContent = d.getHours() + ':' + String(d.getMinutes()).padStart(2, '0');
}
tickClock();
setInterval(tickClock, 30000);

window.addEventListener('hashchange', navigate);
navigate();
