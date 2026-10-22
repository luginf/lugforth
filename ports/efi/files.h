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

// The file words on the file system of the device the application was
// started from (the USB stick): the firmware's own FAT driver, through the
// UEFI Simple File System protocol, long names included. Same entry points
// and errno-like results as ports/rpi/fat.h, so builtins.h maps the same
// words. Paths use '/', from the current directory (cd) or from the root.

#define FAT_ENOENT 2
#define FAT_EIO 5
#define FAT_EBADF 9
#define FAT_EEXIST 17
#define FAT_ENOTDIR 20
#define FAT_EISDIR 21
#define FAT_EINVAL 22
#define FAT_EMFILE 24
#define FAT_ENOSPC 28
#define FAT_ENAMETOOLONG 36

static struct { int ok; } fs;
static EFI_FILE_HANDLE fs_root;
static EFI_HANDLE vol_cur_h;                  // the volume fs_root is on (the one we booted from, at first)
static char fs_cwd_path[256] = "/";

static void fs_init(void) {
  EFI_GUID li_guid = EFI_LOADED_IMAGE_PROTOCOL_GUID;
  EFI_GUID sfs_guid = EFI_SIMPLE_FILE_SYSTEM_PROTOCOL_GUID;
  EFI_LOADED_IMAGE *li;
  EFI_SIMPLE_FILE_SYSTEM_PROTOCOL *sfs;
  if (BS->HandleProtocol(image_handle, &li_guid, (void **) &li) != EFI_SUCCESS) return;
  if (BS->HandleProtocol(li->DeviceHandle, &sfs_guid, (void **) &sfs) != EFI_SUCCESS) return;
  if (sfs->OpenVolume(sfs, &fs_root) != EFI_SUCCESS) return;
  vol_cur_h = li->DeviceHandle;
  fs.ok = 1;
}

static cell_t efi_err(EFI_STATUS s) {
  switch (s) {
    case EFI_SUCCESS: return 0;
    case EFI_NOT_FOUND: return FAT_ENOENT;
    case EFI_VOLUME_FULL: return FAT_ENOSPC;
    case EFI_ACCESS_DENIED: case EFI_WRITE_PROTECTED: return FAT_EINVAL;
    case EFI_INVALID_PARAMETER: return FAT_EINVAL;
    default: return FAT_EIO;
  }
}

// cwd + path, made absolute and tidied ("." and ".." gone): "/a/b".
static int path_abs(const char *p, cell_t n, char *out, int max) {
  char tmp[512];
  int len = 0;
  if (!(n > 0 && p[0] == '/')) {
    int c = (int) strlen(fs_cwd_path);
    if (c >= (int) sizeof tmp) return -1;
    memcpy(tmp, fs_cwd_path, c); len = c;
    tmp[len++] = '/';
  }
  if (len + n >= (cell_t) sizeof tmp) return -1;
  memcpy(tmp + len, p, n); len += (int) n;
  int o = 0;
  out[o++] = '/';
  for (int i = 0; i < len;) {
    while (i < len && tmp[i] == '/') i++;
    int s = i;
    while (i < len && tmp[i] != '/') i++;
    int k = i - s;
    if (k == 0 || (k == 1 && tmp[s] == '.')) continue;
    if (k == 2 && tmp[s] == '.' && tmp[s + 1] == '.') {
      while (o > 1 && out[o - 1] != '/') o--;
      if (o > 1) o--;
      continue;
    }
    if (o > 1) out[o++] = '/';
    if (o + k >= max) return -1;
    memcpy(out + o, tmp + s, k); o += k;
  }
  out[o] = 0;
  return o;
}

static int path_wide(const char *p, cell_t n, CHAR16 *w, int max) {   // "\a\b", UTF-8 to UTF-16
  char a[256];
  int len = path_abs(p, n, a, sizeof a);
  if (len < 0) return -1;
  int o = 0;
  for (int i = 0; i < len && o < max - 1;) {
    unsigned c = (unsigned char) a[i++];
    if (c >= 0xC0 && i < len) {
      int more = c >= 0xE0 ? 2 : 1;
      c &= 0x3F >> more;
      while (more-- && i < len) c = (c << 6) | ((unsigned char) a[i++] & 0x3F);
    }
    w[o++] = c == '/' ? L'\\' : (CHAR16) c;
  }
  w[o] = 0;
  return o;
}

#define FAT_FILES 16
static struct { EFI_FILE_HANDLE h; int dir, writable; } files[FAT_FILES];

