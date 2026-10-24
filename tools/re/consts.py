"""Constantes (floats/doubles en memoria, cadenas, inmediatos y llamadas) que usa cada función.
Uso: python consts.py <pe> <functions.txt> <va_hex> [<va_hex> ...]
"""
import sys, struct
from capstone import Cs, CS_ARCH_X86, CS_MODE_32
from petabla import PE

pe = PE(sys.argv[1])
sizes = {}
for line in open(sys.argv[2]):
    a, name, n = line.split('\t')
    sizes[int(a, 16)] = (name, int(n))
md = Cs(CS_ARCH_X86, CS_MODE_32)

for arg in sys.argv[3:]:
    va = int(arg, 16)
    name, n = sizes.get(va, ('?', 600))
    off = pe.va2off(va)
    print(f'== {va:#x} {name} ({n} bytes)')
    seen = []
    for ins in md.disasm(pe.data[off:off + n], va):
        ops = ins.op_str
        for tok in ops.replace('[', ' ').replace(']', ' ').replace(',', ' ').replace('+', ' ').split():
            if not tok.startswith('0x'):
                continue
            a = int(tok, 16)
            if ins.mnemonic == 'call':
                seen.append(f'call {a:#x}')
                continue
            if a < 0x10000:
                if ins.mnemonic in ('cmp', 'mov', 'and', 'or', 'test', 'sub', 'add') and 'esp' not in ops and 'ebp' not in ops:
                    seen.append(f'imm {ins.mnemonic} {a:#x}')
                continue
            o = pe.va2off(a)
            if o is None:
                continue
            s = pe.cstr(a, 80)
            if s and len(s) > 2:
                seen.append(f'str "{s}"')
            elif 'qword' in ops and ins.mnemonic.startswith('f'):
                seen.append(f'{ins.mnemonic} d[{a:#x}]={struct.unpack_from("<d", pe.data, o)[0]:.9g}')
            elif ins.mnemonic.startswith('f'):
                seen.append(f'{ins.mnemonic} f[{a:#x}]={pe.f32(a):.9g}')
    last = None
    for s in seen:
        if s != last:
            print('  ' + s)
        last = s
