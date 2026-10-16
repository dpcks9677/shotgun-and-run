"""스테이지 1 거리 계측 도구 (통로 중심선 기준).

Stage1Layout.asset의 방 · 통로 좌표에서 level-design.md 6 · 10장의 거리 수치를 계산한다.
배치를 바꾼 뒤 문서의 거리 · 이동 시간을 갱신할 때 쓴다.

사용 (저장소 루트에서):
  python Docs/tools/measure_stage_distance.py <Stage1Layout.asset> [--speed 6.5] [--tp-hold 2]
  예) python Docs/tools/measure_stage_distance.py Assets/_Sandbox/Yechann/Layouts/Stage1Layout.asset

측정 기준 ("통로 중심선", level-design.md 9.2)
  - 통로(DungeonPath)의 중심선을 따라 **방 가장자리에서 방 가장자리까지** 잰다. 양 끝 방 안쪽은 재지 않는다
    (존 안 이동 · 교전은 존 활동 시간에 포함, 9.3).
  - 중간에 지나는 방(갈림길 · 작은 방 · 지나가는 존)은 **들어온 점에서 나가는 점까지 직각(맨해튼) 이동**으로 합산한다.
  - 경유 "X 경유"는 X 방을 반드시 통과하는 경로로 보고 같은 방식으로 합산한다.
  - 방문하는 존(동선표의 Z9 → Z3 등)은 가장자리에 도착한 점에서 다음 구간이 시작한다 (존 안은 재지 않음).
  - 순간이동은 TP-2 ↔ TP-6을 이어 주고, E 홀드 시간 × 이동속도를 거리로 환산해 더한다 (기본 2초 × 6.5u/초 = 13u).
  - 6u 통로 안에서 모서리를 깎는 실제 최단 보행은 이 값보다 보통 10~20% 짧다 (참고).
  - 경로 길이는 u(타일 1칸), 시간은 거리 ÷ 이동속도 (명세 5.1).

필요: Python 3, 같은 폴더의 parse_layout.py.
코드에 고정된 값: 동선표의 파츠별 방문 존 집합(SETS, 명세 4.5 · level-design 5장), 출력할 쌍 목록. 존 구성이 바뀌면 직접 수정.
"""
import argparse
import heapq
import itertools
import sys

from parse_layout import parse  # 같은 폴더(Docs/tools)의 parse_layout.py

# 파츠 A·B·C를 모으는 데 돌 존 집합 (level-design 5 · 6장). 순서는 자유 (가장 짧은 순서를 고른다).
SETS = {
    '총열': {'E1': ['Z9', 'Z3'], 'E2': ['Z2', 'Z1'], 'E3': ['Z4', 'Z3']},
    '약실 & 노리쇠': {'E1': ['Z2', 'Z1'], 'E2': ['Z2', 'Z1'], 'E3': ['Z4', 'Z3']},
    '탄띠': {'E1': ['Z2', 'Z6', 'Z7'], 'E2': ['Z7', 'Z6'], 'E3': ['Z4', 'Z3', 'Z9']},
    '개머리판': {'E1': ['Z2', 'Z6', 'Z7'], 'E2': ['Z2', 'Z6', 'Z7'], 'E3': ['Z4', 'Z3', 'Z9']},
    '탄약': {'E1': ['Z9', 'Z3'], 'E2': ['Z2', 'Z9', 'Z3'], 'E3': ['Z3', 'Z9']},
}
EXITS = ('Exit-A', 'Exit-B', 'Exit-C', 'Exit-D')
PAIRS = [('Z1', 'Z9'), ('Z1', 'Z10'), ('Z1', 'Z2'), ('Z2', 'Z8'), ('Z3', 'Z4'), ('Z3', 'Z9'), ('Z4', 'Z6'), ('Z5', 'Z6'), ('Z6', 'Z7'), ('Z7', 'Z8')]
VIA_PAIRS = [('Z2', 'E2', 'Z7'), ('Z4', 'E3', 'Z9')]
SPAWN_TO_HIGH = [('E1', 'Z2', 'Z8'), ('E2', 'Z2', 'Z8'), ('E3', 'Z4', 'Z5')]


def inside(r, p):
    return r['x'] <= p[0] <= r['x'] + r['width'] and r['y'] <= p[1] <= r['y'] + r['height']


