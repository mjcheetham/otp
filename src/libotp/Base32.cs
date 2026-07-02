namespace Mjcheetham.Otp;

public static class Base32
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static byte[] Decode(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var output = new List<byte>(value.Length * 5 / 8);
        int buffer = 0;
        int bitsLeft = 0;

        foreach (char rawChar in value)
        {
            if (rawChar == '=' || char.IsWhiteSpace(rawChar))
            {
                continue;
            }

            int index = Alphabet.IndexOf(char.ToUpperInvariant(rawChar), StringComparison.Ordinal);
            if (index < 0)
            {
                throw new FormatException($"'{rawChar}' is not a valid Base32 character.");
            }

            buffer = (buffer << 5) | index;
            bitsLeft += 5;

            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                output.Add((byte)((buffer >> bitsLeft) & 0xff));
            }
        }

        return output.ToArray();
    }
}
