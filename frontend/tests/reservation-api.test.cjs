const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const read = file => fs.readFileSync(path.join(__dirname, '..', file), 'utf8');
function setup(responses = [], search = '') {
  const calls = [], redirects = [], nodes = new Map();
  const storage = new Map([['roadride_access_token', 'test-token'], ['roadride_token_expires_at', '2099-01-01T00:00:00Z']]);
  function node(id) {
    if (!nodes.has(id)) nodes.set(id, { id, value: id === 'email' ? 'test@example.com' : 'password123', textContent: '', dataset: {}, hidden: false, attrs: {}, listeners: {}, parentElement: { remove() {} }, setAttribute(k,v) {this.attrs[k]=v;}, getAttribute(k) {return this.attrs[k] || null;}, removeAttribute(k) {delete this.attrs[k];}, addEventListener(k,v) {this.listeners[k]=v;}, reportValidity() {return true;}, setCustomValidity() {}, select() {}, focus() {}, scrollIntoView() {}, querySelector: selector => node(selector), querySelectorAll: () => [] });
    return nodes.get(id);
  }
  const context = vm.createContext({ URLSearchParams, Date, console, window: { location: { search, pathname: '/login/index.html', href: '', assign: p => redirects.push(p), replace: p => redirects.push(p) } }, document: { body: {dataset:{}}, getElementById: node, querySelector: node }, localStorage: {getItem: k => storage.get(k) || null, setItem: (k,v) => storage.set(k,v), removeItem: k => storage.delete(k)}, fetch: async (url, options) => {
    calls.push({url, options});
    const next = responses.shift();
    if (next instanceof Error) throw next;
    assert.ok(next, 'unexpected request');
    return {ok: next.status < 400, status: next.status, json: async () => next.body};
  }});
  vm.runInContext(read('shared/js/auth.js'), context);
  vm.runInContext(read('reservation/assets/js/reservation_api.js'), context);
  return {context, node, calls, redirects, storage, auth: vm.runInContext('RoadRideAuth', context), api: vm.runInContext('RoadRideReservationApi', context)};
}
const form = {pickup:'市役所', destination:'病院', date:'2026-10-02', hour:'03', minute:'30', passengers:'2', notes:''};
test('Bearer header, documented request fields and JST formatting', async () => {
  const s = setup([{status:201, body:{data:{id:'r1'}}}]);
  await s.api.createReservation(form);
  assert.equal(s.calls[0].options.headers.Authorization, 'Bearer test-token');
  const body = JSON.parse(s.calls[0].options.body);
  assert.equal('user_id' in body, false);
  assert.equal(body.requested_pickup_at, '2026-10-02T03:30:00+09:00');
  assert.equal(s.api.formatDateTime('2026-10-01T18:30:00Z'), '2026/10/02 03:30');
});
test('pagination uses API query and total', async () => {
  const s = setup([{status:200,body:{data:[],meta:{total:51,page:3,limit:20}}}]);
  const result = await s.api.listReservations({page:3});
  assert.equal(result.meta.total,51);
  assert.equal(s.calls[0].url,'/api/reservations?page=3&limit=20');
});
for (const status of [400,401,404,409,422,429,500]) test(`HTTP ${status} becomes visible Japanese error`, async () => {
  const s=setup([{status,body:null}]);
  await assert.rejects(s.api.cancelReservation('r1'), error => {
    assert.ok(error instanceof s.auth.ApiError);
    const element = s.node('page-error');
    s.api.showMessage(element,s.api.errorMessage(error));
    assert.match(element.textContent,/[ぁ-んァ-ヶ一-龠]/);
    assert.equal(element.hidden,false);
    return true;
  });
  if(status===401) {assert.equal(s.storage.has('roadride_access_token'),false);assert.match(s.redirects[0],/reason=session-expired/);}
});
test('wrong password is shown in login form and submission is enabled again', async () => {
  const s=setup([{status:401,body:{error:{code:'INVALID_CREDENTIALS'}}}]);
  s.storage.clear();
  vm.runInContext(read('login/assets/js/login.js'),s.context);
  await s.node('login-form').listeners.submit({preventDefault(){}});
  assert.equal(s.node('login-error').textContent,'メールアドレスまたはパスワードが正しくありません。');
  assert.equal(s.node('password').attrs['aria-invalid'],'true');
  assert.equal(s.node('button[type=submit]').disabled,false);
  assert.equal(s.redirects.length,0);
});
test('registration API field error appears beside matching input', async () => {
  const s=setup([{status:422,body:{error:{code:'VALIDATION_ERROR',details:[{field:'kanaLastName',message:'invalid'}]}}}]);
  s.storage.clear();
  vm.runInContext(read('login/assets/js/register.js'),s.context);
  await s.node('register-form').listeners.submit({preventDefault(){}});
  assert.match(s.node('kana-last-name-error').textContent,/全角カタカナ/);
  assert.equal(s.node('kana-last-name').attrs['aria-invalid'],'true');
  assert.equal(s.node('button[type=submit]').disabled,false);
});
test('browser input validation also appears in persistent form error', () => {
  const s=setup();s.storage.clear();
  vm.runInContext(read('login/assets/js/login.js'),s.context);
  s.node('login-form').listeners.invalid({target:{validationMessage:'メールアドレスを入力してください。'}});
  assert.match(s.node('login-error').textContent,/メールアドレス/);
});
test('session expiry message survives navigation to login', () => {
  const s=setup([], '?reason=session-expired');s.storage.clear();
  vm.runInContext(read('login/assets/js/login.js'),s.context);
  assert.match(s.node('login-error').textContent,/有効期限/);
});
test('dispatcher registration stays disabled', () => {
  const s=setup([], '?role=dispatcher');s.storage.clear();
  vm.runInContext(read('login/assets/js/register.js'),s.context);
  assert.equal(s.redirects[0],'index.html?role=dispatcher&next=dispatch');
});
test('network failure is Japanese and does not return cached data', async () => {
  const s=setup([new Error('offline')]);
  await assert.rejects(s.api.listReservations(),/通信環境/);
});
test('invalid server response is handled explicitly', async () => {
  const s=setup([{status:200,body:null}]);
  await assert.rejects(s.auth.login('a','b'),/正しい応答/);
});
test('combined notes limit retained and missing reservation rejected', async () => {
  const s=setup();
  await assert.rejects(s.api.createReservation({...form,care:'wheelchair',notes:'あ'.repeat(500)}),/500文字/);
  await assert.rejects(s.api.cancelReservation(''),/予約が指定/);
  assert.equal(s.calls.length,0);
});
test('failed reservation does not navigate to success page', async () => {
  const query = new URLSearchParams(form).toString();
  const s=setup([{status:400,body:null}], '?' + query);
  vm.runInContext(read('reservation/assets/js/reservation_confirm.js'),s.context);
  await s.node('#confirm-button').listeners.click();
  assert.match(s.node('#page-error').textContent,/形式/);
  assert.equal(s.node('#page-error').hidden,false);
  assert.equal(s.node('#confirm-button').disabled,false);
  assert.equal(s.context.window.location.href,'');
});
