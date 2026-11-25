'use strict';
// «Ведьма-ученица» — прототип рогалика с ордами врагов.
// Всё рисуется кодом на canvas, без внешних ассетов.

const cv = document.getElementById('game');
const ctx = cv.getContext('2d');
let W = 0, H = 0, DPR = 1;

function resize() {
  DPR = window.devicePixelRatio || 1;
  W = window.innerWidth;
  H = window.innerHeight;
  cv.width = Math.floor(W * DPR);
  cv.height = Math.floor(H * DPR);
  cv.style.width = W + 'px';
  cv.style.height = H + 'px';
}
window.addEventListener('resize', resize);
resize();

// ---------- Ввод ----------
const keys = new Set();
const mouse = { x: 0, y: 0, down: false };
let clicks = [];
let pressed = [];
window.addEventListener('keydown', e => {
  if (!keys.has(e.code)) pressed.push(e.code);
  keys.add(e.code);
  if (['Space', 'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight'].includes(e.code)) e.preventDefault();
});
window.addEventListener('keyup', e => keys.delete(e.code));
window.addEventListener('blur', () => { keys.clear(); mouse.down = false; if (state === 'play') state = 'pause'; });
cv.addEventListener('mousemove', e => { mouse.x = e.clientX; mouse.y = e.clientY; });
cv.addEventListener('mousedown', e => { mouse.down = true; clicks.push({ x: e.clientX, y: e.clientY }); audioInit(); });
window.addEventListener('mouseup', () => { mouse.down = false; });
window.addEventListener('keydown', audioInit, { once: true });

// ---------- Звук (простые синтезированные эффекты) ----------
let actx = null;
function audioInit() {
  if (actx) return;
  try { actx = new (window.AudioContext || window.webkitAudioContext)(); } catch (_) { actx = null; }
}
const lastSfx = {};
function sfx(name, freq, dur, type = 'square', vol = 0.05, slide = 0) {
  if (!actx) return;
  const now = actx.currentTime;
  if (lastSfx[name] && now - lastSfx[name] < 0.04) return;
  lastSfx[name] = now;
  try {
    const o = actx.createOscillator();
    const g = actx.createGain();
    o.type = type;
    o.frequency.setValueAtTime(freq, now);
    if (slide) o.frequency.exponentialRampToValueAtTime(Math.max(30, freq + slide), now + dur);
    g.gain.setValueAtTime(vol, now);
    g.gain.exponentialRampToValueAtTime(0.0001, now + dur);
    o.connect(g).connect(actx.destination);
    o.start(now);
    o.stop(now + dur);
  } catch (_) { /* звук не обязателен */ }
}

// ---------- Утилиты ----------
const rand = (a, b) => a + Math.random() * (b - a);
const randi = (a, b) => Math.floor(rand(a, b + 1));
const clamp = (v, a, b) => Math.max(a, Math.min(b, v));
const dist2 = (ax, ay, bx, by) => (ax - bx) ** 2 + (ay - by) ** 2;
const pick = arr => arr[Math.floor(Math.random() * arr.length)];
function shuffle(arr) {
  for (let i = arr.length - 1; i > 0; i--) { const j = Math.floor(Math.random() * (i + 1)); [arr[i], arr[j]] = [arr[j], arr[i]]; }
  return arr;
}
function hash(x, y, s = 0) {
  let h = (x * 374761393 + y * 668265263 + s * 2147483647) | 0;
  h = Math.imul(h ^ (h >>> 13), 1274126177);
  return ((h ^ (h >>> 16)) >>> 0) / 4294967296;
}
function fmtTime(t) {
  const m = Math.floor(t / 60), s = Math.floor(t % 60);
  return m + ':' + String(s).padStart(2, '0');
}

// ---------- Данные ----------
const BASE_LOOK = { dress: '#5b4a8a', hat: '#2c2340', trim: '#c9b6ff', aura: null, hair: '#f2a1c4' };

const SCHOOLS = {
  fire:   { name: 'Огонь',   color: '#ff7043', dress: '#b93a2c', hat: '#6e1a14', trim: '#f5b041', spell: 'fireball',  desc: 'Огненный шар взрывается при попадании' },
  ice:    { name: 'Лёд',     color: '#85c1e9', dress: '#2e86c1', hat: '#173f5f', trim: '#e3f2fd', spell: 'frost',     desc: 'Ледяные осколки кружат вокруг и замедляют' },
  nature: { name: 'Природа', color: '#82e0aa', dress: '#2e8b57', hat: '#1d4d2f', trim: '#f9e79f', spell: 'thorns',    desc: 'Волна шипов отбрасывает врагов' },
  storm:  { name: 'Гроза',   color: '#d7bde2', dress: '#7d3c98', hat: '#3e1f4d', trim: '#f7dc6f', spell: 'lightning', desc: 'Молния бьёт цепью по нескольким врагам' },
};

const SPELLS = {
  spark:     { name: 'Искра',          max: 6, color: '#e8d5ff' },
  fireball:  { name: 'Огненный шар',   max: 5, color: '#ff7043' },
  frost:     { name: 'Ледяной вихрь',  max: 5, color: '#aed6f1' },
  thorns:    { name: 'Волна шипов',    max: 5, color: '#82e0aa' },
  lightning: { name: 'Цепная молния',  max: 5, color: '#f7dc6f' },
};

const FLOWERS = [
  { id: 'rose',   name: 'Роза',       color: '#e74c3c', desc: '+5% шанс крита',       apply: p => { p.crit += 0.05; } },
  { id: 'lily',   name: 'Лилия',      color: '#fdfefe', desc: '+0.5 ОЗ/сек',          apply: p => { p.regen += 0.5; } },
  { id: 'corn',   name: 'Василёк',    color: '#3498db', desc: '+6% скорость бега',    apply: p => { p.speed *= 1.06; } },
  { id: 'sun',    name: 'Подсолнух',  color: '#f4d03f', desc: '+25% радиус сбора',    apply: p => { p.magnet *= 1.25; } },
  { id: 'violet', name: 'Фиалка',     color: '#a569bd', desc: '+6% урон',             apply: p => { p.dmgMul *= 1.06; } },
  { id: 'clover', name: 'Клевер',     color: '#58d68d', desc: '+8 к макс. ОЗ',        apply: p => { p.maxHp += 8; p.hp += 8; } },
];

const ENEMY_TYPES = {
  imp:   { hp: 14,   speed: 88,  r: 13, dmg: 6,  xp: 1,  color: '#b03a2e' },
  bat:   { hp: 8,    speed: 140, r: 10, dmg: 4,  xp: 1,  color: '#6c3483' },
  slime: { hp: 42,   speed: 58,  r: 17, dmg: 12, xp: 3,  color: '#52be80' },
  elite: { hp: 240,  speed: 78,  r: 22, dmg: 16, xp: 12, color: '#922b21' },
  boss:  { hp: 2600, speed: 68,  r: 44, dmg: 24, xp: 0,  color: '#4a0f0f' },
};

const PAGES_TO_BOSS = 3;

// ---------- Состояние игры ----------
let state = 'title';
let G = null;
let choices = [];
let banner = null;
let bestTime = 0;
try { bestTime = Number(localStorage.getItem('witch.bestWin')) || 0; } catch (_) { bestTime = 0; }

function newGame() {
  G = {
    t: 0,
    kills: 0,
    player: {
      x: 0, y: 0, r: 14, face: 1, anim: 0,
      hp: 100, maxHp: 100, regen: 0, speed: 210,
      magnet: 110, dmgMul: 1, rateMul: 1, crit: 0.05, extraProj: 0,
      iframes: 0, dashCd: 0, dashT: 0, dashX: 0, dashY: 0,
      level: 1, xp: 0, xpNext: 6,
      spells: { spark: 1 }, timers: {},
      schools: [], look: { ...BASE_LOOK },
      bouquet: [],
    },
    enemies: [], bullets: [], foeBullets: [], gems: [], flowers: [], pages: [],
    particles: [], texts: [], bolts: [], rings: [],
    spawnAcc: 0, nextElite: 35, pagesFound: 0, bossSpawned: false, boss: null,
    frostAngle: 0, shake: 0, levelupQueue: 0, pageQueue: 0,
  };
  for (let i = 0; i < 6; i++) spawnFlower(true);
  showBanner('Найди страницы гримуара', 'Бесы утащили их в Проклятый лес');
}

function showBanner(title, sub, dur = 3) { banner = { title, sub, t: dur, dur }; }

