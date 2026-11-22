# Lettore BVH minimale (CMU, conversione MotionBuilder 2010) con cinematica diretta in numpy.
# Spazio di uscita: Blender (X = sinistra del personaggio, Z = alto, -Y = davanti), unità BVH.
import numpy as np, math, re

def _rot(axis, deg):
    a = math.radians(deg); c, s = math.cos(a), math.sin(a)
    if axis == 'X': return np.array([[1, 0, 0], [0, c, -s], [0, s, c]])
    if axis == 'Y': return np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]])
    return np.array([[c, -s, 0], [s, c, 0], [0, 0, 1]])

# BVH (x sinistra, y alto, z davanti) -> Blender (x sinistra, z alto, -y davanti)
M = np.array([[1, 0, 0], [0, 0, -1], [0, 1, 0]], float)

class BVH:
    def __init__(self, path):
        txt = open(path).read().split()
        self.names, self.parent, self.offset, self.channels, self.end = [], [], [], [], {}
        i, stack = 0, []
        while txt[i] != 'MOTION':
            t = txt[i]
            if t in ('ROOT', 'JOINT'):
                self.names.append(txt[i + 1]); self.parent.append(stack[-1] if stack else -1)
                self.channels.append([]); self.offset.append(np.zeros(3)); cur = len(self.names) - 1
                i += 2; continue
            if t == 'End':
                # End Site: offset del figlio terminale
                j = i
                while txt[j] != 'OFFSET': j += 1
                self.end[stack[-1]] = np.array([float(x) for x in txt[j + 1:j + 4]])
                k = j + 4
                while txt[k] != '}': k += 1
                i = k + 1; continue
            if t == '{':
                stack.append(len(self.names) - 1); i += 1; continue
            if t == '}':
                stack.pop(); i += 1; continue
            if t == 'OFFSET':
                self.offset[stack[-1]] = np.array([float(x) for x in txt[i + 1:i + 4]]); i += 4; continue
            if t == 'CHANNELS':
                n = int(txt[i + 1]); self.channels[stack[-1]] = txt[i + 2:i + 2 + n]; i += 2 + n; continue
            i += 1
        nf = int(txt[i + 2]); self.dt = float(txt[i + 5])
        vals = np.array([float(x) for x in txt[i + 6:]]).reshape(nf, -1)
        self.frames = vals; self.nf = nf
        self.idx = {n: k for k, n in enumerate(self.names)}

    def fk(self):
        """Posizioni globali (nf, nj, 3) e rotazioni globali (nf, nj, 3, 3) nello spazio di Blender."""
        nj = len(self.names); P = np.zeros((self.nf, nj, 3)); G = np.zeros((self.nf, nj, 3, 3))
        for f in range(self.nf):
            c = 0
            for j in range(nj):
                R = np.eye(3); pos = self.offset[j].copy()
                for ch in self.channels[j]:
                    v = self.frames[f, c]; c += 1
                    if ch.endswith('position'): pos[{'X': 0, 'Y': 1, 'Z': 2}[ch[0]]] = v
                    else: R = R @ _rot(ch[0], v)
                p = self.parent[j]
                if p < 0: G[f, j] = R; P[f, j] = pos
                else: G[f, j] = G[f, p] @ R; P[f, j] = P[f, p] + G[f, p] @ pos
        # conversione di base: posizioni e rotazioni nello spazio di Blender
        P = P @ M.T
        G = np.einsum('ab,fjbc,dc->fjad', M, G, M)
        return P, G

    def end_pos(self, P, G, j):
        return P[:, j] + np.einsum('fab,b->fa', G[:, j], M @ self.end[j]) if j in self.end else None
