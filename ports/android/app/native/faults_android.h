// Same as ports/posix/faults.h, but logs each fault to logcat (with the address).
#include <setjmp.h>
#include <signal.h>

extern "C" int SDL_main(int, char **);
static jmp_buf g_forth_fault;
static int g_forth_signal;

#define FAULT_ENTRY \
  if (setjmp(g_forth_fault)) { THROWIT(g_forth_signal); }

static void forth_signal_handler(int sig, siginfo_t *si, void *ctx) {
  static int logged = 0;
  if (logged++ < 20) {
    __android_log_print(ANDROID_LOG_ERROR, "lugforth", "signal %d addr %p pc-SDL_main %lx", sig,
        si->si_addr, (long) (((ucontext_t *) ctx)->uc_mcontext.pc - (unsigned long) SDL_main));
  }
  switch (sig) {
    case SIGSEGV: g_forth_signal = -9; break;
    case SIGBUS: g_forth_signal = -23; break;
    case SIGINT: g_forth_signal = -28; break;
    case SIGFPE: g_forth_signal = -10; break;
    default: g_forth_signal = -256 - sig; break;
  }
  sigset_t ss;
  sigemptyset(&ss);
  sigprocmask(SIG_SETMASK, &ss, 0);
  longjmp(g_forth_fault, 1);
}

static void forth_faults_setup(void) {
  struct sigaction sa;
  memset(&sa, 0, sizeof(sa));
  sa.sa_sigaction = forth_signal_handler;
  sa.sa_flags = SA_SIGINFO;
  sigaction(SIGSEGV, &sa, 0);
  sigaction(SIGBUS, &sa, 0);
  sigaction(SIGINT, &sa, 0);
  sigaction(SIGFPE, &sa, 0);
}
