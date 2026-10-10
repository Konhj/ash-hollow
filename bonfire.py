import math
import random

import pygame

from settings import *


class Bonfire:
    def __init__(self, x, y):
        self.pos = (float(x), float(y))
        self.radius = 60
        self.t = 0.0

    def update(self, dt):
        self.t += dt

    def in_range(self, center):
        cx, cy = center
        return (cx - self.pos[0]) ** 2 + (cy - self.pos[1]) ** 2 <= self.radius ** 2

    def draw(self, surf):
        x, y = self.pos
        glow = pygame.Surface((180, 180), pygame.SRCALPHA)
        flicker = 90 + 25 * math.sin(self.t * 9) + random.uniform(-10, 10)
        pygame.draw.circle(glow, (240, 140, 40, int(flicker * 0.35)), (90, 90), 66)
        surf.blit(glow, (x - 90, y - 90))
        pygame.draw.line(surf, (90, 55, 30), (x - 26, y + 34), (x + 26, y + 34), 9)
        pygame.draw.line(surf, (110, 70, 40), (x - 30, y + 30), (x + 30, y + 30), 6)
        for i in range(5):
            fh = 26 + 8 * math.sin(self.t * (7 + i) + i * 2)
            fx = x + math.sin(self.t * 6 + i * 2.4) * 8
            color = (255, 160 - i * 12, 40 + i * 6)
            pygame.draw.circle(surf, color, (int(fx), int(y + 14 - i * 4)), max(2, int(10 - i * 1.4)))
        pygame.draw.line(surf, (180, 180, 190), (x + 6, y - 26), (x + 6, y + 22), 3)
        pygame.draw.line(surf, (120, 110, 60), (x, y - 18), (x + 12, y - 18), 3)
