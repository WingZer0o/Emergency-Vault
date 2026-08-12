namespace EmergencyVault.Infrastructure.Http
{
    public sealed class HttpClientSingleton : HttpClient
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        private static HttpClientSingleton instance;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        private static object padlock = new object();
        public static HttpClientSingleton Instance
        {
            get
            {
                return GetThreadSafeInstance();
            }
        }
        private static HttpClientSingleton GetThreadSafeInstance()
        {
            lock (padlock)
            {
                if (instance == null)
                {
                    instance = new HttpClientSingleton();
                }
                instance.DefaultRequestHeaders.Clear();
                return instance;
            }
        }
    }
}