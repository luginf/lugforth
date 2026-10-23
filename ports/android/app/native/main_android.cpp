// lugforth for Android as a shared library loaded by SDLActivity (route C).
// Same as ports/posix/main.c except that: the entry point is SDL_main, stdout and
// stderr go to logcat (tag "lugforth"), and dlopen of "libFoo-2.0.so.0" falls
// back to "libFoo.so", the name under which SDL2 libraries ship in an APK.

#include <android/log.h>
#include <dlfcn.h>
#include <errno.h>
#include <pthread.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/mman.h>
#include <unistd.h>
#include <signal.h>
#if defined(__aarch64__)
// arm64 does not trap an integer division by zero: raise the signal that x86 gets.
# define DIV0_CHECK if (tos == 0) raise(SIGFPE);
#endif

#include "common/tier0_opcodes.h"
#include "common/tier1_opcodes.h"
#include "common/tier2_opcodes.h"
#include "common/floats.h"
#include "common/calls.h"

#define HEAP_SIZE (10 * 1024 * 1024)
#define STACK_CELLS (8 * 1024)

static void *android_dlopen(const char *name, int flags) {
  void *h = dlopen(name, flags);
  if (h || !name) return h;
  // libSDL2_image-2.0.so.0 -> libSDL2_image.so
  char buf[256];
  const char *dash = strstr(name, "-2.0.so");
  if (!dash || (size_t) (dash - name) + 4 > sizeof(buf)) return h;
  memcpy(buf, name, dash - name);
  strcpy(buf + (dash - name), ".so");
  return dlopen(buf, flags);
}

static void *android_dlsym(void *h, const char *name) {
  if (!strcmp(name, "dlopen")) return (void *) android_dlopen;
  return dlsym(h, name);
}

#define PLATFORM_OPCODE_LIST \
  Y(DLSYM, tos = (cell_t) android_dlsym(a1 ? a1 : RTLD_DEFAULT, c0); NIP) \
  XV(internals, "errno", ERRNO_INTERNAL, DUP; tos = (cell_t) errno) \
  XV(internals, "host-mac?", DARWIN_P, DUP; tos = 0) \
  CALLING_OPCODE_LIST \
  FLOATING_POINT_LIST

#define VOCABULARY_LIST V(forth) V(internals)

#include "common/bits.h"
#include "common/core.h"
#include "ports/android/app/native/faults_android.h"
#include "common/calling.h"
#include "common/interp.h"

#include "gen/posix_boot.h"

// Forward what Forth prints to logcat, line by line.
static void *log_pump(void *arg) {
  int fd = (int) (long) arg;
  char line[1024];
  size_t n = 0;
  char c;
  while (read(fd, &c, 1) == 1) {
    if (c == '\n' || n == sizeof(line) - 1) {
      line[n] = 0;
      __android_log_write(ANDROID_LOG_INFO, "lugforth", line);
      n = 0;
    } else if (c != '\r') {
      line[n++] = c;
    }
  }
  return 0;
}

static void redirect_output(void) {
  int p[2];
  if (pipe(p)) return;
  dup2(p[1], 1);
  dup2(p[1], 2);
  setvbuf(stdout, 0, _IONBF, 0);
  setvbuf(stderr, 0, _IONBF, 0);
  pthread_t t;
  pthread_create(&t, 0, log_pump, (void *) (long) p[0]);
  pthread_detach(t);
}

extern "C" __attribute__((visibility("default")))
int SDL_main(int argc, char *argv[]) {
  __android_log_print(ANDROID_LOG_INFO, "lugforth", "SDL_main argc=%d argv1=%s", argc, argc > 1 ? argv[1] : "-");
  redirect_output();
  if (argc > 1) {  // run from the directory of the Forth file, for relative includes
    char dir[512];
    strncpy(dir, argv[1], sizeof(dir) - 1);
    dir[sizeof(dir) - 1] = 0;
    char *slash = strrchr(dir, '/');
    if (slash) { *slash = 0; if (chdir(dir)) { } }
  }
  const char *cwd = getenv("LUGFORTH_CWD");  // set by SdlActivity: the examples folder
  if (cwd && chdir(cwd)) { }
  void *heap = mmap(
      (void *) 0x8000000, HEAP_SIZE,
      PROT_EXEC | PROT_READ | PROT_WRITE, MAP_PRIVATE | MAP_ANONYMOUS, -1, 0);
  if (heap == MAP_FAILED) {
    __android_log_print(ANDROID_LOG_ERROR, "lugforth", "mmap heap failed: %d", errno);
    return 1;
  }
  __android_log_print(ANDROID_LOG_INFO, "lugforth", "heap at %p", heap);
  forth_init(argc, argv, heap, HEAP_SIZE, boot, sizeof(boot));
  __android_log_print(ANDROID_LOG_INFO, "lugforth", "forth_init done");
  for (;;) { g_sys->rp = forth_run(g_sys->rp); }
  return 1;
}
