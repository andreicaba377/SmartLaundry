'use strict';
const $ = (s, r = document) => r.querySelector(s);
const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
const S = { token: localStorage.getItem('slc_token'), admin: localStorage.getItem('slc_admin') === '1', user: null, dorms: [], machines: [], programs: {}, cfg: { speed: 30 },
  dorm: +localStorage.getItem('slc_dorm') || 0, page: 'dashboard', filter: { type: 'all', status: 'all' }, notifs: [], resv: [], modalId: null, sse: null };

const STATUS = { free: 'Liberă', running: 'În funcțiune', finished: 'Terminată', blocked: 'Blocată', reserved: 'Rezervată', out_of_order: 'Defectă', offline: 'Offline' };
const COLORS = { free: '#16a34a', running: '#2563eb', finished: '#d97706', blocked: '#dc2626', reserved: '#7c3aed', out_of_order: '#dc2626', offline: '#64748b' };

async function api(path, opts = {}) {
  const r = await fetch('/api' + path, { method: opts.method || 'GET', headers: { 'Content-Type': 'application/json', ...(S.token ? { Authorization: 'Bearer ' + S.token } : {}) },
    body: opts.body ? JSON.stringify(opts.body) : undefined });
  const d = await r.json().catch(() => ({}));
  if (r.status === 401 && S.token && !path.startsWith('/login')) { logout(false); }
  if (!r.ok) throw new Error(d.error || 'Eroare');
  return d;
}
function toast(msg, kind = '') { const t = document.createElement('div'); t.className = 'toast ' + kind; t.textContent = msg; $('#toasts').append(t); setTimeout(() => t.remove(), 4500); }
const act = async (fn, okMsg) => { try { await fn(); if (okMsg) toast(okMsg, 'ok'); await refresh(); } catch (e) { toast(e.message, 'err'); } };
const dormName = id => (S.dorms.find(d => d.id === id) || {}).name || '';
const fmtT = ts => ts ? new Date(ts).toLocaleString('ro-RO', { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' }) : '–';
const fmtMin = m => m >= 60 ? `${Math.floor(m / 60)}h ${String(m % 60).padStart(2, '0')}m` : `${m} min`;
const simMin = ms => Math.ceil(ms * S.cfg.speed / 60000);

// ---------- THEME ----------
function applyTheme(t) { document.documentElement.dataset.theme = t; localStorage.setItem('slc_theme', t); }
applyTheme(localStorage.getItem('slc_theme') || (matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'));
$('#themeBtn').onclick = () => applyTheme(document.documentElement.dataset.theme === 'dark' ? 'light' : 'dark');

// ---------- AUTH ----------
function logout(call = true) {
  if (call && S.token) fetch('/api/logout', { method: 'POST', headers: { Authorization: 'Bearer ' + S.token } }).catch(() => {});
  localStorage.removeItem('slc_token'); localStorage.removeItem('slc_admin'); S.token = null; S.admin = false; S.sse?.close(); location.reload();
}
$('#logout').onclick = () => logout();
let mode = 'in';
function setMode(m) {
  mode = m; const up = m === 'up';
  document.querySelectorAll('.tab').forEach(t => t.classList.toggle('on', t.dataset.t === m));
  document.querySelectorAll('.f.up').forEach(f => f.classList.toggle('hidden', !up));
  $('#authTitle').textContent = up ? 'Creează-ți contul ✨' : 'Bine ai revenit 👋';
  $('#authSub').textContent = up ? 'Durează mai puțin de un minut.' : 'Conectează-te pentru a-ți gestiona spălările.';
  $('#authBtn').textContent = up ? 'Creează cont' : 'Autentificare';
  $('#fPass').autocomplete = up ? 'new-password' : 'current-password';
  $('#demoBox').classList.toggle('hidden', up); $('#authErr').textContent = '';
}
document.querySelectorAll('.tab').forEach(t => t.onclick = () => setMode(t.dataset.t));
$('#eye').onclick = () => { const p = $('#fPass'); p.type = p.type === 'password' ? 'text' : 'password'; };
document.querySelectorAll('[data-e]').forEach(b => b.onclick = () => { $('#fEmail').value = b.dataset.e; $('#fPass').value = b.dataset.p; $('#authErr').textContent = ''; $('#authForm').requestSubmit(); });
$('#authForm').onsubmit = async ev => {
  ev.preventDefault();
  const err = $('#authErr'), btn = $('#authBtn'); err.textContent = '';
  document.querySelectorAll('#authForm input').forEach(i => i.classList.remove('bad'));
  const bad = (id, msg) => { $(id).classList.add('bad'); $(id).focus(); err.textContent = msg; return false; };
  const email = $('#fEmail').value.trim(), pass = $('#fPass').value;
  if (mode === 'up' && $('#fName').value.trim().length < 3) return bad('#fName', 'Introdu numele complet.');
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) return bad('#fEmail', 'Introdu o adresă de email validă.');
  if (!pass) return bad('#fPass', 'Introdu parola.');
  if (mode === 'up' && pass.length < 6) return bad('#fPass', 'Parola trebuie să aibă minim 6 caractere.');
  if (mode === 'up' && !$('#fRoom').value.trim()) return bad('#fRoom', 'Introdu numărul camerei.');
  btn.classList.add('loading'); const label = btn.textContent; btn.textContent = 'Se procesează…';
  try {
    const body = mode === 'up' ? { name: $('#fName').value, email, password: pass, room: $('#fRoom').value, dorm_id: +$('#fDorm').value } : { email, password: pass };
    const d = await api(mode === 'up' ? '/register' : '/login', { method: 'POST', body });
    S.token = d.token; S.admin = d.role === 'admin'; localStorage.setItem('slc_token', d.token); localStorage.setItem('slc_admin', S.admin ? '1' : '0');
    S.page = S.admin ? 'admin' : 'dashboard'; boot();
  } catch (er) { err.textContent = er.message; btn.classList.remove('loading'); btn.textContent = label; }
};
function heroStats() {
  const ds = S.dorms, f = k => ds.reduce((a, d) => a + d[k], 0);
  $('#heroStats').innerHTML = `<div><b>${f('washers_free')}</b><span>mașini libere acum</span></div><div><b>${f('running')}</b><span>cicluri în desfășurare</span></div><div><b>${ds.length}</b><span>cămine conectate</span></div>`;
}

// ---------- DATA ----------
async function refresh(full = true) {
  const [machines, dorms] = await Promise.all([api('/machines'), api('/dorms')]);
  S.machines = machines; S.dorms = dorms;
  if (!S.admin && full) {
    const me = await api('/me'); S.user = me.user; S.notifs = me.notifications; S.resv = me.reservations;
  }
  renderChrome();
  const ae = document.activeElement;
  if (!(ae && $('#view').contains(ae) && ['INPUT', 'TEXTAREA', 'SELECT'].includes(ae.tagName))) render();
  if (S.modalId) openMachine(S.modalId, true);
}

function renderChrome() {
  const unread = S.notifs.filter(n => !n.is_read).length;
  $('#bellN').textContent = unread; $('#bellN').classList.toggle('hidden', !unread);
  $('#bellBtn').classList.toggle('hidden', S.admin);
  $('#userBox').innerHTML = S.admin ? '<b>Administrator</b><small>acces complet</small>' : `<b>${esc(S.user?.name)}</b><small>Camera ${esc(S.user?.room)}</small>`;
  const sel = $('#dormSel'); const cur = sel.value;
  sel.innerHTML = '<option value="0">Toate căminele</option>' + S.dorms.map(d => `<option value="${d.id}">${esc(d.name)}</option>`).join('');
  sel.value = cur || S.dorm;
  const nav = S.admin ? [['admin', '📊', 'Administrare'], ['dashboard', '🏠', 'Panou'], ['machines', '🧺', 'Mașini'], ['defects', '🔧', 'Defecțiuni']]
    : [['dashboard', '🏠', 'Panou'], ['machines', '🧺', 'Mașini'], ['reservations', '📅', 'Rezervări'], ['history', '🕘', 'Istoric'], ['defects', '🔧', 'Defecțiuni'], ['notifications', '🔔', 'Notificări']];
  const openDef = S.admin ? S.machines.filter(m => m.status === 'out_of_order').length : 0;
  $('#nav').innerHTML = nav.map(([id, ic, l]) => `<a data-p="${id}" class="${S.page === id ? 'on' : ''}"><span>${ic}</span>${l}${id === 'defects' && openDef ? `<span class="n">${openDef}</span>` : ''}${id === 'notifications' && unread ? `<span class="n">${unread}</span>` : ''}</a>`).join('');
  $('#nav').querySelectorAll('a').forEach(a => a.onclick = () => { go(a.dataset.p); $('.side').classList.remove('open'); });
  $('#speedInfo').textContent = `Simulare: ×${S.cfg.speed} (1 s = ${S.cfg.speed} s)`;
}
$('#dormSel').onchange = e => { S.dorm = +e.target.value; localStorage.setItem('slc_dorm', S.dorm); render(); };
$('#bellBtn').onclick = () => go('notifications');
$('#menuBtn').onclick = () => $('.side').classList.toggle('open');
function go(p) { S.page = p; renderChrome(); render(); window.scrollTo(0, 0); }

const titles = { dashboard: 'Panou de control', machines: 'Mașini', reservations: 'Rezervările mele', history: 'Istoric & consum', defects: 'Defecțiuni', notifications: 'Notificări', admin: 'Administrare' };
function render() {
  $('#pageTitle').textContent = titles[S.page];
  $('#dormSel').classList.toggle('hidden', ['reservations', 'history', 'notifications', 'admin'].includes(S.page));
  const v = $('#view');
  ({ dashboard: viewDashboard, machines: viewMachines, reservations: viewReservations, history: viewHistory, defects: viewDefects, notifications: viewNotifs, admin: viewAdmin }[S.page])(v);
}
const visible = () => S.machines.filter(m => !S.dorm || m.dorm_id === S.dorm);

// ---------- COMPONENTE ----------
function ring(m) {
  const R = 32, C = 2 * Math.PI * R, col = COLORS[m.status];
  let inner;
  if (m.status === 'running') inner = `<b>${fmtMin(m.remaining_sim_min)}</b><small>rămas</small>`;
  else if (m.status === 'finished' || m.status === 'blocked') inner = `<b>✔</b><small>gata</small>`;
  else inner = `<span style="font-size:26px">${m.type === 'washer' ? '🧺' : '♨️'}</span>`;
  return `<div class="ring"><svg width="74" height="74"><circle cx="37" cy="37" r="${R}" fill="none" stroke="var(--border)" stroke-width="6"/>
    <circle cx="37" cy="37" r="${R}" fill="none" stroke="${col}" stroke-width="6" stroke-linecap="round" stroke-dasharray="${C}" stroke-dashoffset="${C * (1 - (m.progress || 0))}"/></svg><div class="t"><div>${inner}</div></div></div>`;
}
function machineCard(m) {
  let info = '';
  if (m.status === 'running') info = `<div><b>${esc(m.program_label)}</b></div><div>${esc(m.phase || '')}</div><div>⚡ ${Math.round(m.power_w)} W</div>`;
  else if (m.status === 'free') info = `<div>Disponibilă acum</div><div>${m.type === 'washer' ? 'Mașină de spălat' : 'Uscător'}</div>`;
  else if (m.status === 'finished') info = `<div><b>Terminat</b></div><div>Rufele așteaptă</div>`;
  else if (m.status === 'blocked') info = `<div><b>Blochează mașina</b></div><div>de ${Math.round(simMin(m.blocked_for_ms))} min</div>`;
  else if (m.status === 'reserved') info = `<div><b>Rezervată</b></div><div>${m.held_for_me ? 'pentru tine' : 'de alt student'}</div>`;
  else if (m.status === 'out_of_order') info = `<div><b>Defectă</b></div><div>Service anunțat</div>`;
  else info = `<div><b>Fără semnal</b></div><div>Senzor offline</div>`;
  const tags = [m.mine ? '<span class="tag">Programul tău</span>' : '', m.queue ? `<span class="tag">👥 ${m.queue} în coadă</span>` : '', m.my_reservation ? '<span class="tag">📅 Rezervat de tine</span>' : ''].join('');
  return `<div class="mc s-${m.status}" data-id="${m.id}"><div class="mc-h"><div><b>${esc(m.name)}</b><small>${esc(dormName(m.dorm_id))}</small></div><span class="pill s-${m.status}">${STATUS[m.status]}</span></div>
    <div class="mc-body">${ring(m)}<div class="mc-info">${info}</div></div><div class="mc-foot">${tags}</div></div>`;
}
function bindCards(root) { root.querySelectorAll('.mc').forEach(c => c.onclick = () => openMachine(+c.dataset.id)); }
const kpi = (ic, bg, col, val, label) => `<div class="card kpi"><div class="ic" style="background:${bg};color:${col}">${ic}</div><div><b>${val}</b><span>${label}</span></div></div>`;

// ---------- DASHBOARD ----------
function viewDashboard(v) {
  const ms = visible(); const c = (t, s) => ms.filter(m => m.type === t && m.status === s).length;
  const my = S.admin ? [] : S.machines.filter(m => m.mine && ['running', 'finished', 'blocked'].includes(m.status));
  v.innerHTML = `
  <div class="grid g4">
    ${kpi('🧺', 'var(--free-s)', 'var(--free)', c('washer', 'free') + '/' + ms.filter(m => m.type === 'washer').length, 'Mașini de spălat libere')}
    ${kpi('♨️', 'var(--free-s)', 'var(--free)', c('dryer', 'free') + '/' + ms.filter(m => m.type === 'dryer').length, 'Uscătoare libere')}
    ${kpi('🌀', 'var(--run-s)', 'var(--run)', ms.filter(m => m.status === 'running').length, 'În funcțiune acum')}
    ${kpi('⚠️', 'var(--block-s)', 'var(--block)', ms.filter(m => ['out_of_order', 'blocked', 'offline'].includes(m.status)).length, 'Defecte / blocate')}
  </div>
  ${my.length ? `<div class="section-h"><h2>Ciclurile mele active</h2></div><div class="grid g-auto">${my.map(machineCard).join('')}</div>` : ''}
  <div class="section-h"><h2>Disponibilitate pe cămine</h2></div>
  <div class="grid g2">${S.dorms.map(d => {
    const dm = S.machines.filter(m => m.dorm_id === d.id); const n = dm.length || 1;
    const seg = (st, col) => `<i style="width:${dm.filter(m => st.includes(m.status)).length / n * 100}%;background:${col}"></i>`;
    return `<div class="card dorm"><div class="row-b" style="margin-top:0"><div><h3>${esc(d.name)}</h3><span class="muted xs">${esc(d.address)}</span></div><button class="btn sm" data-d="${d.id}">Vezi mașini</button></div>
      <div class="row-b"><span>🧺 ${d.washers_free}/${d.washers_total} spălat &nbsp; ♨️ ${d.dryers_free}/${d.dryers_total} uscat</span><span class="muted">${d.running} active${d.broken ? ` · ${d.broken} defecte` : ''}</span></div>
      <div class="bar">${seg(['free'], COLORS.free)}${seg(['reserved'], COLORS.reserved)}${seg(['running'], COLORS.running)}${seg(['finished', 'blocked'], COLORS.finished)}${seg(['out_of_order', 'offline'], COLORS.blocked)}</div></div>`;
  }).join('')}</div>
  <div class="legend" style="margin-top:12px">${['free', 'reserved', 'running', 'finished', 'out_of_order'].map(s => `<span><i style="background:${COLORS[s]}"></i>${STATUS[s]}</span>`).join('')}</div>
  <div class="section-h"><h2>Disponibile acum${S.dorm ? ' în ' + esc(dormName(S.dorm)) : ''}</h2></div>
  <div class="grid g-auto">${ms.filter(m => m.status === 'free').slice(0, 8).map(machineCard).join('') || '<div class="card empty" style="grid-column:1/-1">Nicio mașină liberă în acest moment. Rezervă o mașină și vei fi notificat!</div>'}</div>`;
  v.querySelectorAll('[data-d]').forEach(b => b.onclick = () => { S.dorm = +b.dataset.d; $('#dormSel').value = S.dorm; go('machines'); });
  bindCards(v);
}

// ---------- MACHINES ----------
function viewMachines(v) {
  const f = S.filter;
  const chips = (key, opts) => opts.map(([k, l]) => `<span class="chip ${f[key] === k ? 'on' : ''}" data-k="${key}" data-v="${k}">${l}</span>`).join('');
  let ms = visible().filter(m => (f.type === 'all' || m.type === f.type) && (f.status === 'all' || (f.status === 'problem' ? ['out_of_order', 'blocked', 'offline'].includes(m.status) : m.status === f.status)));
  v.innerHTML = `<div class="toolbar">${chips('type', [['all', 'Toate'], ['washer', '🧺 Spălat'], ['dryer', '♨️ Uscat']])}<span style="width:12px"></span>
    ${chips('status', [['all', 'Orice stare'], ['free', 'Libere'], ['running', 'În funcțiune'], ['finished', 'Terminate'], ['reserved', 'Rezervate'], ['problem', 'Probleme']])}</div>
    <div class="grid g-auto">${ms.map(machineCard).join('') || '<div class="card empty" style="grid-column:1/-1">Nicio mașină nu corespunde filtrelor.</div>'}</div>`;
  v.querySelectorAll('.chip').forEach(c => c.onclick = () => { S.filter[c.dataset.k] = c.dataset.v; render(); });
  bindCards(v);
}

// ---------- MACHINE MODAL ----------
function spark(tel) {
  if (!tel.length) return '<div class="muted xs" style="padding:20px 0">Fără date de telemetrie încă.</div>';
  const w = 560, h = 70, max = Math.max(500, ...tel.map(t => t.power_w));
  const pts = tel.map((t, i) => `${(i / Math.max(1, tel.length - 1) * w).toFixed(1)},${(h - 4 - t.power_w / max * (h - 8)).toFixed(1)}`).join(' ');
  return `<svg class="spark" viewBox="0 0 ${w} ${h}" preserveAspectRatio="none"><polyline points="0,${h} ${pts} ${w},${h}" fill="rgba(37,99,235,.12)" stroke="none"/><polyline points="${pts}" fill="none" stroke="var(--primary)" stroke-width="2" vector-effect="non-scaling-stroke"/></svg>`;
}
let selProg = null;
async function openMachine(id, keepProg) {
  S.modalId = id;
  let m; try { m = await api('/machines/' + id); } catch { return closeModal(); }
  if (!keepProg) selProg = null;
  const progs = Object.entries(S.programs).filter(([, p]) => p.type === m.type);
  if (!selProg || !S.programs[selProg] || S.programs[selProg].type !== m.type) selProg = progs[1]?.[0] || progs[0][0];
  const canStart = m.status === 'free' || (m.status === 'reserved' && m.held_for_me);
  let actions = '';
  if (canStart) actions += `<button class="btn primary" id="aStart">▶ Pornește programul</button>`;
  if (['finished', 'blocked'].includes(m.status) && m.mine) actions += `<button class="btn ok" id="aCollect">✔ Mi-am ridicat rufele</button>`;
  if (!S.admin && !m.my_reservation && !m.held_for_me && !['out_of_order', 'offline'].includes(m.status))
    actions += `<button class="btn" id="aReserve">📅 ${m.status === 'free' ? 'Rezervă 5 min' : 'Intră la coadă'}</button>`;
  if (m.my_reservation) actions += `<button class="btn danger" id="aCancel">Anulează rezervarea</button>`;
  if (!S.admin) actions += `<button class="btn" id="aDefect">🔧 Raportează defecțiune</button>`;
  if (S.admin) {
    actions += `<button class="btn" data-a="fault">⚡ Simulează defect</button><button class="btn ok" data-a="release">Resetează / eliberează</button><button class="btn danger" data-a="disable">Scoate din uz</button>`;
  }
  const hold = m.status === 'reserved' && m.hold_until ? `<div class="muted xs">Rezervarea expiră ${new Date(m.hold_until).toLocaleTimeString('ro-RO')}</div>` : '';
  $('#sheet').innerHTML = `<div class="sheet-h"><div><h2>${esc(m.name)}</h2><span class="muted">${esc(dormName(m.dorm_id))} · ${m.type === 'washer' ? 'Mașină de spălat' : 'Uscător'}</span></div>
      <div style="display:flex;gap:10px;align-items:center"><span class="pill s-${m.status}">${STATUS[m.status]}</span><button class="x" id="xClose">×</button></div></div>
    ${m.status === 'running' ? `<div class="card" style="padding:14px;display:flex;gap:16px;align-items:center">${ring(m)}<div><b>${esc(m.program_label)}</b><div class="muted">${esc(m.phase || '')} · ${m.temp_c}°C</div><div class="muted xs">Finalizare estimată în ${fmtMin(m.remaining_sim_min)}</div></div></div>` : ''}
    <div class="tel"><div><b>${Math.round(m.power_w)} W</b><span>Putere</span></div><div><b>${m.rpm}</b><span>Turații/min</span></div><div><b>${(+m.vibration).toFixed(1)}</b><span>Vibrații mm/s</span></div><div><b>${m.door === 'locked' ? '🔒' : '🔓'}</b><span>Ușă</span></div></div>
    <div class="muted xs" style="margin-bottom:4px">Consum instantaneu (ultimele 60 s)</div>${spark(m.telemetry)}${hold}
    ${canStart ? `<h3 style="margin:16px 0 4px">Alege programul</h3><div class="progs">${progs.map(([k, p]) => `<div class="prog ${k === selProg ? 'on' : ''}" data-k="${k}"><div><b>${p.label}</b><small>${p.min} min · ${p.kwh} kWh${p.water ? ` · ${p.water} L` : ''}</small></div><b>${p.price} lei</b></div>`).join('')}</div>` : ''}
    <div class="actions">${actions}</div>
    ${m.defects.length ? `<h3 style="margin:20px 0 6px">Defecțiuni recente</h3>${m.defects.map(d => `<div class="muted xs">• ${esc(d.description)} <span class="tag">${d.status}</span></div>`).join('')}` : ''}
    <div class="muted xs" style="margin-top:16px">Cicluri: ${m.cycles} · Energie totală: ${(+m.kwh_total).toFixed(1)} kWh · Apă: ${Math.round(m.water_total)} L${m.api_key ? ` · Cheie IoT: <code>${m.api_key}</code>` : ''}</div>`;
  $('#modal').classList.remove('hidden');
  $('#xClose').onclick = closeModal;
  $('#sheet').querySelectorAll('.prog').forEach(p => p.onclick = () => { selProg = p.dataset.k; $('#sheet').querySelectorAll('.prog').forEach(x => x.classList.toggle('on', x === p)); });
  const bind = (id, fn) => { const b = $(id); if (b) b.onclick = fn; };
  bind('#aStart', () => act(() => api(`/machines/${id}/start`, { method: 'POST', body: { program: selProg } }), 'Program pornit! Te anunțăm când se termină.').then(closeModal));
  bind('#aCollect', () => act(() => api(`/machines/${id}/collect`, { method: 'POST' }), 'Mașina a fost eliberată.').then(closeModal));
  bind('#aReserve', () => act(() => api(`/machines/${id}/reserve`, { method: 'POST' }), m.status === 'free' ? 'Mașină rezervată 5 minute.' : 'Ai intrat la coadă. Te notificăm când se eliberează.'));
  bind('#aCancel', () => act(() => api(`/reservations/${m.my_reservation}/cancel`, { method: 'POST' }), 'Rezervare anulată.'));
  bind('#aDefect', () => defectForm(m));
  $('#sheet').querySelectorAll('[data-a]').forEach(b => b.onclick = () => act(() => api(`/admin/machines/${id}/${b.dataset.a}`, { method: 'POST' }), 'Acțiune executată.'));
}
function closeModal() { S.modalId = null; $('#modal').classList.add('hidden'); }
$('#modal').onclick = e => { if (e.target.id === 'modal') closeModal(); };

function defectForm(m) {
  S.modalId = null;
  $('#sheet').innerHTML = `<div class="sheet-h"><h2>Raportează defecțiune</h2><button class="x" id="xClose">×</button></div>
    <p class="muted">${esc(m.name)} · ${esc(dormName(m.dorm_id))}</p>
    <form id="dForm" class="grid" style="gap:14px"><label>Descriere<textarea id="dDesc" rows="4" required placeholder="Ce nu funcționează?"></textarea></label>
    <label>Gravitate<select id="dSev"><option value="low">Minoră</option><option value="medium" selected>Medie</option><option value="high">Gravă (scoate mașina din uz)</option></select></label>
    <button class="btn primary" type="submit">Trimite raportul</button></form>`;
  $('#xClose').onclick = closeModal;
  $('#dForm').onsubmit = e => { e.preventDefault(); act(() => api('/defects', { method: 'POST', body: { machine_id: m.id, description: $('#dDesc').value, severity: $('#dSev').value } }), 'Raport trimis. Mulțumim!').then(closeModal); };
}

// ---------- REZERVĂRI ----------
function viewReservations(v) {
  v.innerHTML = `<div class="card tbl">${S.resv.length ? `<table><tr><th>Mașina</th><th>Cămin</th><th>Tip</th><th>Stare mașină</th><th></th></tr>${S.resv.map(r =>
    `<tr><td><b>${esc(r.machine_name)}</b></td><td>${esc(dormName(r.dorm_id))}</td><td>${r.status === 'active' ? '<span class="pill s-reserved">Activă (5 min)</span>' : '<span class="pill s-finished">În coadă</span>'}</td>
    <td>${STATUS[r.mstatus] || r.mstatus}</td><td style="text-align:right"><button class="btn sm" data-o="${r.machine_id}">Deschide</button> <button class="btn sm danger" data-c="${r.id}">Anulează</button></td></tr>`).join('')}</table>`
    : '<div class="empty">Nu ai rezervări sau cozi active.<br>Deschide o mașină ocupată și apasă „Intră la coadă”.</div>'}</div>`;
  v.querySelectorAll('[data-o]').forEach(b => b.onclick = () => openMachine(+b.dataset.o));
  v.querySelectorAll('[data-c]').forEach(b => b.onclick = () => act(() => api(`/reservations/${b.dataset.c}/cancel`, { method: 'POST' }), 'Rezervare anulată.'));
}

// ---------- ISTORIC ----------
async function viewHistory(v) {
  const h = await api('/history'); if (S.page !== 'history') return; const st = S.user.stats;
  v.innerHTML = `<div class="grid g4">${kpi('🔁', 'var(--primary-s)', 'var(--primary)', st.n, 'Cicluri totale')}${kpi('⚡', 'var(--fin-s)', 'var(--fin)', st.kwh.toFixed(1) + ' kWh', 'Energie consumată')}
    ${kpi('💧', 'var(--run-s)', 'var(--run)', Math.round(st.water) + ' L', 'Apă consumată')}${kpi('💳', 'var(--free-s)', 'var(--free)', st.cost + ' lei', 'Cost total')}</div>
    <div class="section-h"><h2>Ultimele utilizări</h2></div><div class="card tbl">${h.length ? `<table><tr><th>Data</th><th>Mașina</th><th>Cămin</th><th>Program</th><th>Energie</th><th>Apă</th><th>Cost</th></tr>${h.map(r =>
      `<tr><td>${fmtT(r.started_at)}</td><td>${esc(r.machine_name)}</td><td>${esc(dormName(r.dorm_id))}</td><td>${esc(r.program_label)}</td><td>${r.kwh} kWh</td><td>${r.water || '–'} ${r.water ? 'L' : ''}</td><td>${r.cost} lei</td></tr>`).join('')}</table>` : '<div class="empty">Încă nu ai niciun ciclu.</div>'}</div>`;
}

// ---------- DEFECȚIUNI ----------
async function viewDefects(v) {
  const d = await api('/defects'); if (S.page !== 'defects') return;
  const cls = { open: 's-blocked', in_progress: 's-finished', resolved: 's-free' }, lab = { open: 'Deschis', in_progress: 'În lucru', resolved: 'Rezolvat' };
  v.innerHTML = `<div class="toolbar"><span class="muted">Raportează o problemă dintr-o mașină (deschide mașina → „Raportează defecțiune”).</span></div>
  <div class="card tbl">${d.length ? `<table><tr><th>Data</th><th>Mașina</th><th>Cămin</th><th>Descriere</th><th>Gravitate</th><th>Sursă</th><th>Stare</th>${S.admin ? '<th></th>' : ''}</tr>${d.map(r =>
    `<tr><td>${fmtT(r.created)}</td><td><b>${esc(r.machine_name)}</b></td><td>${esc(dormName(r.dorm_id))}</td><td style="max-width:340px">${esc(r.description)}${r.user_name ? `<div class="muted xs">de ${esc(r.user_name)}</div>` : ''}</td>
    <td class="sev-${r.severity}"><b>${{ low: 'Minoră', medium: 'Medie', high: 'Gravă' }[r.severity]}</b></td><td>${r.source === 'sensor' ? '📡 Senzor' : '👤 Student'}</td><td><span class="pill ${cls[r.status]}">${lab[r.status]}</span></td>
    ${S.admin ? `<td style="white-space:nowrap">${r.status !== 'resolved' ? `<button class="btn sm" data-s="in_progress" data-id="${r.id}">În lucru</button> <button class="btn sm ok" data-s="resolved" data-id="${r.id}">Rezolvat</button>` : ''}</td>` : ''}</tr>`).join('')}</table>` : '<div class="empty">Nicio defecțiune raportată. 🎉</div>'}</div>`;
  v.querySelectorAll('[data-s]').forEach(b => b.onclick = () => act(() => api('/admin/defects/' + b.dataset.id, { method: 'POST', body: { status: b.dataset.s } }), 'Stare actualizată.').then(() => viewDefects(v)));
}

// ---------- NOTIFICĂRI ----------
function viewNotifs(v) {
  v.innerHTML = `<div class="toolbar"><button class="btn" id="markRead">Marchează toate ca citite</button></div><div class="card" style="padding:0">${S.notifs.length ? S.notifs.map(n =>
    `<div class="notif ${n.is_read ? '' : 'un'}"><div style="flex:1">${esc(n.message)}</div><span class="muted xs">${fmtT(n.created)}</span></div>`).join('') : '<div class="empty">Nicio notificare.</div>'}</div>`;
  $('#markRead').onclick = () => act(() => api('/notifications/read', { method: 'POST' }));
}

// ---------- ADMIN ----------
function barChart(items, { w = 560, h = 190, color = '#2563eb', fmt = v => v } = {}) {
  if (!items.length) return '<div class="muted">Fără date</div>';
  const max = Math.max(1, ...items.map(i => i.v)), bw = w / items.length;
  return `<svg class="chart" viewBox="0 0 ${w} ${h}" preserveAspectRatio="none">${[0.25, 0.5, 0.75, 1].map(f => `<line x1="0" x2="${w}" y1="${(h - 24) * (1 - f) + 4}" y2="${(h - 24) * (1 - f) + 4}" stroke="var(--border)" stroke-dasharray="3 4"/>`).join('')}
  ${items.map((it, i) => { const bh = (h - 28) * it.v / max; return `<rect x="${i * bw + bw * 0.15}" y="${h - 22 - bh}" width="${bw * 0.7}" height="${bh}" rx="4" fill="${color}" opacity=".9"><title>${esc(it.l)}: ${fmt(it.v)}</title></rect>
    <text x="${i * bw + bw / 2}" y="${h - 6}" text-anchor="middle" font-size="10" fill="var(--muted)">${esc(it.l)}</text>`; }).join('')}</svg>`;
}
async function viewAdmin(v) {
  const [st, cfg] = await Promise.all([api('/admin/stats'), api('/config')]); S.cfg = cfg; if (S.page !== 'admin') return;
  const t = st.totals, by = st.byStatus;
  const hours = Array.from({ length: 24 }, (_, h) => ({ l: h, v: (st.perHour.find(x => x.h === h) || {}).n || 0 }));
  v.innerHTML = `<div class="grid g4">${kpi('🔁', 'var(--primary-s)', 'var(--primary)', t.cycles, 'Cicluri totale')}${kpi('⚡', 'var(--fin-s)', 'var(--fin)', Math.round(t.kwh) + ' kWh', 'Energie')}
    ${kpi('💧', 'var(--run-s)', 'var(--run)', Math.round(t.water) + ' L', 'Apă')}${kpi('💰', 'var(--free-s)', 'var(--free)', t.revenue + ' lei', 'Încasări')}</div>
  <div class="grid g4" style="margin-top:16px">${kpi('📈', 'var(--primary-s)', 'var(--primary)', Math.round(st.utilization * 100) + '%', 'Grad de utilizare acum')}${kpi('👥', 'var(--res-s)', 'var(--res)', st.users, 'Studenți înregistrați')}
    ${kpi('🛑', 'var(--block-s)', 'var(--block)', (by.out_of_order || 0) + (by.blocked || 0), 'Defecte + blocate')}${kpi('🧺', 'var(--free-s)', 'var(--free)', st.machines, 'Mașini în rețea')}</div>
  <div class="grid g2" style="margin-top:16px">
    <div class="card"><h3>Cicluri pe zi (14 zile)</h3>${barChart(st.perDay.map(d => ({ l: d.d.slice(8), v: d.n })))}</div>
    <div class="card"><h3>Ore de vârf (cicluri pe oră)</h3>${barChart(hours, { color: '#7c3aed' })}</div>
    <div class="card"><h3>Consum energie pe cămin (kWh)</h3>${barChart(st.perDorm.map(d => ({ l: d.name.split(' - ')[0], v: +d.kwh.toFixed(1) })), { color: '#d97706' })}</div>
    <div class="card"><h3>Programe folosite</h3>${barChart(st.perProgram.map(p => ({ l: p.program === 'uscare' ? 'Uscare' : p.program === 'uscare_s' ? 'Usc.scurt' : (p.label || '').split(' ')[0], v: p.n })), { color: '#16a34a' })}</div>
  </div>
  <div class="grid g2" style="margin-top:16px">
    <div class="card"><h3>⚙️ Control sistem</h3>
      <label>Viteză simulare (×) — 1 = timp real<div style="display:flex;gap:8px"><input id="spd" type="number" min="1" max="600" value="${cfg.speed}" style="flex:1"><button class="btn primary" id="spdBtn">Aplică</button></div></label>
      <div class="muted xs" style="margin:6px 0 14px">Se aplică ciclurilor care pornesc de acum. Rezervarea ține 5 min, iar blocarea se declară după ${cfg.grace_s} s.</div>
      <label>Anunț către toți studenții<div style="display:flex;gap:8px"><input id="bc" placeholder="ex. Mâine apa se oprește 10:00–12:00" style="flex:1"><button class="btn" id="bcBtn">Trimite</button></div></label></div>
    <div class="card"><h3>➕ Adaugă mașină</h3><form id="addM" class="grid" style="gap:10px"><div class="row" style="grid-template-columns:1fr 1fr"><label>Cămin<select id="mDorm">${S.dorms.map(d => `<option value="${d.id}">${esc(d.name)}</option>`).join('')}</select></label>
      <label>Tip<select id="mType"><option value="washer">Mașină de spălat</option><option value="dryer">Uscător</option></select></label></div><label>Nume<input id="mName" required placeholder="Mașina 6"></label><button class="btn primary">Adaugă</button></form>
      <div class="muted xs" style="margin-top:10px">Dispozitive IoT reale: <code>POST /api/iot/telemetry</code> cu <code>{machine_id, api_key, power_w, vibration}</code>. Cheia se vede în fereastra fiecărei mașini.</div></div>
  </div>
  <div class="section-h"><h2>Parc de mașini</h2></div>
  <div class="card tbl"><table><tr><th>Mașina</th><th>Cămin</th><th>Stare</th><th>Cicluri</th><th>Putere</th><th>Ultimul semnal</th><th></th></tr>${S.machines.map(m =>
    `<tr><td><b>${esc(m.name)}</b> ${m.type === 'dryer' ? '♨️' : '🧺'}</td><td>${esc(dormName(m.dorm_id))}</td><td><span class="pill s-${m.status}">${STATUS[m.status]}</span></td><td>${m.cycles}</td><td>${Math.round(m.power_w)} W</td>
    <td>${m.last_seen ? Math.round((Date.now() - m.last_seen) / 1000) + ' s' : '–'}</td><td style="text-align:right"><button class="btn sm" data-o="${m.id}">Detalii</button> <button class="btn sm danger" data-del="${m.id}">Șterge</button></td></tr>`).join('')}</table></div>`;
  $('#spdBtn').onclick = () => act(async () => { await api('/admin/speed', { method: 'POST', body: { speed: +$('#spd').value } }); S.cfg = await api('/config'); }, 'Viteză actualizată.');
  $('#bcBtn').onclick = () => $('#bc').value && act(() => api('/admin/broadcast', { method: 'POST', body: { message: $('#bc').value } }), 'Anunț trimis.');
  $('#addM').onsubmit = e => { e.preventDefault(); act(() => api('/admin/machines', { method: 'POST', body: { dorm_id: +$('#mDorm').value, type: $('#mType').value, name: $('#mName').value } }), 'Mașină adăugată.'); };
  v.querySelectorAll('[data-o]').forEach(b => b.onclick = () => openMachine(+b.dataset.o));
  v.querySelectorAll('[data-del]').forEach(b => b.onclick = () => confirm('Ștergi mașina din rețea?') && act(() => api('/admin/machines/' + b.dataset.del, { method: 'DELETE' }), 'Mașină ștearsă.'));
}

// ---------- LIVE ----------
let lastTick = 0, pending = false;
function connect() {
  S.sse?.close();
  const es = S.sse = new EventSource('/api/stream?token=' + S.token);
  es.onopen = () => { $('.live').classList.remove('off'); $('#liveTxt').textContent = 'Live'; };
  es.onerror = () => { api('/me').catch(() => {}); $('.live').classList.add('off'); $('#liveTxt').textContent = 'Reconectare…'; };
  es.onmessage = async ev => {
    const e = JSON.parse(ev.data);
    if (e.type === 'notification' && !S.admin && e.message) toast(e.message);
    const dynamic = ['dashboard', 'machines'].includes(S.page) || S.modalId;
    const t = Date.now();
    if (pending || (e.type === 'tick' && (!dynamic || t - lastTick < 2000))) return;
    if (['history', 'defects', 'admin'].includes(S.page) && e.type === 'tick') return;
    lastTick = t; pending = true; try { await refresh(e.type !== 'tick' || t % 3 === 0); } finally { pending = false; }
  };
}

async function boot() {
  $('#login').classList.add('hidden'); $('#app').classList.add('hidden');
  S.programs = await api('/programs'); S.cfg = await api('/config');
  S.dorms = await api('/dorms');
  if (!S.token) {
    $('#fDorm').innerHTML = S.dorms.map(d => `<option value="${d.id}">${esc(d.name)}</option>`).join('');
    heroStats(); setInterval(async () => { if (!S.token) { S.dorms = await api('/dorms').catch(() => S.dorms); heroStats(); } }, 4000);
    return $('#login').classList.remove('hidden');
  }
  try { await refresh(); } catch { return logout(false); }
  if (S.admin && !['admin', 'dashboard', 'machines', 'defects'].includes(S.page)) S.page = 'admin';
  if (S.admin && S.page === 'dashboard' && !localStorage.getItem('slc_seen')) S.page = 'admin';
  $('#app').classList.remove('hidden'); renderChrome(); render(); connect();
  setInterval(() => { if (['dashboard', 'machines'].includes(S.page)) { /* actualizare local fără fetch: nimic de făcut, datele vin prin SSE */ } }, 5000);
}
boot();
