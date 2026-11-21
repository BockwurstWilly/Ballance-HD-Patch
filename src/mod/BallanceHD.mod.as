[bml.mod id="compiware.ballancehd"
         name="Ballance HD"
         version="1.0.0"
         author="CompiWare / BockwurstWilly"
         description="Full-screen 16:9 / 21:9 picture: matches the game cameras to the screen (no black bars)"
         bml="0.3.14"]
class BallanceHD {
    int lastCam = 0;
    int lastW = 0;
    int lastH = 0;

    void OnLoad(const BML::ModContext &in ctx) {
        ctx.LogInfo("Ballance HD loaded");
    }

    // Ballance's cameras are 4:3; the engine then draws a 4:3 picture with black bars on wider screens.
    // Giving the active camera the screen's aspect ratio fills the screen; BML+'s WidescreenFix keeps the
    // vertical field of view of the original (Hor+), so nothing is cut off at the top and bottom.
    void OnProcess(const BML::ModContext &in ctx) {
        CKRenderContext@ rc = ctx.BorrowRenderContext();
        if (rc is null)
            return;
        int w = rc.GetWidth();
        int h = rc.GetHeight();
        if (w <= 0 || h <= 0)
            return;
        CKCamera@ cam = rc.GetAttachedCamera();
        if (cam is null)
            return;
        int aw = 0, ah = 0;
        cam.GetAspectRatio(aw, ah);
        if (aw * h == ah * w)
            return;
        cam.SetAspectRatio(w, h);
        int id = cam.GetID();
        if (id != lastCam || w != lastW || h != lastH) {
            ctx.LogInfo("Camera " + BML::CK::GetName(cam) + " " + aw + ":" + ah + " -> " + w + ":" + h);
            lastCam = id;
            lastW = w;
            lastH = h;
        }
    }
}
