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

// The files of DOS, through DJGPP's libc, behind the API of ports/rpi/fat.h
// and ports/efi/files.h (fat_open, fat_read...), so that the standard file
// words of ports/baremetal/builtins.h (BM_FILES_SUPPORT) serve here too.
// Paths take / or \; names are 8.3 unless DOS has long names (DOSBox has
// not, Windows 9x and DOSLFN have).

#define FAT_ENOENT 2
#define FAT_EIO 5
#define FAT_EBADF 9
#define FAT_EACCES 13
#define FAT_EEXIST 17
#define FAT_ENOTDIR 20
#define FAT_EISDIR 21
#define FAT_EINVAL 22
#define FAT_EMFILE 24
#define FAT_ENOSPC 28
#define FAT_ENAMETOOLONG 36

static struct { int ok; } fs = { 1 };
static char fs_cwd_path[260] = "";

// DJGPP's errno numbers are not those of Unix: give the Unix ones, as the other ports do.
static cell_t dos_err(void) {
  switch (errno) {
    case ENOENT: return FAT_ENOENT;
    case EBADF: return FAT_EBADF;
    case EACCES: return FAT_EACCES;
    case EEXIST: return FAT_EEXIST;
    case ENOTDIR: return FAT_ENOTDIR;
    case EISDIR: return FAT_EISDIR;
    case EINVAL: return FAT_EINVAL;
    case EMFILE: return FAT_EMFILE;
    case ENOSPC: return FAT_ENOSPC;
    case ENAMETOOLONG: return FAT_ENAMETOOLONG;
    default: return FAT_EIO;
  }
}

static void fs_update_cwd(void) {
  if (!getcwd(fs_cwd_path, sizeof fs_cwd_path)) strcpy(fs_cwd_path, "?");
}

// With long names on (Windows 9x, DOSLFN, DOSBox-X as DOS 7), DOS no longer
// cuts the names to 8.3, and the files of tools/dos_stage.py (GFX_BREA.FS for
// gfx_breakout.fs) would not be found under the long names of the sources: a
// path that does not exist is tried again with each part cut as DOS would.
static void path_short(char *a) {
  char out[260];
  int o = 0;
  for (const char *p = a; *p && o < 250; ) {
    if (*p == '/' || *p == '\\' || *p == ':') { out[o++] = *p++; continue; }
    const char *e = p;
    while (*e && *e != '/' && *e != '\\') e++;
    const char *dot = p;
    while (dot < e && *dot != '.') dot++;
    if (dot == p) { while (p < e && o < 250) out[o++] = *p++; continue; }   // . and ..
    for (int k = 0; p + k < dot && k < 8; k++) out[o++] = p[k];
    if (dot < e) {
      out[o++] = '.';
      for (int k = 1; dot + k < e && dot[k] != '.' && k <= 3; k++) out[o++] = dot[k];
    }
    p = e;
  }
  out[o] = 0;
  strcpy(a, out);
}

static int path_c(const char *p, cell_t n, char *out, int max) {   // a C string, or -1
  if (n < 0 || n >= max) return -1;
  memcpy(out, p, n);
  out[n] = 0;
  if (n && access(out, F_OK) != 0) {
    char s[260];
    strcpy(s, out);
    path_short(s);
    if (access(s, F_OK) == 0) strcpy(out, s);
  }
  return 0;
}

// ---- the bundle of a turnkey program (see autoboot.fs, `lugforth --turnkey`) ----
// lugforth.exe followed by files, an index and a trailer:
//   index: per file  u16 name length, the name, u32 offset, u32 length
//   trailer (16 bytes): "LFTK", u32 index offset, u32 file count, u32 main file number
// The files are read from the program's own .exe (bundle_fd) as read-only files,
// before the disk is looked at; their names are those of open-file (lower case, /).
static int bundle_fd = -1, bundle_n, bundle_main = -1;
static struct bundle_ent { char *name; uint32_t off, len; } *bundle_ent;

