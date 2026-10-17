namespace AccEngineer.UI.Themes;

/// <summary>
/// The built-in palettes, expressed as editor-inspired application colours rather than
/// syntax themes. Each one fills every semantic token in <see cref="ThemePalette"/>;
/// input channels (throttle/brake/steer) keep their racing convention and stay
/// distinguishable from the status colours.
/// </summary>
internal static class ThemePalettes
{
    public static ThemePalette GruvboxDark { get; } = new()
    {
        // Warm charcoal, muted cream, earthy accents.
        Background = "#FF282828",
        Surface = "#FF32302C",
        SurfaceAlt = "#FF3C3836",
        SurfaceHigh = "#FF504945",

        TextPrimary = "#FFEBDBB2",
        TextSecondary = "#FFD5C4A1",
        TextMuted = "#FFA89984",
        TextOnAccent = "#FF282828",

        Border = "#FF665C54",
        Divider = "#FF504945",

        Accent = "#FFFE8019",
        AccentHover = "#FFFE9839",
        AccentDim = "#33FE8019",

        ControlBg = "#FF3C3836",
        ControlBgHover = "#FF504945",
        ControlBgPressed = "#FF665C54",
        ControlDisabled = "#FF7C6F64",
        InputBg = "#FF282828",
        InputBorder = "#FF665C54",
        TooltipBg = "#FF504945",
        TooltipText = "#FFEBDBB2",

        Selection = "#33FE8019",
        Focus = "#FFFE8019",

        Success = "#FFB8BB26",
        Warning = "#FFFABD2F",
        Error = "#FFFB4934",
        Info = "#FF83A598",

        ChartGrid = "#FF504945",
        ChartAxis = "#FF665C54",
        ChartLine = "#FFFE8019",
        ChartFill = "#33FE8019",
        ChartThrottle = "#FF8EC07C",
        ChartBrake = "#FFCC241D",
        ChartSteer = "#FFD3869B",
        ChartMapPath = "#FFA89984",
        ChartMapMarker = "#FFD5C4A1",
        ChartMapPlayer = "#FFFE8019",

        OverlayPanel = "#E6282828",
        OverlayHeader = "#FF1D2021",
        OverlayBorder = "#FF665C54",
        OverlaySeparator = "#33EBDBB2",
        OverlayText = "#FFFBF1C7",
        OverlayMuted = "#FFA89984",
        OverlayAccent = "#FFFE8019",
        OverlaySuccess = "#FFB8BB26",
        OverlayWarning = "#FFFABD2F",
        OverlaySelectedRow = "#33FE8019",
        OverlayPit = "#4DA89984",
        OverlayBarTrack = "#33EBDBB2",
        OverlayGearBadge = "#14EBDBB2",
        OverlayGraphBg = "#FF1D2021",
        OverlayPersonalBest = "#FFB8BB26",
        OverlaySessionBest = "#FFD3869B",

        FlagBlue = "#FF83A598",
        FlagYellow = "#FFFABD2F",
        FlagRed = "#FFFB4934",
        FlagGreen = "#FFB8BB26",
        FlagWhite = "#FFFBF1C7",
        FlagBlack = "#FF7C6F64",
        FlagCheckered = "#80FABD2F",

        ClassPro = "#FF83A598",
        ClassAm = "#FFFABD2F",
        ClassSilver = "#FFD5C4A1"
    };

