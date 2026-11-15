"""v27.3 = v27.2 + confirm box before printing (Date / Payee / Amount - Print?)."""
import struct, sys
from capstone import Cs, CS_ARCH_X86, CS_MODE_32

SRC, OUT = sys.argv[1], sys.argv[2]
d = bytearray(open(SRC, 'rb').read())
pe = struct.unpack_from('<I', d, 0x3c)[0]
SO = pe + 24 + struct.unpack_from('<H', d, pe + 20)[0]
n, vs, va, rs, ra = struct.unpack_from('<8sIIII', d, SO + 40 * 9)
assert n.rstrip(b'\0') == b'.cave'
CBASE = va + 0x400000
def cfo(x): return ra + (x - CBASE)

WN9 = 0xd9b47b                       # WARN tail in v27.2: mov eax,[edi+378]; ret
assert bytes(d[cfo(WN9):cfo(WN9) + 7]) == bytes.fromhex('8b8778030000c3')
GETTEXT, FORMAT, MSGBOX, GETHANDLE, CAPTION = 0x49fd2c, 0x410100, 0x40a7ec, 0x4a86c0, 0x8ae7a8

# ---- data (after v27.2 data, which ends below +0x1b00) ----
dpos = [CBASE + 0x1c00]
assert d[cfo(CBASE + 0x1b00):cfo(CBASE + 0x2000)] == b'\0' * 0x500
def dalloc(b, align=4):
    p = (dpos[0] + align - 1) & ~(align - 1)
    d[cfo(p):cfo(p) + len(b)] = b; dpos[0] = p + len(b); return p
S_DATE = dalloc(b'\0' * 4); S_PAYEE = dalloc(b'\0' * 4); S_AMT = dalloc(b'\0' * 4); S_MSG = dalloc(b'\0' * 4)
ARGS = dalloc((b'\0\0\0\0\x11\0\0\0') * 3)          # 3 x TVarRec(vtUnicodeString)
fmt = ('Please check the cheque before printing:\r\n\r\n'
       'Date:\t%s\r\nPayee:\t%s\r\nAmount:\t%s\r\n\r\nPrint this cheque now?')
FMT = dalloc(b'\xb0\x04\x02\x00\xff\xff\xff\xff' + struct.pack('<I', len(fmt)) + fmt.encode('utf-16le') + b'\0\0') + 12
assert dpos[0] <= CBASE + 0x2000

# ---- code at +0x500 ----
CONF = CBASE + 0x500
assert d[cfo(CONF):cfo(CONF) + 0x200] == b'\0' * 0x200
b = bytearray(); fix = []
def r(h): b.extend(bytes.fromhex(h.replace(' ', '')))
def i32(v): b.extend(struct.pack('<I', v & 0xffffffff))
def call(t): b.append(0xe8); fix.append((len(b), t)); b.extend(b'\0' * 4)
def jmp(t): b.append(0xe9); fix.append((len(b), t)); b.extend(b'\0' * 4)
r('85db'); r('0f85'); fix.append((len(b), 'END')); b.extend(b'\0' * 4)     # preview -> no confirm
for fld, var in ((0x378, S_DATE), (0x3d4, S_PAYEE), (0x390, S_AMT)):
    r('ba'); i32(var); r('8b87'); i32(fld); call(GETTEXT)                   # GetText(ctrl, var s)
for k, var in enumerate((S_DATE, S_PAYEE, S_AMT)):
    r('a1'); i32(var); r('a3'); i32(ARGS + 8 * k)                           # args[k].VUnicodeString
r('68'); i32(S_MSG); r('b8'); i32(FMT); r('ba'); i32(ARGS); r('b902000000'); call(FORMAT)
r('6a24 68'); i32(CAPTION); r('ff35'); i32(S_MSG)                          # MB_YESNO|MB_ICONQUESTION
r('8bc7'); call(GETHANDLE); r('50'); call(MSGBOX)
r('83f806 74'); fix8 = len(b); b.append(0)                                 # IDYES -> END
r('83c404'); jmp(0x8ae6e6)                                                 # No -> cancel print
end = len(b); b[fix8] = end - (fix8 + 1)
r('8b8778030000 c3')                                                        # END: original tail
for pos, t in fix:
    tgt = CONF + end if t == 'END' else t
    b[pos:pos + 4] = struct.pack('<i', tgt - (CONF + pos + 4))
d[cfo(CONF):cfo(CONF) + len(b)] = b
# WN9 -> jmp CONF
d[cfo(WN9):cfo(WN9) + 7] = b'\xe9' + struct.pack('<i', CONF - (WN9 + 5)) + b'\x90\x90'
open(OUT, 'wb').write(d)

md = Cs(CS_ARCH_X86, CS_MODE_32)
for x in md.disasm(bytes(d[cfo(WN9):cfo(WN9) + 7]), WN9): print(hex(x.address), x.mnemonic, x.op_str)
print('--')
for x in md.disasm(bytes(b), CONF): print(hex(x.address), x.mnemonic, x.op_str)