static uint32_t le32(const uint8_t *p) { return p[0] | p[1] << 8 | p[2] << 16 | (uint32_t) p[3] << 24; }

static void bundle_name(const char *p, cell_t n, char *out) {   // lower case, / for \, no leading ./
  int o = 0;
  if (n > 255) n = 255;
  for (cell_t i = 0; i < n; i++) {
    char c = p[i];
    if (c == '\\') c = '/';
    if (c >= 'A' && c <= 'Z') c += 32;
    out[o++] = c;
  }
  out[o] = 0;
  while (out[0] == '.' && out[1] == '/') memmove(out, out + 2, strlen(out + 1));
}

static void bundle_init(const char *exe) {   // exe: argv[0]; nothing happens if it carries no bundle
  uint8_t t[16];
  int fd = open(exe, O_RDONLY | O_BINARY);
  if (fd < 0) return;
  long size = filelength(fd);
  if (size < 16 || lseek(fd, size - 16, SEEK_SET) < 0 || read(fd, t, 16) != 16 || memcmp(t, "LFTK", 4)) { close(fd); return; }
  uint32_t ioff = le32(t + 4), count = le32(t + 8), mainf = le32(t + 12);
  long ilen = size - 16 - (long) ioff;
  uint8_t *ix = ilen > 0 && count < 4096 ? malloc(ilen) : 0;
  if (!ix || lseek(fd, ioff, SEEK_SET) < 0 || read(fd, ix, ilen) != ilen) { free(ix); close(fd); return; }
  bundle_ent = calloc(count, sizeof *bundle_ent);
  long p = 0;
  for (uint32_t k = 0; k < count; k++) {
    if (p + 2 > ilen) break;
    int nl = ix[p] | ix[p + 1] << 8;
    p += 2;
    if (p + nl + 8 > ilen) break;
    bundle_ent[k].name = malloc(nl + 1);
    memcpy(bundle_ent[k].name, ix + p, nl);
    bundle_ent[k].name[nl] = 0;
    p += nl;
    bundle_ent[k].off = le32(ix + p); bundle_ent[k].len = le32(ix + p + 4);
    p += 8;
    bundle_n = k + 1;
  }
  free(ix);
  bundle_fd = fd;
  bundle_main = mainf < (uint32_t) bundle_n ? (int) mainf : -1;
}

static int bundle_find(const char *p, cell_t n) {
  if (bundle_fd < 0) return -1;
  char a[256];
  bundle_name(p, n, a);
  for (int k = 0; k < bundle_n; k++) if (!strcmp(a, bundle_ent[k].name)) return k;
  return -1;
}

#define FAT_FILES 16
// fd -1: free; fd -2: a file of the bundle (bent, bpos)
static struct { int fd; int dir, done; struct ffblk ff; int bent; uint32_t bpos; } files[FAT_FILES];

static int fh_check(cell_t fh) { return fh >= 1 && fh <= FAT_FILES && (files[fh - 1].fd != -1 || files[fh - 1].dir) ? 0 : -1; }

static void fs_init(void) {
  for (int k = 0; k < FAT_FILES; k++) { files[k].fd = -1; files[k].dir = 0; }
  fs_update_cwd();
}

static int slot_new(void) {
  for (int k = 0; k < FAT_FILES; k++) if (files[k].fd == -1 && !files[k].dir) return k;
  return -1;
}

static int is_dir(const char *a) {
  struct stat st;
  return stat(a, &st) == 0 && S_ISDIR(st.st_mode);
}

