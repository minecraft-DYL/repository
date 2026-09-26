package com.bbsvoice.tts;

import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.nio.charset.StandardCharsets;
import java.util.HashMap;
import java.util.Map;

/**
 * Hanzi -> pinyin lookup, backed by the compact {@code assets/bbsvoice/pinyin.txt} resource that is
 * generated at build time from the pinyin-data project (see {@code tools/BuildPinyinTable.java}).
 *
 * <p>This is what makes the speech synthesiser work completely offline: no network, no external
 * dictionary and no pinyin IME required.</p>
 */
public class PinyinTable
{
    private static final String RESOURCE = "/assets/bbsvoice/pinyin.txt";

    private static Map<Integer, String> table;

    public static synchronized void load() throws IOException
    {
        if (table != null)
        {
            return;
        }

        Map<Integer, String> loaded = new HashMap<>(32768);

        try (InputStream stream = PinyinTable.class.getResourceAsStream(RESOURCE))
        {
            if (stream == null)
            {
                throw new IOException("Missing pinyin resource " + RESOURCE);
            }

            BufferedReader reader = new BufferedReader(new InputStreamReader(stream, StandardCharsets.UTF_8));
            String line;

            while ((line = reader.readLine()) != null)
            {
                int tab = line.indexOf('\t');

                if (tab <= 0 || tab + 1 >= line.length())
                {
                    continue;
                }

                String head = line.substring(0, tab);
                String pinyin = line.substring(tab + 1).trim();

                if (!head.isEmpty() && !pinyin.isEmpty())
                {
                    loaded.put(head.codePointAt(0), pinyin);
                }
            }
        }

        table = loaded;
    }

    public static boolean isLoaded()
    {
        return table != null;
    }

    public static int size()
    {
        return table == null ? 0 : table.size();
    }

    /**
     * @return tone numbered pinyin (e.g. {@code "ni3"}) or {@code null} when the character is unknown.
     */
    public static String lookup(int codePoint)
    {
        try
        {
            load();
        }
        catch (IOException e)
        {
            return null;
        }

        return table.get(codePoint);
    }

    public static boolean isHanzi(int codePoint)
    {
        return (codePoint >= 0x4E00 && codePoint <= 0x9FFF)
            || (codePoint >= 0x3400 && codePoint <= 0x4DBF)
            || (codePoint >= 0xF900 && codePoint <= 0xFAFF);
    }
}
