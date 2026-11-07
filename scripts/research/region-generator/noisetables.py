# Dumps the simplex permutation (512 ints at 0x14501D440) and the 16 float4 gradients at
# 0x1459245E0 (filled by the startup initializer 0x1401997A0) as C# array literals.
import os, sys, struct
SCR = os.path.dirname(os.path.abspath(__file__))
sys.argv = [sys.argv[0], 'unused.hg.json']
exec(compile(open(os.path.join(SCR, 'regionbatch.py')).read().split('\nroot = json.loads')[0], 'regionbatch.py', 'exec'))
perm = rd(0x14501D440, '<512i')
grads = rd(0x1459245E0, '<64f')
print('perm max', max(perm), 'first', perm[:8], 'second half equals first:', perm[256:] == perm[:256])
print('static readonly int[] Permutation = {' + ', '.join(map(str, perm[:256])) + '};')
print('static readonly float[] Gradients = {' + ', '.join(f'{g:g}f' for g in grads) + '};')
