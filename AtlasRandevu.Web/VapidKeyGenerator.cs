
using WebPush;

namespace AtlasRandevu.Web
{
    public static class VapidKeyGenerator
    {
        public static void Generate()
        {
            var keys = VapidHelper.GenerateVapidKeys();

            Console.WriteLine("PUBLIC KEY:");
            Console.WriteLine(keys.PublicKey);

            Console.WriteLine("PRIVATE KEY:");
            Console.WriteLine(keys.PrivateKey);
        }
    }
}
