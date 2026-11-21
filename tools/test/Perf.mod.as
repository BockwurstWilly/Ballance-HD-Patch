[bml.mod id="compiware.perf" name="Perf" version="0.0.2" author="test" bml="0.3.14"]
// test only: frame time statistics every 5 seconds, camera distance to the ball, ball speed
class Perf {
    float acc = 0, mx = 0;
    int n = 0, spikes = 0;
    float dMin = 9999, dMax = 0, vMax = 0;
    VxVector lastBall;

    void OnProcess(const BML::ModContext &in ctx) {
        float dt = ImGui::GetIO().DeltaTime;
        if (dt <= 0) return;
        acc += dt; n++;
        if (dt > mx) mx = dt;
        if (dt > 0.025f) spikes++;
        CK3dEntity@ cam = ctx.BorrowTargetCameraByName("InGameCam");
        CK3dEntity@ t = ctx.Borrow3dEntityByName("Cam_Target");
        CK3dEntity@ b = ctx.Borrow3dEntityByName("Ball_Pos_Frame");
        if (ctx.IsInLevel && cam !is null && t !is null) {
            VxVector a, c;
            cam.GetPosition(a, null);
            t.GetPosition(c, null);
            float d = sqrt((a.x - c.x) * (a.x - c.x) + (a.y - c.y) * (a.y - c.y) + (a.z - c.z) * (a.z - c.z));
            if (d < dMin) dMin = d;
            if (d > dMax) dMax = d;
        }
        if (ctx.IsInLevel && b !is null) {
            VxVector p;
            b.GetPosition(p, null);
            float v = sqrt((p.x - lastBall.x) * (p.x - lastBall.x) + (p.z - lastBall.z) * (p.z - lastBall.z)) / dt;
            if (v > vMax && v < 500) vMax = v;
            lastBall = p;
        }
        if (acc >= 2.5f) {
            ctx.LogInfo("PERF inLevel=" + ctx.IsInLevel + " avg=" + (acc / n * 1000) + "ms max=" + (mx * 1000) + "ms spikes=" + spikes + " camDist=" + dMin + ".." + dMax + " ballSpeedMax=" + vMax);
            acc = 0; mx = 0; n = 0; spikes = 0; dMin = 9999; dMax = 0; vMax = 0;
        }
    }
}
