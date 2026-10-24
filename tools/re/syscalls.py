"""Recorre una tabla de saltos de un despachador `switch` (p. ej. las llamadas del juego al
ejecutable) y, por cada caso, lista las funciones llamadas y las cadenas que usan.

Uso: python syscalls.py <pe> <tabla_va> <n_casos> <strings.txt de ExportFoley> > salida.txt
"""
import sys, collections
from capstone import Cs, CS_ARCH_X86, CS_MODE_32
from petabla import PE

pe = PE(sys.argv[1])
table, count = int(sys.argv[2], 16), int(sys.argv[3])
md = Cs(CS_ARCH_X86, CS_MODE_32)

# función -> cadenas que referencia (formato de strings.txt: va \t texto \t refs "from(func)")
fstr = collections.defaultdict(list)
for line in open(sys.argv[4], encoding='utf-8', errors='replace'):
    parts = line.rstrip('\n').split('\t')
    if len(parts) < 3:
        continue
    for ref in parts[2].split():
        if '(' in ref:
            fstr[int(ref[ref.index('(') + 1:-1], 16)].append(parts[1][:60])


def block(va, limit=80):
    """Instrucciones del caso hasta ret o salto incondicional."""
    off = pe.va2off(va)
    out = []
    for i, ins in enumerate(md.disasm(pe.data[off:off + limit * 15], va)):
        out.append(ins)
        if ins.mnemonic == 'ret' or ins.mnemonic == 'jmp' or i >= limit:
            break
    return out


for k in range(count):
    target = pe.u32(table + 4 * k)
    calls, strs = [], []
    for ins in block(target):
        if ins.mnemonic in ('call', 'jmp') and ins.op_str.startswith('0x'):
            calls.append(int(ins.op_str, 16))
        if ins.mnemonic == 'push' and ins.op_str.startswith('0x'):
            s = pe.cstr(int(ins.op_str, 16), 60)
            if s and len(s) > 2:
                strs.append(s)
    desc = []
    for c in calls:
        hint = fstr.get(c, [])[:2]
        desc.append(f'{c:#x}' + (f' {hint}' if hint else ''))
    print(f'{k:#04x} -> {target:#x}  ' + ' | '.join(desc) + (f'  str={strs}' if strs else ''))
