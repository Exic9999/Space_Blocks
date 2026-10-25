# Space Blocks — progress log (resume point if disconnected)

Run: `./run.sh`   (venv: `.venv`, Python 3.12 + pygame-ce + numpy)
Rebuild venv: `uv venv --python 3.12 .venv && uv pip install --python .venv/bin/python pygame-ce numpy`
Headless test: `SDL_VIDEODRIVER=dummy SDL_AUDIODRIVER=dummy .venv/bin/python tests/smoke.py`

## Design
2D top-down, block-grid ships. Commander Jason runs The Executor (wedge mothership).
NPC pilots fly mini ships to floating islands for Aetherite, dock in the hangar to refuel/repair.
Orders from commander; if ignored too long, NPCs go self-directed.

## Modules / status
- [ ] skeleton + state machine (main.py)
- [x] config.py, ships.py, crew.py, npc_ai.py, world.py (headless sim, tests/sim_test.py passes)
- [ ] save.py (slots + autosave)
- [ ] islands.py (agent: opus)
- [ ] music.py (agent: opus)
- [ ] ships.py / executor.py / npc_ai.py
- [ ] hud.py (radar, island lock-on)
- [ ] story.py
- [ ] builder UI
- [ ] smoke test + screenshots
