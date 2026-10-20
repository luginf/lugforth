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

// Several terminals on the bare-metal ports (Alt+F1 to Alt+F4, or the word
// term): each one is a Forth of its own (its heap, stacks, dictionary) and a
// screen of its own. The first part, here, is what the drivers need to know:
// which terminal runs now and which one has the screen and the keyboard.
// The scheduler and the creation of a terminal are in term_run.h, included
// after the interpreter. See that file.

#define TERM_MAX 4
static int term_cur;                 // the terminal that runs now
static int term_foc;                 // the one that has the screen and the keyboard
static int term_nlive;               // terminals open
static int term_want = -1;           // a switch asked for, done between two slices
#define TERM_FOCUSED() (term_cur == term_foc)

static void term_request(int n) { if (n >= 0 && n < TERM_MAX) term_want = n; }
static void term_poll(void);         // term_run.h: watch the keyboard for Alt+Fn while a program does not
static int term_bye(void);           // term_run.h: a closed terminal other than the first