static cell_t fat_open(const char *p, cell_t n, cell_t mode, int create, cell_t *ior) {
  char a[260];
  if (path_c(p, n, a, sizeof a)) { *ior = FAT_ENAMETOOLONG; return 0; }
  int k = slot_new();
  if (k < 0) { *ior = FAT_EMFILE; return 0; }
  int b = mode == 0 && !create ? bundle_find(p, n) : -1;
  if (b >= 0) { files[k].fd = -2; files[k].dir = 0; files[k].bent = b; files[k].bpos = 0; *ior = 0; return k + 1; }
  if (is_dir(a)) { *ior = FAT_EISDIR; return 0; }
  int flags = O_BINARY | (mode == 0 ? O_RDONLY : mode == 1 ? O_WRONLY : O_RDWR);
  if (create) flags |= O_CREAT | O_TRUNC;
  int fd = open(a, flags, S_IRUSR | S_IWUSR);
  if (fd < 0) { *ior = dos_err(); return 0; }
  files[k].fd = fd; files[k].dir = 0;
  *ior = 0;
  return k + 1;
}

static cell_t fat_close(cell_t fh) {
  if (fh_check(fh)) return FAT_EBADF;
  if (files[fh - 1].dir) { files[fh - 1].dir = 0; return 0; }
  if (files[fh - 1].fd == -2) { files[fh - 1].fd = -1; return 0; }
  int r = close(files[fh - 1].fd);
  files[fh - 1].fd = -1;
  return r ? dos_err() : 0;
}

static int fat_flush(void) {
  for (int k = 0; k < FAT_FILES; k++) if (files[k].fd >= 0 && files[k].fd != bundle_fd) fsync(files[k].fd);
  return 0;
}

static cell_t fat_read(cell_t fh, uint8_t *buf, cell_t n, cell_t *ior) {
  if (fh_check(fh) || files[fh - 1].dir) { *ior = FAT_EBADF; return 0; }
  if (files[fh - 1].fd == -2) {
    struct bundle_ent *e = &bundle_ent[files[fh - 1].bent];
    uint32_t left = e->len - files[fh - 1].bpos;
    if (n > (cell_t) left) n = left;
    if (n > 0 && (lseek(bundle_fd, e->off + files[fh - 1].bpos, SEEK_SET) < 0 || read(bundle_fd, buf, n) != n)) { *ior = FAT_EIO; return 0; }
    files[fh - 1].bpos += n;
    *ior = 0;
    return n;
  }
  int r = read(files[fh - 1].fd, buf, n);
  if (r < 0) { *ior = dos_err(); return 0; }
  *ior = 0;
  return r;
}

static cell_t fat_write(cell_t fh, const uint8_t *buf, cell_t n) {
  if (fh_check(fh) || files[fh - 1].dir) return FAT_EBADF;
  if (files[fh - 1].fd == -2) return FAT_EACCES;
  int r = write(files[fh - 1].fd, buf, n);
  if (r < 0) return dos_err();
  return r == n ? 0 : FAT_ENOSPC;
}

static cell_t fat_size(cell_t fh, cell_t *ior) {
  if (fh_check(fh) || files[fh - 1].dir) { *ior = FAT_EBADF; return 0; }
  if (files[fh - 1].fd == -2) { *ior = 0; return bundle_ent[files[fh - 1].bent].len; }
  long r = filelength(files[fh - 1].fd);
  if (r < 0) { *ior = dos_err(); return 0; }
  *ior = 0;
  return r;
}

static cell_t fat_tell(cell_t fh, cell_t *ior) {
  if (fh_check(fh) || files[fh - 1].dir) { *ior = FAT_EBADF; return 0; }
  if (files[fh - 1].fd == -2) { *ior = 0; return files[fh - 1].bpos; }
  long r = lseek(files[fh - 1].fd, 0, SEEK_CUR);
  if (r < 0) { *ior = dos_err(); return 0; }
  *ior = 0;
  return r;
}

static cell_t fat_seek(cell_t fh, cell_t pos) {
  if (fh_check(fh) || files[fh - 1].dir) return FAT_EBADF;
  if (pos < 0) return FAT_EINVAL;
  if (files[fh - 1].fd == -2) { files[fh - 1].bpos = pos; return 0; }   // past the end reads nothing
  return lseek(files[fh - 1].fd, pos, SEEK_SET) < 0 ? dos_err() : 0;
}