static int fh_check(cell_t fh) { return fh >= 1 && fh <= FAT_FILES && files[fh - 1].h ? 0 : -1; }

static int slot_new(void) {
  for (int k = 0; k < FAT_FILES; k++) if (!files[k].h) return k;
  return -1;
}

static EFI_FILE_INFO *file_info(EFI_FILE_HANDLE h) {   // malloc'ed, or 0
  EFI_GUID id = EFI_FILE_INFO_ID;
  UINTN size = 0;
  EFI_FILE_INFO *info = 0;
  if (h->GetInfo(h, &id, &size, NULL) != EFI_BUFFER_TOO_SMALL) return 0;
  info = malloc(size);
  if (info && h->GetInfo(h, &id, &size, info) != EFI_SUCCESS) { free(info); info = 0; }
  return info;
}

static cell_t fat_open(const char *p, cell_t n, cell_t mode, int create, cell_t *ior) {
  CHAR16 w[256];
  *ior = FAT_EIO;
  if (!fs.ok) return 0;
  if (path_wide(p, n, w, 256) < 0) { *ior = FAT_ENAMETOOLONG; return 0; }
  int k = slot_new();
  if (k < 0) { *ior = FAT_EMFILE; return 0; }
  UINT64 m = EFI_FILE_MODE_READ | (mode != 0 || create ? EFI_FILE_MODE_WRITE : 0) |
             (create ? EFI_FILE_MODE_CREATE : 0);
  EFI_FILE_HANDLE h;
  EFI_STATUS s = fs_root->Open(fs_root, &h, w, m, 0);
  if (s != EFI_SUCCESS) { *ior = efi_err(s); return 0; }
  EFI_FILE_INFO *info = file_info(h);
  if (!info) { h->Close(h); return 0; }
  if (info->Attribute & EFI_FILE_DIRECTORY) { free(info); h->Close(h); *ior = FAT_EISDIR; return 0; }
  if (create && info->FileSize) {                     // create-file empties it
    EFI_GUID id = EFI_FILE_INFO_ID;
    info->FileSize = 0;
    s = h->SetInfo(h, &id, info->Size, info);
    if (s != EFI_SUCCESS) { free(info); h->Close(h); *ior = efi_err(s); return 0; }
  }
  free(info);
  files[k].h = h; files[k].dir = 0; files[k].writable = mode != 0 || create;
  *ior = 0;
  return k + 1;
}

static cell_t fat_close(cell_t fh) {
  if (fh_check(fh)) return FAT_EBADF;
  EFI_STATUS s = files[fh - 1].h->Close(files[fh - 1].h);
  files[fh - 1].h = 0;
  return efi_err(s);
}

static int fat_flush(void) {
  for (int k = 0; k < FAT_FILES; k++)
    if (files[k].h && files[k].writable) files[k].h->Flush(files[k].h);
  return 0;
}

static cell_t fat_read(cell_t fh, uint8_t *buf, cell_t n, cell_t *ior) {
  if (fh_check(fh) || files[fh - 1].dir) { *ior = FAT_EBADF; return 0; }
  UINTN size = (UINTN) n;
  EFI_STATUS s = files[fh - 1].h->Read(files[fh - 1].h, &size, buf);
  *ior = efi_err(s);
  return s == EFI_SUCCESS ? (cell_t) size : 0;
}

static cell_t fat_write(cell_t fh, const uint8_t *buf, cell_t n) {
  if (fh_check(fh) || files[fh - 1].dir) return FAT_EBADF;
  if (!files[fh - 1].writable) return FAT_EBADF;
  UINTN size = (UINTN) n;
  EFI_STATUS s = files[fh - 1].h->Write(files[fh - 1].h, &size, (void *) buf);
  if (s == EFI_SUCCESS && (cell_t) size != n) return FAT_ENOSPC;
  if (s == EFI_SUCCESS) files[fh - 1].h->Flush(files[fh - 1].h);   // on the stick at once, as on the Pi
  return efi_err(s);
}

static cell_t fat_size(cell_t fh, cell_t *ior) {
  if (fh_check(fh)) { *ior = FAT_EBADF; return 0; }
  EFI_FILE_INFO *info = file_info(files[fh - 1].h);
  if (!info) { *ior = FAT_EIO; return 0; }
  cell_t r = (cell_t) info->FileSize;
  free(info);
  *ior = 0;
  return r;
}

static cell_t fat_tell(cell_t fh, cell_t *ior) {
  if (fh_check(fh)) { *ior = FAT_EBADF; return 0; }
  UINT64 pos = 0;
  *ior = efi_err(files[fh - 1].h->GetPosition(files[fh - 1].h, &pos));
  return (cell_t) pos;
}