// ---------- Спавн ----------
function spawnEnemy(type, x, y) {
  const T = ENEMY_TYPES[type];
  const mult = type === 'boss' ? 1 : 1 + G.t / 110;
  if (x === undefined) {
    const a = rand(0, Math.PI * 2);
    const d = Math.hypot(W, H) / 2 + 60;
    x = G.player.x + Math.cos(a) * d;
    y = G.player.y + Math.sin(a) * d;
  }
  const e = {
    type, x, y, r: T.r, hp: T.hp * mult, maxHp: T.hp * mult, speed: T.speed * rand(0.9, 1.1),
    dmg: T.dmg, xp: T.xp, color: T.color, hitFlash: 0, slow: 0, kbx: 0, kby: 0,
    phase: rand(0, 6.28), hits: new Map(), shootCd: 3,
  };
  G.enemies.push(e);
  return e;
}

function spawnFlower(initial) {
  const p = G.player;
  const a = rand(0, Math.PI * 2);
  const d = initial ? rand(200, 700) : rand(Math.max(W, H) * 0.6, Math.max(W, H) * 0.9);
  G.flowers.push({ x: p.x + Math.cos(a) * d, y: p.y + Math.sin(a) * d, kind: pick(FLOWERS), bob: rand(0, 6) });
}

function enemyTypeForTime(t) {
  const r = Math.random();
  if (t > 60 && r < 0.18) return 'slime';
  if (t > 25 && r < 0.45) return 'bat';
  return 'imp';
}

// ---------- Урон ----------
function damageEnemy(e, base, opts = {}) {
  const p = G.player;
  const isCrit = Math.random() < p.crit;
  const dmg = base * p.dmgMul * (isCrit ? 2 : 1);
  e.hp -= dmg;
  e.hitFlash = 0.1;
  if (opts.kb) {
    const dx = e.x - (opts.fromX ?? p.x), dy = e.y - (opts.fromY ?? p.y);
    const d = Math.hypot(dx, dy) || 1;
    const k = e.type === 'boss' ? 0.1 : e.type === 'elite' ? 0.4 : 1;
    e.kbx += dx / d * opts.kb * k;
    e.kby += dy / d * opts.kb * k;
  }
  if (opts.slow) e.slow = Math.max(e.slow, opts.slow);
  G.texts.push({ x: e.x + rand(-6, 6), y: e.y - e.r, v: Math.round(dmg), t: 0.7, crit: isCrit });
  sfx('hit', 220, 0.05, 'square', 0.02);
}

function killEnemy(e) {
  G.kills++;
  for (let i = 0; i < 8; i++) {
    G.particles.push({ x: e.x, y: e.y, vx: rand(-120, 120), vy: rand(-120, 120), t: rand(0.3, 0.6), c: e.color, s: rand(2, 4) });
  }
  if (e.xp) {
    const gems = e.type === 'elite' ? 5 : 1;
    for (let i = 0; i < gems; i++) {
      G.gems.push({ x: e.x + rand(-8, 8), y: e.y + rand(-8, 8), v: e.xp / gems * (e.type === 'elite' ? 1 : 1), pulled: false });
    }
  }
  if (e.type === 'elite') {
    G.pages.push({ x: e.x, y: e.y, bob: 0 });
    sfx('elite', 140, 0.4, 'sawtooth', 0.05, -80);
  }
  if (e.type === 'boss') {
    G.boss = null;
    G.shake = 0.8;
    for (let i = 0; i < 60; i++) {
      G.particles.push({ x: e.x, y: e.y, vx: rand(-300, 300), vy: rand(-300, 300), t: rand(0.6, 1.4), c: pick(['#f7dc6f', '#ff7043', '#d7bde2']), s: rand(3, 6) });
    }
    setTimeout(() => { if (state === 'play') winGame(); }, 1200);
  }
}

function hurtPlayer(dmg) {
  const p = G.player;
  if (p.iframes > 0 || p.dashT > 0) return;
  p.hp -= dmg;
  p.iframes = 0.8;
  G.shake = Math.max(G.shake, 0.25);
  sfx('hurt', 160, 0.2, 'sawtooth', 0.06, -100);
  if (p.hp <= 0) {
    p.hp = 0;
    state = 'over';
  }
}

function winGame() {
  state = 'win';
  if (!bestTime || G.t < bestTime) {
    bestTime = G.t;
    try { localStorage.setItem('witch.bestWin', String(bestTime)); } catch (_) { /* без сохранения */ }
  }
  sfx('win', 523, 0.6, 'triangle', 0.08, 500);
}

// ---------- Улучшения при повышении уровня ----------
function levelUpChoices() {
  const p = G.player;
  const pool = [];
  for (const id of Object.keys(p.spells)) {
    const lv = p.spells[id];
    if (lv < SPELLS[id].max) pool.push({ title: SPELLS[id].name + ' — ур. ' + (lv + 1), desc: spellUpgradeText(id, lv + 1), color: SPELLS[id].color, apply: () => { p.spells[id]++; } });
  }
  pool.push({ title: 'Острый ум', desc: '+12% урон всех заклинаний', color: '#f5b7b1', apply: () => { p.dmgMul *= 1.12; } });
  pool.push({ title: 'Быстрые пальцы', desc: '+10% скорость заклинаний', color: '#f9e79f', apply: () => { p.rateMul *= 1.10; } });
  pool.push({ title: 'Лёгкие туфельки', desc: '+10% скорость бега', color: '#abebc6', apply: () => { p.speed *= 1.10; } });
  pool.push({ title: 'Крепкий чай', desc: '+20 к макс. ОЗ и лечение', color: '#fadbd8', apply: () => { p.maxHp += 20; p.hp = Math.min(p.maxHp, p.hp + 40); } });
  pool.push({ title: 'Карманная луна', desc: '+35% радиус сбора опыта', color: '#d6eaf8', apply: () => { p.magnet *= 1.35; } });
  if (p.extraProj < 2) pool.push({ title: 'Двойное эхо', desc: '+1 снаряд у Искры и Огненного шара', color: '#e8daef', apply: () => { p.extraProj++; } });
  return shuffle(pool).slice(0, 3);
}

function spellUpgradeText(id, lv) {
  switch (id) {
    case 'spark': return lv % 2 === 0 ? '+1 снаряд и урон' : (lv >= 4 ? 'Пробивает врагов, урон выше' : 'Больше урона и скорости');
    case 'fireball': return 'Больше урона и радиус взрыва';
    case 'frost': return '+1 осколок, сильнее холод';
    case 'thorns': return 'Шире волна, больше урона';
    case 'lightning': return '+1 цель в цепи, больше урона';
    default: return '';
  }
}

function pageChoices() {
  const p = G.player;
  const free = Object.keys(SCHOOLS).filter(k => !p.schools.includes(k));
  return shuffle(free).slice(0, 3).map(k => {
    const s = SCHOOLS[k];
    return {
      title: 'Школа: ' + s.name, desc: s.desc, color: s.color, school: k,
      apply: () => {
        p.schools.push(k);
        p.spells[s.spell] = 1;
        // Каждая страница меняет часть образа ведьмы.
        const n = p.schools.length;
        if (n === 1) { p.look.dress = s.dress; p.look.trim = s.trim; }
        if (n === 2) { p.look.hat = s.hat; }
        if (n >= 3) { p.look.aura = s.color; }
        p.hp = Math.min(p.maxHp, p.hp + 25);
      },
    };
  });
}

function openNextModal() {
  if (G.pageQueue > 0) {
    G.pageQueue--;
    choices = pageChoices();
    if (choices.length) { state = 'page'; return true; }
  }
  if (G.levelupQueue > 0) {
    G.levelupQueue--;
    choices = levelUpChoices();
    state = 'levelup';
    return true;
  }
  return false;
}

function applyChoice(i) {
  const c = choices[i];
  if (!c) return;
  c.apply();
  sfx('pick', 660, 0.15, 'triangle', 0.06, 300);
  if (state === 'page' && G.pageQueue === 0 && G.pagesFound >= PAGES_TO_BOSS && !G.bossSpawned) {
    G.bossSpawned = true;
    const a = rand(0, 6.28);
    G.boss = spawnEnemy('boss', G.player.x + Math.cos(a) * 520, G.player.y + Math.sin(a) * 520);
    showBanner('Король бесов', 'Он не отдаст последнюю тайну гримуара без боя!', 3.5);
    sfx('boss', 90, 1.0, 'sawtooth', 0.08, -40);
  }
  state = 'play';
  openNextModal();
}

// ---------- Заклинания ----------
function nearestEnemy(x, y, maxD = Infinity) {
  let best = null, bd = maxD * maxD;
  for (const e of G.enemies) {
    const d = dist2(x, y, e.x, e.y);
    if (d < bd) { bd = d; best = e; }
  }
  return best;
}

function screenToWorld(sx, sy) {
  return { x: sx - W / 2 + G.player.x, y: sy - H / 2 + G.player.y };
}

