namespace AngelBot;

public static class L
{
    public static bool IsDE { get; private set; } = true;
    public static void Set(string lang) => IsDE = lang != "en";

    public static string Detection  => IsDE ? "Erkennung"            : "Detection";
    public static string Method     => IsDE ? "Methode:"             : "Method:";
    public static string ScreenRb   => IsDE ? "Bildschirm"           : "Screen";
    public static string StartKey   => IsDE ? "Start-Taste:"         : "Start key:";
    public static string PressKey   => IsDE ? "Taste drücken …"      : "Press a key …";
    public static string ChangeKey  => IsDE ? "Ändern"               : "Change";
    public static string DrawRegion => IsDE ? "Region einzeichnen"   : "Draw region";
    public static string NoRegion   => IsDE ? "Keine Region gesetzt" : "No region set";
    public static string Ready      => IsDE ? "BEREIT"               : "READY";
    public static string Casting    => IsDE ? "AUSWERFEN …"          : "CASTING …";
    public static string Waiting    => IsDE ? "WARTE AUF BISS …"     : "WAITING FOR BITE …";
    public static string Bite       => IsDE ? "BISS!"                : "BITE!";
    public static string Reeling    => IsDE ? "EINHOLEN …"           : "REELING …";
    public static string Filleting  => IsDE ? "FILETIEREN …"         : "FILLETING …";
    public static string FishCap    => IsDE ? "Fische"               : "Fish";
    public static string RuntimeCap => IsDE ? "Laufzeit"             : "Runtime";
    public static string ErrorsCap  => IsDE ? "Fehler"               : "Errors";
    public static string Save       => IsDE ? "Speichern"            : "Save";
    public static string HelpBtn    => IsDE ? "Hilfe"                : "Help";

    public static string RegionSet(int x, int y, int w, int h)
        => $"Region: {x},{y}  {w}×{h}px";

    // ── Info dialog ──────────────────────────────────────────────────────────
    public static string InfoTitle => IsDE ? "Über Windrose Angelbot" : "About Windrose Angelbot";

    public static string InfoText => IsDE
        ? "Digital Solution\n\nEin gemeinnütziger österreichischer Verein,\nder sich mit Künstlicher Intelligenz,\nAutomatisierung und Programmierung beschäftigt.\n\ndigitalsolution.at\n\n─────────────────────────────────\n\nWindrose Angelbot  v1.0\n\nwindrose.digitalsolution.at\n\n─────────────────────────────────\n\n© 2026 Digital Solution\nAlle Rechte vorbehalten.\n\nAusschließlich zur privaten,\nnicht-kommerziellen Nutzung bestimmt."
        : "Digital Solution\n\nAn Austrian non-profit association\ndedicated to Artificial Intelligence,\nAutomation and Programming.\n\ndigitalsolution.at\n\n─────────────────────────────────\n\nWindrose Angelbot  v1.0\n\nwindrose.digitalsolution.at\n\n─────────────────────────────────\n\n© 2026 Digital Solution\nAll rights reserved.\n\nFor personal, non-commercial use only.";

    // ── Help dialog ──────────────────────────────────────────────────────────
    public static string HelpTitle => IsDE ? "Hilfe — Erkennungsmethoden" : "Help — Detection Methods";

    public static (string title, string body, bool recommended)[] HelpSections => IsDE
        ? [
            ("🔊  Audio-Methode",
             "Das Tool hört den Spielton mit und erkennt automatisch das charakteristische Geräusch wenn ein Fisch anbeißt.\n\n"
             + "✔  Einfach einrichten – keine Region nötig\n"
             + "✔  Sehr zuverlässig bei ruhiger Umgebung\n\n"
             + "⚠  Funktioniert NUR wenn kein anderer Ton vom PC abgespielt wird:\n"
             + "    kein YouTube · kein Discord/TeamSpeak · keine Musik",
             true),
            ("🖥  Bildschirm-Methode",
             "Du zeichnest einen Bereich auf dem Bildschirm ein, der das Wasser oder den Köder zeigt. Das Tool erkennt den weißen Wassersplash wenn ein Fisch anbeißt.\n\n"
             + "✔  Funktioniert unabhängig von anderen Tönen\n"
             + "✔  Gut wenn Musik oder Voice-Chat verwendet wird\n\n"
             + "⚠  Region sorgfältig wählen – nicht zu groß, nicht zu klein\n"
             + "⚠  Bei hellem Tageslicht im Spiel kann die Erkennung schwieriger sein",
             false),
          ]
        : [
            ("🔊  Audio Method",
             "The tool listens to the game audio and automatically recognizes the characteristic sound when a fish bites.\n\n"
             + "✔  Easy to set up — no region drawing required\n"
             + "✔  Very reliable in a quiet environment\n\n"
             + "⚠  Only works when NO other sounds are playing on your PC:\n"
             + "    no YouTube · no Discord/TeamSpeak · no music",
             true),
            ("🖥  Screen Method",
             "You draw a region on screen showing the water or lure. The tool detects the white water splash when a fish bites.\n\n"
             + "✔  Works independently of other sounds\n"
             + "✔  Good when using music or voice chat\n\n"
             + "⚠  Choose the region carefully — not too large, not too small\n"
             + "⚠  Bright daytime scenes in-game can make detection harder",
             false),
          ];
}
