package com.bbsvoice.tts;

import java.util.List;

/**
 * Facade of the offline speech engine: text + voice parameters in, 16 bit PCM out.
 */
public final class VoiceSynthesis
{
    private VoiceSynthesis()
    {}

    public static short[] render(VoiceRequest request)
    {
        if (request == null || request.text == null || request.text.trim().isEmpty())
        {
            return new short[0];
        }

        List<Phone> phones = Phonemizer.phonemize(request.text, request.tone);

        return SpeechSynthesizer.render(request, phones);
    }

    public static float durationSeconds(short[] pcm, int sampleRate)
    {
        return pcm.length / (float) Math.max(1, sampleRate);
    }
}
