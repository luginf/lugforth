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

// A small file system in the last megabyte of the flash of the Pico.
//
// The region has 256 sectors of 4 KB. Sectors 0 and 1 each hold a copy of the
// directory, with a sequence number and a checksum: a new directory is always
// written to the copy that is not in use, so a power cut during the write
// leaves the old one valid. Sectors 2 to 254 hold the data of the files,
// chained FAT style by the table "next" of the directory (0 free, 255 end of a
// chain). A file is written through one 4 KB buffer in RAM, sector by sector,
// into sectors the live directory says are free, and the directory is only
// written when the file is closed: replacing a file is atomic, and a file that
// was never closed leaves nothing.
//
// Names are 1 to 23 characters, a leading / is ignored, there are no
// directories. At most 119 files, one file open for writing at a time, 4 open
// files. The flash is read in place (it is mapped at XIP_BASE); the chip
// functions erase and program a sector at a time with interrupts off (about
// 45 ms to erase and 7 ms to program), so the USB serial line stops answering
// for that time. In the simulator the flash is an array (1 MB) that behaves
// like NOR (erase sets the bytes to 0xff, programming only clears bits), kept
// in the file named by the environment variable LUGFORTH_PICO_FLASH if it is
// set, so that tests can restart with the same flash.

#ifndef LUGFORTH_FLASHFS_H
#define LUGFORTH_FLASHFS_H

#include <errno.h>
#include <stdint.h>
#include <stdlib.h>
#include <string.h>

#define FFS_SECT 4096
#define FFS_NSECT 256
#define FFS_FIRST 2
#define FFS_LAST 254
#define FFS_END 0xff
#define FFS_NAME 24
#define FFS_ENTRIES 119
#define FFS_MAXFD 4
#define FFS_MAGIC 0x5346464cu  // "LFFS"

typedef struct {
  char name[FFS_NAME];  // NUL padded, name[0] == 0: free
  uint16_t first;       // the first data sector, 0 for an empty file
  uint16_t pad;
  uint32_t size;
} ffs_entry;

typedef union {
  struct {
    uint32_t magic, version, seq, crc;
    uint8_t next[256];
    ffs_entry e[FFS_ENTRIES];
  } d;
  uint8_t raw[FFS_SECT];
} ffs_dir;

// ---- The flash: read in place, erase and program a sector ----

#ifdef LUGFORTH_SIM
# include <fcntl.h>
# include <stdio.h>
# include <unistd.h>
static uint8_t ffs_sim[FFS_NSECT * FFS_SECT];
static int ffs_sim_fd = -2;
static inline void ffs_sim_init(void) {
  if (ffs_sim_fd != -2) return;
  memset(ffs_sim, 0xff, sizeof(ffs_sim));
  const char *path = getenv("LUGFORTH_PICO_FLASH");
  ffs_sim_fd = -1;
  if (path) {
    ffs_sim_fd = open(path, O_RDWR | O_CREAT, 0666);
    if (ffs_sim_fd >= 0 && read(ffs_sim_fd, ffs_sim, sizeof(ffs_sim)) < 0) {}
  }
}
static inline const uint8_t *ffs_ptr(uint32_t sector) {
  ffs_sim_init();
  return ffs_sim + (size_t) sector * FFS_SECT;
}
static inline int ffs_write_sector(uint32_t sector, const uint8_t *data) {
  ffs_sim_init();
  uint8_t *flash = ffs_sim + (size_t) sector * FFS_SECT;
  memset(flash, 0xff, FFS_SECT);  // the erase
  for (int i = 0; i < FFS_SECT; ++i) flash[i] &= data[i];  // the programming
  if (ffs_sim_fd >= 0) {
    if (pwrite(ffs_sim_fd, flash, FFS_SECT, (off_t) sector * FFS_SECT) < 0) return EIO;
  }
  return 0;
}
static inline int ffs_region_ok(void) { return 1; }
#else
# include "hardware/flash.h"
# include "hardware/sync.h"
# define FFS_OFFSET (PICO_FLASH_SIZE_BYTES - FFS_NSECT * FFS_SECT)
extern char __flash_binary_end;
static inline const uint8_t *ffs_ptr(uint32_t sector) {
  return (const uint8_t *) (XIP_BASE + FFS_OFFSET + sector * FFS_SECT);
}
// The data must be in RAM. Interrupts are off for the erase, then for the
// programming, and back on in between and after (the USB line is serviced).
static inline int ffs_write_sector(uint32_t sector, const uint8_t *data) {
  uint32_t off = FFS_OFFSET + sector * FFS_SECT;
  uint32_t irq = save_and_disable_interrupts();
  flash_range_erase(off, FFS_SECT);
  restore_interrupts(irq);
  irq = save_and_disable_interrupts();
  flash_range_program(off, data, FFS_SECT);
  restore_interrupts(irq);
  return 0;
}
// The program must end before the region: a bigger program would be erased
static inline int ffs_region_ok(void) {
  return (uintptr_t) &__flash_binary_end <= XIP_BASE + FFS_OFFSET;
}
#endif

