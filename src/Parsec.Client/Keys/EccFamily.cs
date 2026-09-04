namespace Parsec.Client.Keys;

/// <summary>
/// Names a family of elliptic curves.
/// </summary>
/// <remarks>
/// The size of the key picks the curve inside the family. SECP-R1 at 256 bits is the curve that
/// most callers want, and it is the one the specification calls secp256r1.
/// </remarks>
public enum EccFamily
{
    /// <summary>No family. The service rejects this value, so it means the field was never set.</summary>
    None = 0,

    /// <summary>SEC Koblitz curves over prime fields.</summary>
    SecpK1 = 1,

    /// <summary>SEC random curves over prime fields.</summary>
    SecpR1 = 2,

    /// <summary>SEC additional random curves over prime fields. The specification deprecates it.</summary>
    SecpR2 = 3,

    /// <summary>SEC Koblitz curves over binary fields.</summary>
    SectK1 = 4,

    /// <summary>SEC random curves over binary fields.</summary>
    SectR1 = 5,

    /// <summary>SEC additional random curves over binary fields. The specification deprecates it.</summary>
    SectR2 = 6,

    /// <summary>Brainpool random curves over prime fields.</summary>
    BrainpoolPR1 = 7,

    /// <summary>The curve of the French agency for the security of information systems.</summary>
    Frp = 8,

    /// <summary>Curve25519 and Curve448.</summary>
    Montgomery = 9,
}
