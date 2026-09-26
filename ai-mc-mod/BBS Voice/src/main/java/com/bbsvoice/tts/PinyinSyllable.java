package com.bbsvoice.tts;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

/**
 * Turns one tone numbered pinyin syllable (e.g. {@code "zhong1"}) into a phoneme sequence.
 *
 * <p>Only the syllable structure (initial + final) is needed because the phoneme inventory of the
 * synthesiser is already pinyin shaped.</p>
 */
public final class PinyinSyllable
{
    private static final String[] INITIALS = {
        "zh", "ch", "sh",
        "b", "p", "m", "f", "d", "t", "n", "l", "g", "k", "h",
        "j", "q", "x", "r", "z", "c", "s", "y", "w"
    };

    private static final Map<String, String[]> FINALS = new HashMap<>();

    static
    {
        FINALS.put("a", new String[] {"a"});
        FINALS.put("o", new String[] {"o"});
        FINALS.put("e", new String[] {"e"});
        FINALS.put("eh", new String[] {"eh"});
        FINALS.put("i", new String[] {"i"});
        FINALS.put("u", new String[] {"u"});
        FINALS.put("v", new String[] {"v"});
        FINALS.put("er", new String[] {"er"});

        FINALS.put("ai", new String[] {"a", "i"});
        FINALS.put("ei", new String[] {"e", "i"});
        FINALS.put("ao", new String[] {"a", "u"});
        FINALS.put("ou", new String[] {"o", "u"});

        FINALS.put("an", new String[] {"a", "n"});
        FINALS.put("en", new String[] {"schwa", "n"});
        FINALS.put("ang", new String[] {"a", "ng"});
        FINALS.put("eng", new String[] {"e", "ng"});
        FINALS.put("ong", new String[] {"u", "ng"});

        FINALS.put("ia", new String[] {"i", "a"});
        FINALS.put("ie", new String[] {"i", "eh"});
        FINALS.put("iao", new String[] {"i", "a", "u"});
        FINALS.put("iou", new String[] {"i", "o", "u"});
        FINALS.put("ian", new String[] {"i", "eh", "n"});
        FINALS.put("in", new String[] {"i", "n"});
        FINALS.put("iang", new String[] {"i", "a", "ng"});
        FINALS.put("ing", new String[] {"i", "ng"});
        FINALS.put("iong", new String[] {"i", "u", "ng"});

        FINALS.put("ua", new String[] {"u", "a"});
        FINALS.put("uo", new String[] {"u", "o"});
        FINALS.put("uai", new String[] {"u", "a", "i"});
        FINALS.put("uei", new String[] {"u", "e", "i"});
        FINALS.put("uan", new String[] {"u", "a", "n"});
        FINALS.put("uen", new String[] {"u", "schwa", "n"});
        FINALS.put("uang", new String[] {"u", "a", "ng"});
        FINALS.put("ueng", new String[] {"u", "e", "ng"});

        FINALS.put("ve", new String[] {"v", "eh"});
        FINALS.put("van", new String[] {"v", "eh", "n"});
        FINALS.put("vn", new String[] {"v", "n"});
    }

    private static final Map<String, Phoneme> INITIAL_PHONEMES = new HashMap<>();

    static
    {
        INITIAL_PHONEMES.put("b", Phoneme.B);
        INITIAL_PHONEMES.put("p", Phoneme.P);
        INITIAL_PHONEMES.put("m", Phoneme.M);
        INITIAL_PHONEMES.put("f", Phoneme.F);
        INITIAL_PHONEMES.put("d", Phoneme.D);
        INITIAL_PHONEMES.put("t", Phoneme.T);
        INITIAL_PHONEMES.put("n", Phoneme.N);
        INITIAL_PHONEMES.put("l", Phoneme.L);
        INITIAL_PHONEMES.put("g", Phoneme.G);
        INITIAL_PHONEMES.put("k", Phoneme.K);
        INITIAL_PHONEMES.put("h", Phoneme.H);
        INITIAL_PHONEMES.put("j", Phoneme.J);
        INITIAL_PHONEMES.put("q", Phoneme.Q);
        INITIAL_PHONEMES.put("x", Phoneme.X);
        INITIAL_PHONEMES.put("zh", Phoneme.ZH);
        INITIAL_PHONEMES.put("ch", Phoneme.CH);
        INITIAL_PHONEMES.put("sh", Phoneme.SH);
        INITIAL_PHONEMES.put("r", Phoneme.R);
        INITIAL_PHONEMES.put("z", Phoneme.ZD);
        INITIAL_PHONEMES.put("c", Phoneme.CD);
        INITIAL_PHONEMES.put("s", Phoneme.S);
    }

