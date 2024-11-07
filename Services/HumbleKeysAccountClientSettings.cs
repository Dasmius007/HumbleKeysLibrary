namespace HumbleKeys.Services
{
    public class HumbleKeysAccountClientSettings : IHumbleKeysAccountClientSettings
    {
        public bool CacheEnabled { get; set; }
        public string CachePath { get; set; }
    }
}