static cell_t fat_resize(cell_t fh, cell_t size) {
  if (fh_check(fh) || files[fh - 1].dir) return FAT_EBADF;
  if (size < 0) return FAT_EINVAL;
  if (files[fh - 1].fd == -2) return FAT_EACCES;
  return ftruncate(files[fh - 1].fd, size) ? dos_err() : 0;   // DOS can grow a file this way too
}

static cell_t fat_delete(const char *p, cell_t n) {
  char a[260];
  if (path_c(p, n, a, sizeof a)) return FAT_ENAMETOOLONG;
  if (is_dir(a)) return FAT_EISDIR;
  return unlink(a) ? dos_err() : 0;
}

static cell_t fat_mkdir(const char *p, cell_t n) {
  char a[260];
  if (path_c(p, n, a, sizeof a)) return FAT_ENAMETOOLONG;
  struct stat st;
  if (stat(a, &st) == 0) return FAT_EEXIST;
  return mkdir(a, S_IRWXU) ? dos_err() : 0;
}

static cell_t fat_rmdir(const char *p, cell_t n) {
  char a[260];
  if (path_c(p, n, a, sizeof a)) return FAT_ENAMETOOLONG;
  if (!is_dir(a)) return FAT_ENOTDIR;
  return rmdir(a) ? dos_err() : 0;
}

static cell_t fat_rename(const char *from, cell_t fn, const char *to, cell_t tn) {
  char a[260], b[260];
  if (path_c(from, fn, a, sizeof a) || path_c(to, tn, b, sizeof b)) return FAT_ENAMETOOLONG;
  struct stat st;
  if (stat(b, &st) == 0) return FAT_EEXIST;
  return rename(a, b) ? dos_err() : 0;
}

// A directory is read with findfirst/findnext, which give the directory
// flag with the name; "." and ".." are left out, a directory ends with '/'.
static cell_t fat_opendir(const char *p, cell_t n, cell_t *ior) {
  char a[270];
  if (path_c(p, n, a, 260)) { *ior = FAT_ENAMETOOLONG; return 0; }
  if (n == 0) strcpy(a, ".");
  if (!is_dir(a)) { *ior = FAT_ENOTDIR; return 0; }
  int k = slot_new();
  if (k < 0) { *ior = FAT_EMFILE; return 0; }
  size_t l = strlen(a);
  if (l && a[l - 1] != '/' && a[l - 1] != '\\' && a[l - 1] != ':') strcat(a, "/");
  strcat(a, "*.*");
  files[k].dir = 1;
  files[k].done = findfirst(a, &files[k].ff, FA_RDONLY | FA_HIDDEN | FA_SYSTEM | FA_DIREC | FA_ARCH) != 0;
  *ior = 0;
  return k + 1;
}

static cell_t fat_closedir(cell_t dh) { return fat_close(dh); }

static const char *fat_readdir(cell_t dh) {
  static char name[270];
  if (fh_check(dh) || !files[dh - 1].dir) return 0;
  for (;;) {
    if (files[dh - 1].done) return 0;
    struct ffblk *f = &files[dh - 1].ff;
    strcpy(name, f->ff_name);
    int dir = (f->ff_attrib & FA_DIREC) != 0;
    files[dh - 1].done = findnext(f) != 0;
    if (!strcmp(name, ".") || !strcmp(name, "..")) continue;
    int lower = 0;                                   // a short name in capitals: shown in small letters, as DJGPP does
    for (char *c = name; *c; c++) if (*c >= 'a' && *c <= 'z') lower = 1;
    if (!lower) for (char *c = name; *c; c++) if (*c >= 'A' && *c <= 'Z') *c += 32;
    if (dir) strcat(name, "/");
    return name;
  }
}

static cell_t fat_chdir(const char *p, cell_t n) {
  char a[260];
  if (path_c(p, n, a, sizeof a)) return -1;
  if (chdir(a)) return -1;
  fs_update_cwd();
  return 0;
}
