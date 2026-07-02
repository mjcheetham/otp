namespace Mjcheetham.Otp;

public class TimeBasedOtp(string name) : OneTimePassword(OtpKind.TimeBased, name);
