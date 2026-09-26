package com.bbsvoice.tts;

import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

class PinyinSyllableTest
{
    @Test
    void parsesInitialFinalAndTone()
    {
        PinyinSyllable.Result result = PinyinSyllable.parse("zhong1");

        assertEquals(1, result.tone);
        assertEquals(Phoneme.ZH, result.phonemes.get(0));
        assertEquals(Phoneme.U, result.phonemes.get(1));
        assertEquals(Phoneme.NG, result.phonemes.get(2));
        assertEquals(1, result.nucleus, "the nucleus of ong is the u");
    }

    @Test
    void nucleusIsTheMostOpenVowel()
    {
        assertEquals(2, PinyinSyllable.parse("xiao3").nucleus, "x + i-a-u -> a");
        assertEquals(2, PinyinSyllable.parse("jia1").nucleus, "j + i-a -> a");
        assertEquals(1, PinyinSyllable.parse("li4").nucleus, "l + i -> i");
        assertEquals(2, PinyinSyllable.parse("guo2").nucleus, "g + u-o -> o");
    }

    @Test
    void handlesYAndWSpellings()
    {
        assertEquals(Phoneme.I, PinyinSyllable.parse("yi1").phonemes.get(0));
        assertEquals(Phoneme.U, PinyinSyllable.parse("wu3").phonemes.get(0));
        assertEquals(Phoneme.V, PinyinSyllable.parse("yu2").phonemes.get(0));
        assertEquals(Phoneme.I, PinyinSyllable.parse("ya1").phonemes.get(0));
        assertEquals(Phoneme.A, PinyinSyllable.parse("ya1").phonemes.get(1));
    }

    @Test
    void jqxPlusUIsAnUmlaut()
    {
        assertEquals(Phoneme.V, PinyinSyllable.parse("ju4").phonemes.get(1));
        assertEquals(Phoneme.V, PinyinSyllable.parse("qu1").phonemes.get(1));
    }

    @Test
    void retroflexAndDentalApicalVowels()
    {
        assertTrue(PinyinSyllable.parse("zhi1").phonemes.contains(Phoneme.APICAL));
        assertTrue(PinyinSyllable.parse("shi4").phonemes.contains(Phoneme.APICAL));
        assertTrue(PinyinSyllable.parse("zi3").phonemes.contains(Phoneme.DENTAL));
        assertTrue(PinyinSyllable.parse("si1").phonemes.contains(Phoneme.DENTAL));
    }

    @Test
    void unknownFinalFallsBackInsteadOfCrashing()
    {
        assertTrue(PinyinSyllable.parse("zzz9").phonemes.size() >= 0);
    }
}