    public static ThemePalette GruvboxLight { get; } = new()
    {
        Background = "#FFFBF1C7",
        Surface = "#FFF2E8CF",
        SurfaceAlt = "#FFEBDBB2",
        SurfaceHigh = "#FFD5C4A1",

        TextPrimary = "#FF3C3836",
        TextSecondary = "#FF504945",
        TextMuted = "#FF7C6F64",
        TextOnAccent = "#FFFBF1C7",

        Border = "#FFBDAE93",
        Divider = "#FFD5C4A1",

        Accent = "#FFAF3A03",
        AccentHover = "#FF8F2E02",
        AccentDim = "#33AF3A03",

        ControlBg = "#FFEBDBB2",
        ControlBgHover = "#FFD5C4A1",
        ControlBgPressed = "#FFBDAE93",
        ControlDisabled = "#FFA89984",
        InputBg = "#FFF2E8CF",
        InputBorder = "#FFBDAE93",
        TooltipBg = "#FFD5C4A1",
        TooltipText = "#FF3C3836",

        Selection = "#33AF3A03",
        Focus = "#FFAF3A03",

        Success = "#FF79740E",
        Warning = "#FFB57614",
        Error = "#FF9D0006",
        Info = "#FF076678",

        ChartGrid = "#FFD5C4A1",
        ChartAxis = "#FFBDAE93",
        ChartLine = "#FFAF3A03",
        ChartFill = "#33AF3A03",
        ChartThrottle = "#FF427B58",
        ChartBrake = "#FFCC241D",
        ChartSteer = "#FF8F3F71",
        ChartMapPath = "#FFA89984",
        ChartMapMarker = "#FF504945",
        ChartMapPlayer = "#FFAF3A03",

        OverlayPanel = "#E6F2E8CF",
        OverlayHeader = "#FFEBDBB2",
        OverlayBorder = "#FFBDAE93",
        OverlaySeparator = "#333C3836",
        OverlayText = "#FF3C3836",
        OverlayMuted = "#FF665C54",
        OverlayAccent = "#FFAF3A03",
        OverlaySuccess = "#FF79740E",
        OverlayWarning = "#FFB57614",
        OverlaySelectedRow = "#33AF3A03",
        OverlayPit = "#4D7C6F64",
        OverlayBarTrack = "#333C3836",
        OverlayGearBadge = "#143C3836",
        OverlayGraphBg = "#FFF9F1D6",
        OverlayPersonalBest = "#FF79740E",
        OverlaySessionBest = "#FF8F3F71",

        FlagBlue = "#FF076678",
        FlagYellow = "#FFB57614",
        FlagRed = "#FF9D0006",
        FlagGreen = "#FF79740E",
        FlagWhite = "#FFFBF1C7",
        FlagBlack = "#FF3C3836",
        FlagCheckered = "#80B57614",

        ClassPro = "#FF076678",
        ClassAm = "#FFB57614",
        ClassSilver = "#FF7C6F64"
    };

    public static ThemePalette Dracula { get; } = new()
    {
        Background = "#FF282A36",
        Surface = "#FF2F3242",
        SurfaceAlt = "#FF44475A",
        SurfaceHigh = "#FF4F5468",

        TextPrimary = "#FFF8F8F2",
        TextSecondary = "#FFB8BFD0",
        TextMuted = "#FF6272A4",
        TextOnAccent = "#FF282A36",

        Border = "#FF44475A",
        Divider = "#FF3A3E50",

        Accent = "#FFBD93F9",
        AccentHover = "#FFC9A5FF",
        AccentDim = "#33BD93F9",

        ControlBg = "#FF3A3E52",
        ControlBgHover = "#FF44475A",
        ControlBgPressed = "#FF525872",
        ControlDisabled = "#FF6272A4",
        InputBg = "#FF282A36",
        InputBorder = "#FF44475A",
        TooltipBg = "#FF44475A",
        TooltipText = "#FFF8F8F2",

        Selection = "#6644475A",
        Focus = "#FFBD93F9",

        Success = "#FF50FA7B",
        Warning = "#FFF1FA8C",
        Error = "#FFFF5555",
        Info = "#FF8BE9FD",

        ChartGrid = "#FF44475A",
        ChartAxis = "#FF6272A4",
        ChartLine = "#FFBD93F9",
        ChartFill = "#33BD93F9",
        ChartThrottle = "#FF50FA7B",
        ChartBrake = "#FFFF5555",
        ChartSteer = "#FFFF79C6",
        ChartMapPath = "#FF6272A4",
        ChartMapMarker = "#FFB8BFD0",
        ChartMapPlayer = "#FFBD93F9",

        OverlayPanel = "#E6282A36",
        OverlayHeader = "#FF21222C",
        OverlayBorder = "#FF44475A",
        OverlaySeparator = "#33F8F8F2",
        OverlayText = "#FFF8F8F2",
        OverlayMuted = "#FF8B92B5",
        OverlayAccent = "#FFBD93F9",
        OverlaySuccess = "#FF50FA7B",
        OverlayWarning = "#FFF1FA8C",
        OverlaySelectedRow = "#33BD93F9",
        OverlayPit = "#4D6272A4",
        OverlayBarTrack = "#33F8F8F2",
        OverlayGearBadge = "#14F8F8F2",
        OverlayGraphBg = "#FF21222C",
        OverlayPersonalBest = "#FF50FA7B",
        OverlaySessionBest = "#FFBD93F9",

        FlagBlue = "#FF8BE9FD",
        FlagYellow = "#FFF1FA8C",
        FlagRed = "#FFFF5555",
        FlagGreen = "#FF50FA7B",
        FlagWhite = "#FFF8F8F2",
        FlagBlack = "#FF6272A4",
        FlagCheckered = "#80F1FA8C",

        ClassPro = "#FF8BE9FD",
        ClassAm = "#FFF1FA8C",
        ClassSilver = "#FFB8BFD0"
    };

