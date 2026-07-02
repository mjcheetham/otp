using System.Text.Json.Serialization;

namespace Mjcheetham.Otp;

public enum OtpAlgorithm
{
    [JsonStringEnumMemberName("sha1")]
    Sha1,
    [JsonStringEnumMemberName("sha256")]
    Sha256,
    [JsonStringEnumMemberName("sha512")]
    Sha512
}
