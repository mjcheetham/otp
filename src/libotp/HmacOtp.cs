namespace Mjcheetham.Otp;

public class HmacOtp(string name) : OneTimePassword(OtpKind.Hmac, name);
