import struct, sys
from capstone import Cs, CS_ARCH_X86, CS_MODE_32

SRC, OUT = sys.argv[1], sys.argv[2]
d = bytearray(open(SRC, 'rb').read())
D = 0x400c00          # .text/.itext VA - file offset
pe = struct.unpack_from('<I', d, 0x3c)[0]
SO = pe + 24 + struct.unpack_from('<H', d, pe + 20)[0]
def sec(i): return SO + 40 * i
def fo(va): return va - D

def put(va, old, new):
    o = fo(va)
    if old is not None:
        assert bytes(d[o:o + len(old)]) == old, (hex(va), bytes(d[o:o + len(old)]).hex(), old.hex())
    d[o:o + len(new)] = new

# ---------------- constants ----------------
FLAG = 0x8e9df0                     # preview flag byte (.data slack)
LITH = 0x8e9ca0                     # 'cbFontSize' literal header (.data slack)
LIT = LITH + 12
DATA_D = 0x8ce000 - 0x4cc200        # .data VA - file offset
SETTINGS = 0x8e8a40
FINDCOMP, LISTGET = 0x436e14, 0x42b600
GETSIZE, SETSIZE, SETNAME = 0x442c70, 0x442c8c, 0x442c14
WRITER = 0x73aa38
OLDCAVE = 0x745c9d                  # existing cave: orig FormCreate call + test-print checkbox load
BTNPRINT = 0x8ae32c

# ---------------- tiny assembler ----------------
class Asm:
    def __init__(s, base): s.base, s.b, s.labels, s.fix = base, bytearray(), {}, []
    @property
    def pc(s): return s.base + len(s.b)
    def raw(s, h): s.b += bytes.fromhex(h) if isinstance(h, str) else h
    def label(s, n): s.labels[n] = s.pc
    def call(s, t): s.b += b'\xe8'; s.fix.append((len(s.b), t, 4)); s.b += b'\0' * 4
    def jmp(s, t): s.b += b'\xe9'; s.fix.append((len(s.b), t, 4)); s.b += b'\0' * 4
    def jcc8(s, op, t): s.b += bytes([op]); s.fix.append((len(s.b), t, 1)); s.b += b'\0'
    def imm(s, v): s.b += struct.pack('<I', v & 0xffffffff)
    def done(s):
        for pos, t, n in s.fix:
            tgt = s.labels[t] if isinstance(t, str) else t
            rel = tgt - (s.base + pos + n)
            if n == 1: assert -128 <= rel < 128; s.b[pos] = rel & 0xff
            else: s.b[pos:pos + 4] = struct.pack('<i', rel)
        return bytes(s.b)