    public static ThemePalette CatppuccinMocha { get; } = new()
    {
        Background = "#FF1E1E2E",
        Surface = "#FF25273A",
        SurfaceAlt = "#FF313244",
        SurfaceHigh = "#FF45475A",

        TextPrimary = "#FFCDD6F4",
        TextSecondary = "#FFBAC2DE",
        TextMuted = "#FFA6ADC8",
        TextOnAccent = "#FF1E1E2E",

        Border = "#FF45475A",
        Divider = "#FF313244",

        Accent = "#FFCBA6F7",
        AccentHover = "#FFD4B9FA",
        AccentDim = "#33CBA6F7",

        ControlBg = "#FF313244",
        ControlBgHover = "#FF45475A",
        ControlBgPressed = "#FF585B70",
        ControlDisabled = "#FF6C7086",
        InputBg = "#FF1E1E2E",
        InputBorder = "#FF45475A",
        TooltipBg = "#FF313244",
        TooltipText = "#FFCDD6F4",

        Selection = "#6645475A",
        Focus = "#FFCBA6F7",

        Success = "#FFA6E3A1",
        Warning = "#FFF9E2AF",
        Error = "#FFF38BA8",
        Info = "#FF89B4FA",

        ChartGrid = "#FF313244",
        ChartAxis = "#FF45475A",
        ChartLine = "#FFCBA6F7",
        ChartFill = "#33CBA6F7",
        ChartThrottle = "#FFA6E3A1",
        ChartBrake = "#FFF38BA8",
        ChartSteer = "#FF89DCEB",
        ChartMapPath = "#FF6C7086",
        ChartMapMarker = "#FFBAC2DE",
        ChartMapPlayer = "#FFCBA6F7",

        OverlayPanel = "#E61E1E2E",
        OverlayHeader = "#FF181825",
        OverlayBorder = "#FF45475A",
        OverlaySeparator = "#33CDD6F4",
        OverlayText = "#FFCDD6F4",
        OverlayMuted = "#FFA6ADC8",
        OverlayAccent = "#FFCBA6F7",
        OverlaySuccess = "#FFA6E3A1",
        OverlayWarning = "#FFF9E2AF",
        OverlaySelectedRow = "#33CBA6F7",
        OverlayPit = "#4D9399B2",
        OverlayBarTrack = "#33CDD6F4",
        OverlayGearBadge = "#14CDD6F4",
        OverlayGraphBg = "#FF181825",
        OverlayPersonalBest = "#FFA6E3A1",
        OverlaySessionBest = "#FFCBA6F7",

        FlagBlue = "#FF89B4FA",
        FlagYellow = "#FFF9E2AF",
        FlagRed = "#FFF38BA8",
        FlagGreen = "#FFA6E3A1",
        FlagWhite = "#FFF5E0DC",
        FlagBlack = "#FF6C7086",
        FlagCheckered = "#80F9E2AF",

        ClassPro = "#FF89B4FA",
        ClassAm = "#FFF9E2AF",
        ClassSilver = "#FFA6ADC8"
    };

