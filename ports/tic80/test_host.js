// Copyright 2026 Luginfo10
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

// Test host of the tic80 port: loads the module as TIC-80 does, with an API
// that records its calls, and checks the boot, the commands, the stack effects
// of the API words and that the module cannot reach outside its memory.
//   node test_host.js out/tic80/lugforth-tic80.wasm [cart.fth]

'use strict';
const fs = require('fs');

const apiNames = [];
for (const m of fs.readFileSync(__dirname + '/api.h', 'utf8')
    .matchAll(/API\((\d+), "([^"]+)", \w+, (\d+), (\d+)\)/g)) {
  apiNames[+m[1]] = { name: m[2], nargs: +m[3], nres: +m[4] };
}

let mem, out = '', calls = [];
function bytes() { return new Uint8Array(mem.buffer); }
function i32() { return new Int32Array(mem.buffer); }
function check(a, n) {
  if (a < 0 || n < 0 || a + n > mem.buffer.byteLength) throw new Error('out of bounds');
}
function str(a, n) { check(a, n); return Buffer.from(bytes().slice(a, a + n)).toString('latin1'); }

const imports = {
  tic80: {
    api(id, base, n) {
      const w = apiNames[id];
      if (!w || w.nargs !== n || (base & 3)) throw new Error('bad api call ' + id);
      check(base, 4 * Math.max(n, w.nres));
      const args = Array.from(i32().slice(base >> 2, (base >> 2) + n));
      if (w.name === 'PRINT' || w.name === 'TRACE' || w.name === 'FONT') {
        args[0] = str(args[0], args[1]);
        args.splice(1, 1);
      }
      calls.push(w.name + ' ' + args.join(' '));
      const res = w.name === 'MOUSE' ? [1, 2, 3, 4, 5, 6, 7]
                : w.name === 'BTN' ? [args[0] === 2 ? -1 : 0]
                : new Array(w.nres).fill(42);
      i32().set(res, base >> 2);
      return res.length;
    },
    type(a, n) { out += str(a, n); },
    ticks() { return 1234; },
    sin: Math.sin, cos: Math.cos, atan2: Math.atan2, pow: Math.pow, exp: Math.exp, log: Math.log,
  },
};

let failures = 0;
function expect(what, got, want) {
  const ok = JSON.stringify(got) === JSON.stringify(want);
  if (!ok) failures++;
  console.log((ok ? 'ok   ' : 'FAIL ') + what + (ok ? '' : ': got ' + JSON.stringify(got) + ', want ' + JSON.stringify(want)));
}

