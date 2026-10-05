namespace Hakari.Core.Limits;

public enum LimitFailure
{
    None,
    NoCredentials,
    SignInExpired,
    Offline,
    ServiceUnavailable,
}
