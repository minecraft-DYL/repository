package com.bbsvoice.tts;

import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;

/**
 * A fully self contained description of one utterance. Deliberately free of any Minecraft or BBS
 * dependency so it can be unit tested (and even rendered from a plain {@code main()}).
 */
public class VoiceRequest
{
    public String text = "";
    public int sampleRate = 22050;

    /** Voice character. */
    public float f0 = 112F;
    public float formant = 1F;
    public float breath = 0.06F;
    public float vibrato = 0.012F;
    public float jitter = 0.006F;
    public float tilt = 0.45F;
    public int excitation = 0;

    /** Whether Mandarin tone contours should be applied. */
    public boolean tone = true;

    /** Rendering speed bias (does not change the cached audio, only the utterance pace). */
    public float pace = 1F;

    public VoiceRequest copy()
    {
        VoiceRequest request = new VoiceRequest();

        request.text = this.text;
        request.sampleRate = this.sampleRate;
        request.f0 = this.f0;
        request.formant = this.formant;
        request.breath = this.breath;
        request.vibrato = this.vibrato;
        request.jitter = this.jitter;
        request.tilt = this.tilt;
        request.excitation = this.excitation;
        request.tone = this.tone;
        request.pace = this.pace;

        return request;
    }

    /**
     * Stable cache key for the rendered audio. Playback-only parameters (volume, speed, pitch and
     * the automation curves) are intentionally excluded: they are applied live and never baked in.
     */
    public String signature()
    {
        String raw = String.join("|",
            "v2",
            Integer.toString(this.sampleRate),
            Float.toString(this.f0),
            Float.toString(this.formant),
            Float.toString(this.breath),
            Float.toString(this.vibrato),
            Float.toString(this.jitter),
            Float.toString(this.tilt),
            Integer.toString(this.excitation),
            Boolean.toString(this.tone),
            Float.toString(this.pace),
            this.text
        );

        try
        {
            MessageDigest digest = MessageDigest.getInstance("SHA-1");
            byte[] bytes = digest.digest(raw.getBytes(StandardCharsets.UTF_8));
            StringBuilder builder = new StringBuilder();

            for (int i = 0; i < 10; i++)
            {
                builder.append(String.format("%02x", bytes[i]));
            }

            return builder.toString();
        }
        catch (Exception e)
        {
            return Integer.toHexString(raw.hashCode());
        }
    }
}
