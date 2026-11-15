import struct, sys, re
from capstone import Cs, CS_ARCH_X86, CS_MODE_32

SRC, OUT = sys.argv[1], sys.argv[2]
d = bytearray(open(SRC, 'rb').read())
D = 0x400c00
pe = struct.unpack_from('<I', d, 0x3c)[0]
OPT = pe + 24
SO = OPT + struct.unpack_from('<H', d, pe + 20)[0]
def sec(i): return SO + 40 * i
def fo(va): return va - D
def rel5(op, at, tgt): return bytes([op]) + struct.pack('<i', tgt - (at + 5))
def put(va, old, new):
    o = fo(va)
    if old is not None:
        assert bytes(d[o:o + len(old)]) == old, (hex(va), bytes(d[o:o + len(old)]).hex(), old.hex())
    d[o:o + len(new)] = new

# ---------- verify accessor slots via RTTI ----------
def propinfo(cls, prop):
    for m in re.finditer(bytes([len(prop)]) + prop, d[:0x4cc200]):
        a = m.start(); gp, sp = struct.unpack_from('<II', d, a - 22)
        back = d[a - 3000:a]
        if re.findall(rb'T[A-Za-z]+', back) and cls in re.findall(rb'T[A-Za-z]+', back)[-3:]:
            return gp, sp
assert propinfo(b'TCheckBox', b'Checked') == (0xfe0000dc, 0xfe0000e0), propinfo(b'TCheckBox', b'Checked')

# ---------- constants ----------
SETTINGS, CONFIG = 0x8e8a40, 0x8e940c
FINDCOMP, LISTGET, ALLOBJ = 0x436e14, 0x42b600, 0x5569fc
GETSIZE, SETSIZE, SETNAME, GETSTYLE, SETSTYLE = 0x442c70, 0x442c8c, 0x442c14, 0x442cac, 0x442cb8
WRITER, OLDCAVE, FLAG = 0x73aa38, 0x745c9d, 0x8e9df0
MSGBOX, GETHANDLE, CAPTION = 0x40a7ec, 0x4a86c0, 0x8ae7a8

# ================= 1. Options DFM (rebuild from v4 copy at end of .rsrc) =================
RS, RVA0 = 0x549200, 0x556000
OENT = 0x54ce00
orva, olen = struct.unpack_from('<II', d, OENT)
ooff = orva - RVA0 + RS
assert d[ooff:ooff + 4] == b'TPF0'
dfm = bytearray(d[ooff:ooff + olen])
def ss(s): b = s.encode('latin1'); return bytes([len(b)]) + b
def pint(n, v): return ss(n) + (b'\x02' + bytes([v]) if -128 <= v < 128 else b'\x03' + struct.pack('<h', v))
def pstr(n, v): return ss(n) + b'\x06' + ss(v)
def label(name, l, t, cap): return ss('TLabel') + ss(name) + pint('Left', l) + pint('Top', t) + pstr('Caption', cap) + b'\x00\x00'
def combo(name, l, t, w, items, tab, ddc=None):
    b = ss('TComboBox') + ss(name) + pint('Left', l) + pint('Top', t) + pint('Width', w) \
        + ss('Style') + b'\x07' + ss('csDropDownList')
    if ddc: b += pint('DropDownCount', ddc)
    b += pint('TabOrder', tab) + ss('Items.Strings') + b'\x01' + b''.join(b'\x06' + ss(x) for x in items) + b'\x00'
    return b + b'\x00\x00'
# end of cbFontSize block (added in v4)
k = dfm.find(b'\x0acbFontSize'); assert k > 0
k2 = dfm.find(b'\x06\x0aBigger  +3\x00\x00\x00', k); assert k2 > 0
ins1 = k2 + len(b'\x06\x0aBigger  +3\x00\x00\x00')
assert dfm[ins1] == 0          # gbFont children terminator
chk = ss('TCheckBox') + ss('chkFontBold') + pint('Left', 264) + pint('Top', 51) + pint('Width', 97) + pint('Height', 17) \
    + pstr('Caption', '&Bold text') + pint('TabOrder', 2) + b'\x00\x00'
