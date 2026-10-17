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

// A FAT file system on the blocks of storage.h, for the file words of
// builtins.h (open-file, read-file, write-file... open-dir, readdir), so
// include, needs, ls and cat work. FAT12, FAT16 and FAT32, on the first
// partition of an MBR or on a disk without one. Paths are from the root,
// '/' between directories, case ignored. Long names (VFAT, UTF-16 as UTF-8)
// are read and written: a name that fits 8.3 in one case is a short entry
// (shown in lower case if it was typed so), any other name (long, mixed case,
// accents) gets long-name entries and a short alias BASE~N.EXT. mkdir, rmdir,
// chdir. Dates come from the clock of the board. Every write-file goes to the disk before
// returning (the dirty sectors are written and the size updated), so
// pulling the card after a write loses nothing but what follows.
// Errors are positive numbers, as errno values (2 not found...).

#include <ctype.h>

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
#define FAT_ENOTEMPTY 39

static struct {
  int ok, type;                     // 12, 16 or 32
  uint32_t spc, nfats, fat_sectors, fat_lba;
  uint32_t root_lba, root_entries;  // FAT12/16: the fixed root directory
  uint32_t root_cluster;            // FAT32
  uint32_t data_lba, clusters, free_hint;
  uint32_t fsinfo_lba, free_count;  // FAT32: FSInfo's free cluster count (0xFFFFFFFF: unknown)
  int free_dirty;
} fs;

// ---- two one-sector caches: data and directories, and the FAT ----
struct sec_cache { uint32_t lba; int valid, dirty; uint8_t b[512] __attribute__((aligned(64))); };
static struct sec_cache c_data, c_fat;

static int cache_flush(struct sec_cache *c) {
  if (c->valid && c->dirty) {
    if (c == &c_fat) {                                // every copy of the FAT
      for (uint32_t k = 0; k < fs.nfats; k++)
        if (blk_write(c->lba + k * fs.fat_sectors, c->b)) return -1;
    } else if (blk_write(c->lba, c->b)) {
      return -1;
    }
    c->dirty = 0;
  }
  return 0;
}

static uint8_t *cache_get(struct sec_cache *c, uint32_t lba) {
  if (c->valid && c->lba == lba) return c->b;
  if (cache_flush(c)) return 0;
  c->valid = 0;
  if (blk_read(lba, c->b)) return 0;
  c->lba = lba; c->valid = 1;
  return c->b;
}

// FAT32 keeps a count of free clusters in its FSInfo sector: kept right
// as clusters are taken and given back (when it was known to begin with).
static void free_count_add(int d) {
  if (fs.fsinfo_lba && fs.free_count != 0xFFFFFFFFu) { fs.free_count += (uint32_t) d; fs.free_dirty = 1; }
}

static int fsinfo_flush(void) {
  if (!fs.free_dirty) return 0;
  uint8_t *b = cache_get(&c_data, fs.fsinfo_lba);
  if (!b) return -1;
  b[488] = (uint8_t) fs.free_count; b[489] = (uint8_t) (fs.free_count >> 8);
  b[490] = (uint8_t) (fs.free_count >> 16); b[491] = (uint8_t) (fs.free_count >> 24);
  c_data.dirty = 1;
  fs.free_dirty = 0;
  return 0;
}

static int fat_flush(void) { return fsinfo_flush() | cache_flush(&c_data) | cache_flush(&c_fat); }

static uint16_t rd16(const uint8_t *p) { return (uint16_t) (p[0] | (p[1] << 8)); }
static uint32_t rd32(const uint8_t *p) { return p[0] | (p[1] << 8) | (p[2] << 16) | ((uint32_t) p[3] << 24); }
static void wr16(uint8_t *p, uint32_t v) { p[0] = (uint8_t) v; p[1] = (uint8_t) (v >> 8); }
static void wr32(uint8_t *p, uint32_t v) { wr16(p, v); wr16(p + 2, v >> 16); }

// ---- the FAT itself ----
static uint32_t fat_eoc(void) { return fs.type == 32 ? 0x0FFFFFF8u : fs.type == 16 ? 0xFFF8u : 0xFF8u; }

static int fat_byte(uint32_t off, uint8_t **p) {    // a byte of the first FAT, in the cache
  uint8_t *s = cache_get(&c_fat, fs.fat_lba + off / 512);
  if (!s) return -1;
  *p = s + off % 512;
  return 0;
}

static uint32_t fat_get(uint32_t cl) {               // 0xFFFFFFFF on a read error
  uint8_t *p, *q;
  if (fs.type == 32) {
    if (fat_byte(cl * 4, &p)) return 0xFFFFFFFFu;
    return rd32(p) & 0x0FFFFFFFu;
  }
  if (fs.type == 16) {
    if (fat_byte(cl * 2, &p)) return 0xFFFFFFFFu;
    return rd16(p);
  }
  uint32_t off = cl + cl / 2;                        // FAT12: 1.5 bytes, may straddle
  if (fat_byte(off, &p)) return 0xFFFFFFFFu;
  uint32_t lo = *p;
  if (fat_byte(off + 1, &q)) return 0xFFFFFFFFu;
  uint32_t v = lo | ((uint32_t) *q << 8);
  return cl & 1 ? v >> 4 : v & 0xFFF;
}

