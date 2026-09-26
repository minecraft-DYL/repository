package com.bbsvoice.tts;

import org.junit.jupiter.api.Test;

import java.util.List;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

class PhonemizerTest
{
    @Test
    void mandarinSentenceBecomesSyllablesWithTones()
    {
        List<Phone> phones = Phonemizer.phonemize("\u4f60\u597d", true);

        assertTrue(phones.size() >= 5, "ni + hao should expand into several phones: " + phones);

        int nuclei = 0;

        for (Phone phone : phones)
        {
            if (phone.nucleus)
            {
                nuclei++;
            }
        }

        assertEquals(2, nuclei, "one nucleus per syllable: " + phones);
        assertEquals(3, phones.stream().filter((p) -> p.nucleus).findFirst().orElseThrow().tone);
    }

    @Test
    void toneCanBeTurnedOff()
    {
        List<Phone> withTone = Phonemizer.phonemize("\u4f60\u597d", true);
        List<Phone> withoutTone = Phonemizer.phonemize("\u4f60\u597d", false);

        assertEquals(withTone.size(), withoutTone.size());

        for (Phone phone : withoutTone)
        {
            assertEquals(0, phone.tone);
        }
    }

    @Test
    void punctuationBecomesPauses()
    {
        List<Phone> phones = Phonemizer.phonemize("\u4f60\u597d\uff0c\u4e16\u754c\u3002", true);

        assertTrue(phones.stream().anyMatch((p) -> p.phoneme == Phoneme.SILENCE), "punctuation should pause");
    }

    @Test
    void englishWordsUseLetterToSound()
    {
        List<Phone> phones = Phonemizer.phonemize("hello world", true);

        assertFalse(phones.isEmpty());
        assertTrue(phones.stream().anyMatch((p) -> p.phoneme == Phoneme.L));
    }

    @Test
    void digitsAreReadAsChinese()
    {
        List<Phone> phones = Phonemizer.phonemize("2024", true);

        assertEquals(4, phones.stream().filter((p) -> p.nucleus).count(), "each digit is a syllable: " + phones);
    }

    @Test
    void emptyAndBlankInputAreSafe()
    {
        assertTrue(Phonemizer.phonemize("", true).isEmpty());
        assertTrue(Phonemizer.phonemize(null, true).isEmpty());
        assertTrue(Phonemizer.phonemize("   ", true).isEmpty());
        assertTrue(Phonemizer.phonemize("!!!", true).isEmpty());
    }

    @Test
    void unknownCharactersAreSkippedWithoutCrashing()
    {
        List<Phone> phones = Phonemizer.phonemize("\u4f60\u597d\u2603\u2604", true);

        assertFalse(phones.isEmpty());
    }
}
