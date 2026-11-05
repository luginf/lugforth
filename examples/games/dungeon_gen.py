#!/usr/bin/env python3
"""Generates the levels of gfx_dungeon.fs (dungeon_levels.fs): python3 dungeon_gen.py [n_levels] > dungeon_levels.fs

Each level is 20x20: a maze (recursive backtracker) with a few rooms, a
locked door on the way to the stairs with its key on the near side, plain
doors, torches, monsters and items. The checker below refuses a level
where the stairs or an item cannot be reached (keys count). Fixed seeds:
the same levels every time. Legend: see the top of gfx_dungeon.fs."""
import random, sys
from collections import deque
W = H = 20
DIRS = [(0, -1), (1, 0), (0, 1), (-1, 0)]

def maze(rng):
    g = [['#'] * W for _ in range(H)]
    st = [(1, 1)]; g[1][1] = '.'
    while st:
        x, y = st[-1]
        nb = [(x + dx * 2, y + dy * 2, dx, dy) for dx, dy in DIRS
              if 1 <= x + dx * 2 <= 17 and 1 <= y + dy * 2 <= 17 and g[y + dy * 2][x + dx * 2] == '#']
        if not nb: st.pop(); continue
        nx, ny, dx, dy = rng.choice(nb)
        g[y + dy][x + dx] = '.'; g[ny][nx] = '.'; st.append((nx, ny))
    return g

def bfs(g, s, passable):
    d = {s: 0}; q = deque([s])
    while q:
        x, y = q.popleft()
        for dx, dy in DIRS:
            n = (x + dx, y + dy)
            if n not in d and 0 <= n[0] < W and 0 <= n[1] < H and passable(g[n[1]][n[0]]):
                d[n] = d[(x, y)] + 1; q.append(n)
    return d

def path(g, a, b):
    d = bfs(g, b, lambda c: c in '.')
    p = [a]
    while p[-1] != b:
        x, y = p[-1]
        p.append(min(((x + dx, y + dy) for dx, dy in DIRS if (x + dx, y + dy) in d), key=lambda n: d[n]))
    return p