static int fat_set(uint32_t cl, uint32_t v) {
  uint8_t *p;
  if (fs.type == 32) {
    if (fat_byte(cl * 4, &p)) return -1;
    wr32(p, (rd32(p) & 0xF0000000u) | (v & 0x0FFFFFFFu));
  } else if (fs.type == 16) {
    if (fat_byte(cl * 2, &p)) return -1;
    wr16(p, v);
  } else {
    uint32_t off = cl + cl / 2;
    if (fat_byte(off, &p)) return -1;
    if (cl & 1) *p = (uint8_t) ((*p & 0x0F) | ((v << 4) & 0xF0));
    else *p = (uint8_t) v;
    c_fat.dirty = 1;
    if (fat_byte(off + 1, &p)) return -1;
    if (cl & 1) *p = (uint8_t) (v >> 4);
    else *p = (uint8_t) ((*p & 0xF0) | ((v >> 8) & 0x0F));
  }
  c_fat.dirty = 1;
  return 0;
}

static int is_eoc(uint32_t v) { return v >= fat_eoc() || v < 2; }

static uint32_t cluster_lba(uint32_t cl) { return fs.data_lba + (cl - 2) * fs.spc; }

// A free cluster, marked as the end of a chain (and zeroed if asked), or 0.
static uint32_t fat_alloc(int zero) {
  for (uint32_t n = 0; n < fs.clusters; n++) {
    uint32_t cl = 2 + (fs.free_hint + n) % fs.clusters;
    uint32_t v = fat_get(cl);
    if (v == 0xFFFFFFFFu) return 0;
    if (v) continue;
    if (fat_set(cl, 0x0FFFFFFFu)) return 0;
    free_count_add(-1);
    fs.free_hint = cl - 1;
    if (zero) {
      for (uint32_t s = 0; s < fs.spc; s++) {
        uint8_t *b = cache_get(&c_data, cluster_lba(cl) + s);
        if (!b) return 0;
        memset(b, 0, 512);
        c_data.dirty = 1;
      }
    }
    return cl;
  }
  return 0;
}

static int fat_free_chain(uint32_t cl) {
  while (cl >= 2 && !is_eoc(cl)) {
    uint32_t next = fat_get(cl);
    if (next == 0xFFFFFFFFu || fat_set(cl, 0)) return -1;
    free_count_add(1);
    cl = next;
  }
  return 0;
}

// ---- mounting ----
static void fat_mount(void) {
  uint8_t *b = cache_get(&c_data, 0);
  if (!b || b[510] != 0x55 || b[511] != 0xAA) return;
  uint32_t start = 0;
  uint8_t ptype = b[446 + 4];
  int bpb = rd16(b + 11) == 512 && (b[0] == 0xEB || b[0] == 0xE9);
  if (!bpb && (ptype == 0x01 || ptype == 0x04 || ptype == 0x06 || ptype == 0x0B ||
               ptype == 0x0C || ptype == 0x0E)) {
    start = rd32(b + 446 + 8);
    b = cache_get(&c_data, start);
    if (!b) return;
  }
  if (rd16(b + 11) != 512 || !b[13] || !b[16]) return;
  fs.spc = b[13];
  uint32_t reserved = rd16(b + 14);
  fs.nfats = b[16];
  fs.root_entries = rd16(b + 17);
  uint32_t total = rd16(b + 19) ? rd16(b + 19) : rd32(b + 32);
  fs.fat_sectors = rd16(b + 22) ? rd16(b + 22) : rd32(b + 36);
  fs.fat_lba = start + reserved;
  fs.root_lba = fs.fat_lba + fs.nfats * fs.fat_sectors;
  uint32_t root_sectors = (fs.root_entries * 32 + 511) / 512;
  fs.data_lba = fs.root_lba + root_sectors;
  fs.clusters = (total - (fs.data_lba - start)) / fs.spc;
  // As Linux does: FAT32 when the 16-bit FAT size is 0 (a small FAT32 can
  // have fewer clusters than the specification's limit), else by count.
  fs.type = rd16(b + 22) == 0 ? 32 : fs.clusters < 4085 ? 12 : 16;
  fs.root_cluster = fs.type == 32 ? rd32(b + 44) : 0;
  fs.fsinfo_lba = 0; fs.free_count = 0xFFFFFFFFu; fs.free_dirty = 0;
  if (fs.type == 32 && rd16(b + 48) && rd16(b + 48) != 0xFFFF) {
    uint32_t at = start + rd16(b + 48);
    uint8_t *fi = cache_get(&c_data, at);
    if (fi && rd32(fi) == 0x41615252u && rd32(fi + 484) == 0x61417272u) {   // "RRaA", "rrAa"
      fs.fsinfo_lba = at;
      fs.free_count = rd32(fi + 488);
    }
  }
  fs.free_hint = 0;
  fs.ok = 1;
}

// ---- directories: entry i of the directory starting at cluster `dir`
// (0: the fixed root of FAT12/16), as a sector and an offset ----
static uint32_t root_dir(void) { return fs.type == 32 ? fs.root_cluster : 0; }