// ---- The directory ----

static ffs_dir ffs_d;          // the live directory
static int ffs_slot = -1;      // the sector it came from or went to, -1: none yet
static int ffs_mounted = 0, ffs_usable = 0;
static uint8_t ffs_wbuf[FFS_SECT];

static inline uint32_t ffs_crc(const uint8_t *p, size_t n) {
  uint32_t c = 0xffffffffu;
  while (n--) {
    c ^= *p++;
    for (int k = 0; k < 8; ++k) c = (c >> 1) ^ (0xedb88320u & (0u - (c & 1u)));
  }
  return ~c;
}

static inline int ffs_dir_valid(const uint8_t *raw, ffs_dir *out) {
  memcpy(out->raw, raw, FFS_SECT);
  if (out->d.magic != FFS_MAGIC || out->d.version != 1) return 0;
  return out->d.crc == ffs_crc(out->raw + 16, FFS_SECT - 16);
}

static inline void ffs_mount(void) {
  if (ffs_mounted) return;
  ffs_mounted = 1;
  ffs_usable = ffs_region_ok();
  memset(ffs_d.raw, 0, FFS_SECT);
  ffs_d.d.magic = FFS_MAGIC;
  ffs_d.d.version = 1;
  if (!ffs_usable) return;
  static ffs_dir t;
  uint32_t best = 0;
  for (int s = 0; s < 2; ++s) {
    if (ffs_dir_valid(ffs_ptr(s), &t) && (ffs_slot < 0 || t.d.seq > best)) {
      best = t.d.seq;
      ffs_slot = s;
      memcpy(ffs_d.raw, t.raw, FFS_SECT);
    }
  }
}

// Writes the directory nd (a copy of the live one, changed) to the copy that is
// not in use, and makes it the live one. Returns 0 or an errno.
static inline int ffs_commit(ffs_dir *nd) {
  nd->d.magic = FFS_MAGIC;
  nd->d.version = 1;
  nd->d.seq = ffs_d.d.seq + 1;
  nd->d.crc = ffs_crc(nd->raw + 16, FFS_SECT - 16);
  int target = ffs_slot == 0 ? 1 : 0;
  int err = ffs_write_sector(target, nd->raw);
  if (err) return err;
  if (memcmp(ffs_ptr(target), nd->raw, FFS_SECT) != 0) return EIO;
  memcpy(ffs_d.raw, nd->raw, FFS_SECT);
  ffs_slot = target;
  return 0;
}

static inline int ffs_name(const char *name, size_t len, char *out) {
  while (len > 0 && *name == '/') { ++name; --len; }
  if (len == 0 || len >= FFS_NAME) return -1;
  memcpy(out, name, len);
  memset(out + len, 0, FFS_NAME - len);
  return 0;
}

static inline int ffs_find(const ffs_dir *d, const char *n24) {
  for (int i = 0; i < FFS_ENTRIES; ++i)
    if (d->d.e[i].name[0] && memcmp(d->d.e[i].name, n24, FFS_NAME) == 0) return i;
  return -1;
}

static inline void ffs_free_chain(ffs_dir *d, int first) {
  int s = first, guard = 0;
  while (s >= FFS_FIRST && s <= FFS_LAST && guard++ < FFS_NSECT) {
    int n = d->d.next[s];
    d->d.next[s] = 0;
    s = n;
  }
}

// ---- Open files ----

typedef struct {
  int used, writing, entry;
  uint32_t size, pos;
  // writing: the sectors written so far, not yet in the directory
  uint16_t first, last;
  uint32_t fill;
  char name[FFS_NAME];
} ffs_file;
static ffs_file ffs_f[FFS_MAXFD];
static uint8_t ffs_pn[256];     // the chain of the file being written
static uint8_t ffs_pend[256];   // its sectors
static int ffs_writer = -1;

