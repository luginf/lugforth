// Reference C version of examples/bench_lines.fs and examples/bench_lines.tal:
// same 100 deterministic lines (a 16-bit LCG picks the endpoints), same
// Bresenham line drawing, same 50 redraws, so the three are comparable.
// Real SDL2 rendering (one SDL_RenderDrawPoint per pixel), so this measures
// the display path, not just the CPU-side geometry: bench_sieve.* compares
// raw computation, this one compares "draw a lot of small things every frame".
// Build:  cc -O2 -o bench_lines examples/bench_lines.c $(pkg-config --cflags --libs sdl2)
// Run:    SDL_VIDEODRIVER=dummy ./bench_lines   (or drop the env var for a real window)

#include <SDL2/SDL.h>
#include <stdio.h>
#include <stdint.h>
#include <time.h>

#define WIDTH 256
#define HEIGHT 256
#define NLINES 100
#define FRAMES 50

static uint16_t lcg(uint16_t state) { return (uint16_t)(state * 25173u + 13849u); }

static uint16_t checksum = 0;

static void plot(SDL_Renderer *r, int x, int y) {
  checksum = (uint16_t)(checksum + x * 3 + y * 7);
  SDL_RenderDrawPoint(r, x, y);
}

static void draw_line(SDL_Renderer *r, int x0, int y0, int x1, int y1) {
  int dx = x1 > x0 ? x1 - x0 : x0 - x1;
  int sx = x0 < x1 ? 1 : -1;
  int dy = -(y1 > y0 ? y1 - y0 : y0 - y1);
  int sy = y0 < y1 ? 1 : -1;
  int err = dx + dy;
  for (;;) {
    plot(r, x0, y0);
    if (x0 == x1 && y0 == y1) break;
    int e2 = 2 * err;
    if (e2 >= dy) { err += dy; x0 += sx; }
    if (e2 <= dx) { err += dx; y0 += sy; }
  }
}

static double ms_now(void) {
  struct timespec t;
  clock_gettime(CLOCK_MONOTONIC, &t);
  return t.tv_sec * 1000.0 + t.tv_nsec / 1e6;
}

int main(void) {
  SDL_Init(SDL_INIT_VIDEO);
  SDL_Window *win = SDL_CreateWindow("bench_lines", 0, 0, WIDTH, HEIGHT, 0);
  SDL_Renderer *r = SDL_CreateRenderer(win, -1, 0);

  uint16_t state = 1;
  int x0[NLINES], y0[NLINES], x1[NLINES], y1[NLINES];
  for (int i = 0; i < NLINES; i++) {
    state = lcg(state);
    x0[i] = state & 0xff; y0[i] = (state >> 8) & 0xff;
    state = lcg(state);
    x1[i] = state & 0xff; y1[i] = (state >> 8) & 0xff;
  }

  SDL_SetRenderDrawColor(r, 0, 0, 0, 255);
  SDL_RenderClear(r);  // cleared once; the timed loop below only draws and presents
  SDL_SetRenderDrawColor(r, 255, 255, 255, 255);

  double t0 = ms_now();
  for (int f = 0; f < FRAMES; f++) {
    checksum = 0;  // same lines every frame, so the last frame's checksum alone verifies the geometry
    for (int i = 0; i < NLINES; i++) draw_line(r, x0[i], y0[i], x1[i], y1[i]);
    SDL_RenderPresent(r);
  }
  double t1 = ms_now();

  printf("checksum (per frame): %04x\n", checksum);
  printf("%d frames of %d lines: %.3f ms total, %.4f ms/frame\n", FRAMES, NLINES, t1 - t0, (t1 - t0) / FRAMES);

  SDL_DestroyRenderer(r);
  SDL_DestroyWindow(win);
  SDL_Quit();
  return 0;
}