static int dir_locate(uint32_t dir, uint32_t i, uint32_t *lba, uint32_t *off, int grow) {
  if (dir == 0) {
    if (i >= fs.root_entries) return -1;
    *lba = fs.root_lba + i * 32 / 512; *off = i * 32 % 512;
    return 0;
  }
  uint32_t per = fs.spc * 16, cl = dir;
  for (uint32_t k = i / per; k > 0; k--) {
    uint32_t next = fat_get(cl);
    if (next == 0xFFFFFFFFu) return -1;
    if (is_eoc(next)) {
      if (!grow) return -1;
      next = fat_alloc(1);                           // a directory grows by a zeroed cluster
      if (!next || fat_set(cl, next)) return -1;
    }
    cl = next;
  }
  uint32_t j = i % per;
  *lba = cluster_lba(cl) + j / 16; *off = j % 16 * 32;
  return 0;
}

static uint8_t *dir_entry(uint32_t dir, uint32_t i, int grow) {
  uint32_t lba, off;
  if (dir_locate(dir, i, &lba, &off, grow)) return 0;
  uint8_t *s = cache_get(&c_data, lba);
  return s ? s + off : 0;
}

static int ci_eq(const char *a, int an, const char *b, int bn) {
  if (an != bn) return 0;
  for (int i = 0; i < an; i++) {
    char x = a[i], y = b[i];
    if (x >= 'a' && x <= 'z') x -= 32;
    if (y >= 'a' && y <= 'z') y -= 32;
    if (x != y) return 0;
  }
  return 1;
}

// "NAME.EXT" -> out, its length. Byte 12 says which part to show in lower
// case (bit 3 the name, bit 4 the extension), as Windows and Linux write it.
static char case_char(uint8_t c, int lower) { return (char) (lower && c >= 'A' && c <= 'Z' ? c + 32 : c); }

static int short_name(const uint8_t *e, char *out) {
  int n = 0;
  for (int i = 0; i < 8 && e[i] != ' '; i++)
    out[n++] = i == 0 && e[0] == 0x05 ? (char) 0xE5 : case_char(e[i], e[12] & 0x08);
  if (e[8] != ' ') {
    out[n++] = '.';
    for (int i = 8; i < 11 && e[i] != ' '; i++) out[n++] = case_char(e[i], e[12] & 0x10);
  }
  return n;
}

// Walks a directory: the next used entry from *i on, its name (the long
// one if any, else the short one), and where its long name began.
struct dir_hit { uint32_t index, first; char name[256]; int len; uint8_t e[32]; };

static int dir_next(uint32_t dir, uint32_t *i, struct dir_hit *h) {
  uint16_t lfn[256];
  int have_lfn = 0, lfn_len = 0;
  uint32_t first = *i;
  for (;; (*i)++) {
    uint8_t *e = dir_entry(dir, *i, 0);
    if (!e || e[0] == 0) return -1;                   // the end
    if (e[0] == 0xE5) { have_lfn = 0; continue; }
    if (e[11] == 0x0F) {                              // a piece of a long name
      int seq = e[0] & 0x1F;
      if (e[0] & 0x40) { have_lfn = 1; lfn_len = 0; first = *i; memset(lfn, 0, sizeof lfn); }
      static const int at[13] = {1, 3, 5, 7, 9, 14, 16, 18, 20, 22, 24, 28, 30};
      for (int k = 0; k < 13; k++) {
        int pos = (seq - 1) * 13 + k;
        uint16_t u = rd16(e + at[k]);
        if (pos >= 255 || u == 0 || u == 0xFFFF) continue;
        lfn[pos] = u;
        if (pos + 1 > lfn_len) lfn_len = pos + 1;
      }
      continue;
    }
    if (e[11] & 0x08) { have_lfn = 0; continue; }     // the volume label
    h->index = *i;
    h->first = have_lfn ? first : *i;
    memcpy(h->e, e, 32);
    if (have_lfn) {                                   // UTF-16 to UTF-8, as far as the name holds
      int n = 0;
      for (int k = 0; k < lfn_len && n + 3 < (int) sizeof h->name; k++) {
        uint16_t u = lfn[k];
        if (u < 0x80) h->name[n++] = (char) u;
        else if (u < 0x800) { h->name[n++] = (char) (0xC0 | (u >> 6)); h->name[n++] = (char) (0x80 | (u & 0x3F)); }
        else { h->name[n++] = (char) (0xE0 | (u >> 12)); h->name[n++] = (char) (0x80 | ((u >> 6) & 0x3F)); h->name[n++] = (char) (0x80 | (u & 0x3F)); }
      }
      h->len = n;
    } else h->len = short_name(e, h->name);
    h->name[h->len] = 0;
    (*i)++;
    return 0;
  }
}

static uint32_t entry_cluster(const uint8_t *e) {
  return rd16(e + 26) | (fs.type == 32 ? (uint32_t) rd16(e + 20) << 16 : 0);
}

static int dir_find(uint32_t dir, const char *name, int n, struct dir_hit *h) {
  uint32_t i = 0;
  while (dir_next(dir, &i, h) == 0) {
    char sn[13];
    int sl = short_name(h->e, sn);
    if (ci_eq(h->name, h->len, name, n) || ci_eq(sn, sl, name, n)) return 0;
  }
  return -1;
}

// The current directory (CHDIR): its first cluster, 0 for the root, and its path.
static uint32_t fs_cwd;
static char fs_cwd_path[256] = "/";

