"""Vuelca una tabla de campos {char *nombre, int ..., } a partir de una dirección dentro de ella.

Deduce el paso (la distancia entre punteros a nombre consecutivos), retrocede al inicio y avanza
hasta el final. Uso: python campos.py <pe> <va_dentro> [<va_dentro> ...]
"""
import sys
from petabla import PE

pe = PE(sys.argv[1])


def is_name(va):
    p = pe.u32(va)
    s = pe.cstr(p, 64) if p else None
    return s if s and all(c.isalnum() or c in '_.' for c in s) else None


def dump(inside):
    # paso: menor k (4..64) tal que inside+k y inside-k o inside+2k sean nombres
    for k in range(4, 68, 4):
        if is_name(inside) and is_name(inside + k) and is_name(inside + 2 * k):
            break
    else:
        print('sin paso para', hex(inside)); return
    start = inside
    while is_name(start - k):
        start -= k
    va = start
    print(f'# tabla {start:#x} paso {k}')
    while is_name(va):
        words = [pe.u32(va + i) for i in range(4, k, 4)]
        ws = ' '.join(f'{w:#x}' if w and w > 0xffff else str(w) for w in words)
        print(f'{va:#x}\t{is_name(va)}\t{ws}')
        va += k
    print(f'# fin {va:#x} entradas {(va - start) // k}')


for a in sys.argv[2:]:
    dump(int(a, 16))
