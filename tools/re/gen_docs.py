"""Genera los apéndices de docs/re a partir de las tablas extraídas de gamex86.dll 1.5 (SP).

Uso: python gen_docs.py <gamex86.dll> <dir_docs_re>
Escribe: gsc-api.md, armas-campos.md, dvars.md
"""
import sys, os
from petabla import PE

pe = PE(sys.argv[1])
out = sys.argv[2]
_text = next(sec for sec in pe.secs if sec[0] == '.text')
TEXT_LO, TEXT_HI = pe.base + _text[1], pe.base + _text[1] + _text[2]


def name_at(va):
    p = pe.u32(va)
    return pe.cstr(p, 64) if p and pe.va2off(p) is not None else None


def builtins(start, stride):
    rows, va = [], start
    while True:
        n = name_at(va)
        if not n:
            return rows
        fn = pe.u32(va + 4)
        dev = pe.u32(va + 8) if stride == 12 else 0
        if not (TEXT_LO <= fn < TEXT_HI) or dev not in (0, 1):
            return rows
        rows.append((n, fn, dev))
        va += stride


def fields(start, stride):
    rows, va = [], start
    while True:
        n = name_at(va)
        if not n:
            return rows
        rows.append((n, [pe.u32(va + i) for i in range(4, stride, 4)]))
        va += stride


def cols(names, k=6):
    names = [f'`{n}`' for n in names]
    lines = []
    for i in range(0, len(names), k):
        lines.append('| ' + ' | '.join(names[i:i + k] + [''] * (k - len(names[i:i + k]))) + ' |')
    return '\n'.join(['| ' + ' | '.join([' '] * k) + ' |', '|' + '---|' * k] + lines)


SCR_TYPE = {0: 'int', 1: 'short', 2: 'byte', 3: 'float', 4: 'cadena', 5: 'vector', 6: 'entidad',
            7: 'entidad', 8: 'entidad', 9: '?', 10: 'nodo', 11: 'yaw → vector', 12: 'modelo',
            13: '(ignorado)'}

g = []
g.append('# API de GSC: built-ins y campos (gamex86.dll 1.5, un jugador)\n')
g.append('Apéndice generado con `tools/re/gen_docs.py` a partir de las tablas de registro de '
         '`gamex86.dll` (hash en `version.md`). La prosa está en `gsc.md`. Solo nombres, '
         'direcciones y tipos.\n')
g.append('Cada tabla es una lista `{nombre, función[, developer]}` terminada en `NULL`. Las '
         'funciones marcadas con `*` solo existen con `developer 1` (el compilador las ignora si no).\n')

tables = [
    ('Funciones globales', 0x200a4a00, 12),
    ('Métodos de entidad (`self` = cualquier entidad)', 0x2008a828, 8),
    ('Métodos de actor (IA)', 0x20089540, 8),
    ('Métodos de "sentient" (actores y jugador)', 0x200891d8, 8),
    ('Métodos de jugador', 0x2008b1f8, 8),
    ('Métodos de vehículo', 0x2008a600, 8),
    ('Métodos de elemento de HUD (`newhudelem`)', 0x2008b128, 8),
]
total = 0
for title, va, stride in tables:
    rows = builtins(va, stride)
    total += len(rows)
    g.append(f'## {title}\n')
    g.append(f'Tabla en `{va:#x}`, {len(rows)} entradas.\n')
    g.append(cols([n + ('*' if d else '') for n, _, d in rows]))
    g.append('')
g.insert(3, f'Total: **{total}** built-ins en {len(tables)} tablas.\n')

g.append('## Clases de entidad con función de spawn\n')
rows = builtins(0x2008a500, 8)
g.append(f'Tabla `{{classname, spawn}}` en `0x2008a500`, {len(rows)} entradas. Las clases que no '
         'están aquí (`worldspawn`, `actor_*`, `node_*`, `item_*`, `weapon_*`, `misc_mg42`...) se '
         'tratan por otras vías (ver `entidades.md`).\n')
g.append(cols([n for n, _, _ in rows]))
g.append('')

g.append('## Campos accesibles desde GSC\n')
g.append('Tipos (switch del lector de campos, `0x20041b60`, y del parser de claves del BSP, '
         '`0x20040f70`): ' + ', '.join(f'{k} = {v}' for k, v in SCR_TYPE.items()) + '. '
         'El índice de campo que maneja la VM lleva la clase en los bits 14-15: `0x0000` entidad, '
         '`0x4000` sentient, `0x8000` actor, `0xc000` cliente (tabla vacía en un jugador).\n')