// The directory holding a path's last part, and that part. 0 or an error.
static int path_parent(const char *p, int n, uint32_t *dir, const char **leaf, int *leaf_n) {
  uint32_t d = (n > 0 && *p == '/') || !fs_cwd ? root_dir() : fs_cwd;
  for (;;) {
    while (n && *p == '/') { p++; n--; }
    int k = 0;
    while (k < n && p[k] != '/') k++;
    int last = 1;
    for (int j = k; j < n; j++) if (p[j] != '/') last = 0;
    if (last) { *dir = d; *leaf = p; *leaf_n = k; return 0; }
    if (!(k == 1 && p[0] == '.')) {
      struct dir_hit h;
      if (dir_find(d, p, k, &h)) return FAT_ENOENT;
      if (!(h.e[11] & 0x10)) return FAT_ENOTDIR;
      d = entry_cluster(h.e);
      if (d == 0) d = root_dir();                     // ".." to the root
    }
    p += k; n -= k;
  }
}

// The 11 bytes of a short name, or -1 if it does not fit 8.3. *lower gets
// the case bits of byte 12 for a part typed all in lower case.
static int make_short(const char *s, int n, uint8_t out[11], uint8_t *lower) {
  memset(out, ' ', 11);
  int up_base = 0, low_base = 0, up_ext = 0, low_ext = 0;
  int dot = -1;
  for (int i = 0; i < n; i++) if (s[i] == '.') dot = i;
  int base = dot < 0 ? n : dot, ext = dot < 0 ? 0 : n - dot - 1;
  if (base < 1 || base > 8 || ext > 3) return -1;
  for (int i = 0; i < n; i++) {
    if (i == dot) continue;
    char c = s[i];
    int in_base = i < base;
    if (c >= 'a' && c <= 'z') { c -= 32; if (in_base) low_base = 1; else low_ext = 1; }
    else if (c >= 'A' && c <= 'Z') { if (in_base) up_base = 1; else up_ext = 1; }
    if (c <= ' ' || strchr("\"*+,./:;<=>?[\\]|", c)) return -1;
    out[in_base ? i : 8 + (i - dot - 1)] = (uint8_t) c;
  }
  if ((up_base && low_base) || (up_ext && low_ext)) return -2;      // mixed case: only a long name keeps it
  *lower = (uint8_t) ((low_base && !up_base ? 0x08 : 0) | (low_ext && !up_ext ? 0x10 : 0));
  return 0;
}

// ---- open files ----
#define FAT_FILES 8
static struct fat_file {
  int used, mode;
  uint32_t first, size, pos, cur_cl, cur_idx;
  uint32_t ent_lba, ent_off;                          // its directory entry
} files[FAT_FILES];

static int fh_check(cell_t fh) { return fh >= 1 && fh <= FAT_FILES && files[fh - 1].used ? 0 : -1; }

static int entry_where(uint32_t dir, uint32_t index, struct fat_file *f) {
  return dir_locate(dir, index, &f->ent_lba, &f->ent_off, 0);
}

// The clock of the board as a FAT date and time (1980-01-01 if it is earlier).
static void fat_now(uint16_t *date, uint16_t *time) {
  int64_t t = unix_time(), days = t / 86400, sec = t % 86400;
  int64_t z = days + 719468, era = z / 146097, doe = z - era * 146097;
  int64_t yoe = (doe - doe / 1460 + doe / 36524 - doe / 146096) / 365, y = yoe + era * 400;
  int64_t doy = doe - (365 * yoe + yoe / 4 - yoe / 100), mp = (5 * doy + 2) / 153;
  int64_t d = doy - (153 * mp + 2) / 5 + 1, m = mp < 10 ? mp + 3 : mp - 9;
  if (m <= 2) y++;
  *date = y < 1980 ? (1 << 5) | 1 : (uint16_t) (((y - 1980) << 9) | (m << 5) | d);
  *time = (uint16_t) (((sec / 3600) << 11) | ((sec % 3600 / 60) << 5) | (sec % 60 / 2));
}

static int entry_update(struct fat_file *f) {        // size, first cluster and modification time
  uint8_t *s = cache_get(&c_data, f->ent_lba);
  if (!s) return -1;
  uint8_t *e = s + f->ent_off;
  uint16_t date, time;
  fat_now(&date, &time);
  wr32(e + 28, f->size);
  wr16(e + 26, f->first);
  wr16(e + 20, fs.type == 32 ? f->first >> 16 : 0);
  wr16(e + 18, date); wr16(e + 22, time); wr16(e + 24, date);
  c_data.dirty = 1;
  return 0;
}

// ---- new directory entries: a short name, or a long name with its alias ----
static uint8_t lfn_checksum(const uint8_t *sn) {
  uint8_t sum = 0;
  for (int i = 0; i < 11; i++) sum = (uint8_t) (((sum & 1) << 7) + (sum >> 1) + sn[i]);
  return sum;
}

// A UTF-8 name as UTF-16 units: the count, or -1 for a bad name (too long, a
// character FAT forbids, a trailing dot or space, bad UTF-8).
static int utf16_name(const char *p, int n, uint16_t *out) {
  int k = 0;
  for (int i = 0; i < n;) {
    unsigned c = (unsigned char) p[i++], cp;
    if (c < 0x80) cp = c;
    else if (c >= 0xC2 && c < 0xE0 && i < n && (p[i] & 0xC0) == 0x80) cp = ((c & 0x1F) << 6) | (p[i++] & 0x3F);
    else if (c >= 0xE0 && c < 0xF0 && i + 1 < n && (p[i] & 0xC0) == 0x80 && (p[i + 1] & 0xC0) == 0x80) {
      cp = ((c & 0x0F) << 12) | ((p[i] & 0x3F) << 6) | (p[i + 1] & 0x3F);
      i += 2;
    } else return -1;
    if (cp < 0x20 || (cp < 0x80 && strchr("\"*/:<>?\\|", (int) cp))) return -1;
    if (k >= 255) return -1;
    out[k++] = (uint16_t) cp;
  }
  if (k == 0 || out[k - 1] == '.' || out[k - 1] == ' ') return -1;
  return k;
}

