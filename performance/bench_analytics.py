"""
Benchmark + equivalence check for the analytics service: original v1 (pandas/scikit-learn) vs v2.

  python performance/bench_analytics.py equivalence --products 40
  python performance/bench_analytics.py bench --version v2 --seconds 30 --threads 16

Environment: DB_HOST, DB_PORT, DB_NAME, DB_USER, DB_PASSWORD (a database loaded with
seed_analytics_bench.sql), JWT_SECRET (v2). PYTHON_V1 = interpreter that has pandas/scikit-learn installed.
Reports resident memory (RSS) of the server process tree, throughput and latency percentiles.
"""
import argparse, json, os, random, statistics, subprocess, sys, threading, time, urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PORT = {"v1": 8101, "v2": 8102}
SECRET = os.environ.get("JWT_SECRET", "bench-secret-that-is-at-least-32-characters!!")


def token():
    import jwt
    return jwt.encode({"exp": int(time.time()) + 3600}, SECRET, algorithm="HS256")


def start(version):
    env = dict(os.environ, JWT_SECRET=SECRET, CACHE_TTL_SECONDS=os.environ.get("CACHE_TTL_SECONDS", "10"))
    if version == "v1":
        cwd, py = ROOT / "performance" / "baseline_v1", os.environ.get("PYTHON_V1", sys.executable)
    else:
        cwd, py = ROOT / "backend-python", sys.executable
    p = subprocess.Popen([py, "-m", "uvicorn", "main:app", "--host", "127.0.0.1", "--port", str(PORT[version]),
                          "--log-level", "warning"], cwd=cwd, env=env,
                         stdout=subprocess.DEVNULL)  # v1 prints a debug line per step; keep results readable
    for _ in range(200):
        try:
            urllib.request.urlopen(f"http://127.0.0.1:{PORT[version]}/", timeout=1).read()
            return p
        except Exception:
            time.sleep(0.2)
    p.kill(); raise SystemExit(f"{version} did not start")


def rss_mb(proc):
    import psutil
    ps = psutil.Process(proc.pid)
    return sum(c.memory_info().rss for c in [ps] + ps.children(recursive=True)) / 1e6


def get(version, path, tok):
    req = urllib.request.Request(f"http://127.0.0.1:{PORT[version]}{path}")
    if version == "v2":
        req.add_header("Authorization", f"Bearer {tok}")
    with urllib.request.urlopen(req, timeout=60) as r:
        return json.loads(r.read())


def equivalence(n_products):
    tok = token(); out = {}
    procs = {v: start(v) for v in ("v1", "v2")}
    try:
        worst = {"predict": 0.0, "optimize": 0.0}; rel = [0.0]; compared = {"predict": 0, "optimize": 0}; mismatches = []
        ids = random.Random(1).sample(range(1, 3000), n_products)       # popular products have history
        for pid in ids:
            a, b = get("v1", f"/optimize/{pid}", tok), get("v2", f"/optimize/{pid}", tok)
            compared["optimize"] += 1
            for k in a:
                if isinstance(a[k], (int, float)) and abs(a[k] - b[k]) > 0.011:
                    mismatches.append(("optimize", pid, k, a[k], b[k]))
            a, b = get("v1", f"/predict/{pid}", tok), get("v2", f"/predict/{pid}", tok)
            if a["forecasts"] and b["forecasts"]:
                compared["predict"] += 1
                for x, y in zip(a["forecasts"], b["forecasts"]):
                    d = abs(x["prediction"] - y["prediction"]); worst["predict"] = max(worst["predict"], d)
                    if x["prediction"] > 0: rel[0] = max(rel[0], d / x["prediction"])
                    if x["mois"] != y["mois"]: mismatches.append(("predict-month", pid, x["mois"], y["mois"]))
            elif bool(a["forecasts"]) != bool(b["forecasts"]):
                mismatches.append(("predict-availability", pid, bool(a["forecasts"]), bool(b["forecasts"])))
        s1, s2 = get("v1", "/stats", tok), get("v2", "/stats", tok)
        print(json.dumps({"optimize_compared": compared["optimize"], "predict_compared": compared["predict"],
                          "predict_max_abs_diff_units": round(worst["predict"], 3),
                          "predict_max_relative_diff": round(rel[0], 4),
                          "mismatches_optimize_or_months": [m for m in mismatches if m[0] != "predict-availability"][:10],
                          "availability_diffs": [m for m in mismatches if m[0] == "predict-availability"][:10],
                          "stats_v1": s1, "stats_v2": s2}, indent=2, default=str))
    finally:
        for p in procs.values(): p.terminate()


def bench(version, seconds, threads, mix="all"):
    tok = token(); proc = start(version)
    try:
        get(version, "/stats", tok)                       # warm
        idle = rss_mb(proc)
        rnd = random.Random(7); lat, errs, lock = [], [0], threading.Lock(); stop = time.time() + seconds
        peak = [idle]

        def sampler():
            while time.time() < stop:
                peak[0] = max(peak[0], rss_mb(proc)); time.sleep(0.5)
        threading.Thread(target=sampler, daemon=True).start()

        def worker(seed):
            r = random.Random(seed)
            while time.time() < stop:
                pid = r.randint(1, 3000)
                paths = [f"/predict/{pid}", f"/optimize/{pid}"]
                if mix == "all":
                    paths += ["/stats", "/low-stock?limit=50", "/sales-history"]
                path = r.choice(paths)
                t = time.perf_counter()
                try:
                    get(version, path, tok); ok = True
                except Exception:
                    ok = False
                dt = (time.perf_counter() - t) * 1000
                with lock:
                    lat.append(dt)
                    if not ok: errs[0] += 1
        ts = [threading.Thread(target=worker, args=(i,)) for i in range(threads)]
        t0 = time.time(); [t.start() for t in ts]; [t.join() for t in ts]; el = time.time() - t0
        lat.sort(); q = lambda p: lat[min(len(lat) - 1, int(len(lat) * p))]
        print(json.dumps({"version": version, "mix": mix, "cache_ttl": os.environ.get("CACHE_TTL_SECONDS", "10"), "threads": threads, "seconds": round(el, 1), "requests": len(lat),
                          "errors": errs[0], "req_per_sec": round(len(lat) / el, 1),
                          "p50_ms": round(q(.5), 1), "p95_ms": round(q(.95), 1), "p99_ms": round(q(.99), 1),
                          "rss_idle_mb": round(idle, 1), "rss_peak_mb": round(peak[0], 1)}))
    finally:
        proc.terminate()


if __name__ == "__main__":
    ap = argparse.ArgumentParser(); ap.add_argument("mode", choices=["equivalence", "bench"])
    ap.add_argument("--version", default="v2"); ap.add_argument("--seconds", type=int, default=30)
    ap.add_argument("--threads", type=int, default=16); ap.add_argument("--mix", default="all", choices=["all", "product"]); ap.add_argument("--products", type=int, default=40)
    a = ap.parse_args()
    equivalence(a.products) if a.mode == "equivalence" else bench(a.version, a.seconds, a.threads, a.mix)
