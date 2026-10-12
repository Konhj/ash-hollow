import math
import random

import pygame


class Particles:
    def __init__(self):
        self.items = []

    def spawn(self, x, y, color, count, speed, life):
        for _ in range(count):
            ang = random.uniform(0, 6.2832)
            sp = random.uniform(speed * 0.4, speed)
            self.items.append({
                "x": x,
                "y": y,
                "vx": math.cos(ang) * sp,
                "vy": math.sin(ang) * sp,
                "life": random.uniform(life * 0.5, life),
                "max_life": life,
                "color": color,
                "size": random.randint(2, 4),
            })

    def update(self, dt):
        for p in self.items:
            p["x"] += p["vx"] * dt * 60
            p["y"] += p["vy"] * dt * 60
            p["vy"] += 0.06 * dt * 60
            p["life"] -= dt
        self.items = [p for p in self.items if p["life"] > 0]

    def draw(self, surf, ox=0, oy=0):
        for p in self.items:
            a = int(255 * p["life"] / p["max_life"])
            color = (*p["color"][:3], a)
            s = pygame.Surface((p["size"] * 2, p["size"] * 2), pygame.SRCALPHA)
            pygame.draw.circle(s, color, (p["size"], p["size"]), p["size"])
            surf.blit(s, (p["x"] - p["size"] - ox, p["y"] - p["size"] - oy))
