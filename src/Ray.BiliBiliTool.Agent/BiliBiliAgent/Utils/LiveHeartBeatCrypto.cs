using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;

namespace Ray.BiliBiliTool.Agent.BiliBiliAgent.Utils;

public class LiveHeartBeatCrypto
{
    public static string Sypder(string text, ICollection<int> rules, string key)
    {
        string result = text;
        foreach (var rule in rules)
        {
            switch (rule)
            {
                case 0:
                    result = Hash(result, key, "HMACMD5");
                    break;
                case 1:
                    result = Hash(result, key, "HMACSHA1");
                    break;
                case 2:
                    result = Hash(result, key, "HMACSHA256");
                    break;
                case 3:
                    result = Hash(result, key, "HMACSHA224");
                    break;
                case 4:
                    result = Hash(result, key, "HMACSHA512");
                    break;
                case 5:
                    result = Hash(result, key, "HMACSHA384");
                    break;
                default:
                    break;
            }
        }
        return result;
    }

    private static string Hash(string text, string key, string algorithmName)
    {
        if (algorithmName.Equals("HMACSHA224", StringComparison.OrdinalIgnoreCase))
        {
            return HashWithBouncyCastle(text, key, new Sha224Digest());
        }

        using HMAC? hmac = HMAC.Create(algorithmName);
        if (hmac == null)
        {
            throw new ArgumentException($"Unsupported algorithm: {algorithmName}");
        }

        hmac.Key = Encoding.UTF8.GetBytes(key);
        byte[] hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static string HashWithBouncyCastle(string text, string key, Sha224Digest digest)
    {
        var hmac = new HMac(digest);
        hmac.Init(new KeyParameter(Encoding.UTF8.GetBytes(key)));

        byte[] inputBytes = Encoding.UTF8.GetBytes(text);
        hmac.BlockUpdate(inputBytes, 0, inputBytes.Length);

        byte[] hashBytes = new byte[hmac.GetMacSize()];
        hmac.DoFinal(hashBytes, 0);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
