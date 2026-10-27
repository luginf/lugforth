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

// floor, fabs, fmin, fmax and sqrt are WebAssembly instructions. The others
// come from the host, which has a libm: imports of the module "tic80".
#ifndef TIC80_MATH_H
#define TIC80_MATH_H
#define TIC80_IMPORT(n) __attribute__((import_module("tic80"), import_name(#n)))
TIC80_IMPORT(sin) double sin(double x);
TIC80_IMPORT(cos) double cos(double x);
TIC80_IMPORT(atan2) double atan2(double y, double x);
TIC80_IMPORT(pow) double pow(double x, double y);
TIC80_IMPORT(exp) double exp(double x);
TIC80_IMPORT(log) double log(double x);
static inline double floor(double x) { return __builtin_floor(x); }
static inline double fabs(double x) { return __builtin_fabs(x); }
static inline double fmin(double x, double y) { return __builtin_fmin(x, y); }
static inline double fmax(double x, double y) { return __builtin_fmax(x, y); }
static inline double sqrt(double x) { return __builtin_sqrt(x); }
#endif