    private PinyinSyllable()
    {}

    /** Everything a syllable contributes: its phones plus the index of the tonal nucleus. */
    public static class Result
    {
        public final List<Phoneme> phonemes = new ArrayList<>();
        public int nucleus = -1;
        public int tone = 5;
    }

    public static Result parse(String syllable)
    {
        Result result = new Result();

        if (syllable == null || syllable.isEmpty())
        {
            return result;
        }

        String raw = syllable.toLowerCase();

        /* strip the tone digit */
        char last = raw.charAt(raw.length() - 1);

        if (last >= '1' && last <= '5')
        {
            result.tone = last - '0';
            raw = raw.substring(0, raw.length() - 1);
        }

        if (raw.isEmpty())
        {
            return result;
        }

        String initial = "";
        String rest = raw;

        for (String candidate : INITIALS)
        {
            if (raw.startsWith(candidate))
            {
                initial = candidate;
                rest = raw.substring(candidate.length());

                break;
            }
        }

        /* y/w are spelling conventions, their glide already lives in the final */
        String consonant = (initial.equals("y") || initial.equals("w")) ? "" : initial;

        /* the u after j/q/x is really an umlaut u */
        if ((initial.equals("j") || initial.equals("q") || initial.equals("x")) && rest.startsWith("u"))
        {
            rest = "v" + rest.substring(1);
        }

        if (initial.equals("y") && rest.startsWith("u"))
        {
            rest = "v" + rest.substring(1);
        }

        String normalized = normalizeFinal(initial, rest);
        String[] finalPhonemes = FINALS.get(normalized);

        if (finalPhonemes == null)
        {
            finalPhonemes = FINALS.get("e");
        }

        if (!consonant.isEmpty())
        {
            Phoneme phoneme = INITIAL_PHONEMES.get(consonant);

            if (phoneme != null)
            {
                result.phonemes.add(phoneme);
            }
        }

        /* the "i" of zhi/chi/shi/ri and zi/ci/si is not the [i] vowel */
        if (normalized.equals("i") && !consonant.isEmpty())
        {
            if (consonant.equals("zh") || consonant.equals("ch") || consonant.equals("sh") || consonant.equals("r"))
            {
                result.phonemes.add(Phoneme.APICAL);
                result.nucleus = result.phonemes.size() - 1;

                return result;
            }

            if (consonant.equals("z") || consonant.equals("c") || consonant.equals("s"))
            {
                result.phonemes.add(Phoneme.DENTAL);
                result.nucleus = result.phonemes.size() - 1;

                return result;
            }
        }

        int start = result.phonemes.size();

        for (String id : finalPhonemes)
        {
            Phoneme phoneme = Phoneme.get(id);

            if (phoneme != null)
            {
                result.phonemes.add(phoneme);
            }
        }

        result.nucleus = pickNucleus(result.phonemes, start);

        return result;
    }

    /** y/w spelling: "yi" -> i, "ya" -> ia, "wu" -> u, "wo" -> uo, "yu" -> v ... */
    private static String normalizeFinal(String initial, String rest)
    {
        if (initial.equals("y"))
        {
            if (rest.isEmpty())
            {
                return "i";
            }

            if (rest.startsWith("i"))
            {
                return "i" + rest.substring(1);
            }

            if (rest.startsWith("v"))
            {
                return rest;
            }

            return "i" + rest;
        }

        if (initial.equals("w"))
        {
            if (rest.isEmpty())
            {
                return "u";
            }

            if (rest.startsWith("u"))
            {
                return "u" + rest.substring(1);
            }

            return "u" + rest;
        }

        switch (rest)
        {
            case "iu": return "iou";
            case "ui": return "uei";
            case "un": return "uen";
            case "ing": return "ing";
            default: return rest;
        }
    }

    /** The nucleus is the most open vowel of the final, it carries the tone contour. */
    private static int pickNucleus(List<Phoneme> phonemes, int start)
    {
        int best = -1;
        float bestF1 = -1F;

        for (int i = start; i < phonemes.size(); i++)
        {
            Phoneme phoneme = phonemes.get(i);

            if (phoneme.kind != Phoneme.Kind.VOWEL || phoneme.parts.length == 0)
            {
                continue;
            }

            float f1 = phoneme.parts[0].f1;

            if (f1 > bestF1)
            {
                bestF1 = f1;
                best = i;
            }
        }

        return best;
    }

    public static boolean isVowelId(String id)
    {
        Phoneme phoneme = Phoneme.get(id);

        return phoneme != null && phoneme.kind == Phoneme.Kind.VOWEL;
    }
}
