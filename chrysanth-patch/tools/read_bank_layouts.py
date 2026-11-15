import struct, sys, re

d = open(sys.argv[1], 'rb').read()

def ext80(b):
    man = int.from_bytes(b[:8], 'little'); se = int.from_bytes(b[8:10], 'little')
    s = -1 if se & 0x8000 else 1; e = se & 0x7fff
    if e == 0 and man == 0: return 0.0
    return s * man * 2.0 ** (e - 16383 - 63)

class P:
    def __init__(s, d, o): s.d, s.o = d, o
    def u8(s): v = s.d[s.o]; s.o += 1; return v
    def n(s, k): v = s.d[s.o:s.o+k]; s.o += k; return v
    def sstr(s): return s.n(s.u8()).decode('latin1')
    def value(s):
        t = s.u8()
        if t == 0: return None
        if t == 1:
            L = []
            while s.d[s.o] != 0: L.append(s.value())
            s.o += 1; return L
        if t == 2: return struct.unpack('<b', s.n(1))[0]
        if t == 3: return struct.unpack('<h', s.n(2))[0]
        if t == 4: return struct.unpack('<i', s.n(4))[0]
        if t == 5: return round(ext80(s.n(10)), 3)
        if t in (6, 7): return s.sstr()
        if t == 8: return False
        if t == 9: return True
        if t == 10: return s.n(struct.unpack('<i', s.n(4))[0])
        if t == 11:
            L = []
            while True:
                x = s.sstr()
                if not x: break
                L.append(x)
            return L
        if t == 12: return s.n(struct.unpack('<i', s.n(4))[0]).decode('latin1')
        if t == 13: return None
        if t == 14:
            L = []
            while s.d[s.o] != 0:
                if s.d[s.o] in (2, 3, 4): s.value()
                s.o += 1 if s.d[s.o] == 1 else 0
                props = {}
                while s.d[s.o] != 0:
                    k = s.sstr(); props[k] = s.value()
                s.o += 1; L.append(props)
            s.o += 1; return L
        if t == 18: return s.n(struct.unpack('<i', s.n(4))[0] * 2).decode('utf-16le')
        if t == 19: return struct.unpack('<q', s.n(8))[0]
        if t == 20: return s.n(struct.unpack('<i', s.n(4))[0]).decode('utf-8', 'replace')
        if t == 17: return struct.unpack('<d', s.n(8))[0]
        if t == 16: return s.n(8)
        if t == 15: return struct.unpack('<f', s.n(4))[0]
        raise ValueError('type %d at %x' % (t, s.o))
    def obj(s):
        f = s.d[s.o]
        if f & 0xf0 == 0xf0:
            s.o += 1
            if f & 2: s.value()
        cls = s.sstr(); name = s.sstr(); props = {}
        while s.d[s.o] != 0:
            k = s.sstr(); props[k] = s.value()
        s.o += 1
        kids = []
        while s.d[s.o] != 0: kids.append(s.obj())
        s.o += 1
        return (cls, name, props, kids)

want = sys.argv[2:]
for nm in want:
    key = b'\x0aTfrxReport' + bytes([len(nm)]) + nm.encode()
    i = d.find(key)
    if i < 0: print(nm, 'not found'); continue
    p = P(d, i)
    cls, name, props, kids = p.obj()
    print('=====', name)
    def walk(o, depth):
        c, n, pr, k = o
        if c in ('TfrxReportPage', 'TfrxMasterData', 'TfrxReportTitle', 'TfrxPageHeader'):
            keys = {x: pr.get(x) for x in ('PaperWidth', 'PaperHeight', 'Orientation', 'LeftMargin', 'TopMargin', 'Top', 'Height') if x in pr}
            print('  ' * depth + c, n, keys)
        elif c.startswith('Tfrx') and 'View' in c:
            vis = pr.get('Visible', True)
            memo = pr.get('Memo.UTF8W') or pr.get('Memo.UTF8') or pr.get('Memo.Text') or ''
            fh = pr.get('Font.Height'); fn = pr.get('Font.Name')
            print('  ' * depth + '%-22s L=%-8s T=%-8s W=%-8s H=%-7s font=%s/%s %s %s' % (
                n, pr.get('Left'), pr.get('Top'), pr.get('Width'), pr.get('Height'), fn, fh,
                '' if vis else '(hidden)', repr(memo)[:40]))
        for x in k: walk(x, depth + 1)
    for x in kids: walk(x, 1)
