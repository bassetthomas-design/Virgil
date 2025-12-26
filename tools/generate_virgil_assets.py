"""Generate Virgil avatar static and animated assets without external deps."""
from __future__ import annotations

import math
import struct
import zlib
from dataclasses import dataclass
from math import sin, tau
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] / "src" / "Virgil.App" / "assets" / "virgil"
SIZES = (1024, 256, 64)


@dataclass(frozen=True)
class MoodVisual:
    name: str
    bar_color: tuple[int, int, int]
    brow_tilt: float  # -1 calm to +1 alert
    eye_drop: float  # -0.15 relaxed to +0.15 widened
    mouth_curve: float  # -1 frown, 0 neutral, +1 slight smile
    stress_marks: bool = False
    alert_symbol: bool = False
    glow_halo: bool = False


MOODS: list[MoodVisual] = [
    MoodVisual("normal", (46, 214, 187), -0.15, -0.05, 0.0),
    MoodVisual("stress", (255, 150, 46), 0.2, 0.05, -0.15, stress_marks=True),
    MoodVisual("critical", (230, 64, 64), 0.5, 0.12, -0.25, alert_symbol=True, glow_halo=True),
]

SKIN = (228, 232, 238, 255)
OUTLINE = (150, 158, 170, 255)
EYE = (32, 38, 48, 255)
BROW = (38, 46, 58, 255)
MOUTH = (58, 68, 82, 255)
MARK = (120, 200, 230, 180)
BACKGROUND = (0, 0, 0, 0)


def ensure_dirs():
    for sub in (
        "static/1024",
        "static/256",
        "static/64",
        "anim/blink/1024",
        "anim/blink/256",
        "anim/blink/64",
        "anim/glow/normal/1024",
        "anim/glow/normal/256",
        "anim/glow/normal/64",
        "anim/glow/stress/1024",
        "anim/glow/stress/256",
        "anim/glow/stress/64",
        "anim/glow/critical/1024",
        "anim/glow/critical/256",
        "anim/glow/critical/64",
    ):
        (ROOT / sub).mkdir(parents=True, exist_ok=True)


