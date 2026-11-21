[bml.mod id="compiware.probe" name="Probe" version="0.0.7" author="test" bml="0.3.14"]
// test only: camera heading (InGameCam around the ball) and ball speed / spin, logged every 6 frames in a level
class Probe {
    int frames = 0;
    VxVector lastPos;
    float Heading(const BML::ModContext &in ctx) {
        CK3dEntity@ cam = ctx.BorrowTargetCameraByName("InGameCam");
        CK3dEntity@ t = ctx.Borrow3dEntityByName("Cam_Target");
        if (cam is null || t is null) return -999;
        VxVector a, b;
        cam.GetPosition(a, null);
        t.GetPosition(b, null);
        return atan2(a.x - b.x, a.z - b.z) * 57.2958f;
    }
    void OnProcess(const BML::ModContext &in ctx) {
        if (!ctx.IsInLevel || ctx.IsPaused) return;
        if (++frames % 6 != 0) return;
        float dt = ImGui::GetIO().DeltaTime * 6;
        CK3dEntity@ f = ctx.Borrow3dEntityByName("Ball_Pos_Frame");
        CK3dEntity@ ball = f is null ? null : BML::CK::BorrowParent(f);
        string s = "";
        if (ball !is null) {
            VxVector p;
            ball.GetPosition(p, null);
            float v = sqrt((p.x - lastPos.x) * (p.x - lastPos.x) + (p.z - lastPos.z) * (p.z - lastPos.z)) / dt;
            lastPos = p;
            s = " speed=" + v;
        }
        ctx.LogInfo("PRB f=" + frames + " heading=" + Heading(ctx) + s);
    }
}
