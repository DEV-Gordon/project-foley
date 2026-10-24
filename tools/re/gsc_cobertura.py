"""Compara las llamadas de los .gsc de la instalación con los built-ins de gamex86.dll.

Aproximado (expresiones regulares, sin parser): una llamada es `nombre(` no precedida de `::`
ni de `function`, y que no es una función definida en algún .gsc. Imprime los nombres llamados
que no están ni en los built-ins ni en las palabras clave del lenguaje.

Uso: python gsc_cobertura.py <dir main con pk3> <gsc-api.md>
"""
import glob, re, sys, zipfile

KEYWORDS = {'if', 'while', 'for', 'switch', 'return', 'wait', 'waittill', 'waittillmatch',
            'waittillframeend', 'notify', 'endon', 'thread', 'call', 'size', 'isdefined',
            'breakpoint', 'case', 'default', 'else', 'break', 'continue', 'self', 'level',
            'game', 'anim', 'undefined', 'true', 'false'}

_doc = open(sys.argv[2], encoding='utf-8').read().lower()
_doc = _doc[:_doc.index('## clases de entidad')]
_rows = '\n'.join(re.findall(r'^\|.*', _doc, flags=re.M))
api = set(re.findall(r'`([a-z0-9_]+)\*?`', _rows))

sources = {}
for pk3 in sorted(glob.glob(sys.argv[1] + '/*.pk3')):
    with zipfile.ZipFile(pk3) as z:
        for n in z.namelist():
            if n.lower().endswith('.gsc'):
                sources[n.lower()] = z.read(n).decode('latin1')

defined, called = set(), {}
for path, src in sources.items():
    src = re.sub(r'//.*', '', src)
    src = re.sub(r'/\*.*?\*/', '', src, flags=re.S)
    for m in re.finditer(r'^\s*([A-Za-z_][A-Za-z0-9_]*)\s*\([^;{]*\)\s*\{', src, flags=re.M):
        defined.add(m.group(1).lower())
    for m in re.finditer(r'(?<![:\w.])([A-Za-z_][A-Za-z0-9_]*)\s*\(', src):
        called.setdefault(m.group(1).lower(), set()).add(path)

missing = sorted(n for n in called if n not in api and n not in defined and n not in KEYWORDS)
print(f'{len(sources)} .gsc, {len(defined)} funciones definidas, {len(called)} nombres llamados, '
      f'{len(api)} built-ins en la API')
used = sorted(n for n in called if n in api)
print(f'built-ins usados por algún script: {len(used)}')
print(f'built-ins nunca usados ({len(api - set(called))}): {sorted(api - set(called))}')
sp = [n for n in sorted(called) if n not in api and n not in defined and n not in KEYWORDS
      and any('/mp/' not in f for f in called[n])]
print(f'en scripts de un jugador y sin built-in (revisar a mano; hay falsos positivos): {sp}')
print(f'llamados sin definición ni built-in ({len(missing)}):')
for n in missing:
    print(f'  {n}  ({len(called[n])} archivos, p. ej. {sorted(called[n])[0]})')
