"""Lista la tabla de funciones de la VM que el ejecutable pasa al juego (volcado en ejecución).

Por cada entrada: dirección en el ejecutable, cadenas que usa (primeras instrucciones) y nº de
usos en el decompilado del juego (`DAT_200e99xx`).
Uso: python vm_api.py <volcado exe> <tabla_va> <n> <decomp.c del juego> <base_juego>
"""
import re, sys
from capstone import Cs, CS_ARCH_X86, CS_MODE_32
from petabla import PE

pe = PE(sys.argv[1])
table, n = int(sys.argv[2], 16), int(sys.argv[3])
game = open(sys.argv[4], encoding='latin1').read()
gbase = int(sys.argv[5], 16)
md = Cs(CS_ARCH_X86, CS_MODE_32)

# Si el volcado no tiene la tabla rellena, se toman las asignaciones del decompilado del exe
# (`_DAT_<entrada> = &LAB_<fn>` / `FUN_<fn>`), pasado como 6.º argumento.
assigned = {}
if len(sys.argv) > 6:
    for m in re.finditer(r'_?DAT_([0-9a-f]{8}) = &?(?:LAB|FUN)_([0-9a-f]{8});', open(sys.argv[6], encoding='latin1').read()):
        assigned.setdefault(int(m.group(1), 16), int(m.group(2), 16))

for i in range(n):
    fn = pe.u32(table + 4 * i) or assigned.get(table + 4 * i, 0)
    strs = []
    off = pe.va2off(fn) if fn else None
    if off is not None:
        follow = fn
        for k, ins in enumerate(md.disasm(pe.data[off:off + 600], fn)):
            if k == 0 and ins.mnemonic == 'jmp' and ins.op_str.startswith('0x'):
                follow = int(ins.op_str, 16)
                break
        o2 = pe.va2off(follow)
        for k, ins in enumerate(md.disasm(pe.data[o2:o2 + 900], follow)):
            if k > 220 or ins.mnemonic == 'ret':
                break
            for tok in re.findall(r'0x[0-9a-f]{6,}', ins.op_str):
                s = pe.cstr(int(tok, 16), 70)
                if s and len(s) > 3 and s not in strs:
                    strs.append(s)
    gaddr = gbase + 4 * i
    uses = len(re.findall(r'DAT_%08x\b' % gaddr, game))
    print(f'{i:3d} juego {gaddr:#x} exe {fn:#010x} usos {uses:4d}  {strs[:3]}')