    public static ThemePalette CatppuccinMacchiato { get; } = new()
    {
        Background = "#FF24273A",
        Surface = "#FF2B2E41",
        SurfaceAlt = "#FF363A4F",
        SurfaceHigh = "#FF494D64",

        TextPrimary = "#FFCAD3F5",
        TextSecondary = "#FFB8C0E0",
        TextMuted = "#FFA5ADCB",
        TextOnAccent = "#FF24273A",

        Border = "#FF494D64",
        Divider = "#FF363A4F",

        Accent = "#FFC6A0F6",
        AccentHover = "#FFD1B3FA",
        AccentDim = "#33C6A0F6",

        ControlBg = "#FF363A4F",
        ControlBgHover = "#FF494D64",
        ControlBgPressed = "#FF5B6078",
        ControlDisabled = "#FF6E738D",
        InputBg = "#FF24273A",
        InputBorder = "#FF494D64",
        TooltipBg = "#FF363A4F",
        TooltipText = "#FFCAD3F5",

        Selection = "#66494D64",
        Focus = "#FFC6A0F6",

        Success = "#FFA6DA95",
        Warning = "#FFEED49F",
        Error = "#FFED8796",
        Info = "#FF8AADF4",

        ChartGrid = "#FF363A4F",
        ChartAxis = "#FF494D64",
        ChartLine = "#FFC6A0F6",
        ChartFill = "#33C6A0F6",
        ChartThrottle = "#FFA6DA95",
        ChartBrake = "#FFED8796",
        ChartSteer = "#FF8BD5CA",
        ChartMapPath = "#FF6E738D",
        ChartMapMarker = "#FFB8C0E0",
        ChartMapPlayer = "#FFC6A0F6",

        OverlayPanel = "#E624273A",
        OverlayHeader = "#FF1E2030",
        OverlayBorder = "#FF494D64",
        OverlaySeparator = "#33CAD3F5",
        OverlayText = "#FFCAD3F5",
        OverlayMuted = "#FFA5ADCB",
        OverlayAccent = "#FFC6A0F6",
        OverlaySuccess = "#FFA6DA95",
        OverlayWarning = "#FFEED49F",
        OverlaySelectedRow = "#33C6A0F6",
        OverlayPit = "#4D9099B6",
        OverlayBarTrack = "#33CAD3F5",
        OverlayGearBadge = "#14CAD3F5",
        OverlayGraphBg = "#FF1E2030",
        OverlayPersonalBest = "#FFA6DA95",
        OverlaySessionBest = "#FFC6A0F6",

        FlagBlue = "#FF8AADF4",
        FlagYellow = "#FFEED49F",
        FlagRed = "#FFED8796",
        FlagGreen = "#FFA6DA95",
        FlagWhite = "#FFF5E0DC",
        FlagBlack = "#FF6E738D",
        FlagCheckered = "#80EED49F",

        ClassPro = "#FF8AADF4",
        ClassAm = "#FFEED49F",
        ClassSilver = "#FFA5ADCB"
    };

    public static ThemePalette CatppuccinLatte { get; } = new()
    {
        Background = "#FFEFF1F5",
        Surface = "#FFE6E9EF",
        SurfaceAlt = "#FFCCD0DA",
        SurfaceHigh = "#FFBCC0CC",

        TextPrimary = "#FF4C4F69",
        TextSecondary = "#FF5C5F77",
        TextMuted = "#FF6C6F85",
        TextOnAccent = "#FFEFF1F5",

        Border = "#FFBCC0CC",
        Divider = "#FFCCD0DA",

        Accent = "#FF8839EF",
        AccentHover = "#FF7529D9",
        AccentDim = "#338839EF",

        ControlBg = "#FFCCD0DA",
        ControlBgHover = "#FFBCC0CC",
        ControlBgPressed = "#FFACB0BE",
        ControlDisabled = "#FF9CA0B0",
        InputBg = "#FFE6E9EF",
        InputBorder = "#FFBCC0CC",
        TooltipBg = "#FFCCD0DA",
        TooltipText = "#FF4C4F69",

        Selection = "#448839EF",
        Focus = "#FF8839EF",

        Success = "#FF40A02B",
        Warning = "#FFDF8E1D",
        Error = "#FFD20F39",
        Info = "#FF1E66F5",

        ChartGrid = "#FFCCD0DA",
        ChartAxis = "#FFBCC0CC",
        ChartLine = "#FF8839EF",
        ChartFill = "#338839EF",
        ChartThrottle = "#FF40A02B",
        ChartBrake = "#FFD20F39",
        ChartSteer = "#FF179299",
        ChartMapPath = "#FF9CA0B0",
        ChartMapMarker = "#FF5C5F77",
        ChartMapPlayer = "#FF8839EF",

        OverlayPanel = "#E6E6E9EF",
        OverlayHeader = "#FFDCE0E8",
        OverlayBorder = "#FFBCC0CC",
        OverlaySeparator = "#334C4F69",
        OverlayText = "#FF4C4F69",
        OverlayMuted = "#FF6C6F85",
        OverlayAccent = "#FF8839EF",
        OverlaySuccess = "#FF40A02B",
        OverlayWarning = "#FFDF8E1D",
        OverlaySelectedRow = "#338839EF",
        OverlayPit = "#4D8C8FA1",
        OverlayBarTrack = "#334C4F69",
        OverlayGearBadge = "#144C4F69",
        OverlayGraphBg = "#FFE6E9EF",
        OverlayPersonalBest = "#FF40A02B",
        OverlaySessionBest = "#FF8839EF",

        FlagBlue = "#FF1E66F5",
        FlagYellow = "#FFDF8E1D",
        FlagRed = "#FFD20F39",
        FlagGreen = "#FF40A02B",
        FlagWhite = "#FFEFF1F5",
        FlagBlack = "#FF6C6F85",
        FlagCheckered = "#80DF8E1D",

        ClassPro = "#FF1E66F5",
        ClassAm = "#FFDF8E1D",
        ClassSilver = "#FF6C6F85"
    };