class Layout:
    def __init__(self, asset_path):
        rooms, paths, _ = parse(open(asset_path, encoding='utf-8').read())
        self.R = {r['Name']: r for r in rooms}
        self.edges = {}   # 포트 -> [(포트, 비용)]
        self.ports = {}   # 방 -> [포트]
        self._build(paths)
        self._cache = {}

    def _room_of(self, p):
        c = [n for n, r in self.R.items() if inside(r, p)]
        c.sort(key=lambda n: self.R[n]['width'] * self.R[n]['height'])  # 겹치면 작은 방 우선
        return c[0] if c else None

    @staticmethod
    def _sample(poly, step=0.1):
        pts = []
        for (ax, ay), (bx, by) in zip(poly, poly[1:]):
            n = max(1, int(round(max(abs(bx - ax), abs(by - ay)) / step)))
            for i in range(n):
                pts.append((ax + (bx - ax) * i / n, ay + (by - ay) * i / n))
        pts.append(poly[-1])
        return pts

    def _add(self, u, v, c):
        self.edges.setdefault(u, []).append((v, c))
        self.edges.setdefault(v, []).append((u, c))

    def _build(self, paths):
        for p in paths:
            pts = self._sample(p['Points'])
            segs = []
            for i, q in enumerate(pts):
                r = self._room_of(q)
                if r is None:
                    continue
                if segs and segs[-1][0] == r:
                    segs[-1][2] = i
                else:
                    segs.append([r, i, i])
            for (ra, _, ia1), (rb, ib0, _) in zip(segs, segs[1:]):
                if ra == rb:
                    continue
                length = sum(abs(pts[k + 1][0] - pts[k][0]) + abs(pts[k + 1][1] - pts[k][1]) for k in range(ia1, ib0))
                ua = (ra, round(pts[ia1][0], 1), round(pts[ia1][1], 1), p['Label'] + '#a')
                ub = (rb, round(pts[ib0][0], 1), round(pts[ib0][1], 1), p['Label'] + '#b')
                self._add(ua, ub, length)
                self.ports.setdefault(ra, []).append(ua)
                self.ports.setdefault(rb, []).append(ub)
        # 방을 지나는 비용: 같은 방의 두 포트 사이 직각 거리.
        # 홀(1.6)은 사각형 여러 개(HAa · HAb …)로 되어 있으므로, 겹치거나 변이 맞닿은 방 사각형은 한 방으로 묶는다
        group = self._groups()
        byg = {}
        for r, ps in self.ports.items():
            byg.setdefault(group.get(r, r), []).extend(ps)
        for ps in byg.values():
            for u, v in itertools.combinations(ps, 2):
                self._add(u, v, abs(u[1] - v[1]) + abs(u[2] - v[2]))

    def _groups(self):
        names = list(self.R)
        parent = {n: n for n in names}

        def find(n):
            while parent[n] != n:
                parent[n] = parent[parent[n]]
                n = parent[n]
            return n
        for a, b in itertools.combinations(names, 2):
            ra, rb = self.R[a], self.R[b]
            ox = min(ra['x'] + ra['width'], rb['x'] + rb['width']) - max(ra['x'], rb['x'])
            oy = min(ra['y'] + ra['height'], rb['y'] + rb['height']) - max(ra['y'], rb['y'])
            if ox >= 0 and oy >= 0 and (ox > 0 or oy > 0):
                parent[find(a)] = find(b)
        return {n: find(n) for n in names}

    def _dijkstra(self, srcs):
        dist = {s: 0.0 for s in srcs}
        pq = [(0.0, i, s) for i, s in enumerate(srcs)]
        heapq.heapify(pq)
        n = len(srcs)
        while pq:
            d, _, u = heapq.heappop(pq)
            if d > dist.get(u, 1e18):
                continue
            for v, c in self.edges.get(u, []):
                nd = d + c
                if nd < dist.get(v, 1e18):
                    dist[v] = nd
                    n += 1
                    heapq.heappush(pq, (nd, n, v))
        return dist

    def from_room(self, a):
        if a not in self._cache:
            self._cache[a] = self._dijkstra(self.ports[a])
        return self._cache[a]

    def dist(self, a, b):
        """방 a 가장자리 ~ 방 b 가장자리 최단 경로 길이 (중간 방은 직각 통과)."""
        if a == b:
            return 0.0
        d = self.from_room(a)
        return min(d.get(p, 1e18) for p in self.ports[b])

    def via(self, a, v, b):
        """a -> v(통과) -> b. v 안은 들어온 포트에서 나가는 포트까지 직각 이동."""
        da, db = self.from_room(a), self.from_room(b)
        best = 1e18
        for p in self.ports[v]:
            for q in self.ports[v]:
                best = min(best, da.get(p, 1e18) + abs(p[1] - q[1]) + abs(p[2] - q[2]) + db.get(q, 1e18))
        return best


