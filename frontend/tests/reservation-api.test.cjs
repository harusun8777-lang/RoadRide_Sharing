const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const source = fs.readFileSync(path.join(__dirname, '../reservation/assets/js/reservation_api.js'), 'utf8');
const record = { id: 'reservation-1', reservation_number: 'RR-20260926-A1B2', requested_pickup_at: '2026-10-01T18:30:00Z', pickup_location: '市役所', destination: '病院', passenger_count: 2, status: 'matching' };
function setup(responses) {
  const calls = [], redirects = [];
  let cleared = false;
  const context = vm.createContext({ URLSearchParams, Date, window: { location: { assign: url => redirects.push(url) } }, RoadRideAuth: { getToken: () => 'test-token', clearToken: () => { cleared = true; } }, fetch: async (url, options) => {
    calls.push({ url, options });
    const next = responses.shift();
    if (next instanceof Error) throw next;
    assert.ok(next, 'Unexpected request');
    return { ok: next.status < 400, status: next.status, json: async () => next.body };
  }});
  vm.runInContext(source, context);
  return { api: vm.runInContext('RoadRideReservationApi', context), calls, redirects, cleared: () => cleared };
}
test('create sends documented fields and Bearer token; UTC crosses day boundary into JST', async () => {
  const { api, calls } = setup([{ status: 201, body: { data: record } }]);
  const result = await api.createReservation({ pickup: '市役所', destination: '病院', date: '2026-10-02', hour: '03', minute: '30', passengers: '2', care: '', notes: '' });
  assert.equal(calls[0].options.headers.Authorization, 'Bearer test-token');
  const body = JSON.parse(calls[0].options.body);
  assert.equal(body.requested_pickup_at, '2026-10-02T03:30:00+09:00');
  assert.equal('user_id' in body, false);
  assert.equal('Idempotency-Key' in calls[0].options.headers, false);
  assert.equal(result.date, '2026-10-02');
  assert.equal(result.hour, '03');
});
test('list retrieves later pages and does not send user_id', async () => {
  const { api, calls } = setup([{ status: 200, body: { data: Array(50).fill(record), meta: { total: 51 } } }, { status: 200, body: { data: [record], meta: { total: 51 } } }]);
  assert.equal((await api.listReservations()).length, 51);
  assert.equal(calls[1].url, '/api/reservations?page=2&limit=50');
});
for (const status of [401, 404, 409, 422, 500]) test(`cancel rejects HTTP ${status} without returning fabricated success`, async () => {
  const state = setup([{ status, body: { error: { message: '失敗' } } }]);
  await assert.rejects(state.api.cancelReservation({ reservationId: record.id }, '理由'));
  if (status === 401) { assert.equal(state.cleared(), true); assert.equal(state.redirects.length, 1); }
});
test('network errors never return cached list or detail', async () => {
  const { api } = setup([new Error('offline'), new Error('offline')]);
  await assert.rejects(api.listReservations(), /offline/);
  await assert.rejects(api.getReservation(record.id, { status: 'matching' }), /offline/);
});
test('cancel without ID is rejected without sending a request', async () => {
  const { api, calls } = setup([]);
  await assert.rejects(api.cancelReservation({}, '理由'), /予約ID/);
  assert.equal(calls.length, 0);
});
test('cancel preserves authoritative cancellation timestamp and null reason', async () => {
  const { api } = setup([{ status: 200, body: { data: { status: 'cancelled', cancellation_reason: null, cancelled_at: '2026-10-01T00:00:00Z' } } }]);
  const result = await api.cancelReservation({ reservationId: record.id }, undefined);
  assert.equal(result.cancelledAt, '2026-10-01T00:00:00Z');
  assert.equal(result.cancellationReason, null);
});
test('combined care and notes length is validated before sending', async () => {
  const { api, calls } = setup([]);
  await assert.rejects(api.createReservation({ pickup: 'A', destination: 'B', passengers: '1', care: 'wheelchair', notes: 'あ'.repeat(500) }), /500文字/);
  assert.equal(calls.length, 0);
});
test('reservation confirmation failure stays on page and re-enables submission', async () => {
  const button = { addEventListener: (_, fn) => { button.click = fn; } };
  const nodes = {};
  const errors = [];
  const window = { location: { search: '', href: 'reservation_confirm.html' } };
  vm.runInNewContext(fs.readFileSync(path.join(__dirname, '../reservation/assets/js/reservation_confirm.js'), 'utf8'), { URLSearchParams, window, document: { querySelector: id => id === '#confirm-button' ? button : (nodes[id] ||= { addEventListener() {} }) }, RoadRideReservationApi: { fromParams: () => ({}), createReservation: async () => { throw new Error('拒否'); }, showError: e => errors.push(e.message) } });
  await button.click();
  assert.equal(window.location.href, 'reservation_confirm.html');
  assert.equal(button.disabled, false);
  assert.deepEqual(errors, ['拒否']);
});
