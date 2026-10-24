"""Comprueba una hipótesis del formato xanim (v14) contra todos los xanim/ de la instalación.

Lee de uno en uno (sin cargar todos a la vez) y cuenta cuántos se consumen exactamente.
Uso: python xanim_probe.py <dir main con pk3> [n_max]
"""
import glob, struct, sys, zipfile


class R:
    def __init__(self, b):
        self.b, self.p = b, 0

    def u8(self):
        v = self.b[self.p]; self.p += 1; return v

    def u16(self):
        v = struct.unpack_from('<H', self.b, self.p)[0]; self.p += 2; return v

    def i16(self):
        v = struct.unpack_from('<h', self.b, self.p)[0]; self.p += 2; return v

    def f32(self):
        v = struct.unpack_from('<f', self.b, self.p)[0]; self.p += 4; return v

    def cstr(self):
        e = self.b.index(0, self.p); s = self.b[self.p:e].decode('latin1'); self.p = e + 1; return s

    def skip(self, n):
        self.p += n


def keys(r, frames, small_index, per_key):
    """Pista de claves: u16 n; n=1 → un valor; 1<n<frames → índices + n valores; n≥frames → n valores."""
    n = r.u16()
    if n == 0:
        return 0
    if n == 1:
        per_key(r)
        return 1
    if n < frames:
        r.skip(n * (1 if small_index else 2))
    for _ in range(n):
        per_key(r)
    return n


def parse(b):
    r = R(b)
    ver = r.u16()
    assert ver == 14, ver
    nframes, nbones = r.u16(), r.u16()
    flags = r.u8()
    rate = r.u16()
    looping, delta = flags & 1, flags & 2
    frames = nframes + (1 if looping else 0)
    small = frames <= 256
    if delta:
        keys(r, frames, small, lambda r: r.i16())                  # giro: z del cuaternión (w calculado)
        keys(r, frames, small, lambda r: r.skip(12))               # traslación: 3 floats
    nb = (nbones - 1 >> 3) + 1 if nbones else 0
    flip = r.b[r.p:r.p + nb]; r.skip(nb)
    simple = r.b[r.p:r.p + nb]; r.skip(nb)
    names = [r.cstr() for _ in range(nbones)]
    for i in range(nbones):
        is_simple = (simple[i >> 3] >> (i & 7)) & 1
        keys(r, frames, small, (lambda r: r.skip(2)) if is_simple else (lambda r: r.skip(6)))
        keys(r, frames, small, lambda r: r.skip(12))
    notes = []
    for _ in range(r.u8()):                                      # notetracks: nombre + cuadro
        notes.append((r.cstr(), r.u16()))
    tail = len(b) - r.p
    return dict(frames=nframes, bones=nbones, flags=flags, rate=rate, tail=tail, at=r.p, notes=notes)


if __name__ == '__main__':
    lim = int(sys.argv[2]) if len(sys.argv) > 2 else 10 ** 9
    ok = bad = 0
    tails = {}
    seen = set()
    for pk3 in sorted(glob.glob(sys.argv[1] + '/*.pk3'), reverse=True):
        with zipfile.ZipFile(pk3) as z:
            for n in z.namelist():
                if not n.lower().startswith('xanim/') or n.endswith('/') or n.lower() in seen:
                    continue
                seen.add(n.lower())
                if ok + bad >= lim:
                    break
                b = z.read(n)
                try:
                    d = parse(b)
                    ok += 1
                    tails.setdefault(d['tail'], []).append((n, b[d['at']:d['at'] + 40]))
                except Exception as e:
                    bad += 1
                    if bad <= 5:
                        print('FALLA', n, repr(e))
    print('ok', ok, 'fallos', bad, '(resto distinto de 0 = formato mal entendido)')
    for t in sorted(tails, key=lambda t: -len(tails[t]))[:8]:
        print('resto', t, 'bytes en', len(tails[t]), 'archivos; ej', tails[t][0][0], tails[t][0][1][:40])
