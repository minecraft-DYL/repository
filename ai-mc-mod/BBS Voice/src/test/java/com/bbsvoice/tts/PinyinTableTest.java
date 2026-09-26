package com.bbsvoice.tts;

import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertTrue;

class PinyinTableTest
{
    @Test
    void loadsBundledTable()
    {
        assertTrue(PinyinTable.size() > 20000, "the bundled pinyin table should cover CJK");
    }

    @Test
    void looksUpCommonCharacters()
    {
        assertEquals("ni3", PinyinTable.lookup('\u4f60'));      // 你
        assertEquals("hao3", PinyinTable.lookup('\u597d'));     // 好
        assertEquals("shi4", PinyinTable.lookup('\u662f'));     // 是
        assertEquals("zhong1", PinyinTable.lookup('\u4e2d'));   // 中
        assertEquals("lv4", PinyinTable.lookup('\u7eff'));      // 绿
        assertEquals("nv3", PinyinTable.lookup('\u5973'));      // 女
    }

    @Test
    void rejectsLatinAndUnknown()
    {
        assertTrue(PinyinTable.isHanzi('\u4f60'));
        assertTrue(!PinyinTable.isHanzi('a'));
    }

    @Test
    void tableIsCompleteEnoughForAWholeSentence()
    {
        String sentence = "\u8fd9\u662f\u4e00\u6bb5\u79bb\u7ebf\u5408\u6210\u7684\u914d\u97f3\u6d4b\u8bd5";

        for (int i = 0; i < sentence.length(); i++)
        {
            assertNotNull(PinyinTable.lookup(sentence.charAt(i)), "missing reading for " + sentence.charAt(i));
        }
    }
}