// The short alias of a long name: BASE~N.EXT in capitals, N = tail.
static void short_alias(const uint16_t *u, int n, int tail, uint8_t sn[11]) {
  int dot = -1;
  for (int i = 1; i < n; i++) if (u[i] == '.') dot = i;
  char num[12];
  int nl = snprintf(num, sizeof num, "~%d", tail), bl = 0;
  memset(sn, ' ', 11);
  for (int i = 0; i < (dot < 0 ? n : dot) && bl < 8 - nl; i++) {
    unsigned c = u[i];
    if (c == '.' || c == ' ') continue;
    sn[bl++] = (uint8_t) (c >= 'a' && c <= 'z' ? c - 32 : (c < 0x80 && (isalnum((int) c) || strchr("$%'-_@~`!(){}^#&", (int) c))) ? c : '_');
  }
  memcpy(sn + bl, num, (size_t) nl);
  if (dot >= 0) {
    int el = 0;
    for (int i = dot + 1; i < n && el < 3; i++) {
      unsigned c = u[i];
      if (c == ' ') continue;
      sn[8 + el++] = (uint8_t) (c >= 'a' && c <= 'z' ? c - 32 : (c < 0x80 && (isalnum((int) c) || strchr("$%'-_@~`!(){}^#&", (int) c))) ? c : '_');
    }
  }
}

static int short_taken(uint32_t dir, const uint8_t sn[11]) {
  uint32_t i = 0;
  struct dir_hit h;
  while (dir_next(dir, &i, &h) == 0) if (memcmp(h.e, sn, 11) == 0) return 1;
  return 0;
}

// Adds an entry named name (n bytes, UTF-8) to dir, with its attributes and
// first cluster; *index is where its short entry is. 0 or an error number.
// The caller has checked that the name is not already there.
static int dir_add(uint32_t dir, const char *name, int n, uint8_t attr, uint32_t first, uint32_t *index) {
  uint8_t sn[11], lower = 0;
  uint16_t u[256];
  int ucount = utf16_name(name, n, u);
  if (ucount < 0) return n > 255 ? FAT_ENAMETOOLONG : FAT_EINVAL;
  int lfn = make_short(name, n, sn, &lower) != 0;
  int pieces = lfn ? (ucount + 12) / 13 : 0, need = pieces + 1;
  if (lfn) {
    int tail = 1;
    for (;; tail++) {
      if (tail > 999999) return FAT_ENOSPC;
      short_alias(u, ucount, tail, sn);
      if (!short_taken(dir, sn)) break;
    }
    lower = 0;
  }
  uint32_t start = 0;
  int run = 0;
  for (uint32_t i = 0; run < need; i++) {             // a run of free entries, growing the directory
    uint8_t *e = dir_entry(dir, i, 1);
    if (!e) return FAT_ENOSPC;
    if (e[0] == 0 || e[0] == 0xE5) { if (!run++) start = i; } else run = 0;
  }
  uint8_t sum = lfn_checksum(sn);
  for (int j = 0; j < pieces; j++) {                  // the pieces, last one first
    int seq = pieces - j;
    uint8_t *e = dir_entry(dir, start + (uint32_t) j, 1);
    if (!e) return FAT_EIO;
    memset(e, 0, 32);
    e[0] = (uint8_t) (seq | (j == 0 ? 0x40 : 0));
    e[11] = 0x0F; e[13] = sum;
    static const int at[13] = {1, 3, 5, 7, 9, 14, 16, 18, 20, 22, 24, 28, 30};
    for (int k = 0; k < 13; k++) {
      int pos = (seq - 1) * 13 + k;
      wr16(e + at[k], pos < ucount ? u[pos] : pos == ucount ? 0x0000 : 0xFFFF);
    }
    c_data.dirty = 1;
  }
  uint8_t *e = dir_entry(dir, start + (uint32_t) pieces, 1);
  if (!e) return FAT_EIO;
  uint16_t date, time;
  fat_now(&date, &time);
  memset(e, 0, 32);
  memcpy(e, sn, 11);
  e[11] = attr; e[12] = lower;
  wr16(e + 14, time); wr16(e + 16, date); wr16(e + 18, date);
  wr16(e + 20, fs.type == 32 ? first >> 16 : 0);
  wr16(e + 22, time); wr16(e + 24, date);
  wr16(e + 26, first);
  c_data.dirty = 1;
  *index = start + (uint32_t) pieces;
  return 0;
}

