#!/usr/bin/env bash
# Launch Space Blocks with the project venv (system python has no pygame).
cd "$(dirname "$0")"
exec .venv/bin/python main.py "$@"