(async () => {
  const wasm = fs.readFileSync(process.argv[2]);
  const { instance } = await WebAssembly.instantiate(wasm, imports);
  const lf = Object.assign({}, instance.exports);
  mem = lf.memory;

  function evaluate(text) {
    const b = Buffer.from(text, 'latin1');
    const a = lf.lf_alloc(b.length);
    bytes().set(b, a);
    const r = lf.lf_eval(a, b.length);
    lf.lf_free(a);
    return r;
  }
  function find(name) {
    const b = Buffer.from(name, 'latin1');
    const a = lf.lf_alloc(b.length);
    bytes().set(b, a);
    const xt = lf.lf_find(a, b.length);
    lf.lf_free(a);
    return xt;
  }
  function run(text) { out = ''; calls = []; const r = evaluate(text); return { r, out, calls, depth: lf.lf_depth() }; }

  let t = Date.now();
  expect('boot', lf.lf_init(), 0);
  console.log('     boot in ' + (Date.now() - t) + ' ms, memory ' + (mem.buffer.byteLength >> 10) + ' KiB');

  expect('evaluate and type', run('2 3 + . cr').out, '5 \r\n');
  expect('stack emptied between commands', run('1 2 3').depth, 3);
  expect('... and the next one starts empty', run('depth .').out, '0 ');
  expect('definitions stay', run(': sq dup * ; 7 sq .').out, '49 ');
  const nf = run('nosuchword');
  expect('not found throws', [nf.r !== 0, nf.out.includes('nosuchword NOT FOUND')], [true, true]);
  expect('state reset after an error', run(': broken nosuchword ;').r !== 0, true);
  expect('... interpreting again', run('1 .').out, '1 ');
  expect('division by zero throws -10', run('1 0 /').r, -10);
  expect('catch', run(': t 5 throw ; \' t catch .').out, '5 ');
  expect('double numbers', run('1 0 2 0 d+ d.').out, '3 ');
  expect('floats', run('2e fsqrt f.').out.slice(0, 5), '1.414');

  expect('CLS', run('3 cls').calls, ['CLS 3']);
  expect('PRINT', run('s" hi" 10 20 15 0 1 0 print .').calls, ['PRINT hi 10 20 15 0 1 0']);
  expect('PRINT leaves its width', run('s" hi" 10 20 15 0 1 0 print .').out, '42 ');
  expect('SPR', run('1 2 3 -1 1 0 0 1 1 spr depth .').out, '0 ');
  expect('MOUSE pushes 7', run('mouse depth . . . . . . . .').out, '7 7 6 5 4 3 2 1 ');
  expect('BTN', run('2 btn . 0 btn .').out, '-1 0 ');
  expect('TTRI takes 18', run('0 0 0 0 0 0 0 0 0 0 0 0 0 -1 0 0 0 0 ttri depth .').out, '0 ');
  expect('EXITGAME, not EXIT', run(': f 1 if exit then exitgame ; f').calls, []);
  expect('KEYPRESSED, KEY stays', run('5 keypressed . key .').out, '42 -1 ');

  // TIC, BOOT, SCN as TIC-80 calls them.
  run(': tic 1 cls ; : scn ( row -- ) cls ;');
  calls = [];
  expect('lf_call TIC', lf.lf_call(find('TIC')), 0);
  expect('lf_call1 SCN', lf.lf_call1(find('scn'), 9), 0);
  expect('... calls', calls, ['CLS 1', 'CLS 9']);
  expect('lf_find of a missing word', find('BDR'), 0);
  run(': bad 1 2 ;');
  lf.lf_call(find('bad'));
  expect('stack left by a word shows in lf_depth', lf.lf_depth(), 2);

  // The sandbox: every address is in the module's memory, and an access past
  // it traps. A trap leaves the module half way through a word: the host
  // drops the instance and makes a new one (TIC-80 stops the cartridge).
  async function traps(text) {
    const fresh = (await WebAssembly.instantiate(wasm, imports)).instance.exports;
    Object.assign(lf, fresh); mem = fresh.memory;
    lf.lf_init();
    let trapped = false;
    try { run(text); } catch (e) { trapped = e instanceof WebAssembly.RuntimeError; }
    return trapped;
  }
  const size = mem.buffer.byteLength;
  expect('@ past the memory traps', await traps(size + ' @ .'), true);
  expect('! at a negative address traps', await traps('1 -4 !'), true);
  expect('a forged xt traps or does nothing', await traps('-8 execute'), true);
  expect('cmove from far away traps', await traps('here ' + (size - 4) + ' 100 cmove'), true);
  expect('sp! anywhere stays inside', await traps('-100 sp! 1 2 3'), true);

  if (process.argv[3]) {
    const fresh = (await WebAssembly.instantiate(wasm, imports)).instance.exports;
    mem = fresh.memory;
    Object.assign(lf, fresh);
    lf.lf_init();
    const src = fs.readFileSync(process.argv[3], 'latin1');
    t = Date.now();
    const res = run(src);
    console.log('     cart evaluated in ' + (Date.now() - t) + ' ms' + (res.out ? ': ' + res.out.trim() : ''));
    expect('cart ' + process.argv[3], res.r, 0);
    for (const w of ['BOOT', 'TIC']) {
      const xt = find(w);
      if (!xt) continue;
      calls = []; out = '';
      t = Date.now();
      const r = lf.lf_call(xt);
      const errors = calls.filter(c => c.startsWith('TRACE !error')).length;
      if (errors) console.log('     ' + w + ': ' + errors + ' probes threw');
      console.log('     ' + w + ': ' + calls.length + ' API calls in ' + (Date.now() - t) + ' ms' + (out ? ', output ' + JSON.stringify(out) : ''));
      expect('cart ' + w, [r, lf.lf_depth()], [0, 0]);
    }
  }

  console.log(failures ? failures + ' FAILED' : 'all passed');
  process.exit(failures ? 1 : 0);
})();
