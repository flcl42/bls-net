import re
from pathlib import Path
repo = Path(__file__).resolve().parents[1]
src = repo / 'external' / 'blst' / 'src'
root = repo / 'src' / 'Nethermind.Crypto.BlsNet'
def extract(file, name):
    text = re.sub(r'/\*.*?\*/', '', (src/file).read_text(), flags=re.S)
    start = re.search(r'\b'+name+r'(?:\[\])?\s*=\s*\{', text).end()-1
    level=0
    for end in range(start,len(text)):
        level += (text[end]=='{')-(text[end]=='}')
        if level==0: break
    tokens = re.findall(r'\{|\}|0x[0-9a-fA-F]+|\b0\b',text[start:end+1])
    def parse():
        assert tokens.pop(0)=='{'
        a=[]
        while tokens[0]!='}': a.append(parse() if tokens[0]=='{' else tokens.pop(0))
        tokens.pop(0)
        return a
    return parse()
def fp(a):
    if a==['0']: return 'Fp.Zero'
    return 'Fp.FromMontgomery('+', '.join(x+'UL' for x in a)+')'
def fp2(a): return 'new Fp2('+fp(a[0])+', '+fp(a[1])+')'
for n in (1,2):
    field='Fp' if n==1 else 'Fp2'
    fmt=fp if n==1 else fp2
    names=['Aprime_E'+str(n),'Bprime_E'+str(n),'minus_A','Z','ZxA']
    names+=['sqrt_minus_ZZZ'] if n==1 else ['recip_ZZZ','magic_ZZZ']
    text='// Montgomery constants from blst map_to_g%d.c (Apache-2.0).\nusing Nethermind.Crypto.BlsNet.Arithmetic;\n\nnamespace Nethermind.Crypto.BlsNet.Hashing;\n\ninternal static class MapConstants%d\n{\n'%(n,n)
    for name in names:
        text+='    internal static readonly '+field+' '+name+' = '+fmt(extract('map_to_g%d.c'%n,name))+';\n'
    for name in ('x_num','x_den','y_num','y_den'):
        vals=extract('map_to_g%d.c'%n,'isogeny_map_'+name)
        text+='    internal static readonly '+field+'[] '+name+' =\n    [\n'+''.join('        '+fmt(v)+',\n' for v in vals)+'    ];\n'
    (root/'Hashing'/('MapConstants%d.cs'%n)).write_text(text+'}\n')
text='// Montgomery constants from blst e1.c and e2.c (Apache-2.0).\nusing Nethermind.Crypto.BlsNet.Arithmetic;\n\nnamespace Nethermind.Crypto.BlsNet.Curves;\n\ninternal static class CurveConstants\n{\n'
text+='    internal static readonly Fp Beta = '+fp(extract('e1.c','beta'))+';\n'
for name in ('frobenius_x','frobenius_y'):
    text+='    internal static readonly Fp2 '+name+' = '+fp2(extract('e2.c',name))+';\n'
for n in (1,2):
    vals=extract('e%d.c'%n,'BLS12_381_G%d'%n)
    fmt=fp if n==1 else fp2
    text+='    internal static readonly G%dAffine Generator%d = new(%s, %s);\n'%(n,n,fmt(vals[0]),fmt(vals[1]))
text+='}\n'
(root/'Curves'/'CurveConstants.cs').write_text(text)