def route_table(lay, tp_dist):
    """파츠별 · 스폰별 최단 동선. 방문 존 순서는 자유, 순간이동은 TP 환산 거리로 이어 줌."""
    def d(a, b):
        base = lay.dist(a, b)
        via_tp = min(lay.dist(a, 'TP-2') + tp_dist + lay.dist('TP-6', b), lay.dist(a, 'TP-6') + tp_dist + lay.dist('TP-2', b))
        return min(base, via_tp)

    out = {}
    for part, per in SETS.items():
        for sp, zones in per.items():
            best = (1e18, None, None)
            for perm in itertools.permutations(zones):
                seq = [sp, *perm]
                t = sum(d(x, y) for x, y in zip(seq, seq[1:]))
                ex = min((d(seq[-1], e), e) for e in EXITS if e in lay.R)
                if t + ex[0] < best[0]:
                    best = (t + ex[0], seq + [ex[1]], [round(d(x, y)) for x, y in zip(seq, seq[1:])] + [round(ex[0])])
            out[(part, sp)] = best
    return out


def main():
    ap = argparse.ArgumentParser(description='스테이지 1 거리 계측 (통로 중심선 기준)')
    ap.add_argument('asset', help='Stage1Layout.asset 경로')
    ap.add_argument('--speed', type=float, default=6.5, help='이동속도 u/초 (명세 5.1)')
    ap.add_argument('--tp-hold', type=float, default=2.0, help='순간이동 E 홀드 시간(초)')
    args = ap.parse_args()
    sys.stdout.reconfigure(encoding='utf-8')

    lay = Layout(args.asset)
    tp = args.tp_hold * args.speed
    sec = lambda u: f'{u / args.speed:.0f}초'

    print(f'# 거리 계측 (통로 중심선, 이동속도 {args.speed:g}u/초, 순간이동 홀드 {args.tp_hold:g}초 = {tp:g}u)\n')

    print('## 존 ↔ 존 (가장자리 사이, u)')
    print('| 구간 | 거리 | 시간 |\n|---|---|---|')
    for a, b in PAIRS:
        v = lay.dist(a, b)
        print(f'| {a}–{b} | {v:.0f} | {sec(v)} |')
    for a, v, b in VIA_PAIRS:
        x = lay.via(a, v, b)
        print(f'| {a}–{b} ({v} 경유) | {x:.0f} | {sec(x)} |')

    print('\n## 존 → 가장 가까운 Exit')
    print('| 존 | Exit | 거리 | 시간 |\n|---|---|---|---|')
    for z in ('Z1', 'Z2', 'Z4', 'Z9', 'Z10'):
        v, e = min((lay.dist(z, e), e) for e in EXITS if e in lay.R)
        print(f'| {z} | {e} | {v:.0f} | {sec(v)} |')

    print('\n## 스폰 → 고위험 존 (존 1곳 경유)')
    print('| 스폰 | 경로 | 거리 |\n|---|---|---|')
    for sp, mid, hi in SPAWN_TO_HIGH:
        print(f'| {sp} → {hi} | {mid} 경유 | {lay.via(sp, mid, hi):.0f} |')

    print('\n## 파츠별 최단 동선 (스폰 → 존 → … → 가까운 Exit)')
    table = route_table(lay, tp)
    print('| 파츠 | E1 | E2 | E3 | 가장 먼 스폰 |\n|---|---|---|---|---|')
    for part in SETS:
        cells = []
        for sp in ('E1', 'E2', 'E3'):
            t, seq, _ = table[(part, sp)]
            cells.append(f'{t:.0f} ({" → ".join(seq[1:-1])} → {seq[-1]})')
        far = max(table[(part, sp)][0] for sp in ('E1', 'E2', 'E3'))
        print(f'| {part} | ' + ' | '.join(cells) + f' | {far:.0f} |')
    sums = {sp: sum(table[(p, sp)][0] for p in SETS) for sp in ('E1', 'E2', 'E3')}
    print(f'| **합계** | {sums["E1"]:.0f} | {sums["E2"]:.0f} | {sums["E3"]:.0f} | 차이 {max(sums.values()) - min(sums.values()):.0f} |')


if __name__ == '__main__':
    main()
