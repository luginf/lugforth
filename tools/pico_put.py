#!/usr/bin/env python3
"""Send files to a Raspberry Pi Pico (flash): tools/board_put.py with the board chosen (see its help)."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import board_put

if __name__ == '__main__':
    if len(sys.argv) < 2:
        print(board_put.__doc__)
        sys.exit(1)
    sys.exit(board_put.main(sys.argv[1:], 'pico'))