    public static ThemePalette Nord { get; } = new()
    {
        Background = "#FF2E3440",
        Surface = "#FF3B4252",
        SurfaceAlt = "#FF434C5E",
        SurfaceHigh = "#FF4C566A",

        TextPrimary = "#FFECEFF4",
        TextSecondary = "#FFD8DEE9",
        TextMuted = "#FF9AA5B4",
        TextOnAccent = "#FF2E3440",

        Border = "#FF4C566A",
        Divider = "#FF434C5E",

        Accent = "#FF88C0D0",
        AccentHover = "#FF8FCBDC",
        AccentDim = "#3388C0D0",

        ControlBg = "#FF434C5E",
        ControlBgHover = "#FF4C566A",
        ControlBgPressed = "#FF5B6678",
        ControlDisabled = "#FF616E7F",
        InputBg = "#FF3B4252",
        InputBorder = "#FF4C566A",
        TooltipBg = "#FF434C5E",
        TooltipText = "#FFECEFF4",

        Selection = "#664C566A",
        Focus = "#FF88C0D0",

        Success = "#FFA3BE8C",
        Warning = "#FFEBCB8B",
        Error = "#FFBF616A",
        Info = "#FF8FBCBB",

        ChartGrid = "#FF434C5E",
        ChartAxis = "#FF4C566A",
        ChartLine = "#FF88C0D0",
        ChartFill = "#3388C0D0",
        ChartThrottle = "#FFA3BE8C",
        ChartBrake = "#FFD08770",
        ChartSteer = "#FF81A1C1",
        ChartMapPath = "#FF9AA5B4",
        ChartMapMarker = "#FFD8DEE9",
        ChartMapPlayer = "#FF88C0D0",

        OverlayPanel = "#E62E3440",
        OverlayHeader = "#FF3B4252",
        OverlayBorder = "#FF4C566A",
        OverlaySeparator = "#33ECEFF4",
        OverlayText = "#FFECEFF4",
        OverlayMuted = "#FF9AA5B4",
        OverlayAccent = "#FF88C0D0",
        OverlaySuccess = "#FFA3BE8C",
        OverlayWarning = "#FFEBCB8B",
        OverlaySelectedRow = "#3388C0D0",
        OverlayPit = "#4D9AA5B4",
        OverlayBarTrack = "#33ECEFF4",
        OverlayGearBadge = "#14ECEFF4",
        OverlayGraphBg = "#FF3B4252",
        OverlayPersonalBest = "#FFA3BE8C",
        OverlaySessionBest = "#FFB48EAD",

        FlagBlue = "#FF81A1C1",
        FlagYellow = "#FFEBCB8B",
        FlagRed = "#FFBF616A",
        FlagGreen = "#FFA3BE8C",
        FlagWhite = "#FFECEFF4",
        FlagBlack = "#FF4C566A",
        FlagCheckered = "#80EBCB8B",

        ClassPro = "#FF81A1C1",
        ClassAm = "#FFEBCB8B",
        ClassSilver = "#FFD8DEE9"
    };

