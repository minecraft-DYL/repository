package com.bbsvoice.tts;

import com.bbsvoice.voice.VoicePreset;
import org.junit.jupiter.api.Test;

import java.util.List;

import static org.junit.jupiter.api.Assertions.assertArrayEquals;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertNotEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

class SpeechSynthesizerTest
{
    private static VoiceRequest request(String text, VoicePreset preset)
    {
        VoiceRequest request = new VoiceRequest();

        request.text = text;
        request.sampleRate = 22050;
        request.f0 = preset.f0;
        request.formant = preset.formant;
        request.breath = preset.breath;
        request.vibrato = preset.vibrato;
        request.jitter = preset.jitter;
        request.tilt = preset.tilt;
        request.excitation = preset.excitation;

        return request;
    }

    @Test
    void rendersAudibleSpeech()
    {
        short[] pcm = VoiceSynthesis.render(request("\u4f60\u597d\uff0c\u4e16\u754c\uff01", VoicePreset.MALE));

        assertTrue(pcm.length > 11025, "at least half a second of audio, got " + pcm.length + " samples");

        double squares = 0D;
        int peak = 0;

        for (short sample : pcm)
        {
            int absolute = Math.abs(sample);

            peak = Math.max(peak, absolute);
            squares += (double) sample * sample;
        }

        double rms = Math.sqrt(squares / pcm.length);

        assertTrue(peak > 2000, "signal should not be near silent, peak=" + peak);
        assertTrue(peak <= 32767, "16 bit range");
        assertTrue(rms > 150, "audible loudness, rms=" + rms);
    }

    @Test
    void isDeterministicForTheSameRequest()
    {
        VoiceRequest a = request("\u8bed\u97f3\u6d4b\u8bd5", VoicePreset.FEMALE);
        VoiceRequest b = request("\u8bed\u97f3\u6d4b\u8bd5", VoicePreset.FEMALE);

        assertArrayEquals(VoiceSynthesis.render(a), VoiceSynthesis.render(b), "synthesis must be reproducible for the cache");
    }

    @Test
    void differentTextGivesDifferentAudio()
    {
        short[] a = VoiceSynthesis.render(request("\u4f60\u597d", VoicePreset.MALE));
        short[] b = VoiceSynthesis.render(request("\u518d\u89c1", VoicePreset.MALE));

        assertNotEquals(a.length, b.length, "different syllables should give a different duration");
    }

    @Test
    void everyPresetProducesSound()
    {
        for (VoicePreset preset : VoicePreset.all())
        {
            short[] pcm = VoiceSynthesis.render(request("\u6d4b\u8bd5\u4e00\u4e8c\u4e09", preset));

            assertTrue(pcm.length > 0, preset.id + " produced no audio");

            int peak = 0;

            for (short sample : pcm)
            {
                peak = Math.max(peak, Math.abs(sample));
            }

            assertTrue(peak > 1000, preset.id + " is too quiet, peak=" + peak);
        }
    }

    @Test
    void formantScalingChangesTheTimbre()
    {
        VoiceRequest a = request("\u6d4b\u8bd5", VoicePreset.MALE);
        VoiceRequest b = request("\u6d4b\u8bd5", VoicePreset.MALE);

        b.formant = 0.8F;

        assertFalse(java.util.Arrays.equals(VoiceSynthesis.render(a), VoiceSynthesis.render(b)));
    }

    @Test
    void toneContoursChangeThePitchTrack()
    {
        VoiceRequest withTone = request("\u5988\u9ebb", VoicePreset.MALE);
        VoiceRequest flat = request("\u5988\u9ebb", VoicePreset.MALE);

        flat.tone = false;

        assertFalse(java.util.Arrays.equals(VoiceSynthesis.render(withTone), VoiceSynthesis.render(flat)));
    }

    @Test
    void blankTextRendersNothing()
    {
        assertEquals(0, VoiceSynthesis.render(request("", VoicePreset.MALE)).length);
        assertEquals(0, VoiceSynthesis.render(request("   ", VoicePreset.MALE)).length);
    }

    @Test
    void headerIsNotBakedIntoTheCacheKey()
    {
        VoiceRequest request = request("\u4f60\u597d", VoicePreset.MALE);
        VoiceRequest same = request("\u4f60\u597d", VoicePreset.MALE);

        assertEquals(request.signature(), same.signature());

        same.text = "\u4f60\u597d\u5417";
        assertNotEquals(request.signature(), same.signature());
    }

    @Test
    void phonesAreStable()
    {
        List<Phone> phones = Phonemizer.phonemize("\u4f60\u597d", true);

        assertEquals(phones.toString(), Phonemizer.phonemize("\u4f60\u597d", true).toString());
    }
}