nitems = ['%+.1f' % ((v - 8) * 0.5) if v != 8 else '0.0' for v in range(17)]
grp = ss('TGroupBox') + ss('gbNudge') + pint('Left', 17) + pint('Top', 96) + pint('Width', 449) + pint('Height', 156) \
    + pstr('Caption', 'Fine-tune position (mm)') + pint('TabOrder', 1) + b'\x00'
grp += label('lblNdH1', 100, 18, 'Right (+) / Left (-)') + label('lblNdH2', 230, 18, 'Down (+) / Up (-)')
rows = ['Date:', 'Payee:', 'Amount:', 'In words:']
for r, cap in enumerate(rows):
    t = 36 + r * 28
    grp += label('lblNdN%d' % r, 18, t + 4, cap)
    grp += combo('cbNd%dX' % r, 100, t, 110, nitems, 2 * r, 17)
    grp += combo('cbNd%dY' % r, 230, t, 110, nitems, 2 * r + 1, 17)
grp += b'\x00'
dfm[ins1:ins1] = chk
dfm[ins1 + len(chk) + 1:ins1 + len(chk) + 1] = grp
# hide the Arabic RTL group (its setting row is reused)
g = dfm.find(b'\x05gbRTL'); assert g > 0
dfm[g + 6:g + 6] = ss('Visible') + b'\x08'
newlen = len(dfm)

# write DFM back at same place, growing .rsrc
rs = sec(8)
vs, va, rawsz, rawp = struct.unpack_from('<IIII', d, rs + 8)
assert rawp + rawsz == len(d) and ooff >= rawp
end_needed = ooff + newlen
new_raw = ((end_needed - rawp) + 0x1ff) & ~0x1ff
del d[rawp + rawsz:]
d += b'\0' * (rawp + new_raw - len(d))
d[ooff:ooff + newlen] = dfm
new_vs = max(vs, new_raw)
struct.pack_into('<I', d, rs + 16, new_raw)
struct.pack_into('<I', d, rs + 8, new_vs)
struct.pack_into('<II', d, OENT, orva, newlen)
rdsz_off = OPT + 96 + 8 * 2 + 4
struct.pack_into('<I', d, rdsz_off, new_vs)

# ================= 2. new section .cave =================
nsec = struct.unpack_from('<H', d, pe + 6)[0]
assert nsec == 9
CVA = (va + new_vs + 0xfff) & ~0xfff
CSIZE = 0x2000
craw = len(d)
assert craw % 0x200 == 0
hdr = sec(9)
assert hdr + 40 <= 0x400
struct.pack_into('<8sIIIIIIHHI', d, hdr, b'.cave\0\0\0', CSIZE, CVA, CSIZE, craw, 0, 0, 0, 0, 0xE0000060)
struct.pack_into('<H', d, pe + 6, 10)
struct.pack_into('<I', d, OPT + 56, CVA + CSIZE)          # SizeOfImage
d += b'\0' * CSIZE
CBASE = CVA + 0x400000                                     # absolute VA
def cfo(vaabs): return craw + (vaabs - CBASE)

# ---- data area at CBASE+0x1800 ----
DATA = CBASE + 0x1800
dpos = [DATA]
def dalloc(b, align=4):
    p = (dpos[0] + align - 1) & ~(align - 1)
    d[cfo(p):cfo(p) + len(b)] = b
    dpos[0] = p + len(b)
    return p
def ustr(s):
    h = dalloc(b'\xb0\x04\x02\x00\xff\xff\xff\xff' + struct.pack('<I', len(s)) + s.encode('utf-16le') + b'\0\0')
    return h + 12