function castSpells(dt) {
  const p = G.player;
  const T = p.timers;
  for (const id of Object.keys(p.spells)) T[id] = (T[id] || 0) - dt * p.rateMul;
  const lv = p.spells;

  if (lv.spark && T.spark <= 0) {
    T.spark = 0.5 / (1 + 0.1 * (lv.spark - 1));
    const m = screenToWorld(mouse.x, mouse.y);
    let ang = Math.atan2(m.y - p.y, m.x - p.x);
    if (!mouse.down) { // по умолчанию — автоприцел; зажатая ЛКМ — стрельба в курсор
      const e = nearestEnemy(p.x, p.y, 600);
      if (e) ang = Math.atan2(e.y - p.y, e.x - p.x);
    }
    const n = 1 + Math.floor(lv.spark / 2) + p.extraProj;
    for (let i = 0; i < n; i++) {
      const a = ang + (i - (n - 1) / 2) * 0.16;
      G.bullets.push({ kind: 'spark', x: p.x, y: p.y - 18, vx: Math.cos(a) * 540, vy: Math.sin(a) * 540, r: 6, t: 1.2,
        dmg: 10 + 3 * (lv.spark - 1), pierce: lv.spark >= 4 ? 2 : 0, hit: new Set(), color: SPELLS.spark.color });
    }
    sfx('spark', 880, 0.06, 'triangle', 0.025, 400);
  }

  if (lv.fireball && T.fireball <= 0) {
    T.fireball = 1.7;
    const e = nearestEnemy(p.x, p.y, 700);
    if (e) {
      const n = 1 + p.extraProj;
      const base = Math.atan2(e.y - p.y, e.x - p.x);
      for (let i = 0; i < n; i++) {
        const a = base + (i - (n - 1) / 2) * 0.35;
        G.bullets.push({ kind: 'fireball', x: p.x, y: p.y - 18, vx: Math.cos(a) * 360, vy: Math.sin(a) * 360, r: 10, t: 2,
          dmg: 22 + 9 * (lv.fireball - 1), radius: 70 + 12 * lv.fireball, pierce: 0, hit: new Set(), color: SPELLS.fireball.color });
      }
      sfx('fire', 200, 0.25, 'sawtooth', 0.04, -120);
    } else T.fireball = 0.2;
  }

  if (lv.thorns && T.thorns <= 0) {
    T.thorns = 2.4;
    const R = 110 + 18 * lv.thorns;
    G.rings.push({ x: p.x, y: p.y, r: 10, R, t: 0.35, color: SPELLS.thorns.color });
    for (const e of G.enemies) {
      if (dist2(p.x, p.y, e.x, e.y) < (R + e.r) ** 2) damageEnemy(e, 14 + 6 * (lv.thorns - 1), { kb: 420 });
    }
    sfx('thorns', 120, 0.2, 'triangle', 0.06, 80);
  }

  if (lv.lightning && T.lightning <= 0) {
    T.lightning = 1.4;
    const near = G.enemies.filter(e => dist2(p.x, p.y, e.x, e.y) < 480 * 480);
    if (near.length) {
      let cur = pick(near);
      const pts = [{ x: p.x, y: p.y - 30 }];
      const hit = new Set();
      for (let i = 0; i < 1 + lv.lightning && cur; i++) {
        hit.add(cur);
        pts.push({ x: cur.x, y: cur.y });
        damageEnemy(cur, 20 + 7 * (lv.lightning - 1));
        let nx = null, nd = 170 * 170;
        for (const e of G.enemies) {
          if (hit.has(e)) continue;
          const d = dist2(cur.x, cur.y, e.x, e.y);
          if (d < nd) { nd = d; nx = e; }
        }
        cur = nx;
      }
      G.bolts.push({ pts, t: 0.18 });
      sfx('zap', 1200, 0.12, 'sawtooth', 0.03, -900);
    } else T.lightning = 0.2;
  }

  if (lv.frost) {
    G.frostAngle += dt * (2.2 + 0.2 * lv.frost);
    const n = 1 + lv.frost;
    const R = 78 + 4 * lv.frost;
    for (let i = 0; i < n; i++) {
      const a = G.frostAngle + i * Math.PI * 2 / n;
      const sx = p.x + Math.cos(a) * R, sy = p.y - 10 + Math.sin(a) * R;
      for (const e of G.enemies) {
        if (dist2(sx, sy, e.x, e.y) < (e.r + 9) ** 2) {
          const last = e.hits.get('frost') || 0;
          if (G.t - last > 0.45) {
            e.hits.set('frost', G.t);
            damageEnemy(e, 7 + 3 * (lv.frost - 1), { slow: 1.2, kb: 120, fromX: p.x, fromY: p.y });
          }
        }
      }
    }
  }
}

function explode(x, y, radius, dmg) {
  G.rings.push({ x, y, r: 6, R: radius, t: 0.3, color: '#ff8a50' });
  for (let i = 0; i < 14; i++) {
    G.particles.push({ x, y, vx: rand(-200, 200), vy: rand(-200, 200), t: rand(0.2, 0.5), c: pick(['#ff7043', '#f5b041', '#fdebd0']), s: rand(2, 5) });
  }
  for (const e of G.enemies) {
    if (dist2(x, y, e.x, e.y) < (radius + e.r) ** 2) damageEnemy(e, dmg, { kb: 160, fromX: x, fromY: y });
  }
  G.shake = Math.max(G.shake, 0.08);
}

