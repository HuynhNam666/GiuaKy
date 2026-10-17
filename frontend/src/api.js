// Gọi API sinh viên của backend (ASP.NET Boilerplate, Web.Host). Không có màn hình đăng nhập:
// ứng dụng tự lấy token bằng tài khoản sinh viên cấu hình trong .env (xem .env.example).
const BASE = (import.meta.env.VITE_API_URL || 'http://localhost:44311').replace(/\/$/, '');
const USER = import.meta.env.VITE_STUDENT_USER || 'sinhvien';
const PASS = import.meta.env.VITE_STUDENT_PASSWORD || '123qwe';
const TOKEN_KEY = 'toeic.token';
const APP = '/api/services/app/StudentExam';

export class ApiError extends Error {
  constructor(message, { code = 0, status = 0, network = false } = {}) {
    super(message);
    this.code = code;
    this.status = status;
    this.network = network;
  }
}

export const ErrorCodes = { Expired: 4101, AlreadySubmitted: 4102, NotPublished: 4104, InvalidOption: 4105, NotSubmitted: 4106 };

let token = sessionStorage.getItem(TOKEN_KEY);

async function authenticate() {
  let res;
  try {
    res = await fetch(BASE + '/api/TokenAuth/Authenticate', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ userNameOrEmailAddress: USER, password: PASS }),
    });
  } catch {
    throw new ApiError('Không kết nối được máy chủ. Hãy kiểm tra mạng rồi thử lại.', { network: true });
  }
  const json = await res.json().catch(() => null);
  if (!res.ok || !json || json.success === false) {
    throw new ApiError((json && json.error && json.error.message) || 'Không đăng nhập được vào hệ thống.', { status: res.status });
  }
  token = json.result.accessToken;
  sessionStorage.setItem(TOKEN_KEY, token);
}

async function call(method, path, body, retry = true) {
  if (!token) await authenticate();
  let res;
  try {
    res = await fetch(BASE + path, {
      method,
      headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + token },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
  } catch {
    throw new ApiError('Mất kết nối tới máy chủ.', { network: true });
  }
  if (res.status === 401 && retry) {
    token = null;
    sessionStorage.removeItem(TOKEN_KEY);
    return call(method, path, body, false);
  }
  const json = await res.json().catch(() => null);
  if (!res.ok || (json && json.success === false)) {
    const e = json && json.error;
    throw new ApiError((e && e.message) || 'Có lỗi xảy ra (' + res.status + ').', { code: (e && e.code) || 0, status: res.status });
  }
  return json ? json.result : null;
}

export const api = {
  published: () => call('GET', APP + '/GetPublished'),
  start: (examVersionId) => call('POST', APP + '/Start', { examVersionId }),
  getAttempt: (attemptId) => call('GET', APP + '/Get?AttemptId=' + attemptId),
  saveAnswer: (body) => call('POST', APP + '/SaveAnswer', body),
  submit: (attemptId) => call('POST', APP + '/Submit', { attemptId }),
  result: (attemptId) => call('GET', APP + '/GetResult?AttemptId=' + attemptId),
  myResults: () => call('GET', APP + '/GetMyResults'),
};

export const mediaUrl = (sha) => BASE + '/api/media/' + sha;

/** Backend trả thời gian UTC không có hậu tố Z -> coi là UTC. */
export function parseUtc(s) {
  if (!s) return NaN;
  return Date.parse(/[zZ]$|[+-]\d\d:\d\d$/.test(s) ? s : s + 'Z');
}
