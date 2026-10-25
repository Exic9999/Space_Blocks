"""Tiny immediate-mode UI helpers: fonts, panels, bars, clickable hit regions."""
import pygame
from config import *

_fonts = {}


def font(size, bold=False):
    k = (size, bold)
    f = _fonts.get(k)
    if f is None:
        f = pygame.font.SysFont("dejavusans,arial,helvetica", size, bold=bold)
        _fonts[k] = f
    return f


_txt = {}


def text(surf, s, pos, size=16, color=C_TEXT, bold=False, anchor="topleft", alpha=255):
    key = (s, size, color, bold)
    img = _txt.get(key)
    if img is None:
        if len(_txt) > 1500:
            _txt.clear()
        img = font(size, bold).render(s, True, color)
        _txt[key] = img
    r = img.get_rect(**{anchor: pos})
    if alpha < 255:
        img = img.copy()
        img.set_alpha(alpha)
    surf.blit(img, r)
    return r


def panel(surf, rect, title=None, alpha=215, edge=C_EDGE):
    rect = pygame.Rect(rect)
    s = pygame.Surface(rect.size, pygame.SRCALPHA)
    pygame.draw.rect(s, (*C_PANEL, alpha), s.get_rect(), border_radius=8)
    pygame.draw.rect(s, (*edge, 190), s.get_rect(), 1, border_radius=8)
    pygame.draw.line(s, (*C_ACCENT, 120), (10, 1), (46, 1), 2)
    surf.blit(s, rect)
    if title:
        text(surf, title, (rect.x + 12, rect.y + 6), 13, C_ACCENT, True)


def bar(surf, rect, frac, color, back=(25, 35, 55)):
    rect = pygame.Rect(rect)
    pygame.draw.rect(surf, back, rect, border_radius=3)
    f = max(0.0, min(1.0, frac))
    if f > 0:
        pygame.draw.rect(surf, color, (rect.x, rect.y, max(2, int(rect.w * f)), rect.h), border_radius=3)
    pygame.draw.rect(surf, (70, 100, 130), rect, 1, border_radius=3)


class Hits:
    """Clickable regions registered while drawing; resolved on mouse click."""

    def __init__(self):
        self.items = []

    def clear(self):
        self.items = []

    def add(self, rect, fn, tip=None):
        self.items.append((pygame.Rect(rect), fn, tip))

    def click(self, pos):
        for r, fn, _ in reversed(self.items):
            if r.collidepoint(pos):
                fn()
                return True
        return False

    def tip_at(self, pos):
        for r, _, tip in reversed(self.items):
            if tip and r.collidepoint(pos):
                return tip
        return None


def button(surf, hits, rect, label, fn, mouse, enabled=True, color=C_ACCENT, size=14, tip=None, active=False):
    rect = pygame.Rect(rect)
    hover = enabled and rect.collidepoint(mouse)
    base = (24, 44, 74) if enabled else (18, 24, 36)
    if hover:
        base = (40, 78, 120)
    if active:
        base = (50, 100, 140)
    pygame.draw.rect(surf, base, rect, border_radius=6)
    pygame.draw.rect(surf, color if enabled else (60, 70, 90), rect, 1, border_radius=6)
    text(surf, label, rect.center, size, C_TEXT if enabled else (90, 100, 120), True, "center")
    if enabled:
        hits.add(rect, fn, tip)
    return hover