// ---------- Обновление ----------
function update(dt) {
  const p = G.player;
  G.t += dt;

  // Движение
  let mx = 0, my = 0;
  if (keys.has('KeyW') || keys.has('ArrowUp')) my -= 1;
  if (keys.has('KeyS') || keys.has('ArrowDown')) my += 1;
  if (keys.has('KeyA') || keys.has('ArrowLeft')) mx -= 1;
  if (keys.has('KeyD') || keys.has('ArrowRight')) mx += 1;
  const ml = Math.hypot(mx, my);
  if (ml) { mx /= ml; my /= ml; }

  p.dashCd -= dt;
  if (pressed.includes('Space') && p.dashCd <= 0) {
    const dx = ml ? mx : p.face, dy = ml ? my : 0;
    p.dashT = 0.16; p.dashX = dx; p.dashY = dy; p.dashCd = 1.4;
    sfx('dash', 400, 0.12, 'triangle', 0.04, 400);
  }
  if (p.dashT > 0) {
    p.dashT -= dt;
    p.x += p.dashX * 820 * dt;
    p.y += p.dashY * 820 * dt;
    G.particles.push({ x: p.x, y: p.y - 10, vx: 0, vy: 0, t: 0.25, c: p.look.trim, s: 5 });
  } else {
    p.x += mx * p.speed * dt;
    p.y += my * p.speed * dt;
  }
  const m = screenToWorld(mouse.x, mouse.y);
  if (Math.abs(m.x - p.x) > 4) p.face = m.x > p.x ? 1 : -1;
  p.anim += dt * (ml ? 10 : 3);
  p.iframes -= dt;
  if (p.regen) p.hp = Math.min(p.maxHp, p.hp + p.regen * dt);

  // Спавн врагов
  if (!G.boss || G.enemies.length < 60) {
    G.spawnAcc += dt * (0.8 + G.t * 0.025) * (G.boss ? 0.4 : 1);
    while (G.spawnAcc >= 1) {
      G.spawnAcc -= 1;
      if (G.enemies.length < 320) {
        const type = enemyTypeForTime(G.t);
        // Иногда враги идут стайкой
        if (type === 'bat' && Math.random() < 0.3) {
          const a = rand(0, 6.28), d = Math.hypot(W, H) / 2 + 60;
          for (let i = 0; i < 5; i++) spawnEnemy('bat', p.x + Math.cos(a) * d + rand(-40, 40), p.y + Math.sin(a) * d + rand(-40, 40));
        } else spawnEnemy(type);
      }
    }
  }
  if (G.t >= G.nextElite && !G.bossSpawned && G.pagesFound + G.pages.length + countElites() < PAGES_TO_BOSS) {
    G.nextElite = G.t + 45;
    spawnEnemy('elite');
    showBanner('Бес-хранитель', 'У него страница гримуара!', 2.2);
  }

  castSpells(dt);

  // Снаряды
  for (const b of G.bullets) {
    b.x += b.vx * dt; b.y += b.vy * dt; b.t -= dt;
    if (b.kind === 'fireball' && Math.random() < 0.6) {
      G.particles.push({ x: b.x, y: b.y, vx: rand(-30, 30), vy: rand(-30, 30), t: 0.3, c: pick(['#ff7043', '#f5b041']), s: rand(2, 4) });
    }
    for (const e of G.enemies) {
      if (b.t <= 0) break;
      if (b.hit.has(e)) continue;
      if (dist2(b.x, b.y, e.x, e.y) < (b.r + e.r) ** 2) {
        b.hit.add(e);
        if (b.kind === 'fireball') { explode(b.x, b.y, b.radius, b.dmg); b.t = 0; break; }
        damageEnemy(e, b.dmg, { kb: 60, fromX: b.x - b.vx, fromY: b.y - b.vy });
        if (b.pierce-- <= 0) b.t = 0;
      }
    }
  }
  G.bullets = G.bullets.filter(b => b.t > 0);

  // Враги
  for (const e of G.enemies) {
    e.hitFlash -= dt;
    e.slow -= dt;
    const dx = p.x - e.x, dy = p.y - e.y;
    const d = Math.hypot(dx, dy) || 1;
    let sp = e.speed * (e.slow > 0 ? 0.5 : 1);
    let vx = dx / d * sp, vy = dy / d * sp;
    if (e.type === 'bat') {
      e.phase += dt * 6;
      vx += Math.cos(e.phase) * 60; vy += Math.sin(e.phase) * 60;
    }
    if (e.type === 'boss') {
      e.shootCd -= dt;
      if (e.shootCd <= 0) {
        e.shootCd = e.hp < e.maxHp / 2 ? 2.0 : 3.0;
        const n = e.hp < e.maxHp / 2 ? 20 : 14;
        const off = rand(0, 1);
        for (let i = 0; i < n; i++) {
          const a = off + i * Math.PI * 2 / n;
          G.foeBullets.push({ x: e.x, y: e.y, vx: Math.cos(a) * 170, vy: Math.sin(a) * 170, r: 8, t: 5, dmg: 12 });
        }
        for (let i = 0; i < 4; i++) spawnEnemy('imp', e.x + rand(-60, 60), e.y + rand(-60, 60));
        sfx('bossShot', 110, 0.3, 'square', 0.05, -50);
      }
    }
    e.x += (vx + e.kbx) * dt;
    e.y += (vy + e.kby) * dt;
    e.kbx *= Math.pow(0.0005, dt);
    e.kby *= Math.pow(0.0005, dt);
    if (d < e.r + p.r) hurtPlayer(e.dmg);
    // Слишком далеко отставших переносим вперёд
    if (d > Math.hypot(W, H) * 1.3 && e.type !== 'boss' && e.type !== 'elite') {
      const a = rand(0, 6.28), r = Math.hypot(W, H) / 2 + 60;
      e.x = p.x + Math.cos(a) * r; e.y = p.y + Math.sin(a) * r;
    }
  }
  separateEnemies();
  const dead = G.enemies.filter(e => e.hp <= 0);
  G.enemies = G.enemies.filter(e => e.hp > 0);
  dead.forEach(killEnemy);

  // Вражеские снаряды
  for (const b of G.foeBullets) {
    b.x += b.vx * dt; b.y += b.vy * dt; b.t -= dt;
    if (dist2(b.x, b.y, p.x, p.y - 10) < (b.r + p.r) ** 2) { hurtPlayer(b.dmg); b.t = 0; }
  }
  G.foeBullets = G.foeBullets.filter(b => b.t > 0);

  // Опыт
  for (const g of G.gems) {
    const d2 = dist2(g.x, g.y, p.x, p.y);
    if (d2 < p.magnet * p.magnet) g.pulled = true;
    if (g.pulled) {
      const d = Math.sqrt(d2) || 1;
      g.x += (p.x - g.x) / d * 480 * dt;
      g.y += (p.y - g.y) / d * 480 * dt;
      if (d < 16) { g.got = true; gainXp(g.v); }
    }
  }
  G.gems = G.gems.filter(g => !g.got);
  if (G.gems.length > 400) G.gems.splice(0, G.gems.length - 400);

  // Цветы
  for (const f of G.flowers) {
    f.bob += dt * 3;
    if (dist2(f.x, f.y, p.x, p.y) < 28 * 28) {
      f.got = true;
      f.kind.apply(p);
      p.bouquet.push(f.kind.id);
      G.texts.push({ x: p.x, y: p.y - 50, v: f.kind.name + ': ' + f.kind.desc, t: 1.6, color: f.kind.color, big: true });
      sfx('flower', 990, 0.2, 'sine', 0.06, 300);
    } else if (dist2(f.x, f.y, p.x, p.y) > 1600 * 1600) f.got = true;
  }
  G.flowers = G.flowers.filter(f => !f.got);
  while (G.flowers.length < 5) spawnFlower(false);

  // Страницы гримуара
  for (const pg of G.pages) {
    pg.bob += dt * 3;
    if (dist2(pg.x, pg.y, p.x, p.y) < 34 * 34) {
      pg.got = true;
      G.pagesFound++;
      G.pageQueue++;
      showBanner('Страница гримуара ' + G.pagesFound + '/' + PAGES_TO_BOSS, 'Выбери школу магии', 2);
      sfx('page', 523, 0.4, 'triangle', 0.07, 520);
    }
  }
  G.pages = G.pages.filter(pg => !pg.got);

  // Эффекты
  for (const q of G.particles) { q.x += q.vx * dt; q.y += q.vy * dt; q.vx *= 0.92; q.vy *= 0.92; q.t -= dt; }
  G.particles = G.particles.filter(q => q.t > 0);
  if (G.particles.length > 900) G.particles.splice(0, G.particles.length - 900);
  for (const t of G.texts) { t.y -= 40 * dt; t.t -= dt; }
  G.texts = G.texts.filter(t => t.t > 0);
  if (G.texts.length > 120) G.texts.splice(0, G.texts.length - 120);
  for (const b of G.bolts) b.t -= dt;
  G.bolts = G.bolts.filter(b => b.t > 0);
  for (const r of G.rings) { r.t -= dt; r.r += (r.R - r.r) * Math.min(1, dt * 18); }
  G.rings = G.rings.filter(r => r.t > 0);
  G.shake = Math.max(0, G.shake - dt);

  if (state === 'play') openNextModal();
}

function countElites() { return G.enemies.filter(e => e.type === 'elite').length; }

function separateEnemies() {
  // Простая сетка, чтобы враги не слипались в одну точку
  const cell = 48;
  const grid = new Map();
  for (const e of G.enemies) {
    const k = Math.floor(e.x / cell) + ',' + Math.floor(e.y / cell);
    let a = grid.get(k); if (!a) grid.set(k, a = []); a.push(e);
  }
  for (const e of G.enemies) {
    const cx = Math.floor(e.x / cell), cy = Math.floor(e.y / cell);
    for (let ox = -1; ox <= 1; ox++) for (let oy = -1; oy <= 1; oy++) {
      const a = grid.get((cx + ox) + ',' + (cy + oy));
      if (!a) continue;
      for (const o of a) {
        if (o === e) continue;
        const dx = e.x - o.x, dy = e.y - o.y;
        const rr = e.r + o.r;
        const d2 = dx * dx + dy * dy;
        if (d2 > 0 && d2 < rr * rr) {
          const d = Math.sqrt(d2), push = (rr - d) * 0.25;
          const wE = e.type === 'boss' ? 0.05 : 1;
          e.x += dx / d * push * wE; e.y += dy / d * push * wE;
        }
      }
    }
  }
}

function gainXp(v) {
  const p = G.player;
  p.xp += v;
  sfx('xp', 1320, 0.04, 'sine', 0.015);
  while (p.xp >= p.xpNext) {
    p.xp -= p.xpNext;
    p.level++;
    p.xpNext = Math.round(6 + p.level * 4 + p.level * p.level * 0.35);
    G.levelupQueue++;
    sfx('lvl', 784, 0.3, 'triangle', 0.06, 400);
  }
}

// ---------- Отрисовка ----------
function drawGround() {
  const p = G ? G.player : { x: 0, y: 0 };
  ctx.fillStyle = '#16261b';
  ctx.fillRect(0, 0, W, H);
  const ox = p.x - W / 2, oy = p.y - H / 2;
  const C = 160;
  const x0 = Math.floor(ox / C) - 1, y0 = Math.floor(oy / C) - 1;
  const x1 = Math.floor((ox + W) / C) + 1, y1 = Math.floor((oy + H) / C) + 1;
  // пятна травы
  for (let cx = x0; cx <= x1; cx++) for (let cy = y0; cy <= y1; cy++) {
    const h = hash(cx, cy);
    const sx = cx * C - ox + hash(cx, cy, 1) * C, sy = cy * C - oy + hash(cx, cy, 2) * C;
    if (h < 0.5) {
      ctx.fillStyle = h < 0.25 ? '#1b3022' : '#132119';
      ctx.beginPath(); ctx.ellipse(sx, sy, 60 + h * 80, 30 + h * 40, 0, 0, Math.PI * 2); ctx.fill();
    }
    // травинки
    ctx.strokeStyle = '#2c4a33';
    ctx.lineWidth = 2;
    for (let i = 0; i < 4; i++) {
      const gx = cx * C - ox + hash(cx, cy, 10 + i) * C, gy = cy * C - oy + hash(cx, cy, 20 + i) * C;
      ctx.beginPath(); ctx.moveTo(gx, gy); ctx.lineTo(gx - 3, gy - 7); ctx.moveTo(gx, gy); ctx.lineTo(gx + 3, gy - 8); ctx.stroke();
    }
    // грибы
    if (hash(cx, cy, 5) < 0.18) {
      const gx = cx * C - ox + hash(cx, cy, 6) * C, gy = cy * C - oy + hash(cx, cy, 7) * C;
      ctx.fillStyle = '#e8e0d0'; ctx.fillRect(gx - 2, gy - 6, 4, 7);
      ctx.fillStyle = hash(cx, cy, 8) < 0.5 ? '#c0392b' : '#8e44ad';
      ctx.beginPath(); ctx.ellipse(gx, gy - 7, 7, 5, 0, Math.PI, 0); ctx.fill();
    }
  }
}