G_SIZE = dalloc(b'\0' * 4); G_BOLD = dalloc(b'\0' * 4); G_ND = dalloc(b'\x08' * 8); TMP = dalloc(b'\0' * 4)
KHALF = dalloc(struct.pack('<d', 96 / 25.4 * 0.5), 8)
L_SIZE = ustr('cbFontSize'); L_BOLD = ustr('chkFontBold')
L_ND = [ustr('cbNd%d%s' % (r, ax)) for r in range(4) for ax in 'XY']
ND_TBL = dalloc(b''.join(struct.pack('<I', p) for p in L_ND))
WARN = dalloc(('Test print is ON.\r\n\r\nThe cheque picture will be printed too, so use plain A4 paper only '
               '- NOT a real cheque.\r\n\r\nContinue printing?\r\n\r\n(To print real cheques, untick Options > General > Test print.)')
              .encode('utf-16le') + b'\0\0')
assert dpos[0] < CBASE + CSIZE

# ---- assembler ----
class Asm:
    def __init__(s, base): s.base, s.b, s.labels, s.fix = base, bytearray(), {}, []
    @property
    def pc(s): return s.base + len(s.b)
    def r(s, h): s.b += bytes.fromhex(h.replace(' ', ''))
    def i32(s, v): s.b += struct.pack('<I', v & 0xffffffff)
    def L(s, n): assert n not in s.labels; s.labels[n] = s.pc
    def call(s, t): s.b += b'\xe8'; s.fix.append((len(s.b), t, 4)); s.b += b'\0' * 4
    def jmp(s, t): s.b += b'\xe9'; s.fix.append((len(s.b), t, 4)); s.b += b'\0' * 4
    def j8(s, op, t): s.b += bytes([op]); s.fix.append((len(s.b), t, 1)); s.b += b'\0'
    def j32(s, op, t): s.b += b'\x0f' + bytes([op]); s.fix.append((len(s.b), t, 4)); s.b += b'\0' * 4
    def done(s):
        for pos, t, n in s.fix:
            tgt = s.labels[t] if isinstance(t, str) else t
            rel = tgt - (s.base + pos + n)
            if n == 1: assert -128 <= rel < 128, t; s.b[pos] = rel & 0xff
            else: s.b[pos:pos + 4] = struct.pack('<i', rel)
        return bytes(s.b)
JE, JNE, JL, JGE, JLE, JG, JA, JB = 0x74, 0x75, 0x7c, 0x7d, 0x7e, 0x7f, 0x77, 0x72
a = Asm(CBASE)

def settings_eax(): a.r('a1'); a.i32(SETTINGS); a.r('8b00')
# --- GET1 / GET2: clones of setting getter with other enum ids ---
pre = bytes(d[fo(0x73c088):fo(0x73c0b6)]); assert pre[-5:] == b'\xba\x13\x27\x00\x00'
for nm, idv in (('GET1', 0x2711), ('GET2', 0x2724)):
    a.L(nm); a.b += pre[:-5] + b'\xba' + struct.pack('<I', idv); a.jmp(0x73c0b6)

# --- DECODE: rows -> G_SIZE, G_BOLD, G_ND[8] ---
a.L('DECODE')
a.r('53 56 57')                                   # push ebx esi edi
a.r('c705'); a.i32(G_SIZE); a.i32(0)
a.r('c705'); a.i32(G_BOLD); a.i32(0)
a.r('c705'); a.i32(G_ND); a.i32(0x08080808)
a.r('c705'); a.i32(G_ND + 4); a.i32(0x08080808)
settings_eax(); a.call('GET1')
a.r('a9 00000001'); a.j8(JE, 'DC2')               # test eax,0x1000000
a.r('8bc8 83e107 83f905'); a.j8(JA, 'DC1')        # ecx=eax&7; cmp 5; ja
a.r('83e902 890d'); a.i32(G_SIZE)                 # sub ecx,2; mov [G_SIZE],ecx
a.L('DC1')
a.r('8bc8 c1e903 83e101 890d'); a.i32(G_BOLD)     # bold
a.r('c1e804')                                     # shr eax,4
a.r('bf'); a.i32(G_ND); a.call('UNP4')
a.L('DC2')
settings_eax(); a.call('GET2')
a.r('a9 00000001'); a.j8(JE, 'DC9')
a.r('bf'); a.i32(G_ND + 4); a.call('UNP4')
a.L('DC9'); a.r('5f 5e 5b c3')
# UNP4: eax=packed (low 20 bits = 4x5), edi=dest bytes
a.L('UNP4')
a.r('b904000000')                                 # mov ecx,4
a.L('UP1')
a.r('8bd0 83e21f 83fa10'); a.j8(JA, 'UP2')        # edx=eax&31; cmp 16; ja skip
a.r('8817')                                       # mov [edi],dl
a.L('UP2')
a.r('c1e805 47 49'); a.j8(JNE, 'UP1')             # shr eax,5; inc edi; dec ecx; jnz
a.r('c3')

