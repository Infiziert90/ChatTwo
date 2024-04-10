using Dalamud.Game.Text.SeStringHandling;

namespace ChatTwo.Util;

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


internal class URIPayload(Uri uri) : Payload
{
    public override PayloadType Type => (PayloadType) 0x52;

    public Uri Uri { get; init; } = uri;

    private static readonly string[] ExpectedSchemes = ["http", "https"];
    private static readonly string DefaultScheme = "https";

    /// <summary>
    /// Create a URIPayload from a raw URI string. If the URI does not have a
    /// scheme, it will default to https://.
    /// </summary>
    /// <exception cref="UriFormatException">
    /// If the URI is invalid, or if the scheme is not supported.
    /// </exception>
    public static URIPayload ResolveURI(string rawURI)
    {
        ArgumentNullException.ThrowIfNull(rawURI);

        // Check for expected scheme ://, if not add https://
        foreach (var scheme in ExpectedSchemes)
        {
            if (rawURI.StartsWith($"{scheme}://"))
            {
                return new URIPayload(new Uri(rawURI));
            }
        }
        if (rawURI.Contains("://"))
        {
            throw new UriFormatException($"Unsupported scheme in URL: {rawURI}");
        }

        return new URIPayload(new Uri($"{DefaultScheme}://{rawURI}"));
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