function drawTrees(front) {
  // Деревья по краям «клеток» — чисто декор, сквозь них можно пройти.
  const p = G.player;
  const ox = p.x - W / 2, oy = p.y - H / 2;
  const C = 300;
  const x0 = Math.floor(ox / C) - 1, y0 = Math.floor(oy / C) - 1;
  const x1 = Math.floor((ox + W) / C) + 1, y1 = Math.floor((oy + H) / C) + 2;
  for (let cy = y0; cy <= y1; cy++) for (let cx = x0; cx <= x1; cx++) {
    if (hash(cx, cy, 30) > 0.55) continue;
    const wx = cx * C + hash(cx, cy, 31) * C, wy = cy * C + hash(cx, cy, 32) * C;
    const isFront = wy > p.y;
    if (isFront !== front) continue;
    const sx = wx - ox, sy = wy - oy;
    const s = 0.8 + hash(cx, cy, 33) * 0.6;
    const nearPlayer = Math.abs(wx - p.x) < 60 * s && wy - p.y < 140 * s && wy > p.y;
    ctx.globalAlpha = front && nearPlayer ? 0.45 : 1;
    ctx.fillStyle = 'rgba(0,0,0,0.3)';
    ctx.beginPath(); ctx.ellipse(sx, sy, 34 * s, 12 * s, 0, 0, Math.PI * 2); ctx.fill();
    ctx.fillStyle = '#3b2a20';
    ctx.fillRect(sx - 7 * s, sy - 50 * s, 14 * s, 50 * s);
    const tones = ['#0f2a1a', '#153a22', '#1c4a2b'];
    for (let i = 0; i < 3; i++) {
      ctx.fillStyle = tones[i];
      ctx.beginPath();
      ctx.moveTo(sx, sy - (150 - i * 28) * s);
      ctx.lineTo(sx - (58 - i * 10) * s, sy - (40 + i * 18) * s);
      ctx.lineTo(sx + (58 - i * 10) * s, sy - (40 + i * 18) * s);
      ctx.closePath(); ctx.fill();
    }
    ctx.globalAlpha = 1;
  }
}

function drawWitch(x, y, look, face, anim, flash, scale = 1) {
  ctx.save();
  ctx.translate(x, y);
  ctx.scale(face * scale, scale);
  const bob = Math.sin(anim) * 1.5;

  ctx.fillStyle = 'rgba(0,0,0,0.35)';
  ctx.beginPath(); ctx.ellipse(0, 2, 16, 6, 0, 0, Math.PI * 2); ctx.fill();

  if (look.aura) {
    const g = ctx.createRadialGradient(0, -20, 4, 0, -20, 40);
    g.addColorStop(0, look.aura + '88'); g.addColorStop(1, look.aura + '00');
    ctx.fillStyle = g;
    ctx.beginPath(); ctx.arc(0, -20, 40, 0, Math.PI * 2); ctx.fill();
  }

  // ножки
  ctx.fillStyle = '#2b1d2e';
  const step = Math.sin(anim) * 3;
  ctx.fillRect(-6, -6 + Math.max(0, step), 4, 6);
  ctx.fillRect(2, -6 + Math.max(0, -step), 4, 6);

  // волосы сзади
  ctx.fillStyle = look.hair;
  ctx.beginPath(); ctx.ellipse(-4, -30 + bob, 11, 14, 0.2, 0, Math.PI * 2); ctx.fill();

  // платье
  ctx.fillStyle = flash ? '#ffffff' : look.dress;
  ctx.beginPath();
  ctx.moveTo(-6, -27 + bob);
  ctx.lineTo(6, -27 + bob);
  ctx.lineTo(14, -5);
  ctx.lineTo(-14, -5);
  ctx.closePath(); ctx.fill();
  ctx.fillStyle = look.trim;
  ctx.fillRect(-14, -8, 28, 3);
  ctx.fillRect(-2, -25 + bob, 4, 4);

  // голова
  ctx.fillStyle = flash ? '#ffffff' : '#ffe3cf';
  ctx.beginPath(); ctx.arc(1, -35 + bob, 9, 0, Math.PI * 2); ctx.fill();
  // чёлка
  ctx.fillStyle = look.hair;
  ctx.beginPath(); ctx.ellipse(1, -40 + bob, 10, 5, 0, Math.PI, 0); ctx.fill();
  ctx.beginPath(); ctx.ellipse(-7, -33 + bob, 3, 8, 0.2, 0, Math.PI * 2); ctx.fill();
  // глаза
  ctx.fillStyle = '#2b1d2e';
  ctx.fillRect(3, -36 + bob, 2, 3);
  ctx.fillRect(7, -36 + bob, 2, 3);
  ctx.fillStyle = '#f5a3b5';
  ctx.fillRect(7, -32 + bob, 3, 1.5);

  // шляпа
  ctx.fillStyle = look.hat;
  ctx.beginPath(); ctx.ellipse(1, -43 + bob, 17, 4.5, 0, 0, Math.PI * 2); ctx.fill();
  ctx.beginPath();
  ctx.moveTo(-9, -44 + bob);
  ctx.lineTo(10, -44 + bob);
  ctx.quadraticCurveTo(4, -60 + bob, -8, -68 + bob + Math.sin(anim * 0.5) * 2);
  ctx.quadraticCurveTo(-6, -56 + bob, -9, -44 + bob);
  ctx.fill();
  ctx.fillStyle = look.trim;
  ctx.fillRect(-9, -48 + bob, 19, 3);
  ctx.restore();
}