# --- ENCSAVE: globals -> rows ---
a.L('ENCSAVE')
a.r('53 56')
# row1
a.r('a1'); a.i32(G_SIZE); a.r('83c002 83e007 8bd8')        # ebx=(size+2)&7
a.r('a1'); a.i32(G_BOLD); a.r('83e001 c1e003 0bd8')        # |= bold<<3
a.r('be'); a.i32(G_ND + 3); a.call('PK4')                  # eax = packed ND0..3 (ND0 low)
a.r('c1e004 0bc3 0d00000001')                              # shl 4; or ebx; or marker
a.r('ba11270000'); a.call('WROW')
a.r('be'); a.i32(G_ND + 7); a.call('PK4')
a.r('0d00000001 ba24270000'); a.call('WROW')
a.r('5e 5b c3')
# PK4: esi -> last of 4 bytes; returns eax = b0 | b1<<5 | b2<<10 | b3<<15
a.L('PK4')
a.r('33c0 b904000000')
a.L('PK1')
a.r('c1e005 0fb616 0bc2 4e 49'); a.j8(JNE, 'PK1')          # shl 5; movzx edx,[esi]; or; dec esi; dec ecx
a.r('c3')
# WROW: eax=value, edx=id
a.L('WROW')
a.r('6a00 52 6a00 50'); settings_eax(); a.call(WRITER); a.r('c3')

# --- FIND: edx=literal, ebx=form -> eax=component or 0 ---
a.L('FIND'); a.r('8bc3'); a.jmp(FINDCOMP)

# --- LOADUI (FormCreate hook) ---
a.L('LOADUI')
a.call(OLDCAVE)
a.call('DECODE')
a.r('ba'); a.i32(L_SIZE); a.call('FIND'); a.r('85c0'); a.j8(JE, 'LU1')
a.r('8b15'); a.i32(G_SIZE); a.r('83c202 8b08 ff91e0000000')
a.L('LU1')
a.r('ba'); a.i32(L_BOLD); a.call('FIND'); a.r('85c0'); a.j8(JE, 'LU2')
a.r('8b15'); a.i32(G_BOLD); a.r('8b08 ff91e0000000')
a.L('LU2')
a.r('56 33f6')                                              # push esi; esi=0
a.L('LU3')
a.r('8b14b5'); a.i32(ND_TBL); a.call('FIND'); a.r('85c0'); a.j8(JE, 'LU4')
a.r('0fb696'); a.i32(G_ND); a.r('8b08 ff91e0000000')
a.L('LU4')
a.r('46 83fe08'); a.j8(JL, 'LU3')
a.r('5e c3')

# --- SAVEUI (replaces RTL save call in btnOKClick) ---
a.L('SAVEUI')
a.r('ba'); a.i32(L_SIZE); a.call('FIND'); a.r('85c0'); a.j8(JE, 'SV1')
a.r('8b10 ff92dc000000 83f805'); a.j8(JA, 'SV1')           # unsigned: -1 -> skip
a.r('83e802 a3'); a.i32(G_SIZE)
a.L('SV1')
a.r('ba'); a.i32(L_BOLD); a.call('FIND'); a.r('85c0'); a.j8(JE, 'SV2')
a.r('8b10 ff92dc000000 0fb6c0 a3'); a.i32(G_BOLD)
a.L('SV2')
a.r('56 33f6')
a.L('SV3')
a.r('8b14b5'); a.i32(ND_TBL); a.call('FIND'); a.r('85c0'); a.j8(JE, 'SV4')
a.r('8b10 ff92dc000000 83f810'); a.j8(JA, 'SV4')
a.r('8886'); a.i32(G_ND)
a.L('SV4')
a.r('46 83fe08'); a.j8(JL, 'SV3')
a.r('5e'); a.jmp('ENCSAVE')

