package com.bbsvoice.tts;

import java.util.ArrayList;
import java.util.List;

/**
 * A very small English letter-to-sound engine. It is intentionally rule based (no dictionary) so
 * that it works offline and inside a mod jar; the result is understandable, not native-like.
 */
public final class EnglishLts
{
    private EnglishLts()
    {}

    private static final String[] VOWELS = {"a", "e", "i", "o", "u", "y"};

    /** long -> short rule list, matched at the current offset */
    private static final String[][] RULES = {
        {"tion", "sh|schwa|n"},
        {"sion", "sh|schwa|n"},
        {"ough", "ao"},
        {"augh", "aa|f"},
        {"tch", "ch"},
        {"igh", "a|i"},
        {"sch", "s|k"},
        {"ing", "i|ng"},
        {"qu", "k|w"},
        {"ch", "ch"},
        {"sh", "sh"},
        {"th", "th"},
        {"ph", "f"},
        {"wh", "w"},
        {"ck", "k"},
        {"ng", "ng"},
        {"oo", "u"},
        {"ee", "i"},
        {"ea", "i"},
        {"oa", "o"},
        {"ai", "eh|i"},
        {"ay", "eh|i"},
        {"ei", "eh|i"},
        {"ey", "eh|i"},
        {"ie", "i"},
        {"ou", "a|u"},
        {"ow", "a|u"},
        {"oi", "o|i"},
        {"oy", "o|i"},
        {"au", "ao"},
        {"aw", "ao"},
        {"ew", "u"},
        {"eu", "u"},
        {"ar", "aa|r"},
        {"er", "er"},
        {"ir", "er"},
        {"ur", "er"},
        {"or", "ao|r"},
        {"al", "ao|l"},
        {"ght", "t"},
        {"wr", "r"},
        {"kn", "n"},
        {"gn", "n"},
    };

    private static final String[][] SINGLES = {
        {"a", "ae"},
        {"b", "b"},
        {"c", "k"},
        {"d", "d"},
        {"e", "eh"},
        {"f", "f"},
        {"g", "g"},
        {"h", "h"},
        {"i", "ih"},
        {"j", "j"},
        {"k", "k"},
        {"l", "l"},
        {"m", "m"},
        {"n", "n"},
        {"o", "aa"},
        {"p", "p"},
        {"q", "k"},
        {"r", "r"},
        {"s", "s"},
        {"t", "t"},
        {"u", "ah"},
        {"v", "vf"},
        {"w", "w"},
        {"x", "k|s"},
        {"y", "i"},
        {"z", "z"},
    };

    public static List<Phoneme> convert(String word)
    {
        List<Phoneme> phonemes = new ArrayList<>();

        if (word == null || word.isEmpty())
        {
            return phonemes;
        }

        String w = word.toLowerCase();
        StringBuilder cleaned = new StringBuilder();

        for (int i = 0; i < w.length(); i++)
        {
            char c = w.charAt(i);

            if (c >= 'a' && c <= 'z')
            {
                cleaned.append(c);
            }
        }

        w = cleaned.toString();

        if (w.isEmpty())
        {
            return phonemes;
        }

        /* trailing silent e (make -> mak) */
        if (w.length() > 2 && w.endsWith("e") && !w.endsWith("ee") && !w.endsWith("le")
            && w.charAt(w.length() - 2) != 'e' && hasVowel(w.substring(0, w.length() - 1)))
        {
            w = w.substring(0, w.length() - 1);
        }

        int i = 0;

        while (i < w.length())
        {
            /* soft c / soft g */
            if (w.charAt(i) == 'c' && i + 1 < w.length() && (w.charAt(i + 1) == 'e' || w.charAt(i + 1) == 'i' || w.charAt(i + 1) == 'y'))
            {
                phonemes.add(Phoneme.S);
                i += 1;

                continue;
            }

            if (w.charAt(i) == 'g' && i + 1 < w.length() && (w.charAt(i + 1) == 'e' || w.charAt(i + 1) == 'i' || w.charAt(i + 1) == 'y'))
            {
                phonemes.add(Phoneme.J);
                i += 1;

                continue;
            }

            /* final "le" as in "table" */
            if (w.charAt(i) == 'l' && i + 1 < w.length() && w.charAt(i + 1) == 'e' && i + 2 >= w.length())
            {
                phonemes.add(Phoneme.SCHWA);
                phonemes.add(Phoneme.L);
                i += 2;

                continue;
            }

            boolean matched = false;

            for (String[] rule : RULES)
            {
                if (w.startsWith(rule[0], i))
                {
                    add(phonemes, rule[1]);
                    i += rule[0].length();
                    matched = true;

                    break;
                }
            }

            if (matched)
            {
                continue;
            }

            /* doubled letter -> single */
            if (i + 1 < w.length() && w.charAt(i) == w.charAt(i + 1))
            {
                i += 1;
            }

            String single = String.valueOf(w.charAt(i));

            for (String[] rule : SINGLES)
            {
                if (rule[0].equals(single))
                {
                    add(phonemes, rule[1]);

                    break;
                }
            }

            i += 1;
        }

        return phonemes;
    }

    private static void add(List<Phoneme> phonemes, String pipe)
    {
        for (String id : pipe.split("\\|"))
        {
            Phoneme phoneme = Phoneme.get(id);

            if (phoneme != null)
            {
                phonemes.add(phoneme);
            }
        }
    }

    private static boolean hasVowel(String string)
    {
        for (String vowel : VOWELS)
        {
            if (string.contains(vowel))
            {
                return true;
            }
        }

        return false;
    }
}