function drawEnemy(e) {
  const p = G.player;
  const sx = e.x - p.x + W / 2, sy = e.y - p.y + H / 2;
  if (sx < -80 || sy < -100 || sx > W + 80 || sy > H + 100) return;
  const flash = e.hitFlash > 0;
  const face = p.x > e.x ? 1 : -1;
  ctx.save();
  ctx.translate(sx, sy);
  ctx.fillStyle = 'rgba(0,0,0,0.3)';
  ctx.beginPath(); ctx.ellipse(0, e.r * 0.8, e.r, e.r * 0.35, 0, 0, Math.PI * 2); ctx.fill();
  const body = flash ? '#ffffff' : (e.slow > 0 ? '#9fc5e8' : e.color);

  if (e.type === 'bat') {
    const wing = Math.sin(G.t * 20 + e.phase) * 6;
    ctx.fillStyle = body;
    ctx.beginPath(); ctx.moveTo(0, -4); ctx.lineTo(-18, -10 + wing); ctx.lineTo(-8, 2); ctx.closePath(); ctx.fill();
    ctx.beginPath(); ctx.moveTo(0, -4); ctx.lineTo(18, -10 + wing); ctx.lineTo(8, 2); ctx.closePath(); ctx.fill();
    ctx.beginPath(); ctx.arc(0, -2, e.r * 0.7, 0, Math.PI * 2); ctx.fill();
    ctx.fillStyle = '#ffeb3b';
    ctx.fillRect(-4, -4, 2, 2); ctx.fillRect(2, -4, 2, 2);
  } else if (e.type === 'slime') {
    const sq = 1 + Math.sin(G.t * 6 + e.phase) * 0.1;
    ctx.fillStyle = body;
    ctx.globalAlpha = 0.9;
    ctx.beginPath(); ctx.ellipse(0, 0, e.r * sq, e.r / sq, 0, Math.PI, 0); ctx.lineTo(e.r * sq, e.r * 0.5); ctx.lineTo(-e.r * sq, e.r * 0.5); ctx.closePath(); ctx.fill();
    ctx.globalAlpha = 1;
    ctx.fillStyle = '#123';
    ctx.fillRect(-6 + face * 3, -6, 3, 4); ctx.fillRect(3 + face * 3, -6, 3, 4);
  } else {
    // бесы: обычный, хранитель страницы, король
    const r = e.r;
    if (e.type === 'elite' || e.type === 'boss') {
      const g = ctx.createRadialGradient(0, 0, r * 0.5, 0, 0, r * 1.8);
      g.addColorStop(0, e.type === 'boss' ? 'rgba(255,60,60,0.35)' : 'rgba(255,215,0,0.35)');
      g.addColorStop(1, 'rgba(0,0,0,0)');
      ctx.fillStyle = g; ctx.beginPath(); ctx.arc(0, 0, r * 1.8, 0, Math.PI * 2); ctx.fill();
    }
    const hop = Math.abs(Math.sin(G.t * 8 + e.phase)) * r * 0.15;
    ctx.translate(0, -hop);
    ctx.fillStyle = flash ? '#fff' : '#3d0c0c';
    ctx.beginPath(); ctx.moveTo(-r * 0.6, -r * 0.6); ctx.lineTo(-r * 0.9, -r * 1.3); ctx.lineTo(-r * 0.2, -r * 0.8); ctx.fill();
    ctx.beginPath(); ctx.moveTo(r * 0.6, -r * 0.6); ctx.lineTo(r * 0.9, -r * 1.3); ctx.lineTo(r * 0.2, -r * 0.8); ctx.fill();
    ctx.fillStyle = body;
    ctx.beginPath(); ctx.arc(0, 0, r, 0, Math.PI * 2); ctx.fill();
    ctx.fillStyle = '#ffd54f';
    ctx.beginPath(); ctx.arc(-r * 0.3 + face * r * 0.15, -r * 0.15, r * 0.16, 0, Math.PI * 2); ctx.fill();
    ctx.beginPath(); ctx.arc(r * 0.3 + face * r * 0.15, -r * 0.15, r * 0.16, 0, Math.PI * 2); ctx.fill();
    ctx.strokeStyle = '#1a0505'; ctx.lineWidth = Math.max(1.5, r * 0.08);
    ctx.beginPath(); ctx.arc(face * r * 0.15, r * 0.25, r * 0.3, 0.2, Math.PI - 0.2); ctx.stroke();
    if (e.type === 'elite') {
      // страница над головой
      ctx.fillStyle = '#f8f1dc';
      ctx.save(); ctx.translate(0, -r * 1.9 + Math.sin(G.t * 3) * 3); ctx.rotate(0.15);
      ctx.fillRect(-8, -10, 16, 20);
      ctx.fillStyle = '#8d6e63'; ctx.fillRect(-5, -6, 10, 1.5); ctx.fillRect(-5, -2, 10, 1.5); ctx.fillRect(-5, 2, 7, 1.5);
      ctx.restore();
    }
    if (e.type === 'boss') {
      ctx.fillStyle = '#f4d03f';
      ctx.beginPath();
      ctx.moveTo(-r * 0.5, -r * 0.85); ctx.lineTo(-r * 0.5, -r * 1.25); ctx.lineTo(-r * 0.25, -r * 1.05);
      ctx.lineTo(0, -r * 1.35); ctx.lineTo(r * 0.25, -r * 1.05); ctx.lineTo(r * 0.5, -r * 1.25); ctx.lineTo(r * 0.5, -r * 0.85);
      ctx.closePath(); ctx.fill();
    }
  }
  ctx.restore();
  if ((e.type === 'elite' || e.type === 'slime') && e.hp < e.maxHp) {
    ctx.fillStyle = '#000a'; ctx.fillRect(sx - 18, sy - e.r - 14, 36, 4);
    ctx.fillStyle = '#e74c3c'; ctx.fillRect(sx - 18, sy - e.r - 14, 36 * e.hp / e.maxHp, 4);
  }
}

function drawFlower(f) {
  const p = G.player;
  const sx = f.x - p.x + W / 2, sy = f.y - p.y + H / 2 + Math.sin(f.bob) * 2;
  if (sx < -40 || sy < -40 || sx > W + 40 || sy > H + 40) return;
  const g = ctx.createRadialGradient(sx, sy - 8, 2, sx, sy - 8, 26);
  g.addColorStop(0, f.kind.color + '66'); g.addColorStop(1, f.kind.color + '00');
  ctx.fillStyle = g; ctx.beginPath(); ctx.arc(sx, sy - 8, 26, 0, Math.PI * 2); ctx.fill();
  ctx.strokeStyle = '#2e7d32'; ctx.lineWidth = 2;
  ctx.beginPath(); ctx.moveTo(sx, sy); ctx.lineTo(sx, sy - 10); ctx.stroke();
  ctx.fillStyle = f.kind.color;
  for (let i = 0; i < 5; i++) {
    const a = i * Math.PI * 2 / 5 + f.bob * 0.2;
    ctx.beginPath(); ctx.arc(sx + Math.cos(a) * 4.5, sy - 12 + Math.sin(a) * 4.5, 3.5, 0, Math.PI * 2); ctx.fill();
  }
  ctx.fillStyle = '#fff59d'; ctx.beginPath(); ctx.arc(sx, sy - 12, 2.5, 0, Math.PI * 2); ctx.fill();
}

function drawWorld() {
  const p = G.player;
  let shx = 0, shy = 0;
  if (G.shake > 0) { shx = rand(-6, 6) * G.shake * 3; shy = rand(-6, 6) * G.shake * 3; }
  ctx.save();
  ctx.translate(shx, shy);
  drawGround();
  const toS = (x, y) => [x - p.x + W / 2, y - p.y + H / 2];

  for (const r of G.rings) {
    const [sx, sy] = toS(r.x, r.y);
    ctx.strokeStyle = r.color; ctx.globalAlpha = clamp(r.t * 3, 0, 1); ctx.lineWidth = 5;
    ctx.beginPath(); ctx.arc(sx, sy, r.r, 0, Math.PI * 2); ctx.stroke();
    ctx.globalAlpha = 1;
  }
  for (const g of G.gems) {
    const [sx, sy] = toS(g.x, g.y);
    if (sx < -10 || sy < -10 || sx > W + 10 || sy > H + 10) continue;
    ctx.fillStyle = g.v >= 3 ? '#f5b7b1' : '#a9cce3';
    ctx.beginPath(); ctx.moveTo(sx, sy - 6); ctx.lineTo(sx + 4, sy); ctx.lineTo(sx, sy + 6); ctx.lineTo(sx - 4, sy); ctx.closePath(); ctx.fill();
  }
  G.flowers.forEach(drawFlower);
  for (const pg of G.pages) {
    const [sx, sy] = toS(pg.x, pg.y);
    const g = ctx.createRadialGradient(sx, sy, 4, sx, sy, 40);
    g.addColorStop(0, 'rgba(255,230,150,0.6)'); g.addColorStop(1, 'rgba(255,230,150,0)');
    ctx.fillStyle = g; ctx.beginPath(); ctx.arc(sx, sy, 40, 0, Math.PI * 2); ctx.fill();
    ctx.save(); ctx.translate(sx, sy - 10 + Math.sin(pg.bob) * 4); ctx.rotate(Math.sin(pg.bob * 0.7) * 0.2);
    ctx.fillStyle = '#f8f1dc'; ctx.fillRect(-10, -13, 20, 26);
    ctx.fillStyle = '#8d6e63'; for (let i = 0; i < 4; i++) ctx.fillRect(-6, -8 + i * 5, i === 3 ? 8 : 12, 1.5);
    ctx.restore();
  }
  drawTrees(false);

  // враги и героиня по глубине
  const actors = G.enemies.slice();
  actors.push({ isPlayer: true, y: p.y });
  actors.sort((a, b) => a.y - b.y);
  for (const a of actors) {
    if (a.isPlayer) {
      const blink = p.iframes > 0 && Math.floor(G.t * 20) % 2 === 0;
      drawWitch(W / 2, H / 2, p.look, p.face, p.anim, blink);
    } else drawEnemy(a);
  }

  // ледяные осколки
  if (p.spells.frost) {
    const n = 1 + p.spells.frost, R = 78 + 4 * p.spells.frost;
    for (let i = 0; i < n; i++) {
      const a = G.frostAngle + i * Math.PI * 2 / n;
      const sx = W / 2 + Math.cos(a) * R, sy = H / 2 - 10 + Math.sin(a) * R;
      ctx.save(); ctx.translate(sx, sy); ctx.rotate(a + Math.PI / 2);
      ctx.fillStyle = '#d6eaf8';
      ctx.beginPath(); ctx.moveTo(0, -10); ctx.lineTo(5, 0); ctx.lineTo(0, 10); ctx.lineTo(-5, 0); ctx.closePath(); ctx.fill();
      ctx.restore();
    }
  }

  for (const b of G.bullets) {
    const [sx, sy] = toS(b.x, b.y);
    const g = ctx.createRadialGradient(sx, sy, 1, sx, sy, b.r * 2.2);
    g.addColorStop(0, '#ffffff'); g.addColorStop(0.4, b.color); g.addColorStop(1, b.color + '00');
    ctx.fillStyle = g; ctx.beginPath(); ctx.arc(sx, sy, b.r * 2.2, 0, Math.PI * 2); ctx.fill();
  }
  for (const b of G.foeBullets) {
    const [sx, sy] = toS(b.x, b.y);
    ctx.fillStyle = '#ff5252'; ctx.beginPath(); ctx.arc(sx, sy, b.r, 0, Math.PI * 2); ctx.fill();
    ctx.fillStyle = '#ffcdd2'; ctx.beginPath(); ctx.arc(sx, sy, b.r * 0.45, 0, Math.PI * 2); ctx.fill();
  }
  for (const bl of G.bolts) {
    ctx.strokeStyle = '#fff9c4'; ctx.lineWidth = 3; ctx.globalAlpha = clamp(bl.t * 8, 0, 1);
    ctx.beginPath();
    bl.pts.forEach((pt, i) => {
      const [sx, sy] = toS(pt.x, pt.y);
      if (i === 0) ctx.moveTo(sx, sy);
      else {
        const [px, py] = toS(bl.pts[i - 1].x, bl.pts[i - 1].y);
        for (let k = 1; k < 4; k++) ctx.lineTo(px + (sx - px) * k / 4 + rand(-10, 10), py + (sy - py) * k / 4 + rand(-10, 10));
        ctx.lineTo(sx, sy);
      }
    });
    ctx.stroke(); ctx.globalAlpha = 1;
  }
  for (const q of G.particles) {
    const [sx, sy] = toS(q.x, q.y);
    ctx.globalAlpha = clamp(q.t * 3, 0, 1);
    ctx.fillStyle = q.c; ctx.fillRect(sx - q.s / 2, sy - q.s / 2, q.s, q.s);
  }
  ctx.globalAlpha = 1;
  drawTrees(true);

  ctx.textAlign = 'center';
  for (const t of G.texts) {
    const [sx, sy] = toS(t.x, t.y);
    ctx.globalAlpha = clamp(t.t * 2, 0, 1);
    if (t.big) { ctx.font = 'bold 16px sans-serif'; ctx.fillStyle = t.color; }
    else { ctx.font = t.crit ? 'bold 18px sans-serif' : 'bold 13px sans-serif'; ctx.fillStyle = t.crit ? '#ffd54f' : '#ffffff'; }
    ctx.strokeStyle = '#000'; ctx.lineWidth = 3;
    ctx.strokeText(t.v, sx, sy); ctx.fillText(t.v, sx, sy);
  }
  ctx.globalAlpha = 1;
  ctx.restore();

  // туман по краям
  const v = ctx.createRadialGradient(W / 2, H / 2, Math.min(W, H) * 0.35, W / 2, H / 2, Math.max(W, H) * 0.75);
  v.addColorStop(0, 'rgba(5,10,8,0)'); v.addColorStop(1, 'rgba(5,10,8,0.75)');
  ctx.fillStyle = v; ctx.fillRect(0, 0, W, H);

  // стрелки к страницам и хранителям за экраном
  const targets = G.pages.map(pg => ({ x: pg.x, y: pg.y, c: '#f8e7a1' }))
    .concat(G.enemies.filter(e => e.type === 'elite' || e.type === 'boss').map(e => ({ x: e.x, y: e.y, c: e.type === 'boss' ? '#ff5252' : '#f4d03f' })));
  for (const tg of targets) {
    const [sx, sy] = toS(tg.x, tg.y);
    if (sx > 0 && sy > 0 && sx < W && sy < H) continue;
    const a = Math.atan2(sy - H / 2, sx - W / 2);
    const ax = clamp(W / 2 + Math.cos(a) * W, 30, W - 30), ay = clamp(H / 2 + Math.sin(a) * H, 30, H - 30);
    ctx.save(); ctx.translate(ax, ay); ctx.rotate(a);
    ctx.fillStyle = tg.c; ctx.beginPath(); ctx.moveTo(14, 0); ctx.lineTo(-8, -9); ctx.lineTo(-8, 9); ctx.closePath(); ctx.fill();
    ctx.restore();
  }
}