static inline int ffs_fdok(int fd) {
  fd -= 10;
  return fd >= 0 && fd < FFS_MAXFD && ffs_f[fd].used;
}

static inline int ffs_slotfree(void) {
  for (int i = 0; i < FFS_MAXFD; ++i) if (!ffs_f[i].used) return i;
  return -1;
}

// Returns a descriptor (>= 10) or -errno.
static inline int ffs_open(const char *name, size_t len, int mode) {
  ffs_mount();
  if (!ffs_usable) return -EIO;
  if (mode != 0) return -EACCES;  // existing files are read only: create-file writes
  char n[FFS_NAME];
  if (ffs_name(name, len, n)) return -ENOENT;
  int idx = ffs_find(&ffs_d, n);
  if (idx < 0) return -ENOENT;
  int s = ffs_slotfree();
  if (s < 0) return -EMFILE;
  ffs_file *f = &ffs_f[s];
  memset(f, 0, sizeof(*f));
  f->used = 1;
  f->entry = idx;
  f->first = ffs_d.d.e[idx].first;
  f->size = ffs_d.d.e[idx].size;
  memcpy(f->name, n, FFS_NAME);
  return s + 10;
}

static inline int ffs_create(const char *name, size_t len) {
  ffs_mount();
  if (!ffs_usable) return -EIO;
  char n[FFS_NAME];
  if (ffs_name(name, len, n)) return -EINVAL;
  if (ffs_writer >= 0) return -EBUSY;
  int s = ffs_slotfree();
  if (s < 0) return -EMFILE;
  ffs_file *f = &ffs_f[s];
  memset(f, 0, sizeof(*f));
  f->used = 1;
  f->writing = 1;
  memcpy(f->name, n, FFS_NAME);
  memset(ffs_pn, 0, sizeof(ffs_pn));
  memset(ffs_pend, 0, sizeof(ffs_pend));
  memset(ffs_wbuf, 0xff, FFS_SECT);
  ffs_writer = s;
  return s + 10;
}

static inline void ffs_abort_writer(void) {
  memset(ffs_pend, 0, sizeof(ffs_pend));
  ffs_writer = -1;
}

// Writes the buffer to a free sector and chains it. 0 or an errno.
static inline int ffs_flush(ffs_file *f) {
  int s = 0;
  for (int i = FFS_FIRST; i <= FFS_LAST; ++i)
    if (ffs_d.d.next[i] == 0 && !ffs_pend[i]) { s = i; break; }
  if (!s) return ENOSPC;
  int err = ffs_write_sector(s, ffs_wbuf);
  if (err) return err;
  ffs_pend[s] = 1;
  ffs_pn[s] = FFS_END;
  if (f->last) ffs_pn[f->last] = s; else f->first = s;
  f->last = s;
  f->fill = 0;
  memset(ffs_wbuf, 0xff, FFS_SECT);
  return 0;
}

static inline int ffs_write(int fd, const void *data, size_t len) {
  if (!ffs_fdok(fd)) return -EBADF;
  ffs_file *f = &ffs_f[fd - 10];
  if (!f->writing) return -EBADF;
  const uint8_t *p = (const uint8_t *) data;
  size_t left = len;
  while (left) {
    size_t room = FFS_SECT - f->fill;
    size_t n = left < room ? left : room;
    memcpy(ffs_wbuf + f->fill, p, n);
    f->fill += n; f->size += n; p += n; left -= n;
    if (f->fill == FFS_SECT) {
      int err = ffs_flush(f);
      if (err) return -err;
    }
  }
  return (int) len;
}

static inline int ffs_close(int fd) {
  if (!ffs_fdok(fd)) return EBADF;
  ffs_file *f = &ffs_f[fd - 10];
  int err = 0;
  if (f->writing) {
    if (f->fill > 0) err = ffs_flush(f);
    if (!err) {
      static ffs_dir nd;
      memcpy(nd.raw, ffs_d.raw, FFS_SECT);
      int idx = ffs_find(&nd, f->name);
      if (idx >= 0) {
        ffs_free_chain(&nd, nd.d.e[idx].first);
      } else {
        for (int i = 0; i < FFS_ENTRIES; ++i)
          if (!nd.d.e[i].name[0]) { idx = i; break; }
      }
      if (idx < 0) {
        err = ENOSPC;
      } else {
        for (int s = FFS_FIRST; s <= FFS_LAST; ++s) if (ffs_pend[s]) nd.d.next[s] = ffs_pn[s];
        memcpy(nd.d.e[idx].name, f->name, FFS_NAME);
        nd.d.e[idx].first = f->first;
        nd.d.e[idx].pad = 0;
        nd.d.e[idx].size = f->size;
        err = ffs_commit(&nd);
      }
    }
    ffs_abort_writer();
  }
  f->used = 0;
  return err;
}