// The cluster holding byte `pos`; allocated when writing past the end.
static uint32_t file_cluster(struct fat_file *f, uint32_t pos, int grow) {
  uint32_t csize = fs.spc * 512, idx = pos / csize;
  if (!f->first) {
    if (!grow) return 0;
    f->first = fat_alloc(0);
    if (!f->first) return 0;
    f->cur_cl = f->first; f->cur_idx = 0;
  }
  if (!f->cur_cl || idx < f->cur_idx) { f->cur_cl = f->first; f->cur_idx = 0; }
  while (f->cur_idx < idx) {
    uint32_t next = fat_get(f->cur_cl);
    if (next == 0xFFFFFFFFu) return 0;
    if (is_eoc(next)) {
      if (!grow) return 0;
      next = fat_alloc(0);
      if (!next || fat_set(f->cur_cl, next)) return 0;
    }
    f->cur_cl = next; f->cur_idx++;
  }
  return f->cur_cl;
}

static cell_t fat_open(const char *p, cell_t n, cell_t mode, int create, cell_t *ior) {
  *ior = FAT_EIO;
  if (!fs.ok) return 0;
  uint32_t dir;
  const char *leaf;
  int ln;
  if ((*ior = path_parent(p, (int) n, &dir, &leaf, &ln))) return 0;
  if (!ln) { *ior = FAT_EISDIR; return 0; }
  int k = 0;
  while (k < FAT_FILES && files[k].used) k++;
  if (k == FAT_FILES) { *ior = FAT_EMFILE; return 0; }
  struct fat_file *f = &files[k];
  memset(f, 0, sizeof *f);
  struct dir_hit h;
  if (dir_find(dir, leaf, ln, &h) == 0) {
    if (h.e[11] & 0x10) { *ior = FAT_EISDIR; return 0; }
    if (entry_where(dir, h.index, f)) { *ior = FAT_EIO; return 0; }
    f->first = entry_cluster(h.e);
    f->size = rd32(h.e + 28);
    if (create) {                                     // create-file empties it
      if (fat_free_chain(f->first)) { *ior = FAT_EIO; return 0; }
      f->first = 0; f->size = 0;
      if (entry_update(f) || fat_flush()) { *ior = FAT_EIO; return 0; }
    }
  } else {
    if (!create) { *ior = FAT_ENOENT; return 0; }
    uint32_t i;
    if ((*ior = dir_add(dir, leaf, ln, 0x20, 0, &i))) return 0;
    if (entry_where(dir, i, f) || entry_update(f) || fat_flush()) { *ior = FAT_EIO; return 0; }
  }
  f->used = 1; f->mode = (int) mode;
  *ior = 0;
  return k + 1;
}

static cell_t fat_close(cell_t fh) {
  if (fh_check(fh)) return FAT_EBADF;
  files[fh - 1].used = 0;
  return fat_flush() ? FAT_EIO : 0;
}

static cell_t fat_read(cell_t fh, uint8_t *buf, cell_t n, cell_t *ior) {
  *ior = 0;
  if (fh_check(fh)) { *ior = FAT_EBADF; return 0; }
  struct fat_file *f = &files[fh - 1];
  cell_t done = 0;
  while (done < n && f->pos < f->size) {
    uint32_t cl = file_cluster(f, f->pos, 0);
    if (!cl) { *ior = FAT_EIO; break; }
    uint32_t in = f->pos % (fs.spc * 512);
    uint8_t *s = cache_get(&c_data, cluster_lba(cl) + in / 512);
    if (!s) { *ior = FAT_EIO; break; }
    uint32_t k = 512 - in % 512;
    if (k > f->size - f->pos) k = f->size - f->pos;
    if ((cell_t) k > n - done) k = (uint32_t) (n - done);
    memcpy(buf + done, s + in % 512, k);
    done += k; f->pos += k;
  }
  return done;
}

static cell_t fat_write(cell_t fh, const uint8_t *buf, cell_t n) {
  if (fh_check(fh)) return FAT_EBADF;
  struct fat_file *f = &files[fh - 1];
  if (f->mode == 0) return FAT_EBADF;                 // r/o
  cell_t done = 0;
  while (done < n) {
    uint32_t cl = file_cluster(f, f->pos, 1);
    if (!cl) { entry_update(f); fat_flush(); return FAT_ENOSPC; }
    uint32_t in = f->pos % (fs.spc * 512);
    uint8_t *s = cache_get(&c_data, cluster_lba(cl) + in / 512);
    if (!s) return FAT_EIO;
    uint32_t k = 512 - in % 512;
    if ((cell_t) k > n - done) k = (uint32_t) (n - done);
    memcpy(s + in % 512, buf + done, k);
    c_data.dirty = 1;
    done += k; f->pos += k;
    if (f->pos > f->size) f->size = f->pos;
  }
  if (entry_update(f) || fat_flush()) return FAT_EIO;
  return 0;
}

static cell_t fat_size(cell_t fh, cell_t *ior) {
  if (fh_check(fh)) { *ior = FAT_EBADF; return 0; }
  *ior = 0;
  return files[fh - 1].size;
}

static cell_t fat_tell(cell_t fh, cell_t *ior) {
  if (fh_check(fh)) { *ior = FAT_EBADF; return 0; }
  *ior = 0;
  return files[fh - 1].pos;
}

static cell_t fat_seek(cell_t fh, cell_t pos) {
  if (fh_check(fh)) return FAT_EBADF;
  if (pos < 0 || (uint64_t) pos > files[fh - 1].size) return FAT_EINVAL;
  files[fh - 1].pos = (uint32_t) pos;
  return 0;
}

