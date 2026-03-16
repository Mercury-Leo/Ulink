namespace Ulink.Runtime
{
    public static class UlinkSeparators
    {
        // Unicode Private Use Area characters (U+E000–U+F8FF).
        // Valid XML 1.0, never appear in C# AQNs, and won't be typed by users.
        public const char TypeSeparator       = ';';       // between AQNs
        public const char SectionSeparator    = '\uE000';  // types section ↔ data section
        public const char ComponentSeparator  = '\uE001';  // between per-component entries
        public const char DataSeparator       = '\uE002';  // AQN ↔ field data
        public const char FieldSeparator      = '\uE003';  // between field=value pairs
        public const char FieldValueSeparator = '\uE004';  // within a field value (e.g. table\uE004key)
    }
}
