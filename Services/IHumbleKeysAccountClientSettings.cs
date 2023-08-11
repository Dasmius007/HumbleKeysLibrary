namespace HumbleKeys.Services
{
    public interface IHumbleKeysAccountClientSettings
    {
        bool CacheEnabled { get; set; }
        string CachePath { get; set; }
    }
}