function bar(x, y, w, h, frac, fg, bg = '#00000099') {
  ctx.fillStyle = bg; ctx.fillRect(x, y, w, h);
  ctx.fillStyle = fg; ctx.fillRect(x, y, w * clamp(frac, 0, 1), h);
  ctx.strokeStyle = '#ffffff33'; ctx.lineWidth = 1; ctx.strokeRect(x + 0.5, y + 0.5, w - 1, h - 1);
}

function drawHud() {
  const p = G.player;
  bar(0, 0, W, 8, p.xp / p.xpNext, '#8e7cc3');
  ctx.textAlign = 'left';
  ctx.font = 'bold 15px sans-serif';
  ctx.fillStyle = '#fff';
  bar(16, 20, 220, 16, p.hp / p.maxHp, '#e74c3c');
  ctx.fillText('ОЗ ' + Math.ceil(p.hp) + ' / ' + p.maxHp, 22, 33);
  ctx.fillText('Уровень ' + p.level, 16, 58);

  ctx.textAlign = 'center';
  ctx.font = 'bold 22px sans-serif';
  ctx.fillText(fmtTime(G.t), W / 2, 40);

  ctx.textAlign = 'right';
  ctx.font = 'bold 15px sans-serif';
  ctx.fillStyle = '#f8e7a1';
  ctx.fillText('Страницы гримуара: ' + G.pagesFound + ' / ' + PAGES_TO_BOSS, W - 16, 33);
  ctx.fillStyle = '#ddd';
  ctx.fillText('Побеждено: ' + G.kills, W - 16, 55);

  // заклинания
  ctx.textAlign = 'left';
  ctx.font = '13px sans-serif';
  let y = 84;
  for (const id of Object.keys(p.spells)) {
    ctx.fillStyle = SPELLS[id].color;
    ctx.beginPath(); ctx.arc(22, y - 4, 5, 0, Math.PI * 2); ctx.fill();
    ctx.fillStyle = '#eee';
    ctx.fillText(SPELLS[id].name + ' ' + p.spells[id], 34, y);
    y += 20;
  }
  // рывок
  ctx.fillStyle = p.dashCd > 0 ? '#777' : '#cfe';
  ctx.fillText(p.dashCd > 0 ? 'Рывок: ' + p.dashCd.toFixed(1) : 'Рывок готов [Пробел]', 16, y + 4);

  // букет
  if (p.bouquet.length) {
    ctx.textAlign = 'left';
    ctx.fillStyle = '#ddd';
    ctx.fillText('Букет:', 16, H - 22);
    p.bouquet.slice(-24).forEach((id, i) => {
      const f = FLOWERS.find(fl => fl.id === id);
      ctx.fillStyle = f.color;
      ctx.beginPath(); ctx.arc(72 + i * 14, H - 26, 5, 0, Math.PI * 2); ctx.fill();
    });
  }

  // полоска босса
  if (G.boss) {
    const w = Math.min(500, W - 80);
    bar(W / 2 - w / 2, H - 46, w, 14, G.boss.hp / G.boss.maxHp, '#c0392b');
    ctx.textAlign = 'center'; ctx.fillStyle = '#fff'; ctx.font = 'bold 14px sans-serif';
    ctx.fillText('Король бесов', W / 2, H - 54);
  }

  if (banner) {
    const a = clamp(Math.min(banner.t, banner.dur - banner.t) * 3, 0, 1);
    ctx.globalAlpha = a;
    ctx.textAlign = 'center';
    ctx.fillStyle = '#f8e7a1'; ctx.font = 'bold 30px serif';
    ctx.strokeStyle = '#000'; ctx.lineWidth = 4;
    ctx.strokeText(banner.title, W / 2, H * 0.22); ctx.fillText(banner.title, W / 2, H * 0.22);
    ctx.fillStyle = '#fff'; ctx.font = '17px sans-serif';
    ctx.strokeText(banner.sub, W / 2, H * 0.22 + 28); ctx.fillText(banner.sub, W / 2, H * 0.22 + 28);
    ctx.globalAlpha = 1;
  }
}

function wrap(text, x, y, maxW, lh) {
  const words = text.split(' ');
  let line = '';
  for (const w of words) {
    const test = line ? line + ' ' + w : w;
    if (ctx.measureText(test).width > maxW && line) { ctx.fillText(line, x, y); line = w; y += lh; }
    else line = test;
  }
  if (line) ctx.fillText(line, x, y);
  return y + lh;
}

