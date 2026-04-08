using Dalamud.Game.Text.SeStringHandling;

namespace ChatTwo.Util;

internal class PartyFinderPayload : Payload
{
    public override PayloadType Type => (PayloadType) 0x50;

    internal uint Id { get; }

    internal PartyFinderPayload(uint id)
    {
        Id = id;
    }

    protected override byte[] EncodeImpl()
    {
        throw new NotImplementedException();
    }

    protected override void DecodeImpl(BinaryReader reader, long endOfStream)
    {
        throw new NotImplementedException();
    }
}

internal class AchievementPayload : Payload
{
    public override PayloadType Type => (PayloadType) 0x51;

    internal uint Id { get; }

    internal AchievementPayload(uint id)
    {
        Id = id;
    }

    protected override byte[] EncodeImpl()
    {
        throw new NotImplementedException();
    }

    protected override void DecodeImpl(BinaryReader reader, long endOfStream)
    {
        throw new NotImplementedException();
    }
}


internal class UriPayload(Uri uri) : Payload
{
    public override PayloadType Type => (PayloadType) 0x52;

    public Uri Uri { get; } = uri;

    private const string DefaultScheme = "https";
    private static readonly string[] ExpectedSchemes = ["http", "https"];

    /// <summary>
    /// Create a URIPayload from a raw URI string. If the URI does not have a
    /// scheme, it will default to https://.
    /// </summary>
    /// <exception cref="UriFormatException">
    /// If the URI is invalid, or if the scheme is not supported.
    /// </exception>
    public static UriPayload ResolveUri(string rawUri)
    {
        ArgumentNullException.ThrowIfNull(rawUri);

        // Check for an expected scheme '://', if not add 'https://'
        if (ExpectedSchemes.Any(scheme => rawUri.StartsWith($"{scheme}://")))
            return new UriPayload(new Uri(rawUri));

        if (rawUri.Contains("://"))
            throw new UriFormatException($"Unsupported scheme in URL: {rawUri}");

        return new UriPayload(new Uri($"{DefaultScheme}://{rawUri}"));
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

    public string Code = string.Empty;

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
