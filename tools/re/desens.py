"""Desensambla N instrucciones desde una VA, anotando cadenas y floats referenciados.
Uso: python dis.py <pe> <va_hex> [n]
"""
import sys, struct
from capstone import Cs, CS_ARCH_X86, CS_MODE_32
from petabla import PE

pe = PE(sys.argv[1])
va = int(sys.argv[2], 16)
n = int(sys.argv[3]) if len(sys.argv) > 3 else 60
md = Cs(CS_ARCH_X86, CS_MODE_32)
off = pe.va2off(va)
for i, ins in enumerate(md.disasm(pe.data[off:off + n * 16], va)):
    if i >= n:
        break
    note = ''
    for tok in ins.op_str.replace('[', ' ').replace(']', ' ').replace(',', ' ').split():
        if tok.startswith('0x') and len(tok) >= 9:
            a = int(tok, 16)
            if pe.va2off(a) is not None:
                s = pe.cstr(a, 80)
                if s and len(s) >= 2:
                    note += f' "{s}"'
                else:
                    f = pe.f32(a); d = pe.u32(a)
                    note += f' [{a:#x}]=f{f:.6g}/{d:#x}'
    print(f'{ins.address:#x}: {ins.mnemonic} {ins.op_str}{note}')
