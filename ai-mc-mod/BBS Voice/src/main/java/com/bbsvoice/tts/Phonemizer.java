package com.bbsvoice.tts;

import java.util.ArrayList;
import java.util.List;

/**
 * Grapheme -> phone front end. Handles Mandarin (through the bundled hanzi/pinyin table),
 * English words (rule based letter-to-sound), digits and punctuation pauses.
 */
public final class Phonemizer
{
    private Phonemizer()
    {}

    /** Digit characters are read through the same table so "2024" becomes "er4 ling2 er4 si4". */
    private static final char[] DIGITS = {'\u96f6', '\u4e00', '\u4e8c', '\u4e09', '\u56db', '\u4e94', '\u516d', '\u4e03', '\u516b', '\u4e5d'};

    public static List<Phone> phonemize(String text, boolean withTone)
    {
        List<Phone> phones = new ArrayList<>();

        if (text == null || text.isEmpty())
        {
            return phones;
        }

        int i = 0;

        while (i < text.length())
        {
            int codePoint = text.codePointAt(i);
            int count = Character.charCount(codePoint);

            if (PinyinTable.isHanzi(codePoint))
            {
                addHanzi(phones, codePoint, withTone);
                i += count;

                continue;
            }

            if (isAsciiLetter(codePoint))
            {
                int start = i;

                while (i < text.length())
                {
                    int next = text.codePointAt(i);

                    if (isAsciiLetter(next) || next == '\'')
                    {
                        i += Character.charCount(next);
                    }
                    else
                    {
                        break;
                    }
                }

                addEnglish(phones, text.substring(start, i));

                continue;
            }

            if (codePoint >= '0' && codePoint <= '9')
            {
                codePoint = DIGITS[codePoint - '0'];

                addHanzi(phones, codePoint, withTone);
                i += count;

                continue;
            }

            float pause = pauseFor(codePoint);

            if (pause > 0F)
            {
                phones.add(new Phone(Phoneme.SILENCE, 0, false, pause));
            }

            i += count;
        }

        applyPhraseFinalLengthening(phones);
        stripLeadingAndTrailingPauses(phones);

        return phones;
    }

    private static void addHanzi(List<Phone> phones, int codePoint, boolean withTone)
    {
        String pinyin = PinyinTable.lookup(codePoint);

        if (pinyin == null)
        {
            return;
        }

        PinyinSyllable.Result result = PinyinSyllable.parse(pinyin);

        for (int i = 0; i < result.phonemes.size(); i++)
        {
            Phoneme phoneme = result.phonemes.get(i);
            boolean nucleus = i == result.nucleus;
            float scale = 1F;

            if (phoneme.kind == Phoneme.Kind.VOWEL && !nucleus)
            {
                scale = 0.62F;
            }
            else if (phoneme.kind == Phoneme.Kind.GLIDE)
            {
                scale = 0.8F;
            }

            phones.add(new Phone(phoneme, withTone ? result.tone : 0, nucleus, scale));
        }
    }

    private static void addEnglish(List<Phone> phones, String word)
    {
        List<Phoneme> phonemes = EnglishLts.convert(word);
        boolean nucleusDone = false;

        for (Phoneme phoneme : phonemes)
        {
            boolean vowel = phoneme.kind == Phoneme.Kind.VOWEL;
            boolean nucleus = vowel && !nucleusDone;

            if (nucleus)
            {
                nucleusDone = true;
            }

            float scale = vowel && !nucleus ? 0.72F : 1F;

            /* first vowel of a word is stressed, the rest are neutral */
            phones.add(new Phone(phoneme, nucleus ? (phones.isEmpty() || isPause(phones.get(phones.size() - 1)) ? 1 : 5) : 0, nucleus, scale));
        }
    }

    private static boolean isPause(Phone phone)
    {
        return phone.phoneme.kind == Phoneme.Kind.SILENCE;
    }

    private static boolean isAsciiLetter(int codePoint)
    {
        return (codePoint >= 'a' && codePoint <= 'z') || (codePoint >= 'A' && codePoint <= 'Z');
    }

    private static float pauseFor(int codePoint)
    {
        switch (codePoint)
        {
            case '\n': case '\r': return 4.2F;
            case '\u3002': case '\uff01': case '\uff1f': case '!': case '?': case '.': return 3.4F;
            case '\u3001': case '\uff0c': case '\uff1b': case '\uff1a': case ',': case ';': case ':': return 1.9F;
            case '\u2026': case '\u2014': case '\u2013': return 2.6F;
            case ' ': case '\t': return 0.6F;
            case '-': case '/': return 0.32F;
            default: return 0F;
        }
    }

    /** The syllable before a punctuation mark is lengthened, exactly like in real prosody. */
    private static void applyPhraseFinalLengthening(List<Phone> phones)
    {
        for (int i = 0; i < phones.size(); i++)
        {
            Phone phone = phones.get(i);

            if (phone.phoneme.kind != Phoneme.Kind.SILENCE || phone.durationScale < 1.5F)
            {
                continue;
            }

            for (int j = i - 1; j >= 0; j--)
            {
                Phone previous = phones.get(j);

                if (previous.phoneme.kind == Phoneme.Kind.SILENCE)
                {
                    break;
                }

                if (previous.nucleus)
                {
                    previous.durationScale *= 1.45F;

                    break;
                }
            }
        }
    }

    private static void stripLeadingAndTrailingPauses(List<Phone> phones)
    {
        while (!phones.isEmpty() && phones.get(0).phoneme.kind == Phoneme.Kind.SILENCE)
        {
            phones.remove(0);
        }

        while (!phones.isEmpty() && phones.get(phones.size() - 1).phoneme.kind == Phoneme.Kind.SILENCE)
        {
            phones.remove(phones.size() - 1);
        }
    }
}
