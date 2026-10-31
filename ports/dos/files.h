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

static int path_c(const char *p, cell_t n, char *out, int max) {   // a C string, or -1
  if (n < 0 || n >= max) return -1;
  memcpy(out, p, n);
  out[n] = 0;
  return 0;
}

#define FAT_FILES 16
static struct { int fd; int dir, done; struct ffblk ff; } files[FAT_FILES];

static int fh_check(cell_t fh) { return fh >= 1 && fh <= FAT_FILES && (files[fh - 1].fd >= 0 || files[fh - 1].dir) ? 0 : -1; }

static void fs_init(void) {
  for (int k = 0; k < FAT_FILES; k++) { files[k].fd = -1; files[k].dir = 0; }
  fs_update_cwd();
}

static int slot_new(void) {
  for (int k = 0; k < FAT_FILES; k++) if (files[k].fd < 0 && !files[k].dir) return k;
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
  int r = close(files[fh - 1].fd);
  files[fh - 1].fd = -1;
  return r ? dos_err() : 0;
}

static int fat_flush(void) {
  for (int k = 0; k < FAT_FILES; k++) if (files[k].fd >= 0) fsync(files[k].fd);
  return 0;
}

static cell_t fat_read(cell_t fh, uint8_t *buf, cell_t n, cell_t *ior) {
  if (fh_check(fh) || files[fh - 1].dir) { *ior = FAT_EBADF; return 0; }
  int r = read(files[fh - 1].fd, buf, n);
  if (r < 0) { *ior = dos_err(); return 0; }
  *ior = 0;
  return r;
}

static cell_t fat_write(cell_t fh, const uint8_t *buf, cell_t n) {
  if (fh_check(fh) || files[fh - 1].dir) return FAT_EBADF;
  int r = write(files[fh - 1].fd, buf, n);
  if (r < 0) return dos_err();
  return r == n ? 0 : FAT_ENOSPC;
}

static cell_t fat_size(cell_t fh, cell_t *ior) {
  if (fh_check(fh) || files[fh - 1].dir) { *ior = FAT_EBADF; return 0; }
  long r = filelength(files[fh - 1].fd);
  if (r < 0) { *ior = dos_err(); return 0; }
  *ior = 0;
  return r;
}

static cell_t fat_tell(cell_t fh, cell_t *ior) {
  if (fh_check(fh) || files[fh - 1].dir) { *ior = FAT_EBADF; return 0; }
  long r = lseek(files[fh - 1].fd, 0, SEEK_CUR);
  if (r < 0) { *ior = dos_err(); return 0; }
  *ior = 0;
  return r;
}

static cell_t fat_seek(cell_t fh, cell_t pos) {
  if (fh_check(fh) || files[fh - 1].dir) return FAT_EBADF;
  if (pos < 0) return FAT_EINVAL;
  return lseek(files[fh - 1].fd, pos, SEEK_SET) < 0 ? dos_err() : 0;
}

static cell_t fat_resize(cell_t fh, cell_t size) {
  if (fh_check(fh) || files[fh - 1].dir) return FAT_EBADF;
  if (size < 0) return FAT_EINVAL;
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
