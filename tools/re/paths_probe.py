"""Valida el formato del lump 31 (caminos de la IA) en los BSP de la instalación, de uno en uno.

Formato hipotético: i32 versión (7), u16 nº de nodos; por nodo u16 nº de enlaces y por enlace
(u16 nodo destino, f32 distancia); después la matriz de visibilidad entre nodos comprimida.
Uso: python paths_probe.py <dir main con pk3>
"""
import glob, re, struct, sys, zipfile


def lump(bsp, i):
    length, ofs = struct.unpack_from('<II', bsp, 8 + i * 8)
    return bsp[ofs:ofs + length]


def count_nodes(bsp):
    ents = lump(bsp, 29).decode('latin1')
    return len(re.findall(r'"classname"\s+"node_', ents))


def parse(b):
    p = 0
    ver, = struct.unpack_from('<i', b, p); p += 4
    n, = struct.unpack_from('<H', b, p); p += 2
    links = 0
    for _ in range(n):
        k, = struct.unpack_from('<H', b, p); p += 2
        p += k * 6
        links += k
    need = ((n - 1) * n + 7) // 8      # bits de visibilidad, como el cargador
    got = 0
    while got < need:
        c = struct.unpack_from('<b', b, p)[0]; p += 1
        if c < 0:
            got += ~c; p += ~c          # ~c bytes literales
        else:
            got += c + 1; p += 1        # c ceros y un byte literal
    return ver, n, links, len(b) - p, got - need


seen = set()
for pk3 in sorted(glob.glob(sys.argv[1] + '/*.pk3'), reverse=True):
    with zipfile.ZipFile(pk3) as z:
        for name in z.namelist():
            if not name.lower().endswith('.bsp') or name.lower() in seen:
                continue
            seen.add(name.lower())
            bsp = z.read(name)
            b = lump(bsp, 31)
            if not b:
                print(f'{name}: sin caminos ({count_nodes(bsp)} nodos en entidades)')
                continue
            try:
                ver, n, links, rest, extra = parse(b)
                print(f'{name}: v{ver} {n} nodos (entidades: {count_nodes(bsp)}), {links} enlaces, '
                      f'resto {rest} B, exceso RLE {extra}')
            except Exception as e:
                print(f'{name}: FALLA {e!r}')
            del bsp
