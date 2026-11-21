[bml.mod id="compiware.probe" name="Probe" version="0.0.6" author="test" bml="0.3.14"]
// test only: visible 2D entities (to recognise the game's menu pages) and the shared memory channel
class Probe {
    int frames = 0;
    string last = "";
    void OnProcess(const BML::ModContext &in ctx) {
        if (++frames % 90 != 0) return;
        CKContext@ ck = ctx.BorrowCKContext();
        string line = "";
        for (int id = 0; id < 60000; id++) {
            CKObject@ o = ck.GetObject(id);
            if (o is null) continue;
            int cid = BML::CK::GetClassId(o);
            if (cid != 27 && cid != 28) continue;      // CK2dEntity, CKSprite
            if (!BML::CK::IsVisible(o)) continue;
            line += BML::CK::GetName(o) + "|";
        }
        if (line != last) {
            ctx.LogInfo("VIS " + line);
            last = line;
        }
    }
}