CAVE = 0x8b16a0
a = Asm(CAVE)
# GETSZ: clone of setting getter 0x73c088 prefix with enum 0x2711, then continue in original
a.label('GETSZ')
pre = bytes(d[fo(0x73c088):fo(0x73c0b6)])
assert pre[-5:] == b'\xba\x13\x27\x00\x00'
a.raw(pre[:-5] + b'\xba\x11\x27\x00\x00'); a.jmp(0x73c0b6)
# OFS: eax = size offset (-2..3), 0 if unset
a.label('OFS')
a.raw('a1'); a.imm(SETTINGS); a.raw('8b00'); a.call('GETSZ')
a.raw('2de8030000')          # sub eax,1000
a.raw('83f8fe'); a.jcc8(0x7c, 'OFS0')
a.raw('83f803'); a.jcc8(0x7f, 'OFS0')
a.raw('c3')
a.label('OFS0'); a.raw('31c0c3')
# FONTHOOK: replaces Font.Name := ... call in memo loop (ebx=index, edi=list)
a.label('FONTHOOK')
a.call(SETNAME)
a.raw('56')                  # push esi
a.raw('8bd3 8bc7'.replace(' ', '')); a.call(LISTGET)
a.raw('8bf0')                # mov esi,eax (memo)
a.raw('8b460c 85c0'.replace(' ', '')); a.jcc8(0x75, 'FH1')
a.raw('8b4630'); a.call(GETSIZE)
a.raw('05e8030000 89460c'.replace(' ', ''))
a.label('FH1')
a.call('OFS')
a.raw('03460c 2de8030000 83f804'.replace(' ', '')); a.jcc8(0x7d, 'FH2')
a.raw('b804000000')
a.label('FH2')
a.raw('8bd0 8b4630'.replace(' ', '')); a.call(SETSIZE)
a.raw('5ec3')
# LOADUI: FormCreate hook (ebx=form)
a.label('LOADUI')
a.call(OLDCAVE)
a.raw('8bc3 ba'.replace(' ', '')); a.imm(LIT); a.call(FINDCOMP)
a.raw('85c0'); a.jcc8(0x74, 'LU9')
a.raw('50'); a.call('OFS'); a.raw('83c002 8bd0 58 8b08 ff91e0000000'.replace(' ', ''))
a.label('LU9'); a.raw('c3')
# SAVEUI: btnOKClick hook (ebx=form)
a.label('SAVEUI')
a.call(0x73c62c)
a.raw('8bc3 ba'.replace(' ', '')); a.imm(LIT); a.call(FINDCOMP)
a.raw('85c0'); a.jcc8(0x74, 'SU9')
a.raw('8b10 ff92dc000000 85c0'.replace(' ', '')); a.jcc8(0x7c, 'SU9')
a.raw('05e6030000')          # add eax,998
a.raw('6a00 6811270000 6a00 50'.replace(' ', ''))
a.raw('a1'); a.imm(SETTINGS); a.raw('8b00'); a.call(WRITER)
a.label('SU9'); a.raw('c3')
# PREV: preview menu handler
a.label('PREV')
a.raw('c605'); a.imm(FLAG); a.raw('01'); a.call(BTNPRINT)
a.raw('c605'); a.imm(FLAG); a.raw('00'); a.raw('c3')
# LOADFLAG: replaces 'xor ebx,ebx; mov eax,[edi+378]' in btnPrintClick
a.label('LOADFLAG')
a.raw('0fb61d'); a.imm(FLAG); a.raw('8b8778030000 c3'.replace(' ', ''))
# POSTCHK: skip after-print steps when previewing
a.label('POSTCHK')
a.raw('803d'); a.imm(FLAG); a.raw('00'); a.jcc8(0x75, 'PC1')
a.raw('a1'); a.imm(SETTINGS); a.jmp(0x8ae698)
a.label('PC1'); a.jmp(0x8ae6e6)
code = a.done()
L = a.labels
assert CAVE + len(code) <= 0x8b1800, hex(len(code))
assert bytes(d[fo(CAVE):fo(CAVE) + len(code)]) == b'\0' * len(code)
put(CAVE, None, code)
print('cave bytes', len(code), 'free', 0x8b1800 - CAVE - len(code))

# .text VirtualSize -> cover cave; .data VirtualSize -> cover slack
struct.pack_into('<I', d, sec(0) + 8, 0x4b0800)
struct.pack_into('<I', d, sec(2) + 8, 0x1be00)

# literal 'cbFontSize' in .data slack
lit = b'\xb0\x04\x02\x00\xff\xff\xff\xff' + struct.pack('<I', 10) + 'cbFontSize'.encode('utf-16le') + b'\0\0'
o = LITH - DATA_D; assert d[o:o + len(lit)] == b'\0' * len(lit); d[o:o + len(lit)] = lit
assert d[FLAG - DATA_D] == 0

def rel5(op, at, tgt): return bytes([op]) + struct.pack('<i', tgt - (at + 5))
# hooks
put(0x748850, rel5(0xe8, 0x748850, SETNAME), rel5(0xe8, 0x748850, L['FONTHOOK']))
put(0x745d2f, rel5(0xe8, 0x745d2f, OLDCAVE), rel5(0xe8, 0x745d2f, L['LOADUI']))
put(0x745c10, rel5(0xe8, 0x745c10, 0x73c62c), rel5(0xe8, 0x745c10, L['SAVEUI']))
put(0x8ae55a, bytes.fromhex('33db8b8778030000'), rel5(0xe8, 0x8ae55a, L['LOADFLAG']) + b'\x90\x90\x90')
put(0x8ae693, bytes.fromhex('a1408a8e00'), rel5(0xe9, 0x8ae693, L['POSTCHK']))