def level(n, total, seed):
    rng = random.Random(seed)
    while True:
        g = maze(rng)
        for _ in range(3):                      # rooms
            rw, rh = rng.choice([(3, 3), (3, 2), (2, 3)])
            x0 = rng.randrange(1, 17 - rw + 1); y0 = rng.randrange(1, 17 - rh + 1)
            for y in range(y0, y0 + rh):
                for x in range(x0, x0 + rw): g[y][x] = '.'
        opens = [p for p in bfs(g, (1, 1), lambda c: c == '.')]
        start = (1, 1)
        far = bfs(g, start, lambda c: c == '.')
        # stairs down: a wall cell on the border touching an open cell, far from the start
        cands = []
        for (x, y) in far:
            for dx, dy in DIRS:
                wx, wy = x + dx, y + dy
                if g[wy][wx] == '#' and (wx in (0, 18) or wy in (0, 18)) and 1 <= wx <= 18 and 1 <= wy <= 18 or \
                   g[wy][wx] == '#' and (wx == 0 or wy == 0 or wx == 18 or wy == 18):
                    cands.append((far[(x, y)], wx, wy, x, y))
        cands = [c for c in cands if 0 < c[1] < 19 and 0 < c[2] < 19 or True]
        cands.sort(reverse=True)
        if not cands: continue
        _, sx, sy, ax, ay = cands[0]
        last = (n == total - 1)
        goal = (ax, ay)
        # up stairs above the start: the border wall next to (1,1)
        g[0][1] = '<'
        if not last: g[sy][sx] = '>'
        p = path(g, start, goal)
        if len(p) < 14: continue
        # locked door on the way (in the middle third), key on the near side
        lp = p[len(p) // 2 + rng.randrange(-2, 3)]
        if lp == start or lp == goal: continue
        g[lp[1]][lp[0]] = 'L'
        near = bfs(g, start, lambda c: c == '.')
        if goal in near: continue
        if len(near) < 12: continue
        # plain doors on the path elsewhere
        for k in (len(p) // 4, 3 * len(p) // 4):
            q = p[k]
            if g[q[1]][q[0]] == '.' and q != start: g[q[1]][q[0]] = 'D'
        # torches: walls next to the way
        wallsnext = [(x + dx, y + dy) for (x, y) in p for dx, dy in DIRS
                     if 1 <= x + dx <= 18 and 1 <= y + dy <= 18 and g[y + dy][x + dx] == '#']
        rng.shuffle(wallsnext)
        for (x, y) in wallsnext[:7]: g[y][x] = 'T'
        # floor cells: near side and the rest
        allc = [c for c, v in bfs(g, start, lambda c: c in '.DL').items() if g[c[1]][c[0]] == '.']
        nearc = [c for c in near if g[c[1]][c[0]] == '.' and c != start and abs(c[0]-1)+abs(c[1]-1) > 3]
        far_c = [c for c in allc if c not in near and c != goal]
        def put(cells, ch):
            c = rng.choice(cells); cells.remove(c); g[c[1]][c[0]] = ch
            if c in allc: allc.remove(c)
            if c in nearc: nearc.remove(c)
            if c in far_c: far_c.remove(c)
        if len(nearc) < 6 or len(far_c) < 10: continue
        put(nearc, 'k')
        for ch in 'hhf' + ('w' if n == 0 else '') + ('a' if n == 1 else '') + ('w' if n == 2 else '') + 'gg':
            put(rng.choice([nearc, far_c]), ch)
        put(far_c, 'h'); put(far_c, 'f')
        mons = {0: '1111122', 1: '22223333', 2: '3333444'}.get(n, '34444')
        cnt = 0
        for ch in mons:
            pool = [c for c in allc if abs(c[0]-1)+abs(c[1]-1) > 6]
            if not pool: break
            put(pool, ch)
        if last:
            gc = goal
            g[gc[1]][gc[0]] = '*'
            # the lich next to the amulet
            for dx, dy in DIRS:
                c = (gc[0] + dx, gc[1] + dy)
                if g[c[1]][c[0]] == '.': g[c[1]][c[0]] = '5'; break
        g[1][1] = '@' if n == 0 else '.'
        # check: simulate the key
        if not check(g, start, last): continue
        return g

def check(g, start, last):
    # open doors one key at a time; everything of value must be reachable
    keys = 0; opened = set(); got = set()
    while True:
        r = bfs(g, start, lambda c: c in '.@DhfkwagL12345*' and c != 'L')
        # keys found
        for (x, y) in r:
            if g[y][x] == 'k' and (x, y) not in got: got.add((x, y)); keys += 1
        progress = False
        for (x, y) in list(r):
            for dx, dy in DIRS:
                n = (x + dx, y + dy)
                if g[n[1]][n[0]] == 'L' and n not in opened and keys > 0:
                    keys -= 1; opened.add(n); g[n[1]][n[0]] = 'D'; progress = True
        if not progress: break
    for (x, y) in opened: g[y][x] = 'L'
    r = bfs(g, start, lambda c: c in '.@DhfkwagL12345*' )
    for y in range(H):
        for x in range(W):
            if g[y][x] in 'hfkwag12345' and (x, y) not in r: return False
    # something next to the stairs / amulet is reachable
    for y in range(H):
        for x in range(W):
            if g[y][x] in '<>*':
                if not any((x + dx, y + dy) in r for dx, dy in DIRS): return False
    return True

if __name__ == '__main__':
    total = int(sys.argv[1]) if len(sys.argv) > 1 else 3
    print('\\ Levels of gfx_dungeon.fs, made by dungeon_gen.py %d (or by the level editor).' % total)
    print('\\ 20 rows of 20 characters per level, in order; dg-row stores one row.')
    print('\\ Legend: see the top of gfx_dungeon.fs. Room for 8 levels (dg-cap).')
    print('8 constant dg-cap   400 constant dg-ns')
    print('create dg-levels dg-cap dg-ns * allot   dg-levels dg-cap dg-ns * erase')
    print('0 value dg-lp   0 value dg-nlev')
    print(': dg-row ( a n -- )   ( refuses a row past the room for dg-cap levels )')
    print('  drop  dg-lp dg-cap dg-ns * 20 - > if drop exit then  dg-levels dg-lp + 20 cmove   20 +to dg-lp ;')
    for n in range(total):
        g = level(n, total, 1000 + n * 17)
        print('\\ level %d' % (n + 1))
        for row in g: print('s" %s" dg-row' % ''.join(row))
    print('dg-lp dg-ns / to dg-nlev')