static cell_t fat_seek(cell_t fh, cell_t pos) {
  if (fh_check(fh)) return FAT_EBADF;
  if (pos < 0) return FAT_EINVAL;
  return efi_err(files[fh - 1].h->SetPosition(files[fh - 1].h, (UINT64) pos));
}

static cell_t fat_resize(cell_t fh, cell_t size) {
  if (fh_check(fh)) return FAT_EBADF;
  if (size < 0) return FAT_EINVAL;
  EFI_FILE_INFO *info = file_info(files[fh - 1].h);
  if (!info) return FAT_EIO;
  EFI_GUID id = EFI_FILE_INFO_ID;
  info->FileSize = (UINT64) size;
  EFI_STATUS s = files[fh - 1].h->SetInfo(files[fh - 1].h, &id, info->Size, info);
  free(info);
  return efi_err(s);
}

static cell_t fat_delete(const char *p, cell_t n) {
  CHAR16 w[256];
  if (!fs.ok) return FAT_EIO;
  if (path_wide(p, n, w, 256) < 0) return FAT_ENAMETOOLONG;
  EFI_FILE_HANDLE h;
  EFI_STATUS s = fs_root->Open(fs_root, &h, w, EFI_FILE_MODE_READ | EFI_FILE_MODE_WRITE, 0);
  if (s != EFI_SUCCESS) return efi_err(s);
  EFI_FILE_INFO *info = file_info(h);
  if (info && (info->Attribute & EFI_FILE_DIRECTORY)) { free(info); h->Close(h); return FAT_EISDIR; }
  free(info);
  return h->Delete(h) == EFI_SUCCESS ? 0 : FAT_EIO;    // Delete closes the handle
}

// mkdir: 0, or an errno-like code (FAT_EEXIST when the name is taken).
static cell_t fat_mkdir(const char *p, cell_t n) {
  CHAR16 w[256];
  if (!fs.ok) return FAT_EIO;
  if (path_wide(p, n, w, 256) < 0) return FAT_ENAMETOOLONG;
  EFI_FILE_HANDLE h;
  EFI_STATUS s = fs_root->Open(fs_root, &h, w, EFI_FILE_MODE_READ, 0);
  if (s == EFI_SUCCESS) { h->Close(h); return FAT_EEXIST; }
  s = fs_root->Open(fs_root, &h, w, EFI_FILE_MODE_READ | EFI_FILE_MODE_WRITE | EFI_FILE_MODE_CREATE,
                    EFI_FILE_DIRECTORY);
  if (s != EFI_SUCCESS) return efi_err(s);
  h->Close(h);
  return 0;
}

// rmdir: an empty directory only.
static cell_t fat_rmdir(const char *p, cell_t n) {
  CHAR16 w[256];
  if (!fs.ok) return FAT_EIO;
  if (path_wide(p, n, w, 256) < 0) return FAT_ENAMETOOLONG;
  EFI_FILE_HANDLE h;
  EFI_STATUS s = fs_root->Open(fs_root, &h, w, EFI_FILE_MODE_READ | EFI_FILE_MODE_WRITE, 0);
  if (s != EFI_SUCCESS) return efi_err(s);
  EFI_FILE_INFO *info = file_info(h);
  int isdir = info && (info->Attribute & EFI_FILE_DIRECTORY);
  free(info);
  if (!isdir) { h->Close(h); return FAT_ENOTDIR; }
  return h->Delete(h) == EFI_SUCCESS ? 0 : FAT_EIO;    // not empty: the firmware refuses
}

// rename-file: the firmware's FAT driver moves a file when the new name is
// an absolute path.
static cell_t fat_rename(const char *from, cell_t fn, const char *to, cell_t tn) {
  CHAR16 w[256], w2[256];
  if (!fs.ok) return FAT_EIO;
  if (path_wide(from, fn, w, 256) < 0 || path_wide(to, tn, w2, 256) < 0) return FAT_ENAMETOOLONG;
  EFI_FILE_HANDLE h, other;
  if (fs_root->Open(fs_root, &other, w2, EFI_FILE_MODE_READ, 0) == EFI_SUCCESS) {
    other->Close(other);
    return FAT_EEXIST;
  }
  EFI_STATUS s = fs_root->Open(fs_root, &h, w, EFI_FILE_MODE_READ | EFI_FILE_MODE_WRITE, 0);
  if (s != EFI_SUCCESS) return efi_err(s);
  EFI_FILE_INFO *info = file_info(h);
  if (!info) { h->Close(h); return FAT_EIO; }
  int len = 0;
  while (w2[len]) len++;
  UINTN size = SIZE_OF_EFI_FILE_INFO + (len + 1) * sizeof(CHAR16);
  EFI_FILE_INFO *ni = malloc(size);
  if (!ni) { free(info); h->Close(h); return FAT_EIO; }
  memcpy(ni, info, SIZE_OF_EFI_FILE_INFO);
  memcpy(ni->FileName, w2, (len + 1) * sizeof(CHAR16));
  ni->Size = size;
  EFI_GUID id = EFI_FILE_INFO_ID;
  s = h->SetInfo(h, &id, size, ni);
  free(ni); free(info);
  h->Close(h);
  return efi_err(s);
}