// Reads at the position; returns the count or -errno.
static inline int ffs_read(int fd, void *buf, size_t len) {
  if (!ffs_fdok(fd)) return -EBADF;
  ffs_file *f = &ffs_f[fd - 10];
  if (f->writing) return -EBADF;
  uint8_t *out = (uint8_t *) buf;
  size_t done = 0;
  while (done < len && f->pos < f->size) {
    uint32_t k = f->pos / FFS_SECT, off = f->pos % FFS_SECT;
    int s = f->first;
    for (uint32_t i = 0; i < k && s >= FFS_FIRST && s <= FFS_LAST; ++i) s = ffs_d.d.next[s];
    if (s < FFS_FIRST || s > FFS_LAST) return -EIO;
    size_t n = FFS_SECT - off;
    if (n > len - done) n = len - done;
    if (n > f->size - f->pos) n = f->size - f->pos;
    memcpy(out + done, ffs_ptr(s) + off, n);
    done += n;
    f->pos += n;
  }
  return (int) done;
}

static inline int ffs_size(int fd) {
  if (!ffs_fdok(fd)) return -EBADF;
  return (int) ffs_f[fd - 10].size;
}
static inline int ffs_tell(int fd) {
  if (!ffs_fdok(fd)) return -EBADF;
  ffs_file *f = &ffs_f[fd - 10];
  return (int) (f->writing ? f->size : f->pos);
}
static inline int ffs_seek(int fd, uint32_t pos) {
  if (!ffs_fdok(fd)) return -EBADF;
  ffs_file *f = &ffs_f[fd - 10];
  if (f->writing) return -EINVAL;
  f->pos = pos > f->size ? f->size : pos;
  return 0;
}

static inline int ffs_delete(const char *name, size_t len) {
  ffs_mount();
  if (!ffs_usable) return EIO;
  char n[FFS_NAME];
  if (ffs_name(name, len, n)) return ENOENT;
  int idx = ffs_find(&ffs_d, n);
  if (idx < 0) return ENOENT;
  static ffs_dir nd;
  memcpy(nd.raw, ffs_d.raw, FFS_SECT);
  ffs_free_chain(&nd, nd.d.e[idx].first);
  memset(&nd.d.e[idx], 0, sizeof(ffs_entry));
  return ffs_commit(&nd);
}

static inline int ffs_rename(const char *from, size_t flen, const char *to, size_t tlen) {
  ffs_mount();
  if (!ffs_usable) return EIO;
  char a[FFS_NAME], b[FFS_NAME];
  if (ffs_name(from, flen, a)) return ENOENT;
  if (ffs_name(to, tlen, b)) return EINVAL;
  int i = ffs_find(&ffs_d, a);
  if (i < 0) return ENOENT;
  static ffs_dir nd;
  memcpy(nd.raw, ffs_d.raw, FFS_SECT);
  int j = ffs_find(&nd, b);
  if (j >= 0 && j != i) {
    ffs_free_chain(&nd, nd.d.e[j].first);
    memset(&nd.d.e[j], 0, sizeof(ffs_entry));
  }
  memcpy(nd.d.e[i].name, b, FFS_NAME);
  return ffs_commit(&nd);
}

// ---- Listing: opendir, readdir ----

static int ffs_dirpos = 0;
static inline cell_t ffs_opendir(const char *name, size_t len) {
  ffs_mount();
  while (len > 0 && *name == '/') { ++name; --len; }
  if (len != 0 && !(len == 1 && name[0] == '.')) return 0;
  ffs_dirpos = 0;
  return 1;
}
static inline const char *ffs_readdir(void) {
  while (ffs_dirpos < FFS_ENTRIES) {
    ffs_entry *e = &ffs_d.d.e[ffs_dirpos++];
    if (e->name[0]) return e->name;
  }
  return 0;
}

// What is left: free sectors, for the words that ask
static inline int ffs_free_sectors(void) {
  ffs_mount();
  int n = 0;
  for (int i = FFS_FIRST; i <= FFS_LAST; ++i) if (ffs_d.d.next[i] == 0) ++n;
  return n;
}

#endif
