using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;

namespace Lanw.Cipher.Cipher;

/// <summary>
/// ChaCha7539 封装器，支持自定义轮数（由 Nirvana.Cipher.Cipher.ChaChaPacker 移植，自研）。
/// 默认 8 轮，用于网易 Yggdrasil 加密报文（Pack/Unpack）。
/// </summary>
public sealed class ChaChaPacker : ChaCha7539Engine
{
    public ChaChaPacker(byte[] key, byte[] iv, bool encryption, int rounds = 8)
    {
        this.rounds = rounds;
        Init(encryption, new ParametersWithIV(new KeyParameter(key), iv));
    }

    public override string AlgorithmName => $"ChaCha{rounds}";
}