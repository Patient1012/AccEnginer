namespace AccEngineer.Telemetry.Broadcasting;

/// <summary>
/// Car manufacturer (brand) of an ACC car model. Derived from the broadcast entry list's
/// car model id — the same id space as the Kunos Server Admin Handbook's car model list
/// (mirrored by <see cref="AccCarModelCatalog"/>) — never from the team or cup category.
/// </summary>
public enum AccCarBrand : byte
{
    Unknown = 0,
    Porsche,
    Mercedes,
    Ferrari,
    Audi,
    Lamborghini,
    McLaren,
    Nissan,
    BMW,
    Bentley,
    AstonMartin,
    Jaguar,
    Lexus,
    Honda,
    Alpine,
    Chevrolet,
    Ginetta,
    Ktm,
    Maserati,
    Ford
}

/// <summary>
/// Static mapping from ACC car model ids to their manufacturer. Ids follow the Kunos
/// Server Admin Handbook car model list (mirrored by the ACC Shared Memory documentation,
/// Appendix 6); ids added by later DLC beyond the documented set — and any id not listed —
/// map to <see cref="AccCarBrand.Unknown"/> rather than a guess.
/// </summary>
public static class AccCarBrandCatalog
{
    /// <summary>Manufacturer for a broadcast car model id; unknown ids stay unknown.</summary>
    public static AccCarBrand BrandOf(int carModelId) => carModelId switch
    {
        0 or 9 or 23 or 28 or 33 or 34 or 61 or 85 or 86 => AccCarBrand.Porsche,
        1 or 25 or 60 or 84 => AccCarBrand.Mercedes,
        2 or 24 or 26 or 32 => AccCarBrand.Ferrari,
        3 or 19 or 31 or 52 or 80 => AccCarBrand.Audi,
        4 or 16 or 18 or 29 => AccCarBrand.Lamborghini,
        5 or 22 or 35 or 59 => AccCarBrand.McLaren,
        6 or 10 => AccCarBrand.Nissan,
        7 or 27 or 30 or 53 => AccCarBrand.BMW,
        8 or 11 => AccCarBrand.Bentley,
        12 or 20 or 51 => AccCarBrand.AstonMartin,
        13 or 14 => AccCarBrand.Jaguar,
        15 => AccCarBrand.Lexus,
        17 or 21 => AccCarBrand.Honda,
        50 => AccCarBrand.Alpine,
        55 => AccCarBrand.Chevrolet,
        56 => AccCarBrand.Ginetta,
        57 or 82 => AccCarBrand.Ktm,
        58 or 83 => AccCarBrand.Maserati,
        36 => AccCarBrand.Ford,
        _ => AccCarBrand.Unknown
    };

    /// <summary>Short uppercase monogram for a manufacturer; empty when unknown.</summary>
    public static string Code(AccCarBrand brand) => brand switch
    {
        AccCarBrand.Porsche => "POR",
        AccCarBrand.Mercedes => "MER",
        AccCarBrand.Ferrari => "FER",
        AccCarBrand.Audi => "AUD",
        AccCarBrand.Lamborghini => "LAM",
        AccCarBrand.McLaren => "MCL",
        AccCarBrand.Nissan => "NIS",
        AccCarBrand.BMW => "BMW",
        AccCarBrand.Bentley => "BEN",
        AccCarBrand.AstonMartin => "AMR",
        AccCarBrand.Jaguar => "JAG",
        AccCarBrand.Lexus => "LEX",
        AccCarBrand.Honda => "HON",
        AccCarBrand.Alpine => "ALP",
        AccCarBrand.Chevrolet => "CHE",
        AccCarBrand.Ginetta => "GIN",
        AccCarBrand.Ktm => "KTM",
        AccCarBrand.Maserati => "MAS",
        AccCarBrand.Ford => "FOR",
        _ => string.Empty
    };
}