let buttons = [];
function button(x, y, w, h, title, desc, color, onClick, hotkey) {
  const hover = mouse.x > x && mouse.x < x + w && mouse.y > y && mouse.y < y + h;
  ctx.fillStyle = hover ? '#3a2f55' : '#241d36';
  ctx.fillRect(x, y, w, h);
  ctx.strokeStyle = color; ctx.lineWidth = hover ? 3 : 2;
  ctx.strokeRect(x + 1, y + 1, w - 2, h - 2);
  ctx.textAlign = 'center';
  ctx.fillStyle = color; ctx.font = 'bold 18px sans-serif';
  ctx.fillText(title, x + w / 2, y + 32);
  if (desc) {
    ctx.fillStyle = '#ddd'; ctx.font = '14px sans-serif';
    wrap(desc, x + w / 2, y + 60, w - 24, 18);
  }
  if (hotkey) { ctx.fillStyle = '#888'; ctx.font = '12px sans-serif'; ctx.fillText('[' + hotkey + ']', x + w / 2, y + h - 12); }
  buttons.push({ x, y, w, h, onClick });
}

function drawChoiceModal(title, subtitle) {
  ctx.fillStyle = 'rgba(8,6,16,0.72)';
  ctx.fillRect(0, 0, W, H);
  ctx.textAlign = 'center';
  ctx.fillStyle = '#f8e7a1'; ctx.font = 'bold 32px serif';
  ctx.fillText(title, W / 2, H / 2 - 150);
  ctx.fillStyle = '#ccc'; ctx.font = '16px sans-serif';
  ctx.fillText(subtitle, W / 2, H / 2 - 120);
  const n = choices.length;
  const bw = Math.min(240, (W - 80) / n - 16), bh = 150;
  const total = n * bw + (n - 1) * 20;
  choices.forEach((c, i) => {
    button(W / 2 - total / 2 + i * (bw + 20), H / 2 - 80, bw, bh, c.title, c.desc, c.color, () => applyChoice(i), String(i + 1));
  });
  if (state === 'page') {
    // предпросмотр образа
    drawWitch(W / 2, H / 2 + 230, G.player.look, 1, G.t * 3, false, 2);
  }
}

function drawTitle(t) {
  ctx.fillStyle = '#0d1611'; ctx.fillRect(0, 0, W, H);
  // звёзды и луна
  for (let i = 0; i < 80; i++) {
    ctx.fillStyle = 'rgba(255,255,255,' + (0.3 + 0.5 * Math.abs(Math.sin(t + i))) + ')';
    ctx.fillRect(hash(i, 1) * W, hash(i, 2) * H * 0.6, 2, 2);
  }
  ctx.fillStyle = '#fdf2c4'; ctx.beginPath(); ctx.arc(W * 0.78, H * 0.2, 50, 0, Math.PI * 2); ctx.fill();
  ctx.fillStyle = '#0d1611'; ctx.beginPath(); ctx.arc(W * 0.78 + 20, H * 0.2 - 10, 45, 0, Math.PI * 2); ctx.fill();
  // силуэт леса
  ctx.fillStyle = '#08100b';
  for (let i = 0; i < 30; i++) {
    const x = i / 29 * W, h = 120 + hash(i, 9) * 140;
    ctx.beginPath(); ctx.moveTo(x - 50, H); ctx.lineTo(x, H - h); ctx.lineTo(x + 50, H); ctx.fill();
  }
  drawWitch(W / 2, H * 0.62, BASE_LOOK, 1, t * 3, false, 3);

  ctx.textAlign = 'center';
  ctx.fillStyle = '#f8e7a1'; ctx.font = 'bold 54px serif';
  ctx.fillText('Ведьма-ученица', W / 2, H * 0.16);
  ctx.fillStyle = '#ddd'; ctx.font = '17px sans-serif';
  wrap('Юная ведьма проспала экзамен, а бесы растащили гримуар наставницы по Проклятому лесу. ' +
    'Найди ' + PAGES_TO_BOSS + ' страницы, выбери свои школы магии и одолей Короля бесов!', W / 2, H * 0.16 + 40, Math.min(640, W - 40), 24);

  ctx.fillStyle = '#aaa'; ctx.font = '14px sans-serif';
  ctx.fillText('WASD — бег · Искра целится сама, зажми ЛКМ — стрельба в курсор · Пробел — рывок · Esc — пауза', W / 2, H - 70);
  if (bestTime) ctx.fillText('Лучшее время победы: ' + fmtTime(bestTime), W / 2, H - 48);
  ctx.fillStyle = 'rgba(248,231,161,' + (0.6 + 0.4 * Math.sin(t * 3)) + ')';
  ctx.font = 'bold 22px sans-serif';
  ctx.fillText('Нажми Enter или кликни, чтобы начать', W / 2, H * 0.84);
}

function drawEnd(win) {
  ctx.fillStyle = 'rgba(8,6,16,0.78)'; ctx.fillRect(0, 0, W, H);
  ctx.textAlign = 'center';
  ctx.fillStyle = win ? '#f8e7a1' : '#e57373'; ctx.font = 'bold 44px serif';
  const p = G.player;
  let title, text;
  if (win) {
    const fast = G.t < 8 * 60;
    title = fast ? 'Экзамен сдан с отличием!' : 'Экзамен сдан!';
    text = fast ? 'Наставница впечатлена: гримуар собран, а ты ещё и успела к чаю.' : 'Гримуар снова цел. Наставница ворчит, но ставит «зачёт».';
  } else {
    title = 'Ведьма выбилась из сил…';
    text = 'Совы вынесли тебя из леса. Экзамен придётся пересдавать.';
  }
  ctx.fillText(title, W / 2, H / 2 - 150);
  ctx.fillStyle = '#ddd'; ctx.font = '18px sans-serif';
  ctx.fillText(text, W / 2, H / 2 - 112);
  ctx.fillText('Время: ' + fmtTime(G.t) + '   ·   Уровень: ' + p.level + '   ·   Побеждено бесов: ' + G.kills + '   ·   Страницы: ' + G.pagesFound, W / 2, H / 2 - 80);
  drawWitch(W / 2, H / 2 + 80, p.look, 1, G.t * 3, false, 2);
  button(W / 2 - 120, H / 2 + 115, 240, 70, 'Новый забег', '', '#f8e7a1', () => { newGame(); state = 'play'; }, 'Enter');
}

function drawPause() {
  ctx.fillStyle = 'rgba(8,6,16,0.7)'; ctx.fillRect(0, 0, W, H);
  ctx.textAlign = 'center';
  ctx.fillStyle = '#f8e7a1'; ctx.font = 'bold 40px serif';
  ctx.fillText('Пауза', W / 2, H / 2 - 80);
  button(W / 2 - 120, H / 2 - 40, 240, 60, 'Продолжить', '', '#f8e7a1', () => { state = 'play'; }, 'Esc');
  button(W / 2 - 120, H / 2 + 40, 240, 60, 'В главное меню', '', '#bbb', () => { state = 'title'; G = null; });
}

// ---------- Главный цикл ----------
let last = performance.now();
let titleT = 0;
function frame(now) {
  const dt = Math.min(0.05, (now - last) / 1000);
  last = now;
  ctx.setTransform(DPR, 0, 0, DPR, 0, 0);

  // клики по кнопкам прошлого кадра
  const prevButtons = buttons;
  buttons = [];
  for (const c of clicks) {
    const b = prevButtons.find(b => c.x > b.x && c.x < b.x + b.w && c.y > b.y && c.y < b.y + b.h);
    if (b) b.onClick();
    else if (state === 'title') { newGame(); state = 'play'; }
  }
  clicks = [];

  if (state === 'title') {
    if (pressed.includes('Enter') || pressed.includes('Space')) { newGame(); state = 'play'; }
  } else if (state === 'play') {
    if (pressed.includes('Escape') || pressed.includes('KeyP')) state = 'pause';
    else update(dt);
    if (banner) { banner.t -= dt; if (banner.t <= 0) banner = null; }
  } else if (state === 'pause') {
    if (pressed.includes('Escape') || pressed.includes('KeyP')) state = 'play';
  } else if (state === 'levelup' || state === 'page') {
    for (let i = 0; i < choices.length; i++) if (pressed.includes('Digit' + (i + 1))) { applyChoice(i); break; }
  } else if (state === 'over' || state === 'win') {
    if (pressed.includes('Enter')) { newGame(); state = 'play'; }
  }
  pressed = [];

  if (state === 'title' || !G) {
    titleT += dt;
    drawTitle(titleT);
  } else {
    drawWorld();
    drawHud();
    if (state === 'levelup') drawChoiceModal('Новый уровень!', 'Выбери улучшение (клик или 1–' + choices.length + ')');
    else if (state === 'page') drawChoiceModal('Страница гримуара', 'Новая школа магии меняет и заклинания, и твой образ');
    else if (state === 'pause') drawPause();
    else if (state === 'over') drawEnd(false);
    else if (state === 'win') drawEnd(true);
  }
  requestAnimationFrame(frame);
}
requestAnimationFrame(frame);

// Для отладки и автотестов
window.__witch = {
  get state() { return state; },
  get game() { return G; },
  start() { newGame(); state = 'play'; },
  choose(i) { applyChoice(i); },
};