static cell_t fat_resize(cell_t fh, cell_t size) {
  if (fh_check(fh)) return FAT_EBADF;
  struct fat_file *f = &files[fh - 1];
  if (size < 0 || size > 0xFFFFFFFFL) return FAT_EINVAL;
  if ((uint32_t) size > f->size) {                    // longer: zeros at the end
    static const uint8_t zero[512];
    uint32_t keep = f->pos;
    f->pos = f->size;
    while (f->size < (uint32_t) size) {
      uint32_t k = (uint32_t) size - f->size;
      cell_t r = fat_write(fh, zero, k > 512 ? 512 : k);
      if (r) return r;
    }
    f->pos = keep;
    return 0;
  }
  uint32_t csize = fs.spc * 512, keep_cl = ((uint32_t) size + csize - 1) / csize;
  if (keep_cl == 0) {
    if (fat_free_chain(f->first)) return FAT_EIO;
    f->first = 0;
  } else {
    uint32_t last = file_cluster(f, (keep_cl - 1) * csize, 0);
    if (!last) return FAT_EIO;
    uint32_t rest = fat_get(last);
    if (!is_eoc(rest) && (fat_free_chain(rest) || fat_set(last, 0x0FFFFFFFu))) return FAT_EIO;
  }
  f->size = (uint32_t) size;
  f->cur_cl = 0; f->cur_idx = 0;
  if (f->pos > f->size) f->pos = f->size;
  return entry_update(f) || fat_flush() ? FAT_EIO : 0;
}

static int entry_delete(uint32_t dir, struct dir_hit *h) {   // the entry and its long name
  for (uint32_t i = h->first; i <= h->index; i++) {
    uint8_t *e = dir_entry(dir, i, 0);
    if (!e) return -1;
    e[0] = 0xE5;
    c_data.dirty = 1;
  }
  return 0;
}

static int path_is_open(uint32_t dir, uint32_t index) {
  uint32_t lba, off;
  if (dir_locate(dir, index, &lba, &off, 0)) return 0;
  for (int k = 0; k < FAT_FILES; k++)
    if (files[k].used && files[k].ent_lba == lba && files[k].ent_off == off) return 1;
  return 0;
}

static cell_t fat_delete(const char *p, cell_t n) {
  if (!fs.ok) return FAT_EIO;
  uint32_t dir;
  const char *leaf;
  int ln;
  struct dir_hit h;
  cell_t r = path_parent(p, (int) n, &dir, &leaf, &ln);
  if (r) return r;
  if (dir_find(dir, leaf, ln, &h)) return FAT_ENOENT;
  if (h.e[11] & 0x10) return FAT_EISDIR;
  if (path_is_open(dir, h.index)) return FAT_EINVAL;
  if (fat_free_chain(entry_cluster(h.e)) || entry_delete(dir, &h) || fat_flush()) return FAT_EIO;
  return 0;
}

// rename-file: a file to a new name, in its directory or another.
static cell_t fat_rename(const char *from, cell_t fn, const char *to, cell_t tn) {
  if (!fs.ok) return FAT_EIO;
  uint32_t d1, d2;
  const char *l1, *l2;
  int n1, n2;
  struct dir_hit h, other;
  cell_t r = path_parent(from, (int) fn, &d1, &l1, &n1);
  if (r) return r;
  if ((r = path_parent(to, (int) tn, &d2, &l2, &n2))) return r;
  if (dir_find(d1, l1, n1, &h)) return FAT_ENOENT;
  if (h.e[11] & 0x10) return FAT_EISDIR;
  if (path_is_open(d1, h.index)) return FAT_EINVAL;
  if (dir_find(d2, l2, n2, &other) == 0) return FAT_EEXIST;
  uint32_t i;
  if ((r = dir_add(d2, l2, n2, h.e[11], 0, &i))) return (cell_t) r;
  uint8_t *e = dir_entry(d2, i, 0);                   // the new entry takes the rest of the old one
  if (!e) return FAT_EIO;
  memcpy(e + 13, h.e + 13, 19);
  e[11] = h.e[11];
  c_data.dirty = 1;
  if (entry_delete(d1, &h) || fat_flush()) return FAT_EIO;
  return 0;
}

// ---- open-dir and readdir ----
#define FAT_DIRS 4
static struct { int used; uint32_t dir, i; } dirs[FAT_DIRS];

static cell_t fat_opendir(const char *p, cell_t n, cell_t *ior) {
  *ior = FAT_EIO;
  if (!fs.ok) return 0;
  uint32_t dir;
  const char *leaf;
  int ln;
  if ((*ior = path_parent(p, (int) n, &dir, &leaf, &ln))) return 0;
  if (ln && !(ln == 1 && leaf[0] == '.')) {
    struct dir_hit h;
    if (dir_find(dir, leaf, ln, &h)) { *ior = FAT_ENOENT; return 0; }
    if (!(h.e[11] & 0x10)) { *ior = FAT_ENOTDIR; return 0; }
    dir = entry_cluster(h.e);
    if (dir == 0) dir = root_dir();
  }
  for (int k = 0; k < FAT_DIRS; k++) {
    if (dirs[k].used) continue;
    dirs[k].used = 1; dirs[k].dir = dir; dirs[k].i = 0;
    *ior = 0;
    return k + 1;
  }
  *ior = FAT_EMFILE;
  return 0;
}

