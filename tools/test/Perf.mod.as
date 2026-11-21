[bml.mod id="compiware.perf" name="Perf" version="0.0.3" author="test" bml="0.3.14"]
// test only: frame time distribution every 5 seconds (ms): avg, p50, p95, p99, max, frames over 1.5x the median
class Perf {
    array<float> times;
    float acc = 0;

    void OnProcess(const BML::ModContext &in ctx) {
        float dt = ImGui::GetIO().DeltaTime;
        if (dt <= 0) return;
        times.insertLast(dt * 1000);
        acc += dt;
        if (acc < 5.0f) return;
        array<float> s = times;
        s.sortAsc();
        uint n = s.length();
        float sum = 0;
        for (uint i = 0; i < n; i++) sum += s[i];
        float p50 = s[n / 2];
        int hitches = 0;
        for (uint i = 0; i < n; i++) if (s[i] > p50 * 1.5f) hitches++;
        ctx.LogInfo("PERF inLevel=" + ctx.IsInLevel + " n=" + n + " avg=" + (sum / n) + " p50=" + p50 + " p95=" + s[n * 95 / 100] + " p99=" + s[n * 99 / 100] + " max=" + s[n - 1] + " hitches=" + hitches);
        times.resize(0);
        acc = 0;
    }
}