    public static ThemePalette TokyoNight { get; } = new()
    {
        Background = "#FF1A1B26",
        Surface = "#FF1F2335",
        SurfaceAlt = "#FF292E42",
        SurfaceHigh = "#FF33384F",

        TextPrimary = "#FFC0CAF5",
        TextSecondary = "#FFA9B1D6",
        TextMuted = "#FF737AA2",
        TextOnAccent = "#FF1A1B26",

        Border = "#FF3B4261",
        Divider = "#FF292E42",

        Accent = "#FF7AA2F7",
        AccentHover = "#FF89B0FA",
        AccentDim = "#337AA2F7",

        ControlBg = "#FF292E42",
        ControlBgHover = "#FF33384F",
        ControlBgPressed = "#FF3D4460",
        ControlDisabled = "#FF545C7E",
        InputBg = "#FF1A1B26",
        InputBorder = "#FF3B4261",
        TooltipBg = "#FF1F2335",
        TooltipText = "#FFC0CAF5",

        Selection = "#66292E42",
        Focus = "#FF7AA2F7",

        Success = "#FF9ECE6A",
        Warning = "#FFE0AF68",
        Error = "#FFF7768E",
        Info = "#FF73DACA",

        ChartGrid = "#FF292E42",
        ChartAxis = "#FF3B4261",
        ChartLine = "#FF7AA2F7",
        ChartFill = "#337AA2F7",
        ChartThrottle = "#FF9ECE6A",
        ChartBrake = "#FFF7768E",
        ChartSteer = "#FFBB9AF7",
        ChartMapPath = "#FF737AA2",
        ChartMapMarker = "#FFA9B1D6",
        ChartMapPlayer = "#FF7AA2F7",

        OverlayPanel = "#E61A1B26",
        OverlayHeader = "#FF16161E",
        OverlayBorder = "#FF3B4261",
        OverlaySeparator = "#33C0CAF5",
        OverlayText = "#FFC0CAF5",
        OverlayMuted = "#FF737AA2",
        OverlayAccent = "#FF7AA2F7",
        OverlaySuccess = "#FF9ECE6A",
        OverlayWarning = "#FFE0AF68",
        OverlaySelectedRow = "#337AA2F7",
        OverlayPit = "#4D737AA2",
        OverlayBarTrack = "#33C0CAF5",
        OverlayGearBadge = "#14C0CAF5",
        OverlayGraphBg = "#FF16161E",
        OverlayPersonalBest = "#FF9ECE6A",
        OverlaySessionBest = "#FFBB9AF7",

        FlagBlue = "#FF7AA2F7",
        FlagYellow = "#FFE0AF68",
        FlagRed = "#FFF7768E",
        FlagGreen = "#FF9ECE6A",
        FlagWhite = "#FFC0CAF5",
        FlagBlack = "#FF565F89",
        FlagCheckered = "#80E0AF68",

        ClassPro = "#FF7AA2F7",
        ClassAm = "#FFE0AF68",
        ClassSilver = "#FFA9B1D6"
    };

    public static ThemePalette OneDark { get; } = new()
    {
        Background = "#FF282C34",
        Surface = "#FF2C313A",
        SurfaceAlt = "#FF353B45",
        SurfaceHigh = "#FF3E4451",

        TextPrimary = "#FFABB2BF",
        TextSecondary = "#FF8B93A1",
        TextMuted = "#FF7C8495",
        TextOnAccent = "#FF282C34",

        Border = "#FF3E4451",
        Divider = "#FF353B45",

        Accent = "#FF61AFEF",
        AccentHover = "#FF79B8F5",
        AccentDim = "#3361AFEF",

        ControlBg = "#FF353B45",
        ControlBgHover = "#FF3E4451",
        ControlBgPressed = "#FF4A5160",
        ControlDisabled = "#FF5C6370",
        InputBg = "#FF282C34",
        InputBorder = "#FF3E4451",
        TooltipBg = "#FF353B45",
        TooltipText = "#FFABB2BF",

        Selection = "#663E4451",
        Focus = "#FF61AFEF",

        Success = "#FF98C379",
        Warning = "#FFE5C07B",
        Error = "#FFE06C75",
        Info = "#FF56B6C2",

        ChartGrid = "#FF353B45",
        ChartAxis = "#FF4B5263",
        ChartLine = "#FF61AFEF",
        ChartFill = "#3361AFEF",
        ChartThrottle = "#FF98C379",
        ChartBrake = "#FFE06C75",
        ChartSteer = "#FFC678DD",
        ChartMapPath = "#FF5C6370",
        ChartMapMarker = "#FFABB2BF",
        ChartMapPlayer = "#FF61AFEF",

        OverlayPanel = "#E6282C34",
        OverlayHeader = "#FF21252B",
        OverlayBorder = "#FF3E4451",
        OverlaySeparator = "#33ABB2BF",
        OverlayText = "#FFABB2BF",
        OverlayMuted = "#FF8B93A1",
        OverlayAccent = "#FF61AFEF",
        OverlaySuccess = "#FF98C379",
        OverlayWarning = "#FFE5C07B",
        OverlaySelectedRow = "#3361AFEF",
        OverlayPit = "#4D8B93A1",
        OverlayBarTrack = "#33ABB2BF",
        OverlayGearBadge = "#14ABB2BF",
        OverlayGraphBg = "#FF21252B",
        OverlayPersonalBest = "#FF98C379",
        OverlaySessionBest = "#FFC678DD",

        FlagBlue = "#FF61AFEF",
        FlagYellow = "#FFE5C07B",
        FlagRed = "#FFE06C75",
        FlagGreen = "#FF98C379",
        FlagWhite = "#FFABB2BF",
        FlagBlack = "#FF5C6370",
        FlagCheckered = "#80E5C07B",

        ClassPro = "#FF61AFEF",
        ClassAm = "#FFE5C07B",
        ClassSilver = "#FFABB2BF"
    };

