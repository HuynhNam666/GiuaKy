using _2380601390.Debugging;

namespace _2380601390;

public class _2380601390Consts
{
    public const string LocalizationSourceName = "2380601390";

    public const string ConnectionStringName = "Default";

    public const bool MultiTenancyEnabled = true;


    /// <summary>
    /// Default pass phrase for SimpleStringCipher decrypt/encrypt operations
    /// </summary>
    public static readonly string DefaultPassPhrase =
        DebugHelper.IsDebug ? "gsKxGZ012HLL3MI5" : "dfcbdcfda12844c49c646a73f0d4ba08";
}