class Canvas:
    def __init__(self, width: int, height: int, bg: tuple[int, int, int, int] = BACKGROUND):
        self.width = width
        self.height = height
        r, g, b, a = bg
        self.pixels = [[[r, g, b, a] for _ in range(width)] for _ in range(height)]

    def _blend(self, dst: list[int], src: tuple[int, int, int, int]):
        sr, sg, sb, sa = src
        if sa == 0:
            return
        dr, dg, db, da = dst
        src_a = sa / 255.0
        dst_a = da / 255.0
        out_a = src_a + dst_a * (1 - src_a)
        if out_a == 0:
            dst[:] = [0, 0, 0, 0]
            return
        out_r = (sr * src_a + dr * dst_a * (1 - src_a)) / out_a
        out_g = (sg * src_a + dg * dst_a * (1 - src_a)) / out_a
        out_b = (sb * src_a + db * dst_a * (1 - src_a)) / out_a
        dst[:] = [int(out_r + 0.5), int(out_g + 0.5), int(out_b + 0.5), int(out_a * 255 + 0.5)]

    def set_pixel(self, x: int, y: int, color: tuple[int, int, int, int]):
        if 0 <= x < self.width and 0 <= y < self.height:
            self._blend(self.pixels[y][x], color)

    def fill_rect(self, x0: float, y0: float, x1: float, y1: float, color: tuple[int, int, int, int]):
        ix0, iy0, ix1, iy1 = map(int, (math.floor(x0), math.floor(y0), math.ceil(x1), math.ceil(y1)))
        for y in range(iy0, iy1):
            if 0 <= y < self.height:
                row = self.pixels[y]
                for x in range(ix0, ix1):
                    if 0 <= x < self.width:
                        self._blend(row[x], color)

    def fill_ellipse(self, x0: float, y0: float, x1: float, y1: float, color: tuple[int, int, int, int]):
        cx = (x0 + x1) / 2
        cy = (y0 + y1) / 2
        rx = (x1 - x0) / 2
        ry = (y1 - y0) / 2
        if rx <= 0 or ry <= 0:
            return
        ix0, iy0, ix1, iy1 = map(int, (math.floor(x0), math.floor(y0), math.ceil(x1), math.ceil(y1)))
        for y in range(iy0, iy1):
            if not (0 <= y < self.height):
                continue
            dy = (y + 0.5 - cy) / ry
            dy2 = dy * dy
            if dy2 > 1:
                continue
            span = rx * math.sqrt(1 - dy2)
            sx0 = int(cx - span)
            sx1 = int(cx + span + 1)
            row = self.pixels[y]
            for x in range(sx0, sx1):
                if 0 <= x < self.width:
                    self._blend(row[x], color)

    def line(self, x0: float, y0: float, x1: float, y1: float, color: tuple[int, int, int, int], width: int = 1):
        dx = x1 - x0
        dy = y1 - y0
        steps = int(max(abs(dx), abs(dy))) + 1
        for i in range(steps):
            t = i / max(steps - 1, 1)
            x = x0 + dx * t
            y = y0 + dy * t
            for yy in range(int(y - width // 2), int(y + width // 2 + 1)):
                for xx in range(int(x - width // 2), int(x + width // 2 + 1)):
                    self.set_pixel(xx, yy, color)

    def save_png(self, path: Path):
        raw = bytearray()
        for row in self.pixels:
            raw.append(0)  # filter type 0
            for r, g, b, a in row:
                raw.extend([r, g, b, a])
        compressed = zlib.compress(bytes(raw))
        with path.open("wb") as f:
            f.write(b"\x89PNG\r\n\x1a\n")
            self._write_chunk(f, b"IHDR", struct.pack(">IIBBBBB", self.width, self.height, 8, 6, 0, 0, 0))
            self._write_chunk(f, b"IDAT", compressed)
            self._write_chunk(f, b"IEND", b"")

    @staticmethod
    def _write_chunk(f, chunk_type: bytes, data: bytes):
        f.write(struct.pack(">I", len(data)))
        f.write(chunk_type)
        f.write(data)
        crc = zlib.crc32(chunk_type)
        crc = zlib.crc32(data, crc)
        f.write(struct.pack(">I", crc))


def fill_polygon(canvas: Canvas, points: list[tuple[float, float]], color: tuple[int, int, int, int]):
    if not points:
        return
    xs, ys = zip(*points)
    min_y = int(math.floor(min(ys)))
    max_y = int(math.ceil(max(ys)))
    for y in range(min_y, max_y + 1):
        intersections = []
        for i in range(len(points)):
            x1, y1 = points[i]
            x2, y2 = points[(i + 1) % len(points)]
            if y1 == y2:
                continue
            if (y >= min(y1, y2)) and (y < max(y1, y2)):
                t = (y - y1) / (y2 - y1)
                intersections.append(x1 + (x2 - x1) * t)
        intersections.sort()
        for i in range(0, len(intersections), 2):
            if i + 1 < len(intersections):
                x_start = int(math.floor(intersections[i]))
                x_end = int(math.ceil(intersections[i + 1]))
                for x in range(x_start, x_end + 1):
                    canvas.set_pixel(x, y, color)


def draw_face(canvas: Canvas, size: int, mood: MoodVisual, blink: float, glow: float):
    scale = size / 1024
    w = canvas.width
    h = canvas.height
    cx = w * 0.48
    cy = h * 0.48

    face_w = w * 0.62
    face_h = h * 0.66
    face_bbox = (
        cx - face_w * 0.5,
        cy - face_h * 0.55,
        cx + face_w * 0.5,
        cy + face_h * 0.45,
    )
    canvas.fill_ellipse(*face_bbox, SKIN)
    outline_thickness = max(4, int(8 * scale))
    canvas.line(face_bbox[0], face_bbox[1], face_bbox[2], face_bbox[1], OUTLINE, outline_thickness)
    canvas.line(face_bbox[2], face_bbox[1], face_bbox[2], face_bbox[3], OUTLINE, outline_thickness)
    canvas.line(face_bbox[2], face_bbox[3], face_bbox[0], face_bbox[3], OUTLINE, outline_thickness)
    canvas.line(face_bbox[0], face_bbox[3], face_bbox[0], face_bbox[1], OUTLINE, outline_thickness)

    mark_w = max(4, int(10 * scale))
    for i in range(3):
        y = cy - face_h * 0.25 + i * mark_w * 1.6
        canvas.line(cx + face_w * 0.24, y, cx + face_w * 0.32, y, MARK, mark_w)
    canvas.fill_rect(
        cx - face_w * 0.42,
        cy + face_h * 0.2,
        cx - face_w * 0.38,
        cy + face_h * 0.32,
        MARK,
    )

    eye_w = face_w * 0.18
    eye_h_open = face_h * 0.095
    eye_spacing = face_w * 0.22
    blink_height = max(1.0, eye_h_open * (1 - blink))
    eye_y = cy - face_h * (0.05 + mood.eye_drop * 0.18)
    for offset in (-eye_spacing, eye_spacing):
        x0 = cx + offset - eye_w * 0.5
        y0 = eye_y - blink_height * 0.5
        x1 = cx + offset + eye_w * 0.5
        y1 = eye_y + blink_height * 0.5
        canvas.fill_rect(x0, y0, x1, y1, EYE)

    brow_y = eye_y - face_h * 0.12
    brow_w = eye_w * 1.1
    brow_tilt = mood.brow_tilt
    brow_thickness = max(4, int(12 * scale))
    for side in (-1, 1):
        tilt_dir = brow_tilt * side
        x0 = cx + side * eye_spacing - brow_w * 0.5
        x1 = cx + side * eye_spacing + brow_w * 0.5
        y0 = brow_y - tilt_dir * face_h * 0.08
        y1 = brow_y + tilt_dir * face_h * 0.08
        canvas.line(x0, y0, x1, y1, BROW, brow_thickness)

    mouth_w = face_w * 0.28
    mouth_y = cy + face_h * 0.16
    curve = mood.mouth_curve
    y_left = mouth_y + curve * face_h * 0.025
    y_right = mouth_y + curve * face_h * 0.025
    canvas.line(cx - mouth_w * 0.5, y_left, cx + mouth_w * 0.5, y_right, MOUTH, max(4, int(10 * scale)))

    if mood.stress_marks:
        stress_color = (240, 110, 56, 180)
        canvas.line(
            cx + face_w * 0.05,
            cy - face_h * 0.28,
            cx + face_w * 0.16,
            cy - face_h * 0.20,
            stress_color,
            max(4, int(8 * scale)),
        )
        canvas.line(
            cx + face_w * 0.1,
            cy - face_h * 0.20,
            cx + face_w * 0.2,
            cy - face_h * 0.12,
            stress_color,
            max(4, int(7 * scale)),
        )

    bar_w = max(18, int(52 * scale))
    bar_h = face_h * 0.86
    bar_x = cx + face_w * 0.52
    bar_y0 = cy - bar_h * 0.52
    bar_y1 = cy + bar_h * 0.48
    base_color = mood.bar_color
    glow_delta = int(255 * glow)
    color = (
        max(0, min(255, base_color[0] + glow_delta)),
        max(0, min(255, base_color[1] + glow_delta)),
        max(0, min(255, base_color[2] + glow_delta)),
        255,
    )
    canvas.fill_rect(bar_x, bar_y0, bar_x + bar_w, bar_y1, color)

    if mood.glow_halo:
        halo_color = (color[0], color[1], color[2], 70)
        canvas.fill_rect(bar_x - bar_w * 0.7, bar_y0 - bar_w, bar_x + bar_w * 1.7, bar_y1 + bar_w, halo_color)

    if mood.alert_symbol:
        tri_w = bar_w * 1.8
        tri_h = bar_w * 1.6
        tri_x = bar_x + bar_w * 0.5 - tri_w * 0.5
        tri_y = bar_y0 - tri_h * 1.3
        pts = [
            (tri_x, tri_y + tri_h),
            (tri_x + tri_w * 0.5, tri_y),
            (tri_x + tri_w, tri_y + tri_h),
        ]
        fill_polygon(canvas, pts, (color[0], color[1], color[2], 210))
        canvas.line(
            tri_x + tri_w * 0.5,
            tri_y + tri_h * 0.3,
            tri_x + tri_w * 0.5,
            tri_y + tri_h * 0.8,
            (30, 30, 30, 220),
            max(3, int(8 * scale)),
        )
        canvas.fill_rect(
            tri_x + tri_w * 0.45,
            tri_y + tri_h * 0.82,
            tri_x + tri_w * 0.55,
            tri_y + tri_h * 0.94,
            (30, 30, 30, 220),
        )


def render_frame(size: int, mood: MoodVisual, blink_level: float, glow_level: float) -> Canvas:
    canvas = Canvas(size, size, BACKGROUND)
    draw_face(canvas, size, mood, blink_level, glow_level)
    return canvas


def export_static():
    for mood in MOODS:
        for size in SIZES:
            canvas = render_frame(size, mood, blink_level=0.0, glow_level=0.0)
            target = ROOT / "static" / str(size) / f"virgil_{mood.name}.png"
            canvas.save_png(target)
            print("saved", target)


def export_blink():
    frames = [0.0, 0.55, 1.0]
    for size in SIZES:
        for idx, blink in enumerate(frames, start=1):
            canvas = render_frame(size, MOODS[0], blink_level=blink, glow_level=0.0)
            target = ROOT / "anim" / "blink" / str(size) / f"virgil_blink_{idx:02d}.png"
            canvas.save_png(target)
            print("saved", target)


def export_glow():
    frame_count = 8
    for mood in MOODS:
        for size in SIZES:
            for idx in range(frame_count):
                phase = idx / frame_count
                glow = sin(phase * tau) * 0.08  # +/-8%
                canvas = render_frame(size, mood, blink_level=0.0, glow_level=glow)
                target = ROOT / "anim" / "glow" / mood.name / str(size) / f"virgil_glow_{mood.name}_{idx+1:02d}.png"
                canvas.save_png(target)
                print("saved", target)


def main():
    ensure_dirs()
    export_static()
    export_blink()
    export_glow()


if __name__ == "__main__":
    main()
