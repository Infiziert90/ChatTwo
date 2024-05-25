using Dalamud.Game.Text.SeStringHandling;

namespace ChatTwo.Util;

internal static class PayloadExt
{
    // TODO: Remove after Key and Group in AutoTranslatePayload became public
    // From: https://github.com/goatcorp/Dalamud/blob/master/Dalamud/Game/Text/SeStringHandling/Payload.cs#L366
    /// <summary>
    /// Retrieve the packed integer from SE's native data format.
    /// </summary>
    /// <param name="input">The BinaryReader instance.</param>
    /// <returns>An integer.</returns>
    internal static uint GetInteger(BinaryReader input)
    {
        uint marker = input.ReadByte();
        if (marker < 0xD0)
            return marker - 1;

        marker = (marker + 1) & 0b1111;

        var ret = new byte[4];
        for (var i = 3; i >= 0; i--)
        {
            ret[i] = (marker & (1 << i)) == 0 ? (byte)0 : input.ReadByte();
        }

        return BitConverter.ToUInt32(ret, 0);
    }
}

internal class PartyFinderPayload : Payload {
    public override PayloadType Type => (PayloadType) 0x50;

    internal uint Id { get; }

    internal PartyFinderPayload(uint id) {
        Id = id;
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
        Id = id;
    }

    protected override byte[] EncodeImpl() {
        throw new NotImplementedException();
    }

    protected override void DecodeImpl(BinaryReader reader, long endOfStream) {
        throw new NotImplementedException();
    }
}


internal class UriPayload(Uri uri) : Payload
{
    public override PayloadType Type => (PayloadType) 0x52;

    public Uri Uri { get; } = uri;

    private static readonly string[] ExpectedSchemes = ["http", "https"];
    private static readonly string DefaultScheme = "https";

    /// <summary>
    /// Create a URIPayload from a raw URI string. If the URI does not have a
    /// scheme, it will default to https://.
    /// </summary>
    /// <exception cref="UriFormatException">
    /// If the URI is invalid, or if the scheme is not supported.
    /// </exception>
    public static UriPayload ResolveURI(string rawURI)
    {
        ArgumentNullException.ThrowIfNull(rawURI);

        // Check for expected scheme ://, if not add https://
        foreach (var scheme in ExpectedSchemes)
        {
            if (rawURI.StartsWith($"{scheme}://"))
            {
                return new UriPayload(new Uri(rawURI));
            }
        }
        if (rawURI.Contains("://"))
        {
            throw new UriFormatException($"Unsupported scheme in URL: {rawURI}");
        }

        return new UriPayload(new Uri($"{DefaultScheme}://{rawURI}"));
    }

    protected override void DecodeImpl(BinaryReader reader, long endOfStream)
    {
        throw new NotImplementedException();
    }

    protected override byte[] EncodeImpl()
    {
        throw new NotImplementedException();
    }
}

internal class EmotePayload : Payload
{
    public override PayloadType Type => (PayloadType) 0x53;

    public string Code;

    public static EmotePayload ResolveEmote(string code)
    {
        return new EmotePayload { Code = code };
    }

    protected override void DecodeImpl(BinaryReader reader, long endOfStream)
    {
        throw new NotImplementedException();
    }

    protected override byte[] EncodeImpl()
    {
        throw new NotImplementedException();
    }
}