for title, va, stride, note in [
    ('Entidad (`gentity_t`) — también son las claves que se leen del BSP', 0x2008a200, 16,
     'Columnas: desplazamiento en `gentity_t`, tipo, función propia de escritura (si no es 0).'),
    ('Actor', 0x20089760, 20, 'Columnas: desplazamiento en `actor_t`, tipo, función de escritura, función de lectura.'),
    ('Sentient', 0x20089200, 20, 'Columnas: desplazamiento en `sentient_t`, tipo, escritura, lectura.'),
    ('Elemento de HUD', 0x2008b048, 20, 'Columnas: desplazamiento, tipo, escritura, lectura.'),
    ('Nodo de caminos (`getnode`)', 0x200a0098, 16, 'Columnas: desplazamiento en `pathnode_t`, tipo, escritura.'),
    ('Nodo de vehículo (`getvehiclenode`)', 0x200a4768, 12, 'Columnas: desplazamiento, tipo.'),
]:
    rows = fields(va, stride)
    g.append(f'### {title}\n')
    g.append(f'Tabla en `{va:#x}`, {len(rows)} campos. {note}\n')
    g.append('| Campo | Desp. | Tipo | Funciones |')
    g.append('|---|---|---|---|')
    for n, w in rows:
        extra = ' '.join(f'`{x:#x}`' for x in w[2:] if x)
        g.append(f'| `{n}` | {w[0]} | {SCR_TYPE.get(w[1], w[1])} | {extra} |')
    g.append('')
open(os.path.join(out, 'gsc-api.md'), 'w', encoding='utf-8').write('\n'.join(g) + '\n')

# Armas
WTYPE = {0: 'cadena', 1: 'cadena[1024]', 2: 'cadena[64]', 3: 'cadena[256]', 4: 'int', 5: 'bool',
         6: 'float', 7: 'ms (segundos × 1000, truncado)', 8: 'enum weaponType', 9: 'enum weaponClass',
         10: 'enum adsOverlayReticle', 11: 'enum weaponSlot', 12: 'enum stance',
         13: 'enum projExplosionType'}
rows = fields(0x200a63b0, 12)
a = ['# Archivo de arma: campos (gamex86.dll 1.5, un jugador)\n',
     'Apéndice generado con `tools/re/gen_docs.py`. Tabla `{clave, desplazamiento, tipo}` en '
     f'`0x200a63b0`, {len(rows)} campos (se pasan 0xf8 = 248 al parser). La prosa está en `armas.md`.\n',
     '| Clave | Desp. | Tipo |', '|---|---|---|']
for n, w in rows:
    a.append(f'| `{n}` | {w[0]} | {WTYPE.get(w[1], w[1])} |')
open(os.path.join(out, 'armas-campos.md'), 'w', encoding='utf-8').write('\n'.join(a) + '\n')

# Dvars
def dvar(va):
    """Registro de cvarTable válido: nombre, valor (puede ser ""), flags < 0x10000, trackChange 0/1."""
    n = name_at(va + 4)
    p = pe.u32(va + 8)
    if not n or not p or pe.va2off(p) is None:
        return None
    o = pe.va2off(p)
    v = '' if pe.data[o] == 0 else pe.cstr(p, 200)
    f, t = pe.u32(va + 12), pe.u32(va + 20)
    if v is None or f is None or f >= 0x10000 or t not in (0, 1):
        return None
    return n, v, f, t


va = 0x200a5068
while dvar(va - 24):
    va -= 24
rows = []
while dvar(va):
    rows.append(dvar(va))
    va += 24
d = ['# Dvars del juego (gamex86.dll 1.5, un jugador)\n',
     'Apéndice generado con `tools/re/gen_docs.py` desde la tabla `cvarTable` de `gamex86.dll` '
     '(registros de 24 bytes `{vmCvar*, nombre, valor, flags, modificationCount, trackChange}`, '
     f'como `g_main.c` de Quake 3), {len(rows)} entradas. Los flags son los de `cvar_t` del motor; '
     'su significado exacto en CoD1 está por confirmar (`0x200` aparece en todas las de depuración '
     'y `0x8` en `pmove_*`).\n',
     '| Dvar | Valor por defecto | Flags | Avisa al cambiar |', '|---|---|---|---|']
for n, v, f, t in rows:
    d.append(f'| `{n}` | `{v}` | `{f:#x}` | {"sí" if t else ""} |')
open(os.path.join(out, 'dvars.md'), 'w', encoding='utf-8').write('\n'.join(d) + '\n')
print('ok', total)
