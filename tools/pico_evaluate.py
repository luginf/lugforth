#!/usr/bin/env python3
"""Evaluates a Forth file on a Raspberry Pi Pico: tools/board_evaluate.py with the board chosen."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import board_evaluate

if __name__ == '__main__':
    board_evaluate.main('pico')
