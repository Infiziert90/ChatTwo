using Dalamud.Game.Text.SeStringHandling;

namespace ChatTwo.Util;

internal class PartyFinderPayload : Payload {
    public override PayloadType Type => (PayloadType) 0x50;

    internal uint Id { get; }

    internal PartyFinderPayload(uint id) {
        this.Id = id;
    }

    protected override byte[] EncodeImpl() {
        throw new NotImplementedException();
    }

    protected override void DecodeImpl(BinaryReader reader, long endOfStream) {
        throw new NotImplementedException();
    }
}

internal class AchievementPayload : Payload {
    public override PayloadType Type => (PayloadType) 0x51;

    internal uint Id { get; }

    internal AchievementPayload(uint id) {
        this.Id = id;
    }

    protected override byte[] EncodeImpl() {
        throw new NotImplementedException();
    }

    protected override void DecodeImpl(BinaryReader reader, long endOfStream) {
        throw new NotImplementedException();
    }
}
