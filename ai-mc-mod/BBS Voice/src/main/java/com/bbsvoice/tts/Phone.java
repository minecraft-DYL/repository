package com.bbsvoice.tts;

/**
 * One timed phone in an utterance. {@code durationScale} lets the phonemizer stretch or squeeze the
 * phoneme's intrinsic duration (short glides, long phrase-final syllables, punctuation pauses…).
 */
public class Phone
{
    public final Phoneme phoneme;
    public final int tone;
    public final boolean nucleus;
    public float durationScale;

    public Phone(Phoneme phoneme, int tone, boolean nucleus, float durationScale)
    {
        this.phoneme = phoneme;
        this.tone = tone;
        this.nucleus = nucleus;
        this.durationScale = durationScale;
    }

    public static Phone of(Phoneme phoneme, float scale)
    {
        return new Phone(phoneme, 0, false, scale);
    }

    @Override
    public String toString()
    {
        return this.phoneme.id + (this.nucleus ? "*" : "") + (this.tone > 0 ? this.tone : "");
    }
}
