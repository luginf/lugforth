\ Levels of gfx_dungeon.fs, made by dungeon_gen.py 3 (or by the level editor).
\ 20 rows of 20 characters per level, in order; dg-row stores one row.
\ Legend: see the top of gfx_dungeon.fs. Room for 8 levels (dg-cap).
8 constant dg-cap   400 constant dg-ns
create dg-levels dg-cap dg-ns * allot   dg-levels dg-cap dg-ns * erase
0 value dg-lp   0 value dg-nlev
: dg-row ( a n -- )   ( refuses a row past the room for dg-cap levels )
  drop  dg-lp dg-cap dg-ns * 20 - > if drop exit then  dg-levels dg-lp + 20 cmove   20 +to dg-lp ;
\ level 1
s" #<##################" dg-row
s" #@#...#.........#.##" dg-row
s" #.#.#.#.#.#####.#.##" dg-row
s" #.#.#...#.....#...##" dg-row
s" #.#.#####...#.###.##" dg-row
s" #.#...#.....#...#.##" dg-row
s" #.###.#.###.#####.##" dg-row
s" #...#.#.#.T.....#.##" dg-row
s" #T#1#.#.#.#.#..g#.##" dg-row
s" #...#.T.#.#..h#.#.##" dg-row
s" #.###.#.#.#D###.#.##" dg-row
s" #.kw#.#1T.#.#...#1##" dg-row
s" ###D#.#.#.T...#.#.##" dg-row
s" #.#2#.#.#....f#.#.##" dg-row
s" #h#h##TL#.##.1###1##" dg-row
s" #f.g#...#2..#.....##" dg-row
s" #.#T#.#####.########" dg-row
s" #.....#...........>#" dg-row
s" ####################" dg-row
s" ####################" dg-row
\ level 2
s" #<##################" dg-row
s" #.#.......#.3...k.##" dg-row
s" #.#####.#.#.###.Th##" dg-row
s" #.#.....#.#f#...#.##" dg-row
s" #.#.###T#.###.T##a##" dg-row
s" #...#...D.#...#.L.##" dg-row
s" #####...###.###.####" dg-row
s" #.....h...2.#.#.#g##" dg-row
s" #.####..#.###3#.#.##" dg-row
s" #.#3.....2#...#...##" dg-row
s" #.###.#####g#.###.##" dg-row
s" #.....#.....#.#...##" dg-row
s" #####T#.#.#####f####" dg-row
s" #.....#......3..#.##" dg-row
s" #.##T.#...#..####.##" dg-row
s" #.T.#.#.......#...##" dg-row
s" #.#.#.####T..h###2##" dg-row
s" #...#.......D2....##" dg-row
s" ###>################" dg-row
s" ####################" dg-row
\ level 3
s" #<##################" dg-row
s" #.......4.#.h....D##" dg-row
s" ####T####.#.#####.##" dg-row
s" #...#...#...#.#...##" dg-row
s" #.###.#.#####.#.####" dg-row
s" #...#.#...........##" dg-row
s" #.#.#.#####.##....##" dg-row
s" #.#...#...#...3...##" dg-row
s" #.#####.####...#####" dg-row
s" #.....#.4......3#*##" dg-row
s" #####.#####.#...#5##" dg-row
s" #.h.#.....#.....#.##" dg-row
s" #.#g###T#.###...#.##" dg-row
s" #3#w#.f.#.k...#3#.##" dg-row
s" #.###.#.#T###T#.#.##" dg-row
s" #.....#.h.g.L...#.##" dg-row
s" #f##########T####.##" dg-row
s" #....D............##" dg-row
s" #######T########T###" dg-row
s" ####################" dg-row
dg-lp dg-ns / to dg-nlev
