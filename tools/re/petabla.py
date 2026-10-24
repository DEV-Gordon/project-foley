"""Utilidades para leer tablas de un PE de 32 bits (DLL en claro o volcado desmapeado).

Uso: python petabla.py <pe> <cadena>  -> direcciones virtuales de la cadena y punteros a ella.
"""
import struct, sys


class PE:
    def __init__(self, path):
        self.data = open(path, 'rb').read()
        d = self.data
        pe = struct.unpack_from('<I', d, 0x3c)[0]
        nsec = struct.unpack_from('<H', d, pe + 6)[0]
        optsz = struct.unpack_from('<H', d, pe + 20)[0]
        self.base = struct.unpack_from('<I', d, pe + 24 + 28)[0]
        self.secs = []
        off = pe + 24 + optsz
        for i in range(nsec):
            name = d[off:off + 8].rstrip(b'\0').decode('latin1')
            vsize, va, rsize, raw = struct.unpack_from('<IIII', d, off + 8)
            self.secs.append((name, va, vsize, raw, rsize))
            off += 40

    def va2off(self, va):
        rva = va - self.base
        for name, sva, vsz, raw, rsz in self.secs:
            if sva <= rva < sva + max(vsz, rsz):
                o = raw + rva - sva
                return o if o < len(self.data) else None
        return None

    def off2va(self, off):
        for name, sva, vsz, raw, rsz in self.secs:
            if raw <= off < raw + rsz:
                return self.base + sva + off - raw
        return None

    def u32(self, va):
        o = self.va2off(va)
        return None if o is None or o + 4 > len(self.data) else struct.unpack_from('<I', self.data, o)[0]

    def f32(self, va):
        o = self.va2off(va)
        return None if o is None else struct.unpack_from('<f', self.data, o)[0]

    def cstr(self, va, maxlen=256):
        o = self.va2off(va)
        if o is None:
            return None
        e = self.data.find(b'\0', o, o + maxlen)
        if e < 0:
            return None
        s = self.data[o:e]
        try:
            t = s.decode('ascii')
        except UnicodeDecodeError:
            return None
        return t if t and all(32 <= ord(c) < 127 or c in '\t\n' for c in t) else None

    def find_str(self, s):
        """VAs de la cadena exacta (terminada en NUL)."""
        pat = s.encode() + b'\0'
        res, i = [], 0
        while True:
            i = self.data.find(pat, i)
            if i < 0:
                return res
            if i == 0 or self.data[i - 1] == 0:
                va = self.off2va(i)
                if va:
                    res.append(va)
            i += 1

    def find_ptr(self, va):
        pat = struct.pack('<I', va)
        res, i = [], 0
        while True:
            i = self.data.find(pat, i)
            if i < 0:
                return res
            v = self.off2va(i)
            if v:
                res.append(v)
            i += 1


if __name__ == '__main__':
    pe = PE(sys.argv[1])
    print('base', hex(pe.base), [(n, hex(v)) for n, v, *_ in pe.secs])
    for va in pe.find_str(sys.argv[2]):
        print(hex(va), 'ptrs', [hex(p) for p in pe.find_ptr(va)])