# --- PREFONT: decode before memo font loop ---
a.L('PREFONT'); a.call('DECODE'); a.jmp(0x748800)

# --- CLASSIFY: eax=obj -> eax=group 0..3 or -1 ---
a.L('CLASSIFY')
a.r('8b5008 85d2'); a.j8(JE, 'CLN')
a.r('8b4afc')                                               # ecx=len
a.r('66833a6d'); a.j8(JNE, 'CLN')                           # 'm'
a.r('66837a026d'); a.j8(JNE, 'CLN')                         # 'm'
a.r('0fb74204')                                             # eax=char2
a.r('83f850'); a.j8(JNE, 'CLA')                             # 'P'
a.r('83f907'); a.j8(JE, 'CL1'); a.j8(0xeb, 'CLN')
a.L('CLA')
a.r('83f841'); a.j8(JNE, 'CLD')                             # 'A'
a.r('83f908'); a.j8(JE, 'CL2')
a.r('83f90d'); a.j8(JE, 'CL3'); a.j8(0xeb, 'CLN')
a.L('CLD')
a.r('83f905'); a.j8(JG, 'CLN')
for ch in 'DMYS':
    a.r('83f8%02x' % ord(ch)); a.j8(JE, 'CL0')
a.L('CLN'); a.r('83c8ff c3')
a.L('CL0'); a.r('33c0 c3')
a.L('CL1'); a.r('b801000000 c3')
a.L('CL2'); a.r('b802000000 c3')
a.L('CL3'); a.r('b803000000 c3')

# --- NUDGE: eax=obj, edx=sign(+1/-1) ---
a.L('NUDGE')
a.r('53 56 8bf0 8bda')
a.call('CLASSIFY')
a.r('85c0'); a.j32(0x8c, 'NG9')                             # jl
a.r('0fb68c00'); a.i32(G_ND)                                # movzx ecx,[G_ND+eax*2]  (eax+eax)
a.r('0fb69400'); a.i32(G_ND + 1)                            # movzx edx,[G_ND+1+eax*2]
a.r('83e908 0fafcb 83ea08 0fafd3 52')                       # (v-8)*sign ; push dy
a.r('85c9'); a.j8(JE, 'NG1')
a.r('890d'); a.i32(TMP); a.r('db05'); a.i32(TMP); a.r('dc0d'); a.i32(KHALF)
a.r('db6e40 dec1 83c4f4 db3c24 9b 8bc6 8b10 ff5234')        # fld tbyte[esi+40]; faddp; push ext; SetLeft
a.L('NG1')
a.r('5a 85d2'); a.j8(JE, 'NG9')
a.r('8915'); a.i32(TMP); a.r('db05'); a.i32(TMP); a.r('dc0d'); a.i32(KHALF)
a.r('db6e50 dec1 83c4f4 db3c24 9b 8bc6 8b10 ff5238')        # Top
a.L('NG9'); a.r('5e 5b c3')

# --- FONTHOOK2: memo loop (ebx=index, edi=list); eax=Font, edx=name ---
a.L('FONTHOOK2')
a.call(SETNAME)
a.r('56 8bd3 8bc7'); a.call(LISTGET); a.r('8bf0')
a.r('8b460c a900000040'); a.j8(JNE, 'FK1')                  # tag initialised?
a.r('8b4630'); a.call(GETSTYLE); a.r('0fb6c0 c1e010 50')
a.r('8b4630'); a.call(GETSIZE); a.r('25ffff0000 5a 0bc2 0d00000040 89460c')
a.L('FK1')
a.r('8b460c 25ffff0000 0305'); a.i32(G_SIZE)
a.r('83f804'); a.j8(JGE, 'FK2'); a.r('b804000000')
a.L('FK2')
a.r('8bd0 8b4630'); a.call(SETSIZE)
a.r('8b460c c1e810 25ff000000 833d'); a.i32(G_BOLD); a.r('00'); a.j8(JE, 'FK3')
a.r('83c801')
a.L('FK3')
a.r('8bd0 8b4630'); a.call(SETSTYLE)
a.r('8bc6 ba01000000'); a.call('NUDGE')
a.r('5e c3')

