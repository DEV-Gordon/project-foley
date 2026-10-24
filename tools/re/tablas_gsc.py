"""Vuelca las tablas {nombre, función, developer} de built-ins de GSC de gamex86.dll.

Busca tablas de registros de 12 bytes: puntero a cadena ASCII, puntero a .text, entero 0/1.
Uso: python tablas_gsc.py <gamex86.dll> <salida.tsv>
"""
import sys
from petabla import PE

pe = PE(sys.argv[1])
text = next(s for s in pe.secs if s[0] == '.text')
tlo, thi = pe.base + text[1], pe.base + text[1] + text[2]


def entry(va, stride):
    name = pe.u32(va)
    fn = pe.u32(va + 4)
    if name is None or fn is None:
        return None
    s = pe.cstr(name, 64)
    if not s or not (tlo <= fn < thi):
        return None
    if not all(c.isalnum() or c == '_' for c in s):
        return None
    dev = pe.u32(va + 8) if stride == 12 else 0
    if dev not in (0, 1):
        return None
    return s, fn, dev


tables = []
for name, sva, vsz, raw, rsz in pe.secs:
    if name not in ('.rdata', '.data'):
        continue
    va = pe.base + sva
    end = va + min(vsz, rsz)
    while va < end - 12:
        for stride in (12, 8):
            run = []
            p = va
            while True:
                e = entry(p, stride)
                if not e:
                    break
                run.append((p, *e))
                p += stride
            if len(run) >= 3:
                tables.append((va, stride, run))
                va = p
                break
        else:
            va += 4

with open(sys.argv[2], 'w') as w:
    for va, stride, run in tables:
        print(f'tabla {va:#x} paso {stride} entradas {len(run)}: {run[0][1]} .. {run[-1][1]}')
        for p, s, fn, dev in run:
            w.write(f'{va:#x}\t{p:#x}\t{s}\t{fn:#x}\t{dev}\n')