    public static ThemePalette ClassicRacing { get; } = new()
    {
        // Racing HUD preset: near-black panels, white text, motorsport amber accent
        // with conventional green/purple best-lap colours and racing red for brake.
        Background = "#FF0B0B0C",
        Surface = "#FF141416",
        SurfaceAlt = "#FF1E1E21",
        SurfaceHigh = "#FF2A2A2E",

        TextPrimary = "#FFF5F5F5",
        TextSecondary = "#FFC8C8CC",
        TextMuted = "#FF8E8E94",
        TextOnAccent = "#FF0B0B0C",

        Border = "#FF48484E",
        Divider = "#FF323236",

        Accent = "#FFFFB000",
        AccentHover = "#FFFFC233",
        AccentDim = "#33FFB000",

        ControlBg = "#FF1E1E21",
        ControlBgHover = "#FF2A2A2E",
        ControlBgPressed = "#FF36363B",
        ControlDisabled = "#FF48484E",
        InputBg = "#FF101012",
        InputBorder = "#FF48484E",
        TooltipBg = "#FF2A2A2E",
        TooltipText = "#FFF5F5F5",

        Selection = "#33FFB000",
        Focus = "#FFFFB000",

        Success = "#FF2ECC40",
        Warning = "#FFFFB000",
        Error = "#FFE10600",
        Info = "#FF00A8E8",

        ChartGrid = "#FF2A2A2E",
        ChartAxis = "#FF48484E",
        ChartLine = "#FFFFB000",
        ChartFill = "#33FFB000",
        ChartThrottle = "#FF2ECC40",
        ChartBrake = "#FFE10600",
        ChartSteer = "#FF00A8E8",
        ChartMapPath = "#FF8E8E94",
        ChartMapMarker = "#FFC8C8CC",
        ChartMapPlayer = "#FFFFB000",

        OverlayPanel = "#E60B0B0C",
        OverlayHeader = "#FF161618",
        OverlayBorder = "#FF48484E",
        OverlaySeparator = "#33FFFFFF",
        OverlayText = "#FFF5F5F5",
        OverlayMuted = "#FF9A9AA0",
        OverlayAccent = "#FFFFB000",
        OverlaySuccess = "#FF2ECC40",
        OverlayWarning = "#FFFFB000",
        OverlaySelectedRow = "#33FFB000",
        OverlayPit = "#4D8E8E94",
        OverlayBarTrack = "#33FFFFFF",
        OverlayGearBadge = "#14FFFFFF",
        OverlayGraphBg = "#FF0A0A0A",
        OverlayPersonalBest = "#FF2ECC40",
        OverlaySessionBest = "#FFB36BFF",

        FlagBlue = "#FF00A8E8",
        FlagYellow = "#FFFFB000",
        FlagRed = "#FFE10600",
        FlagGreen = "#FF2ECC40",
        FlagWhite = "#FFF5F5F5",
        FlagBlack = "#FF9A9AA0",
        FlagCheckered = "#80FFB000",

        ClassPro = "#FF00A8E8",
        ClassAm = "#FFFFB000",
        ClassSilver = "#FFC8C8CC"
    };
}