# --- UNDOHOOK: before app's offset-undo after printing ---
a.L('UNDOHOOK')
a.r('8b442404 8b40fc 8b4064')                               # report
a.r('53 56 57'); a.call(ALLOBJ); a.r('8bf8 8b7708 33db')
a.L('UD1')
a.r('3bde'); a.j8(JGE, 'UD2')
a.r('8bd3 8bc7'); a.call(LISTGET); a.r('baffffffff'); a.call('NUDGE')
a.r('43'); a.j8(0xeb, 'UD1')
a.L('UD2')
a.r('5f 5e 5b'); a.jmp(0x7486d4)

# --- WARN: replaces LOADFLAG in btnPrintClick (edi=form) ---
a.L('WARN')
a.r('0fb61d'); a.i32(FLAG)
a.r('85db'); a.j8(JNE, 'WN9')
a.r('a1'); a.i32(CONFIG); a.r('8b00 80785400'); a.j8(JE, 'WN9')
a.r('6834010000 68'); a.i32(CAPTION); a.r('68'); a.i32(WARN)
a.r('8bc7'); a.call(GETHANDLE); a.r('50'); a.call(MSGBOX)
a.r('83f806'); a.j8(JE, 'WN9')
a.r('83c404'); a.jmp(0x8ae6e6)
a.L('WN9'); a.r('8b8778030000 c3')

code = a.done()
assert len(code) < 0x1800, hex(len(code))
d[cfo(CBASE):cfo(CBASE) + len(code)] = code
Lb = a.labels
print('cave code', hex(len(code)), 'data end', hex(dpos[0] - CBASE))

# ================= 3. hooks =================
OLD_LOADUI, OLD_SAVEUI, OLD_FONTHOOK, OLD_LOADFLAG = 0x8b173d, 0x8b1767, 0x8b16f2, 0x8b17b8
put(0x745d2f, rel5(0xe8, 0x745d2f, OLD_LOADUI), rel5(0xe8, 0x745d2f, Lb['LOADUI']))
put(0x745c10, rel5(0xe8, 0x745c10, OLD_SAVEUI), rel5(0xe8, 0x745c10, 0x73c62c))      # test flag: original save
put(0x745c27, rel5(0xe8, 0x745c27, 0x73c7bc), rel5(0xe8, 0x745c27, Lb['SAVEUI']))   # RTL save -> our save
put(0x748850, rel5(0xe8, 0x748850, OLD_FONTHOOK), rel5(0xe8, 0x748850, Lb['FONTHOOK2']))
put(0x74887b, rel5(0xe8, 0x74887b, 0x748800), rel5(0xe8, 0x74887b, Lb['PREFONT']))
put(0x74888b, rel5(0xe8, 0x74888b, 0x7486d4), rel5(0xe8, 0x74888b, Lb['UNDOHOOK']))
put(0x8ae55a, rel5(0xe8, 0x8ae55a, OLD_LOADFLAG), rel5(0xe8, 0x8ae55a, Lb['WARN']))
put(0x73b0b8, rel5(0xe8, 0x73b0b8, 0x73be04), b'\x31\xc0\x90\x90\x90')             # RTL never on
put(0x8aaf80, b'\x55\x8b\xec', b'\xc3\x8b\xec')                                    # update thread: exit
open(OUT, 'wb').write(d)

md = Cs(CS_ARCH_X86, CS_MODE_32)
inv = {v: k for k, v in Lb.items()}
for x in md.disasm(code, CBASE):
    print('%-10s %s %-6s %s' % ((inv.get(x.address, '') + ':') if x.address in inv else '', hex(x.address), x.mnemonic, x.op_str))
