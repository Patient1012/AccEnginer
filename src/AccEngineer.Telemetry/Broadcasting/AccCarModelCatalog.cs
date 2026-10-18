namespace AccEngineer.Telemetry.Broadcasting;

/// <summary>
/// Vehicle class of an ACC car model (GT3, GT4, GT2, GTC one-make cups, TCX).
/// Derived from the broadcast entry list's car model id — the same id space as the
/// Kunos server handbook's car model list — never from the cup category.
/// </summary>
public enum AccCarClass : byte
{
    Unknown = 0,
    Gt3,
    Gt4,
    Gt2,
    Gtc,
    Tcx
}

/// <summary>
/// Static mapping from ACC car model ids to their vehicle class. Ids and class
/// groupings follow the Kunos Server Admin Handbook car model list (mirrored by the
/// ACC Shared Memory documentation, Appendix 6); ids added by later DLC beyond the
/// documented set map to <see cref="AccCarClass.Unknown"/> rather than a guess.
/// </summary>
public static class AccCarModelCatalog
{
    /// <summary>Vehicle class for a broadcast car model id; unknown ids stay unknown.</summary>
    public static AccCarClass ClassOf(int carModelId) => carModelId switch
    {
        // GT3 (2018-2024): 991 GT3 R, AMG GT3, 488 GT3, R8 LMS, Huracan GT3,
        // 650S, GT-R 2018, M6, Continental 2018, GT-R 2015, Continental 2015,
        // V12 Vantage, R-EX, Jaguar G3, RC F, Huracan Evo, NSX, R8 Evo,
        // V8 Vantage, NSX Evo, 720S, 991 II R, 488 Evo, AMG Evo, M4, R8 Evo II,
        // 296, Huracan Evo 2, 992 R, 720S Evo, Mustang.
        >= 0 and <= 8 => AccCarClass.Gt3,
        >= 10 and <= 17 => AccCarClass.Gt3,
        >= 19 and <= 25 => AccCarClass.Gt3,
        >= 30 and <= 36 => AccCarClass.Gt3,
        // GTC one-make cups: 991 II GT3 Cup, Huracan SuperTrofeo, 992 GT3 Cup,
        // SuperTrofeo EVO2.
        9 or 18 or 28 or 29 => AccCarClass.Gtc,
        // TCX: 488 Challenge Evo, BMW M2 CS Racing.
        26 or 27 => AccCarClass.Tcx,
        // GT4 (2018 pack): A110, Vantage, R8, M4, Camaro, G55, X-Bow, MC4,
        // 570S, AMG GT4, 718 Cayman.
        >= 50 and <= 53 => AccCarClass.Gt4,
        >= 55 and <= 61 => AccCarClass.Gt4,
        // GT2: R8 LMS GT2, X-Bow GT2, MC20 GT2, AMG GT2, 911 GT2 RS CS Evo, 935.
        80 or >= 82 and <= 86 => AccCarClass.Gt2,
        _ => AccCarClass.Unknown
    };
}
