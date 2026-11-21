[bml.mod id="compiware.probe" name="Probe" version="0.0.4" author="test" bml="0.3.14"]
class Probe {
    int frames = 0;
    VxVector start;
    bool armed = false;

    CK3dEntity@ Ball(const BML::ModContext &in ctx) {
        CK3dEntity@ f = ctx.Borrow3dEntityByName("Ball_Pos_Frame");
        return f is null ? null : BML::CK::BorrowParent(f);
    }

    void OnProcess(const BML::ModContext &in ctx) {
        if (!ctx.IsInLevel) return;
        BML::InputHook@ input = ctx.BorrowInputManager();
        if (input.IsKeyPressed(CKKEY_F)) { armed = true; frames = 0; }
        if (!armed) return;
        CK3dEntity@ ball = Ball(ctx);
        CK3dEntity@ oref = ctx.Borrow3dEntityByName("Cam_OrientRef");
        if (ball is null) { ctx.LogInfo("PROBE no ball"); armed = false; return; }
        frames++;
        if (frames == 1) {
            ctx.LogInfo("PROBE ball=" + BML::CK::GetName(ball) + " cid=" + BML::CK::GetClassId(ball));
            ball.GetPosition(start, null);
        }
        // 1..30: +Z (OrientRef) every frame, 31..60: -X every frame, then clear
        if (frames == 1 || frames == 31) {
            VxVector dir = frames <= 30 ? VxVector(0, 0, 1) : VxVector(-1, 0, 0);
            bool ok = BML::Physics::SetForce(ball, VxVector(0, 0, 0), null, dir, oref, 0.43f);
            ctx.LogInfo("PROBE SetForce f" + frames + " ok=" + ok);
            VxVector ax, az, o;
            oref.GetPosition(o, null);
            oref.Transform(ax, VxVector(1, 0, 0), null);
            oref.Transform(az, VxVector(0, 0, 1), null);
            ctx.LogInfo("PROBE oref +X world dir " + (ax.x - o.x) + "," + (ax.z - o.z) + "  +Z world dir " + (az.x - o.x) + "," + (az.z - o.z));
        }
        if (frames == 30 || frames == 60) {
            VxVector now;
            ball.GetPosition(now, null);
            ctx.LogInfo("PROBE f" + frames + " moved " + (now.x - start.x) + ", " + (now.z - start.z));
            start = now;
        }
        if (frames == 61)
            ctx.LogInfo("PROBE clear ok=" + BML::Physics::ClearForce(ball));
        if (frames == 120) {
            VxVector now;
            ball.GetPosition(now, null);
            ctx.LogInfo("PROBE f120 coasting " + (now.x - start.x) + ", " + (now.z - start.z));
            armed = false;
        }
    }
}
