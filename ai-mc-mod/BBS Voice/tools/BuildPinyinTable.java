import java.io.*;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.util.*;

/**
 * Build time helper: converts mozillazg/pinyin-data's "pinyin.txt" into the compact
 * {@code assets/bbsvoice/pinyin.txt} resource shipped inside the mod jar.
 *
 * <p>Usage: {@code java tools/BuildPinyinTable.java <source pinyin.txt> <output pinyin.txt>}</p>
 *
 * <p>All non-ASCII characters are written as \\uXXXX escapes on purpose so that this file stays
 * pure ASCII and can be compiled from any console.</p>
 */
public class BuildPinyinTable
{
    /* accented vowel -> base letter, tone number */
    private static final char[][] TONE_MAP = {
        {'\u0101', 'a', '1'}, {'\u00e1', 'a', '2'}, {'\u01ce', 'a', '3'}, {'\u00e0', 'a', '4'},
        {'\u0113', 'e', '1'}, {'\u00e9', 'e', '2'}, {'\u011b', 'e', '3'}, {'\u00e8', 'e', '4'},
        {'\u012b', 'i', '1'}, {'\u00ed', 'i', '2'}, {'\u01d0', 'i', '3'}, {'\u00ec', 'i', '4'},
        {'\u014d', 'o', '1'}, {'\u00f3', 'o', '2'}, {'\u01d2', 'o', '3'}, {'\u00f2', 'o', '4'},
        {'\u016b', 'u', '1'}, {'\u00fa', 'u', '2'}, {'\u01d4', 'u', '3'}, {'\u00f9', 'u', '4'},
        {'\u01d6', 'v', '1'}, {'\u01d8', 'v', '2'}, {'\u01da', 'v', '3'}, {'\u01dc', 'v', '4'},
        {'u', 'u', '0'}, // placeholder, never matched
    };

    public static void main(String[] args) throws Exception
    {
        if (args.length < 2)
        {
            System.err.println("usage: BuildPinyinTable <in> <out>");
            System.exit(1);
        }

        List<String> lines = Files.readAllLines(Paths.get(args[0]), StandardCharsets.UTF_8);
        Map<Integer, String> table = new TreeMap<>();

        for (String line : lines)
        {
            String trimmed = line.trim();

            if (trimmed.isEmpty() || trimmed.startsWith("#") || !trimmed.startsWith("U+"))
            {
                continue;
            }

            int colon = trimmed.indexOf(':');

            if (colon < 0)
            {
                continue;
            }

            int codePoint;

            try
            {
                codePoint = Integer.parseInt(trimmed.substring(2, colon).trim(), 16);
            }
            catch (NumberFormatException e)
            {
                continue;
            }

            if (codePoint > 0xFFFF)
            {
                continue; // keep the resource BMP only, no surrogate pairs to worry about
            }

            String rest = trimmed.substring(colon + 1);

            /* strip trailing comment / first reading only */
            int hash = rest.indexOf('#');

            if (hash >= 0)
            {
                rest = rest.substring(0, hash);
            }

            String[] readings = rest.split("[,/]");

            for (String reading : readings)
            {
                String converted = convert(reading.trim());

                if (converted != null && !table.containsKey(codePoint))
                {
                    table.put(codePoint, converted);
                }
            }
        }

        StringBuilder out = new StringBuilder(table.size() * 8);

        for (Map.Entry<Integer, String> entry : table.entrySet())
        {
            out.appendCodePoint(entry.getKey());
            out.append('\t');
            out.append(entry.getValue());
            out.append('\n');
        }

        Files.write(Paths.get(args[1]), out.toString().getBytes(StandardCharsets.UTF_8));

        System.out.println("entries: " + table.size() + ", bytes: " + out.length());
    }

    private static String convert(String reading)
    {
        if (reading.isEmpty())
        {
            return null;
        }

        /* only keep pure pinyin syllables, drop anything with digits/latin punctuation */
        StringBuilder builder = new StringBuilder();
        char tone = '5';

        for (int i = 0; i < reading.length(); i++)
        {
            char c = reading.charAt(i);

            if (c >= 'a' && c <= 'z')
            {
                builder.append(c);

                continue;
            }

            if (c >= 'A' && c <= 'Z')
            {
                builder.append(Character.toLowerCase(c));

                continue;
            }

            char mapped = 0;
            char mappedTone = 0;

            for (char[] entry : TONE_MAP)
            {
                if (entry[0] == c)
                {
                    mapped = entry[1];
                    mappedTone = entry[2];

                    break;
                }
            }

            if (mapped != 0)
            {
                builder.append(mapped);

                if (mappedTone != '0' && tone == '5')
                {
                    tone = mappedTone;
                }

                continue;
            }

            if (c == '\u00fc')
            {
                builder.append('v');

                continue;
            }

            if (c == '\u00ea')
            {
                builder.append("eh");

                continue;
            }

            if (c == ':' || c == '\u00a8')
            {
                continue;
            }

            return null; // unsupported character, drop the reading
        }

        String syllable = builder.toString();

        if (syllable.isEmpty())
        {
            return null;
        }

        /* normalise "u:" style and stray v after j/q/x/y already handled by callers */
        return syllable + tone;
    }
}