# method table: miPrintEnvelopeClick -> PREV
nm = b'\x14miPrintEnvelopeClick'
hits = [j for j in range(len(d)) if d.startswith(nm, j) and j < 0x4cc200 and struct.unpack_from('<I', d, j - 4)[0] == 0x8afeac]
assert len(hits) == 1, hits
i = hits[0]
assert struct.unpack_from('<I', d, i - 4)[0] == 0x8afeac
struct.pack_into('<I', d, i - 4, L['PREV'])

# ---------------- DFM edits ----------------
RS, RVA0 = 0x549200, 0x556000
# main form: rename both Print Envelope captions (same length)
MS, ML = 0x961374, 0x3271
old_c, new_c = b'\x06\x0fPrint &Envelope', b'\x06\x0fPre&view Cheque'
mf = bytes(d[MS:MS + ML]); assert mf.count(old_c) == 2
d[MS:MS + ML] = mf.replace(old_c, new_c)

# options form: insert Size label + combo into gbFont, grow gbFont
OS, OL, OENT = 0x959208, 0x2400, 0x54ce00
assert struct.unpack_from('<II', d, OENT) == (OS - RS + RVA0, OL)
dfm = bytearray(d[OS:OS + OL])
g = dfm.find(b'\x06gbFont')
h = dfm.find(b'\x06Height\x02\x34', g); assert 0 < h - g < 60
dfm[h + 8] = 0x52
endc = dfm.find(b'\x05Width\x03\xb2\x00\x00\x00', g); assert endc > 0
ins_at = endc + len(b'\x05Width\x03\xb2\x00') + 2   # after cxfncbFont props-end and children-end
def ss(s): b = s.encode('latin1'); return bytes([len(b)]) + b
def pint(n, v): return ss(n) + (b'\x02' + bytes([v]) if v < 128 else b'\x03' + struct.pack('<h', v))
new = ss('TLabel') + ss('lblFontSize') + pint('Left', 0x12) + pint('Top', 0x35) + pint('Width', 0x1a) + pint('Height', 0x0d) \
    + ss('Caption') + b'\x06' + ss('Size:') + b'\x00\x00'
items = ['Smaller  -2', 'Smaller  -1', 'Normal', 'Bigger  +1', 'Bigger  +2', 'Bigger  +3']
new += ss('TComboBox') + ss('cbFontSize') + pint('Left', 0x44) + pint('Top', 0x31) + pint('Width', 0xb2) \
    + ss('Style') + b'\x07' + ss('csDropDownList') + pint('TabOrder', 1) \
    + ss('Items.Strings') + b'\x01' + b''.join(b'\x06' + ss(x) for x in items) + b'\x00' + b'\x00\x00'
dfm[ins_at:ins_at] = new
newlen = len(dfm)
# append to end of .rsrc
rs = sec(8)
vs, va, rawsz, rawp = struct.unpack_from('<IIII', d, rs + 8)
assert rawp + rawsz == len(d), 'overlay present'
pad = (-len(d)) % 16
newoff = len(d) + pad
grow = pad + newlen
grow_al = (grow + 0x1ff) & ~0x1ff
d += b'\0' * pad + bytes(dfm) + b'\0' * (grow_al - grow)
struct.pack_into('<II', d, OENT, newoff - RS + RVA0, newlen)
struct.pack_into('<I', d, rs + 16, rawsz + grow_al)
struct.pack_into('<I', d, rs + 8, vs + grow_al)
soi = struct.unpack_from('<I', d, pe + 24 + 56)[0]
need = (va + vs + grow_al + 0xfff) & ~0xfff
if need > soi: struct.pack_into('<I', d, pe + 24 + 56, need)
# resource data directory size
rdsz_off = pe + 24 + 96 + 8 * 2 + 4
old_rd = struct.unpack_from('<I', d, rdsz_off)[0]
assert old_rd == vs
struct.pack_into('<I', d, rdsz_off, old_rd + grow_al)
print('rsrc dir size', hex(old_rd), '->', hex(old_rd + grow_al), 'new DFM at', hex(newoff), 'len', hex(newlen))
open(OUT, 'wb').write(d)

md = Cs(CS_ARCH_X86, CS_MODE_32)
for x in md.disasm(code, CAVE):
    tag = [k for k, v in L.items() if v == x.address]
    print(('%-9s' % (tag[0] + ':' if tag else '')), hex(x.address), x.mnemonic, x.op_str)
print('labels', {k: hex(v) for k, v in L.items()})