static cell_t fat_opendir(const char *p, cell_t n, cell_t *ior) {
  CHAR16 w[256];
  *ior = FAT_EIO;
  if (!fs.ok) return 0;
  if (path_wide(p, n, w, 256) < 0) { *ior = FAT_ENAMETOOLONG; return 0; }
  int k = slot_new();
  if (k < 0) { *ior = FAT_EMFILE; return 0; }
  EFI_FILE_HANDLE h;
  EFI_STATUS s = fs_root->Open(fs_root, &h, w, EFI_FILE_MODE_READ, 0);
  if (s != EFI_SUCCESS) { *ior = efi_err(s); return 0; }
  EFI_FILE_INFO *info = file_info(h);
  int dir = info && (info->Attribute & EFI_FILE_DIRECTORY);
  free(info);
  if (!dir) { h->Close(h); *ior = FAT_ENOTDIR; return 0; }
  files[k].h = h; files[k].dir = 1; files[k].writable = 0;
  *ior = 0;
  return k + 1;
}

static cell_t fat_closedir(cell_t dh) { return fat_close(dh); }

// The next name (UTF-8, until the next call), or 0; "." and ".." left out,
// a directory's name ending with '/'.
static const char *fat_readdir(cell_t dh) {
  static char name[512];
  static uint8_t buf[1024] __attribute__((aligned(8)));
  if (fh_check(dh) || !files[dh - 1].dir) return 0;
  for (;;) {
    UINTN size = sizeof buf;
    if (files[dh - 1].h->Read(files[dh - 1].h, &size, buf) != EFI_SUCCESS || size == 0) return 0;
    EFI_FILE_INFO *info = (EFI_FILE_INFO *) buf;
    int o = 0;
    for (CHAR16 *c = info->FileName; *c && o < (int) sizeof name - 4; c++) {
      unsigned u = *c;
      if (u < 0x80) name[o++] = (char) u;
      else if (u < 0x800) { name[o++] = (char) (0xC0 | (u >> 6)); name[o++] = (char) (0x80 | (u & 63)); }
      else { name[o++] = (char) (0xE0 | (u >> 12)); name[o++] = (char) (0x80 | ((u >> 6) & 63));
             name[o++] = (char) (0x80 | (u & 63)); }
    }
    name[o] = 0;
    if (!strcmp(name, ".") || !strcmp(name, "..")) continue;
    if (info->Attribute & EFI_FILE_DIRECTORY) { name[o++] = '/'; name[o] = 0; }
    return name;
  }
}

// cd: 0, or -1 when there is no such directory.
static cell_t fat_chdir(const char *p, cell_t n) {
  char a[256];
  if (!fs.ok || path_abs(p, n, a, sizeof a) < 0) return -1;
  cell_t ior;
  cell_t dh = fat_opendir(a, (cell_t) strlen(a), &ior);
  if (ior) return -1;
  fat_closedir(dh);
  memcpy(fs_cwd_path, a, strlen(a) + 1);
  return 0;
}

// ---- several volumes: the sticks and disks the firmware shows as FAT file systems ----
// `volumes` lists them, `mount n` makes one the disk of every file word (and of cd, ls...).
// The list is made again each time (after a connect_all, so that a stick plugged after the
// start is found, if the firmware sees it).
#define VOL_MAX 16
static void connect_all(void);
static EFI_HANDLE vol_h[VOL_MAX];
static int vol_n;

static cell_t vol_count(void) {
  EFI_GUID g = EFI_SIMPLE_FILE_SYSTEM_PROTOCOL_GUID;
  EFI_HANDLE *h;
  UINTN n;
  connect_all();
  vol_n = 0;
  if (BS->LocateHandleBuffer(ByProtocol, &g, NULL, &n, &h) != EFI_SUCCESS) return 0;
  for (UINTN i = 0; i < n && vol_n < VOL_MAX; i++) vol_h[vol_n++] = h[i];
  BS->FreePool(h);
  return vol_n;
}