// Makes a directory the current one: relative names are looked for there.
static cell_t fat_chdir(const char *p, cell_t n) {
  if (!fs.ok) return FAT_EIO;
  uint32_t dir;
  const char *leaf;
  int ln, e;
  if ((e = path_parent(p, (int) n, &dir, &leaf, &ln))) return e;
  if (ln && !(ln == 1 && leaf[0] == '.')) {
    struct dir_hit h;
    if (dir_find(dir, leaf, ln, &h)) return FAT_ENOENT;
    if (!(h.e[11] & 0x10)) return FAT_ENOTDIR;
    dir = entry_cluster(h.e);
    if (dir == 0) dir = root_dir();
  }
  fs_cwd = dir == root_dir() ? 0 : dir;
  if (n > 0 && *p == '/') strcpy(fs_cwd_path, "/");
  while (n > 0) {                                     // the path text follows the names
    while (n && *p == '/') { p++; n--; }
    int k = 0;
    while (k < n && p[k] != '/') k++;
    size_t len = strlen(fs_cwd_path);
    if (k == 2 && p[0] == '.' && p[1] == '.') {
      while (len > 1 && fs_cwd_path[len - 1] != '/') len--;
      if (len > 1) len--;
      fs_cwd_path[len] = 0;
    } else if (k && !(k == 1 && p[0] == '.') && len + k + 2 < sizeof fs_cwd_path) {
      if (len > 1) fs_cwd_path[len++] = '/';
      memcpy(fs_cwd_path + len, p, (size_t) k);
      fs_cwd_path[len + k] = 0;
    }
    p += k; n -= k;
  }
  return 0;
}

// mkdir: a directory, with its . and .. entries.
static cell_t fat_mkdir(const char *p, cell_t n) {
  if (!fs.ok) return FAT_EIO;
  uint32_t dir, i;
  const char *leaf;
  int ln;
  struct dir_hit h;
  cell_t r = path_parent(p, (int) n, &dir, &leaf, &ln);
  if (r) return r;
  if (!ln || (ln == 1 && leaf[0] == '.') || dir_find(dir, leaf, ln, &h) == 0) return FAT_EEXIST;
  uint32_t cl = fat_alloc(1);
  if (!cl) return FAT_ENOSPC;
  if ((r = dir_add(dir, leaf, ln, 0x10, cl, &i))) { fat_free_chain(cl); return r; }
  uint8_t *s = cache_get(&c_data, cluster_lba(cl));
  if (!s) return FAT_EIO;
  uint16_t date, time;
  fat_now(&date, &time);
  for (int k = 0; k < 2; k++) {                       // "." is this directory, ".." its parent (0: the root)
    uint8_t *e = s + 32 * k;
    uint32_t target = k == 0 ? cl : (dir == root_dir() ? 0 : dir);
    memset(e, 0, 32);
    memset(e, ' ', 11);
    e[0] = '.'; if (k) e[1] = '.';
    e[11] = 0x10;
    wr16(e + 14, time); wr16(e + 16, date); wr16(e + 18, date);
    wr16(e + 20, fs.type == 32 ? target >> 16 : 0);
    wr16(e + 22, time); wr16(e + 24, date);
    wr16(e + 26, target);
  }
  c_data.dirty = 1;
  return fat_flush() ? FAT_EIO : 0;
}

// rmdir: an empty directory (only . and ..), not the current one.
static cell_t fat_rmdir(const char *p, cell_t n) {
  if (!fs.ok) return FAT_EIO;
  uint32_t dir;
  const char *leaf;
  int ln;
  struct dir_hit h;
  cell_t r = path_parent(p, (int) n, &dir, &leaf, &ln);
  if (r) return r;
  if (!ln || dir_find(dir, leaf, ln, &h)) return FAT_ENOENT;
  if (!(h.e[11] & 0x10)) return FAT_ENOTDIR;
  uint32_t cl = entry_cluster(h.e), i = 0;
  if (cl == 0 || cl == fs_cwd) return FAT_EINVAL;
  struct dir_hit in;
  while (dir_next(cl, &i, &in) == 0) {
    if (in.name[0] == '.' && (in.len == 1 || (in.len == 2 && in.name[1] == '.'))) continue;
    return FAT_ENOTEMPTY;
  }
  if (fat_free_chain(cl) || entry_delete(dir, &h) || fat_flush()) return FAT_EIO;
  return 0;
}

static cell_t fat_closedir(cell_t dh) {
  if (dh < 1 || dh > FAT_DIRS || !dirs[dh - 1].used) return FAT_EBADF;
  dirs[dh - 1].used = 0;
  return 0;
}

// The next name (a C string, valid until the next call), or 0 at the end;
// "." and ".." are left out, a directory's name ends with '/'.
static const char *fat_readdir(cell_t dh) {
  static struct dir_hit h;
  if (dh < 1 || dh > FAT_DIRS || !dirs[dh - 1].used) return 0;
  while (dir_next(dirs[dh - 1].dir, &dirs[dh - 1].i, &h) == 0) {
    if (h.name[0] == '.' && (h.len == 1 || (h.len == 2 && h.name[1] == '.'))) continue;
    if (h.e[11] & 0x10) { h.name[h.len++] = '/'; h.name[h.len] = 0; }
    return h.name;
  }
  return 0;
}

static void fs_init(void) {
  blk_init();
  if (blk_ready) fat_mount();
}