// The index of the volume in use, or -1 (not in the list: taken out?).
static cell_t vol_now(void) {
  for (int i = 0; i < vol_n; i++) if (vol_h[i] == vol_cur_h) return i;
  return -1;
}

static EFI_FILE_HANDLE vol_root(cell_t i) {            // to Close, or 0
  EFI_GUID g = EFI_SIMPLE_FILE_SYSTEM_PROTOCOL_GUID;
  EFI_SIMPLE_FILE_SYSTEM_PROTOCOL *sfs;
  EFI_FILE_HANDLE r;
  if (i < 0 || i >= vol_n) return 0;
  if (BS->HandleProtocol(vol_h[i], &g, (void **) &sfs) != EFI_SUCCESS) return 0;
  if (sfs->OpenVolume(sfs, &r) != EFI_SUCCESS) return 0;
  return r;
}

static EFI_FILE_SYSTEM_INFO *vol_info(EFI_FILE_HANDLE r) {   // malloc'ed, or 0
  EFI_GUID id = EFI_FILE_SYSTEM_INFO_ID;
  UINTN size = 0;
  EFI_FILE_SYSTEM_INFO *info;
  if (r->GetInfo(r, &id, &size, NULL) != EFI_BUFFER_TOO_SMALL) return 0;
  info = malloc(size);
  if (info && r->GetInfo(r, &id, &size, info) != EFI_SUCCESS) { free(info); info = 0; }
  return info;
}

// The label of volume i, in ASCII, at most max bytes: how many.
static cell_t vol_label(cell_t i, char *out, cell_t max) {
  EFI_FILE_HANDLE r = vol_root(i);
  cell_t o = 0;
  if (!r) return 0;
  EFI_FILE_SYSTEM_INFO *info = vol_info(r);
  if (info) {
    for (CHAR16 *c = info->VolumeLabel; *c && o < max; c++) out[o++] = (*c >= 32 && *c < 127) ? (char) *c : '?';
    free(info);
  }
  r->Close(r);
  return o;
}

static cell_t vol_size_mb(cell_t i) {
  EFI_FILE_HANDLE r = vol_root(i);
  cell_t mb = 0;
  if (!r) return 0;
  EFI_FILE_SYSTEM_INFO *info = vol_info(r);
  if (info) { mb = (cell_t) (info->VolumeSize / 1048576); free(info); }
  r->Close(r);
  return mb;
}

// mount: 0, -1 (no such volume) or -2 (files are open: they would stay on the old volume,
// where a stick pulled out before they are closed loses their last writes). The old volume is
// flushed before it is left. The current directory goes back to the root.
static cell_t vol_mount(cell_t i) {
  for (int k = 0; k < FAT_FILES; k++) if (files[k].h) return -2;
  EFI_FILE_HANDLE r = vol_root(i);
  if (!r) return -1;
  if (fs.ok && fs_root) { fs_root->Flush(fs_root); fs_root->Close(fs_root); }
  fs_root = r;
  vol_cur_h = vol_h[i];
  fs.ok = 1;
  fs_cwd_path[0] = '/'; fs_cwd_path[1] = 0;
  return 0;
}

// ---- file system drivers: a UEFI driver (.efi) read from the disk in use, started, then
// bound to the disks (connect_all), so that the volumes it understands (exFAT, ext4...)
// show in `volumes`. The firmware itself only has FAT. 0, or -1 the file cannot be read,
// -2 the firmware refuses the image (not an x86-64 UEFI image, or Secure Boot is on),
// -3 its start failed (a driver that finds nothing to do may also say so).
static cell_t efi_load_driver(const char *p, cell_t n) {
  cell_t ior, size;
  cell_t fh = fat_open(p, n, 0, 0, &ior);
  if (ior) return -1;
  size = fat_size(fh, &ior);
  uint8_t *buf = ior || size <= 0 ? 0 : malloc(size);
  if (!buf || fat_read(fh, buf, size, &ior) != size || ior) { free(buf); fat_close(fh); return -1; }
  fat_close(fh);
  EFI_HANDLE h = 0;
  EFI_STATUS s = BS->LoadImage(FALSE, image_handle, NULL, buf, (UINTN) size, &h);
  free(buf);
  if (s != EFI_SUCCESS) return -2;
  if (BS->StartImage(h, NULL, NULL) != EFI_SUCCESS) return -3;
  connect_all();
  return 0;
}
